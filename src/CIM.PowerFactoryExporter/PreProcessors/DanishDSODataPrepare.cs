using CIM.PhysicalNetworkModel;
using CIM.PhysicalNetworkModel.FeederInfo;
using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Extensions;
using CIM.PhysicalNetworkModel.Traversal.Internals;
using DAX.IO.CIM;

namespace CIM.PowerFactoryExporter.PreProcessors
{
    /// <summary>
    /// Used to massage CIM data to Danish DSO needs
    /// </summary>
    public class DanishDSODataPrepare : IPreProcessor
    {
        int _guidOffset = 1000;

        MappingContext _mappingContext;

        public DanishDSODataPrepare(MappingContext mappingContext)
        {
            _mappingContext = mappingContext;
        }

        public IEnumerable<IdentifiedObject> Transform(CimContext inputContext, IEnumerable<IdentifiedObject> inputParam)
        {
            var context = new InMemCimContext(inputParam);
            
            FeederInfoContext feederContext = new FeederInfoContext(context);
            feederContext.CreateFeederObjects();

            List<IdentifiedObject> input = inputParam.ToList();

            HashSet<IdentifiedObject> dropList = new HashSet<IdentifiedObject>();
            List<IdentifiedObject> addList = new List<IdentifiedObject>();

            // AssetInfo to Asset ref dictionary
            Dictionary<string, string> assetInfoToAssetRef = new Dictionary<string, string>();

            foreach (var inputCimObject in input)
            {
                if (inputCimObject is Asset)
                {
                    var asset = inputCimObject as Asset;

                    if (asset.AssetInfo != null && !assetInfoToAssetRef.ContainsKey(asset.AssetInfo.@ref))
                        assetInfoToAssetRef.Add(asset.AssetInfo.@ref, asset.mRID);
                }
            }

            // Asset to Equipment ref dictionary
            Dictionary<string, string> assetToEquipmentRef = new Dictionary<string, string>();

            foreach (var inputCimObject in input)
            {
                if (inputCimObject is PowerSystemResource)
                {
                    var psr = inputCimObject as PowerSystemResource;
                    if (psr.Assets != null)
                        assetToEquipmentRef.Add(psr.Assets.@ref, psr.mRID);
                }
            }

            // Set busbar names to station + voltagelevel + bay
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is BusbarSection)
                {
                    var bus = inputCimObject as BusbarSection;

                    var st = bus.GetSubstation(context);

                    bus.name = st.name + "_" + GetVoltageLevelStr(bus.BaseVoltage) + "_" + bus.name;

                    var feederInfo = feederContext.GeConductingEquipmentFeederInfo(bus);
                    if (feederInfo != null && feederInfo.Feeders != null && feederInfo.Feeders.Count > 0)
                    {
                        var feeder = feederInfo.Feeders[0];

                        if (feeder.ConnectionPoint.Substation != null)
                            bus.description = feeder.ConnectionPoint.Substation.name;
                    }
                }
            }

