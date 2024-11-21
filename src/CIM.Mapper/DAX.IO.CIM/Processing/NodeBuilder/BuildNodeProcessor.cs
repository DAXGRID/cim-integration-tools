using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.NetworkModel.CIM;
using DAX.Util;

namespace DAX.IO.CIM
{
    public class BuildNodeProcessor : IGraphProcessor
    {
        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

        public void Run(CIMGraph g, TableLogger tableLogger)
        {
            Logger.Log(LogLevel.Debug, "Node Processor: Build node-breaker model (indre skematik) in nodes...");

            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.BuildNode)
                {
                    var psrType = obj.GetPSRType(CIMMetaDataManager.Repository);

                    if (psrType == "CableBox" || psrType == "T-Junction" || psrType == "Tower")
                        CreateCableBoxOrTJunctionOrTower(g, tableLogger, obj, psrType);
                    else if (psrType == "SecondarySubstation" || psrType == "PrimarySubstation")
                        CreateSubstation(g, tableLogger, obj);
                    else
                        Logger.Log(LogLevel.Warning, "Node Processor: Build node with id: '" + obj.mRID + "' has unknown psrType");
                }
            }
        }

        private void CreateCableBoxOrTJunctionOrTower(CIMGraph g, TableLogger tableLogger, CIMIdentifiedObject cableBoxNode, string psrType)
        {
            var topologyData = (ITopologyProcessingResult)g.GetProcessingResult("Topology");

            // Create equipment container
            CIMEquipmentContainer simpleNode = new CIMEquipmentContainer(g.ObjectManager) { Name = cableBoxNode.Name, ExternalId = cableBoxNode.ExternalId, Coords = cableBoxNode.Coords, ClassType = CIMClassEnum.Enclosure, VoltageLevel = 400 };
            simpleNode.mRID = cableBoxNode.mRID;
            simpleNode.SetPSRType(CIMMetaDataManager.Repository, psrType);

            int nextDerivedGuidCounter = 3;

            g.IndexObject(simpleNode);

            // Create CN
            CIMConnectivityNode busbarCn = new CIMConnectivityNode(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(cableBoxNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.ConnectivityNode, EquipmentContainerRef = simpleNode, VoltageLevel = 400 };
            busbarCn.EquipmentContainerRef = simpleNode;

            nextDerivedGuidCounter += 3;

            // Copy vertex id from old t-junction punkt to CN in new t-juction
            int vertexId = g.ObjectManager.AdditionalObjectAttributes(cableBoxNode).Vertex1Id;
            g.ObjectManager.AdditionalObjectAttributes(busbarCn).Vertex1Id = vertexId;

            // Create busbar
            CIMConductingEquipment busbar = new CIMConductingEquipment(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(cableBoxNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.BusbarSection, EquipmentContainerRef = simpleNode, VoltageLevel = 400 };

            // Add busbar to container
            simpleNode.Children.Add(busbar);

            // Connect CN and busbar
            Connect(busbarCn, busbar);

            // Busbar kan have mange naboer
            nextDerivedGuidCounter += 50;

            // Create bay and disconnector for each cable going to t-junction
            List<CIMIdentifiedObject> neighbours = new List<CIMIdentifiedObject>();
            neighbours.AddRange(cableBoxNode.Neighbours);

            foreach (var cable in neighbours)
            {
                // Fjern existerende forbindelse til build node
                cableBoxNode.RemoveNeighbour(cable);
                cable.RemoveNeighbour(cableBoxNode);

                var bay = new CIMEquipmentContainer(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(cableBoxNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.Bay, VoltageLevel = 400, EquipmentContainerRef = simpleNode };
                nextDerivedGuidCounter += 3;
                simpleNode.Children.Add(bay);

                var dis = new CIMConductingEquipment(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(cableBoxNode.mRID, nextDerivedGuidCounter, true), VoltageLevel = 400, ClassType = CIMClassEnum.Disconnector, EquipmentContainerRef = bay };
                nextDerivedGuidCounter += 3;
                bay.Children.Add(dis);

                // Open switch in node if cable says so
                if (cable.ContainsPropertyValue("dax.open.node.mrid"))
                {
                    Guid nodeId = Guid.Parse(cable.GetPropertyValueAsString("dax.open.node.mrid"));

                    if (nodeId.Equals(cableBoxNode.mRID))
                    {
                        Logger.Log(LogLevel.Debug, cableBoxNode.Name + ": open switch on cable box bay");
                        dis.SetPropertyValue("cim.normalopen", true);
                    }
                }

                // Connect disconnector to CN
                Connect(busbarCn, dis);

                // Create CN between switch and cable
                CIMConnectivityNode switchCableCn = new CIMConnectivityNode(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(cableBoxNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.ConnectivityNode, EquipmentContainerRef = simpleNode, VoltageLevel = 400 };
                nextDerivedGuidCounter += 1;

                // Connect disconnector to cn
                Connect(dis, switchCableCn);

                // Connect cable to cn
                Connect(cable, switchCableCn);

            }


            // Slet build node punkt
            g.ObjectManager.Delete(cableBoxNode);

            // Create DAX node - ugly should be refactored!
            var daxNode = new DAXElectricNode(g.ObjectManager) { Name = simpleNode.Name, Description = simpleNode.Description, CIMObjectId = simpleNode.InternalId, ClassType = simpleNode.ClassType, Coords = simpleNode.Coords, VoltageLevel = simpleNode.VoltageLevel };
            ((TopologyProcessingResult)topologyData)._daxNodeByCimObj.Add(simpleNode, daxNode);
        }

        private void CreateSubstation(CIMGraph g, TableLogger tableLogger, CIMIdentifiedObject buildNode)
        {
            int nTransformers = 1;

            List<string> priBusbarsNames = new List<string>();
            Dictionary<int, BusbarInfo> priBusbarByTrafoNo = new Dictionary<int, BusbarInfo>();

            // Try get transformer count, if specified
            if (buildNode.ContainsPropertyValue("dax.transformer.count"))
            {
                Int32.TryParse(buildNode.GetPropertyValueAsString("dax.transformer.count"), out nTransformers);
            }

            // Try primary busbar names, if specified
            if (buildNode.VoltageLevel < 20000 && buildNode.ContainsPropertyValue("dax.busbar.pri"))
            {
                string[] busbarSplit = buildNode.GetPropertyValueAsString("dax.busbar.pri").Split(';');

                foreach (var busbarStr in busbarSplit)
                {
                    string[] busbarTrafoSplit = busbarStr.Split(',');

                    // Only busbar name
                    if (busbarTrafoSplit.Length == 1)
                    {
                        priBusbarByTrafoNo.Add(0, new BusbarInfo() { Name = busbarSplit[0] });

                        if (!priBusbarsNames.Contains(busbarSplit[0]))
                            priBusbarsNames.Add(busbarSplit[0]);
                    }
                    else if (busbarTrafoSplit.Length == 2)
                    {
                        int trafoNo = Convert.ToInt32(busbarTrafoSplit[0].Replace("TRF", ""));
                        priBusbarByTrafoNo.Add(trafoNo, new BusbarInfo() { Name = busbarTrafoSplit[1], TrafoNo = trafoNo });

                        if (!priBusbarsNames.Contains(busbarTrafoSplit[1]))
                            priBusbarsNames.Add(busbarTrafoSplit[1]);
                    }

                }

            }

            if (priBusbarsNames.Count == 0)
                priBusbarsNames.Add("1");



            var topologyData = (ITopologyProcessingResult)g.GetProcessingResult("Topology");

            // Create substation equipment container
            CIMEquipmentContainer newSubstation = new CIMEquipmentContainer(g.ObjectManager) { Name = buildNode.Name, ExternalId = buildNode.ExternalId, Coords = buildNode.Coords, ClassType = CIMClassEnum.Substation, VoltageLevel = buildNode.VoltageLevel };
            newSubstation.mRID = buildNode.mRID;
            newSubstation.SetPSRType(CIMMetaDataManager.Repository, buildNode.GetPSRType(CIMMetaDataManager.Repository));

            // Create DAX node - ugly should be refactored!
            var daxNode = new DAXElectricNode(g.ObjectManager) { Name = newSubstation.Name, Description = newSubstation.Description, CIMObjectId = newSubstation.InternalId, ClassType = newSubstation.ClassType, Coords = newSubstation.Coords, VoltageLevel = newSubstation.VoltageLevel };
            ((TopologyProcessingResult)topologyData)._daxNodeByCimObj.Add(newSubstation, daxNode);


            int nextDerivedGuidCounter = 25;

            g.IndexObject(newSubstation);

            ////////////////////////////////////////
            // Create secondary side

            int secVoltageLevel = 400;
            if (buildNode.VoltageLevel > 20000)
                secVoltageLevel = 10000;

            List<CIMIdentifiedObject> neighbours = new List<CIMIdentifiedObject>();
            neighbours.AddRange(buildNode.Neighbours);

            List<CIMConnectivityNode> secBusbarCNs = new List<CIMConnectivityNode>();

            for (int i = 0; i < nTransformers; i++)
            {
                // Create CN
                CIMConnectivityNode secondaryBusBarCn = new CIMConnectivityNode(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.ConnectivityNode, EquipmentContainerRef = newSubstation, VoltageLevel = secVoltageLevel };
                secBusbarCNs.Add(secondaryBusBarCn);

                nextDerivedGuidCounter += 1;
                secondaryBusBarCn.EquipmentContainerRef = newSubstation;

                // Copy vertex id from old t-junction punkt to CN in new t-juction
                int vertexId = g.ObjectManager.AdditionalObjectAttributes(buildNode).Vertex1Id;

                // Create LV busbar
                CIMConductingEquipment secondaryBusBar = new CIMConductingEquipment(g.ObjectManager) { Name = "" + (i + 1), mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.BusbarSection, EquipmentContainerRef = newSubstation, VoltageLevel = secVoltageLevel };
                nextDerivedGuidCounter += 3;

                // Add busbar to container
                newSubstation.Children.Add(secondaryBusBar);

                // Connect CN and busbar
                Connect(secondaryBusBarCn, secondaryBusBar);


                // Create bay and disconnector for each lower voltage cable 
                int bayCounter = 1;
                Dictionary<string, CIMEquipmentContainer> bayNameToBay = new Dictionary<string, CIMEquipmentContainer>();

                foreach (var cable in neighbours)
                {
                    if (cable.VoltageLevel < buildNode.VoltageLevel)
                    {
                        // Try get transformer count, if specified
                        if (nTransformers > 1)
                        {

                            int tranformerToConnect = 0;

                            if (cable.ContainsPropertyValue("dax.node.connect"))
                                Int32.TryParse(cable.GetPropertyValueAsString("dax.node.connect"), out tranformerToConnect);

                            // Don't connect cable unless specified which transformer to connect to
                            if (tranformerToConnect == 0)
                            {
                                Logger.Log(LogLevel.Warning, "NodeBuilder: Multiple transformer, but no dax.node.connect attribut specified for cable: " + cable.ToString() + " Will connect to transformer 1");
                                tranformerToConnect = 1;
                            }

                            // Detemine of cable should be connected now

                            if (tranformerToConnect != (i + 1))
                                continue; // not yet
                        }



                        secondaryBusBar.VoltageLevel = cable.VoltageLevel;

                        // Fjern existerende forbindelse til build node (dumt punkt)
                        buildNode.RemoveNeighbour(cable);
                        cable.RemoveNeighbour(buildNode);

                        CIMEquipmentContainer bay = new CIMEquipmentContainer(g.ObjectManager) { Name = "Bay " + bayCounter, mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.Bay, VoltageLevel = cable.VoltageLevel, EquipmentContainerRef = newSubstation };

                        // Try find if we need to connect to existing bay
                        if (cable.ContainsPropertyValue("dax.bay.name") && bayNameToBay.ContainsKey(cable.GetPropertyValueAsString("dax.bay.name")))
                        {
                            string bayName = cable.GetPropertyValueAsString("dax.bay.name");
                            var existingBay = bayNameToBay[bayName];
                            existingBay.AllowMultiFeed = true;
                            bay.AllowMultiFeed = true;
                        }


                        if (cable.ContainsPropertyValue("dax.bay.name") && !bayNameToBay.ContainsKey(cable.GetPropertyValueAsString("dax.bay.name")))
                            bayNameToBay.Add(cable.GetPropertyValueAsString("dax.bay.name"), bay);

                        nextDerivedGuidCounter += 1;
                        bayCounter++;
                        newSubstation.Children.Add(bay);


                        var switchType = CIMClassEnum.Fuse;
                        if (buildNode.VoltageLevel > 20000)
                            switchType = CIMClassEnum.Breaker;

                        var feederSwitch = new CIMConductingEquipment(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), VoltageLevel = cable.VoltageLevel, ClassType = switchType, EquipmentContainerRef = bay };
                        nextDerivedGuidCounter += 3;
                        feederSwitch.SetPropertyValue("cim.normalopen", false);
                        bay.Children.Add(feederSwitch);

                        // Open switch in node if cable says so
                        if (cable.ContainsPropertyValue("dax.open.node.mrid"))
                        {
                            Guid nodeId = Guid.Parse(cable.GetPropertyValueAsString("dax.open.node.mrid"));

                            if (nodeId.Equals(buildNode.mRID))
                            {
                                feederSwitch.SetPropertyValue("cim.normalopen", true);
                                Logger.Log(LogLevel.Debug, buildNode.Name + ": open switch on st bay: " + (bayCounter - 1));
                            }
                        }


                        // Connect switch to busbar CN
                        Connect(feederSwitch, secondaryBusBarCn);

                        // Create CN between switch and cable
                        CIMConnectivityNode feederCableCn = new CIMConnectivityNode(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.ConnectivityNode, EquipmentContainerRef = newSubstation, VoltageLevel = cable.VoltageLevel };
                        nextDerivedGuidCounter += 1;

                        feederCableCn.EquipmentContainerRef = bay;
                        g.ObjectManager.AdditionalObjectAttributes(feederCableCn).Vertex1Id = vertexId;

                        // Connect switch to cable CN
                        Connect(feederSwitch, feederCableCn);

                        // Connect cable CN to cable
                        Connect(feederCableCn, cable);

                        // Create feeder
                        CreateFeederInfo feederInfo = new CreateFeederInfo();
                        feederInfo.ConnectivityNode = feederCableCn;
                        feederInfo.IsTransformerFeeder = false;
                        g.AddFeeder(feederInfo, feederCableCn);

                    }
                }
            }


            ////////////////////////////////////////
            // Create primary side

            List<CIMConductingEquipment> priBusbars = new List<CIMConductingEquipment>();
            List<CIMConnectivityNode> priBusbarCNs = new List<CIMConnectivityNode>();

            foreach (var busbarName in priBusbarsNames)
            {

                // Create CN
                CIMConnectivityNode primaryBusBarCn = new CIMConnectivityNode(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.ConnectivityNode, EquipmentContainerRef = newSubstation, VoltageLevel = buildNode.VoltageLevel };
                primaryBusBarCn.EquipmentContainerRef = newSubstation;
                nextDerivedGuidCounter += 1;

                // Create busbar
                CIMConductingEquipment primaryBusbar = new CIMConductingEquipment(g.ObjectManager) { Name = busbarName, mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.BusbarSection, EquipmentContainerRef = newSubstation, VoltageLevel = buildNode.VoltageLevel };
                nextDerivedGuidCounter += 3;


                // Add busbar to container
                newSubstation.Children.Add(primaryBusbar);

                // Connect CN and busbar
                Connect(primaryBusBarCn, primaryBusbar);

                priBusbars.Add(primaryBusbar);
                priBusbarCNs.Add(primaryBusBarCn);

            }

            // Create bay and disconnector for high voltage cable 
            neighbours = new List<CIMIdentifiedObject>();
            neighbours.AddRange(buildNode.Neighbours);

            foreach (var cable in neighbours)
            {
                if (cable.VoltageLevel == buildNode.VoltageLevel)
                {
                    //primaryBusbar.VoltageLevel = cable.VoltageLevel;

                    // Fjern existerende forbindelse til build node (dumt punkt)
                    buildNode.RemoveNeighbour(cable);
                    cable.RemoveNeighbour(buildNode);

                    var bay = new CIMEquipmentContainer(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.Bay, VoltageLevel = cable.VoltageLevel, EquipmentContainerRef = newSubstation };
                    nextDerivedGuidCounter += 1;
                    newSubstation.Children.Add(bay);

                    var switchType = CIMClassEnum.LoadBreakSwitch;
                    if (buildNode.VoltageLevel > 20000)
                        switchType = CIMClassEnum.Breaker;

                    var priFeederSwitch = new CIMConductingEquipment(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true), VoltageLevel = cable.VoltageLevel, ClassType = switchType, EquipmentContainerRef = bay };
                    priFeederSwitch.SetPropertyValue("cim.normalopen", false);
                    nextDerivedGuidCounter += 3;
                    bay.Children.Add(priFeederSwitch);

                    // Connect switch to busbar CN
                    Connect(priFeederSwitch, priBusbarCNs[0]);

                    // Connect switch to cable
                    Connect(priFeederSwitch, cable);
                }
            }

            

            for (int i = 0; i < nTransformers; i++)
            {

                ////////////////////////////////////////
                // Create transformer
                var trafo = new CIMPowerTransformer(g.ObjectManager)
                {
                    Name = "T" + (i+1),
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    ClassType = CIMClassEnum.PowerTransformer,
                    VoltageLevel = buildNode.VoltageLevel,
                    EquipmentContainerRef = newSubstation
                };
                nextDerivedGuidCounter += 110;

                newSubstation.Children.Add(trafo);

                // Create primary transformer CN
                CIMConnectivityNode primaryTfCn = new CIMConnectivityNode(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    ClassType = CIMClassEnum.ConnectivityNode,
                    EquipmentContainerRef = newSubstation,
                    VoltageLevel = buildNode.VoltageLevel
                };
                nextDerivedGuidCounter += 1;

                // Connect trafo to primary CN
                Connect(trafo, primaryTfCn);


                // Create secondary transformer CN
                CIMConnectivityNode secTfCn = new CIMConnectivityNode(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    ClassType = CIMClassEnum.ConnectivityNode,
                    EquipmentContainerRef = newSubstation,
                    VoltageLevel = secVoltageLevel
                };
                nextDerivedGuidCounter += 1;

                // Connect trafo to secondary CN
                Connect(trafo, secTfCn);


                // Create primary transformer cable
                var priCable = new CIMConductingEquipment(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    VoltageLevel = buildNode.VoltageLevel,
                    ClassType = CIMClassEnum.ACLineSegment,
                    EquipmentContainerRef = newSubstation
                };
                nextDerivedGuidCounter += 3;

                priCable.SetPropertyValue("cim.length", 1.0);
                newSubstation.Children.Add(priCable);

                // Create primary cable CN
                CIMConnectivityNode primaryCableCn = new CIMConnectivityNode(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    ClassType = CIMClassEnum.ConnectivityNode,
                    EquipmentContainerRef = newSubstation,
                    VoltageLevel = buildNode.VoltageLevel
                };
                nextDerivedGuidCounter += 1;


                // Connect cable to primary CN
                Connect(priCable, primaryCableCn);

                // Connect primary trafo CN to primary cable CN
                Connect(primaryTfCn, priCable);

                // Create primary transformer bay
                var tfPriBay = new CIMEquipmentContainer(g.ObjectManager)
                {
                    Name = "T" + (i + 1) + " PRI",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    ClassType = CIMClassEnum.Bay,
                    VoltageLevel = buildNode.VoltageLevel,
                    EquipmentContainerRef = newSubstation
                };
                nextDerivedGuidCounter += 1;

                newSubstation.Children.Add(tfPriBay);

                CIMClassEnum switchType2 = CIMClassEnum.LoadBreakSwitch;
                if (buildNode.VoltageLevel > 20000)
                    switchType2 = CIMClassEnum.Breaker;

                var tfPriBaySwitch = new CIMConductingEquipment(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    VoltageLevel = buildNode.VoltageLevel,
                    ClassType = switchType2,
                    EquipmentContainerRef = tfPriBay
                };
                nextDerivedGuidCounter += 3;

                tfPriBaySwitch.SetPropertyValue("cim.normalopen", false);

                tfPriBay.Children.Add(tfPriBaySwitch);

                // Connect primary cable CN to switch
                Connect(primaryCableCn, tfPriBaySwitch);

                // Connect switch to primary busbar
                if (priBusbars.Count == 1)
                    Connect(tfPriBaySwitch, priBusbarCNs[0]);
                else
                {
                    // More busbars, fall back to busbar 1
                    var busbarCnToUse = priBusbarCNs[0];

                    // Try lookup trafo busbar connect info
                    if (priBusbarByTrafoNo.ContainsKey(i+1))
                    {
                        string trafoBusBarName = priBusbarByTrafoNo[i + 1].Name;

                        // try find busbar with that name
                        for (int b = 0; b < priBusbars.Count; b++)
                        {
                            if (priBusbars[b].Name == trafoBusBarName)
                                busbarCnToUse = priBusbarCNs[b];
                        }
                    }

                    Connect(tfPriBaySwitch, busbarCnToUse);

                }


                // Create secondary transformer cable
                var secCable = new CIMConductingEquipment(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    VoltageLevel = secVoltageLevel,
                    ClassType = CIMClassEnum.ACLineSegment,
                    EquipmentContainerRef = newSubstation
                };

                
                if (secCable.mRID.ToString() == "4bee251e-b43f-fff0-919d-62de8a508d0e")
                {
                    GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true);
                }

                nextDerivedGuidCounter += 3;

                secCable.SetPropertyValue("cim.length", 1.0);
                newSubstation.Children.Add(secCable);

                // Create secondary cable CN
                CIMConnectivityNode secCableCn = new CIMConnectivityNode(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    ClassType = CIMClassEnum.ConnectivityNode,
                    EquipmentContainerRef = newSubstation,
                    VoltageLevel = buildNode.VoltageLevel
                };
                nextDerivedGuidCounter += 1;

                // Connect cable to secondary cable CN
                Connect(secCable, secCableCn);

                // Connect secondary trafo CN to secondary cable
                Connect(secTfCn, secCable);

                // Create secondary transformer bay
                var tfSecBay = new CIMEquipmentContainer(g.ObjectManager)
                {
                    Name = "T" + (i + 1) + " SEC",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    ClassType = CIMClassEnum.Bay,
                    VoltageLevel = secVoltageLevel,
                    EquipmentContainerRef = newSubstation
                };
                nextDerivedGuidCounter += 1;

                newSubstation.Children.Add(tfSecBay);

                var switchType3 = CIMClassEnum.Disconnector;

                var tfSecBaySwitch = new CIMConductingEquipment(g.ObjectManager)
                {
                    Name = "auto generated",
                    mRID = GUIDHelper.CreateDerivedGuid(buildNode.mRID, nextDerivedGuidCounter, true),
                    VoltageLevel = secVoltageLevel,
                    ClassType = switchType3,
                    EquipmentContainerRef = tfSecBay
                };
                nextDerivedGuidCounter += 3;

                tfSecBaySwitch.SetPropertyValue("cim.normalopen", false);

                tfSecBay.Children.Add(tfSecBaySwitch);

                // Connect sec cable CN to switch
                Connect(secCableCn, tfSecBaySwitch);

                // Connect switch to secondary busbar
                Connect(tfSecBaySwitch, secBusbarCNs[i]);
            }


            // Slet build node punkt
            g.ObjectManager.Delete(buildNode);

        }

        private void Connect(CIMIdentifiedObject eq1, CIMIdentifiedObject eq2)
        {
             eq1.AddNeighbour(eq2);
             eq2.AddNeighbour(eq1);
        }
    }

    public class BusbarInfo
    {
        public string Name { get; set; }
        public int TrafoNo { get; set; }
    }
}