            // Set feeder name on junction connectivity nodes between cables
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is ConnectivityNode)
                {
                    var cn = inputCimObject as ConnectivityNode;

                    var cnEqs = context.GetConnections(cn);

                    if (cnEqs.Count > 0)
                    {
                        if (cnEqs.All(e => e.ConductingEquipment is ACLineSegment))
                        {
                            var eq = cnEqs.First().ConductingEquipment;

                            var feederInfo = feederContext.GeConductingEquipmentFeederInfo(eq);

                            if (feederInfo != null && feederInfo.Feeders != null && feederInfo.Feeders.Count > 0)
                            {
                                var feeder = feederInfo.Feeders[0];

                                if (feeder.ConnectionPoint.Substation != null)
                                    cn.description = feeder.ConnectionPoint.Substation.name;
                            }
                        }
                    }
                }
            }

            // Set peterson coil name to station + gis name + min og max
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is PetersenCoil)
                {
                    var coil = inputCimObject as PetersenCoil;
                                     
                    var st = coil.GetSubstation(context);

                    if (coil.Asset != null && coil.Asset(context).AssetInfo != null && coil.Asset(context).AssetInfo.@ref != null)
                    {
                        var coilInfo = context.GetObject<PetersenCoilInfoExt>(coil.Asset(context).AssetInfo.@ref);

                        coil.name = st.name + " " + coil.name;

                        if (coilInfo != null && coilInfo.minimumCurrent != null && coilInfo.maximumCurrent != null)
                            coil.name += " " + (int)coilInfo?.minimumCurrent?.Value + "-" + (int)coilInfo?.maximumCurrent?.Value;
                        else
                            Logger.Log(LogLevel.Warning, "Slukkepole på station: " + st.name + " mangler værdier.");
                    }
                    else
                        coil.name = st.name + " " + coil.name + " 0-0";
                }
            }

            // Set reactor coil (linear shunt compensator) to station + gis name + min og max
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is LinearShuntCompensator)
                {
                    var coil = inputCimObject as LinearShuntCompensator;

                    var st = coil.GetSubstation(context);

                    if (coil.Asset(context) != null && coil.Asset(context).AssetInfo != null && coil.Asset(context).AssetInfo.@ref != null)
                    {
                        var coilInfo = context.GetObject<LinearShuntCompensatorInfoExt>(coil.Asset(context).AssetInfo.@ref);

                        coil.name = st.name + " " + coil.name;

                        if (coilInfo != null && coilInfo.minimumReactivePower != null && coilInfo.maximumReactivePower != null)
                            coil.name += " " + (int)coilInfo?.minimumReactivePower?.Value + "-" + (int)coilInfo?.maximumReactivePower?.Value;
                        else
                            Logger.Log(LogLevel.Warning, "Reaktorspole på station: " + st.name + " mangler værdier.");
                    }
                    else
                        coil.name = st.name + " " + coil.name + " 0-0";
                }
            }

            // Remove injection > 50 kV
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is ExternalNetworkInjection)
                {
                    var inj = inputCimObject as ExternalNetworkInjection;

                    if (inj.BaseVoltage > 50000)
                    {
                        dropList.Add(inj);

                        var injConnections = context.GetConnections(inj);

                        foreach (var injCon in injConnections)
                            dropList.Add(injCon.Terminal);
                    }
                }
            }


            // Put x in front of all cable boxes and busbars in cable boxes
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is Substation)
                {
                    var st = inputCimObject as Substation;

                    if (st.PSRType == "CableBox")
                    {
                        st.name = "X" + st.name;
                    }
                }
                else if (inputCimObject is BusbarSection)
                {
                    var busbar = inputCimObject as BusbarSection;

                    var st = busbar.GetSubstation(context);

                    if (st.PSRType == "CableBox")
                    {
                        busbar.name = "X" + busbar.name;
                    }
                }
            }

            // Fix and check objects
            foreach (var inputCimObject in input)
            {
                // Remove switch gear busbar asset model information (because PF complain about missing type, and Konstant/Thue says he don't want types into PF for now
                if (inputCimObject is BusbarSectionInfo)
                {
                    BusbarSectionInfo bsi = inputCimObject as BusbarSectionInfo;
                    bsi.AssetModel = null;

                    var assetMrid = assetInfoToAssetRef[inputCimObject.mRID];
                    var asset = context.GetObject<Asset>(assetMrid);
                    asset.type = null;
                    asset.name = null;
                    asset.AssetModel = null;
                }

                // Remove asset manufacture information on busbars and switches
                if (inputCimObject is BusbarSection || inputCimObject is Switch)
                {
                    ConductingEquipment ci = inputCimObject as ConductingEquipment;

                    if (ci.Assets != null)
                    {
                        var asset = context.GetObject<Asset>(ci.Assets.@ref);
                        asset.type = null;
                        asset.name = null;
                        asset.AssetModel = null;
                    }
                }

                // Remove measurment current transformer and cts sitting on voltage level < 60 kV
                if (inputCimObject is CurrentTransformer && !((CurrentTransformer)inputCimObject).PSRType.ToLower().Contains("kundemaaling"))
                {
                    var ct = inputCimObject as CurrentTransformer;
                 
                    if (context.Exists(ct.Terminal.@ref))
                    {
                        var ctTerminal = context.GetObject<Terminal>(ct.Terminal.@ref);

                        var ctEq = context.GetObject<ConductingEquipment>(ctTerminal.ConductingEquipment.@ref);

                        if (ctEq.BaseVoltage < 60000)
                        {
                            dropList.Add(ct);
                        }
                    }
                    else
                    {
                        dropList.Add(ct);
                    }
                  
                    // Move CT to line end, transformer end
                    var st = ct.GetSubstation(context);

                    foreach (var eq in st.GetEquipments(context))
                    {
                        // If component inside same bay as ct
                        if (eq is ConductingEquipment && eq.EquipmentContainer.@ref == ct.EquipmentContainer.@ref)
                        {
                            var ci = eq as ConductingEquipment;

                            var ciConnections = context.GetConnections(ci);
                            

                            foreach (var ciConnection in ciConnections)
                            {
                                var ciNeighbors = context.GetConnections(ciConnection.ConnectivityNode).Where(c => c.ConductingEquipment != ci).ToList();

                                if (ciNeighbors.Any(c => c.ConductingEquipment is ACLineSegment))
                                {
                                    ct.Terminal.@ref = ciConnection.Terminal.mRID;
                                }
                                else if (ciNeighbors.Any(c => c.ConductingEquipment is PowerTransformer))
                                {
                                    ct.Terminal.@ref = ciConnection.Terminal.mRID;
                                }
                                else if (ciNeighbors.Count == 0)
                                {
                                    ct.Terminal.@ref = ciConnection.Terminal.mRID;
                                }

                            }
                        }
                    }
                }

                // Check that current transformer infos has currents 
                if (inputCimObject is CurrentTransformerInfoExt)
                {
                    var ctInfo = inputCimObject as CurrentTransformerInfoExt;
                    var assetMrid = assetInfoToAssetRef[ctInfo.mRID];
                    var eqMrid = assetToEquipmentRef[assetMrid];

                    var ct = context.GetObject<CurrentTransformerExt>(eqMrid);
                    var ctAsset = context.GetObject<Asset>(assetMrid);

                    if (ct.PSRType != null && ct.PSRType == "StromTransformer")
                    {
                        // Make sure primary and secondary current is set, because otherwise PF import fails
                        if (ctInfo.primaryCurrent == null)
                        {
                            var stName = ct.GetSubstation(context).name;
                            var bayName = ct.GetBay(context).name;

                            Logger.Log(LogLevel.Warning, "CT Missing primary current. Will not be transfered to PF: " + stName + " " + bayName);
                            ctInfo.primaryCurrent = new CurrentFlow() { Value = 0, unit = UnitSymbol.A };

                            dropList.Add(ct);
                            dropList.Add(ctAsset);
                            dropList.Add(ctInfo);
                        }

                        if (ctInfo.secondaryCurrent == null)
                        {
                            var stName = ct.GetSubstation(context).name;
                            var bayName = ct.GetBay(context).name;

                            Logger.Log(LogLevel.Warning, "CT Missing secondary current: " + stName + " " + bayName);
                            ctInfo.secondaryCurrent = new CurrentFlow() { Value = 0, unit = UnitSymbol.A };
                        }
                    }
                    else
                    {
                        dropList.Add(ct);
                        dropList.Add(ctAsset);
                        dropList.Add(ctInfo);
                    }
                }

                // Remove potential transformers sitting on voltage level < 60 kV
                if (inputCimObject is PotentialTransformer)
                {
                    var pt = inputCimObject as PotentialTransformer;

                    // If terminal point to object we don't have including - i.e. some 400 volt component - don't bother with the CT
                    if (context.GetAllObjects().Exists(o => o.mRID == pt.Terminal.@ref))
                    {
                        var ptTerminal = context.GetObject<Terminal>(pt.Terminal.@ref);

                        var ptEq = context.GetObject<ConductingEquipment>(ptTerminal.ConductingEquipment.@ref);

                        if (ptEq.BaseVoltage < 60000)
                        {
                            dropList.Add(pt);
                        }
                    }
                    else
                        dropList.Add(pt);
                }

                // Check that potential transformer info has voltages
                if (inputCimObject is PotentialTransformerInfoExt)
                {
                    var vtInfo = inputCimObject as PotentialTransformerInfoExt;

                    var assetMrid = assetInfoToAssetRef[vtInfo.mRID];
                    var eqMrid = assetToEquipmentRef[assetMrid];

                    var vtAsset = context.GetObject<Asset>(assetMrid);
                    var vt = context.GetObject<PotentialTransformer>(eqMrid);
                    var vtSt = vt.GetSubstation(context);

                    // Make sure primary and secondary voltage is set, because otherwise PF import fails
                    if (vtInfo.primaryVoltage == null)
                    {
                        vtInfo.primaryVoltage = new Voltage() { Value = 0, unit = UnitSymbol.V };

                        var stName = vt.GetSubstation(context).name;
                        var bayName = vt.GetBay(context).name;

                        Logger.Log(LogLevel.Warning, "VT Missing primary voltage. VT will not be transfered to PF." + stName + " " + bayName);

                        dropList.Add(vt);
                        dropList.Add(vtAsset);
                        dropList.Add(vtInfo);
                    }
                    else if (vtInfo.secondaryVoltage == null)
                    {
                        vtInfo.secondaryVoltage = new Voltage() { Value = 0, unit = UnitSymbol.V };

                        var stName = vt.GetSubstation(context).name;
                        var bayName = vt.GetBay(context).name;

                        Logger.Log(LogLevel.Warning, "VT Missing secondary voltage. VI will not be transfered to PF." + stName + " " + bayName);

                        dropList.Add(vt);
                        dropList.Add(vtAsset);
                        dropList.Add(vtInfo);
                    }
                }


                // Set relay names to station + bay
                if (inputCimObject is ProtectionEquipment)
                {
                    var relay = inputCimObject as ProtectionEquipment;

                    // get relay station and bay via the switch it is connected to
                    if (relay.ProtectedSwitches != null && relay.ProtectedSwitches.Length > 0)
                    {
                        try
                        {
                            var peSw = context.GetObject<PowerSystemResource>(relay.ProtectedSwitches[0].@ref);
                            var bay = peSw.GetBay(context);
                            var st = peSw.GetSubstation(context);

                            relay.name = st.name + " " + bay.name;
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Warning, "Cannot find switch: " + relay.ProtectedSwitches[0].@ref + " connected to replay: " + inputCimObject.mRID);
                        }
                    }
                    else
                        dropList.Add(inputCimObject);
                }

                // Set electrical values on internal substation cables to 0
                if (inputCimObject is ACLineSegment)
                {
                    var acls = inputCimObject as ACLineSegment;

                    if (acls.PSRType == "InternalCable")
                    {
                        // Set name to internal cable
                        acls.name = "Internal Cable";

                        // Set length to 1 meter
                        acls.length.Value = 1;

                        // Set value to zero
                        acls.r = new Resistance() { Value = 0 };
                        acls.r0 = new Resistance() { Value = 0 };
                        acls.x = new Reactance() { Value = 0 };
                        acls.x0 = new Reactance() { Value = 0 };
                        acls.bch = new Susceptance() { Value = 0 };
                        acls.b0ch = new Susceptance() { Value = 0 };
                        acls.gch = new Conductance() { Value = 0 };
                        acls.g0ch = new Conductance() { Value = 0 };
                    }
                }

                // Sæt transformer vikling navn og r,x,b,g værdier på vikling 2 til 0 
                if (inputCimObject is PowerTransformerEndExt)
                {
                    var ptEnd = inputCimObject as PowerTransformerEndExt;

                    var pt = context.GetObject<PowerTransformer>(ptEnd.PowerTransformer.@ref);

                    ptEnd.name = pt.Substation(context).name + "_" + pt.name + "_T" + ptEnd.endNumber;


                    if (ptEnd.endNumber == "2")
                    {
                        // Set value to zero
                        ptEnd.r = new Resistance() { Value = 0 };
                        ptEnd.r0 = new Resistance() { Value = 0 };
                        ptEnd.x = new Reactance() { Value = 0 };
                        ptEnd.x0 = new Reactance() { Value = 0 };
                        ptEnd.b = new Susceptance() { Value = 0 };
                        ptEnd.b0 = new Susceptance() { Value = 0 };
                        ptEnd.g = new Conductance() { Value = 0 };
                        ptEnd.g0 = new Conductance() { Value = 0 };
                    }
                }
                            

                // Ensure bay name is max 32 charaters
                if (inputCimObject is IdentifiedObject && inputCimObject.name != null && inputCimObject.name.Length > 32)
                {
                    inputCimObject.name = inputCimObject.name.Substring(0, 32);
                }

                // Set name of disconnectors to ADSK
                if (inputCimObject is Disconnector)
                {
                    inputCimObject.name = "ADSK";
                }

                // Set name of disconnectors to ADSK
                if (inputCimObject is Fuse)
                {
                    inputCimObject.name = "SIKRING";
                }

                // Ensure connectivity nodes / busbars have proper names. 
                // Needed to support short circuit result extracts etc. PF uses the name of the node/busbar in reports.
                // Also needed to support time series import (Jakob busbar naming)
                if (inputCimObject is ConnectivityNode)
                {
                    var cn = inputCimObject as ConnectivityNode;

                    if (cn.name == null || cn.name.Length == 0)
                    {
                        var cnNeighbors = cn.GetNeighborConductingEquipments(context);

                        var pt = cnNeighbors.Find(o => o is PowerTransformer) as PowerTransformer;
                        var bus = cnNeighbors.Find(o => o is BusbarSection);

                        if (pt != null)
                        {
                            var stVoltageLevels = context.GetSubstationVoltageLevels(pt.Substation(context));

                            var ptConnections = context.GetConnections(pt);
                            var ptTerminal = ptConnections.First(c => c.ConnectivityNode == cn);
                            var ptEnds = pt.GetEnds(context);
                            var ptEnd = ptEnds.Find(e => e.Terminal.@ref == ptTerminal.Terminal.mRID);
                            double ptEndVoltageLevel = ptEnd.BaseVoltage;

                            if (pt.name != null)
                            {
                                // HACK SHOULD BE CLEANED UP
                                // Terminal actual exist in source, but get trown away in filter, because nothing connected to it

                                if (!ptConnections.Exists(c => c.Terminal.sequenceNumber == "2"))
                                {
                                    Logger.Log(LogLevel.Info, "Station: " + pt.Substation(context).name + " Trafo: " + pt.name + " mangler secondær skinne. Vil bliver oprettet");

                                    var ptLvCn = new ConnectivityNode() { mRID = Guid.NewGuid().ToString(), name = pt.GetSubstation(context).name + "_" + GetVoltageLevelStr(400) + "_" + pt.name.Replace("TRF", "") };

                                    addList.Add(ptLvCn);

                                    if (stVoltageLevels.Exists(o => o.BaseVoltage == 400))
                                    {
                                        var vl = stVoltageLevels.First(o => o.BaseVoltage == 400);

                                        _mappingContext.ConnectivityNodeToVoltageLevel.Add(ptLvCn, vl);
                                    }
                                    else
                                    {
                                        var vl = new VoltageLevel()
                                        {
                                            mRID = Guid.NewGuid().ToString(),
                                            name = "0,4 kV",
                                            EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = pt.Substation(context).mRID },
                                            BaseVoltage = 400
                                        };

                                        addList.Add(vl);

                                        stVoltageLevels.Add(vl);
                                        _mappingContext.ConnectivityNodeToVoltageLevel.Add(ptLvCn, vl);
                                    }

                                    var ptLvTerminal = new Terminal()
                                    {
                                        mRID = Guid.NewGuid().ToString(),
                                        phases = PhaseCode.ABCN,
                                        phasesSpecified = true,
                                        sequenceNumber = "2",
                                        ConnectivityNode = new TerminalConnectivityNode() { @ref = ptLvCn.mRID },
                                        ConductingEquipment = new TerminalConductingEquipment { @ref = pt.mRID }
                                    };

                                    var ptEnd2 = context.GetPowerTransformerEnds(pt).First(p => p.endNumber == "2");
                                    ptEnd2.Terminal = new TransformerEndTerminal() { @ref = ptLvTerminal.mRID };

                                    addList.Add(ptLvTerminal);
                                }
                            }



                            // IF the PT END CN is connected to a conducting equipment
                            if (cnNeighbors.Exists(o => !(o is PowerTransformer) && o.BaseVoltage > 0.0))
                            {
                                ptEndVoltageLevel = cnNeighbors.First(o => !(o is PowerTransformer) && o.BaseVoltage > 0.0).BaseVoltage;

                                if (stVoltageLevels.Exists(o => o.BaseVoltage == ptEndVoltageLevel))
                                {
                                    var vl = stVoltageLevels.First(o => o.BaseVoltage == ptEndVoltageLevel);

                                    _mappingContext.ConnectivityNodeToVoltageLevel.Add(cn, vl);
                                }
                            }
                            // IF the PT END CN is *not* connected to anything
                            else
                            {
                                // Set CN to PT END voltage level
                                var vl = stVoltageLevels.First(o => o.BaseVoltage == ptEndVoltageLevel);
                                _mappingContext.ConnectivityNodeToVoltageLevel.Add(cn, vl);

                                // If 60 kV transformer secondary side
                                if (ptEndVoltageLevel < 20000 && ptEndVoltageLevel > 5000)
                                {
                                    if (ptEnds.Exists(e => e.BaseVoltage == 60000))
                                    {
                                        EnergyConsumer ec = new EnergyConsumer()
                                        {
                                            mRID = GUIDHelper.CreateDerivedGuid(Guid.Parse(pt.mRID), 1001, true).ToString(),
                                            name = pt.GetSubstation(context).name + "_" + GetVoltageLevelStr(ptEndVoltageLevel) + "_" + pt.name.Replace("TRF", ""),
                                            EquipmentContainer = new EquipmentEquipmentContainer() { @ref = vl.mRID },
                                            BaseVoltage = vl.BaseVoltage
                                        };

                                        Terminal ecTerm = new Terminal()
                                        {
                                            mRID = GUIDHelper.CreateDerivedGuid(Guid.Parse(pt.mRID), 1002, true).ToString(),
                                            name = ec.name + "_T1",
                                            ConductingEquipment = new TerminalConductingEquipment() { @ref = ec.mRID },
                                            ConnectivityNode = new TerminalConnectivityNode() { @ref = cn.mRID }
                                        };

                                        addList.Add(ec);
                                        addList.Add(ecTerm);
                                    }
                                }

                            }

                            cn.name = pt.GetSubstation(context).name + "_" + GetVoltageLevelStr(ptEndVoltageLevel) + "_" + pt.name;
                        }
                        else if (bus != null && bus.name != null)
                        {
                            cn.name = bus.name;
                            cn.description = bus.description;
                        }
                        else
                        {
                            var bay = cn.GetBay(context);

                            if (bay != null)
                            {
                                if (cnNeighbors.Count == 1 || cnNeighbors.Exists(n => !(n is Switch)))
                                    cn.name = bay.name + " ENDE";
                                else
                                    cn.name = bay.name;
                            }
                            else
                                cn.name = "CN";
                        }
                    }
                }
            }


            // return objects, except the one dropped
            foreach (var inputObj in input)
            {
                if (!dropList.Contains(inputObj))
                    yield return inputObj;
            }

            // yield added objects,
            foreach (var inputObj in addList)
            {
                yield return inputObj;
            }
        }

        private string GetVoltageLevelStr(double voltageLevel)
        {
            string vlStr = "";
            if (voltageLevel == 400)
                vlStr = "004";
            else if (voltageLevel == 10000)
                vlStr = "010";
            else if (voltageLevel == 15000)
                vlStr = "015";
            else if (voltageLevel == 30000)
                vlStr = "030";
            else if (voltageLevel == 60000)
                vlStr = "060";
            else
                throw new Exception("Don't know how to handle voltage level: " + voltageLevel);

            return vlStr;
        }
    }
}
