using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.NetworkModel.CIM;
using DAX.Util;

namespace DAX.IO.CIM
{
    public class NetSamSimpleJunctionProcessor : IGraphProcessor
    {
        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

        public void Run(CIMGraph g, CimErrorLogger tableLogger)
        {
            Logger.Log(LogLevel.Debug, "NetSam Simple Junction Processor: Build node-breaker model (indre skematik) in i.e. T-Junctions and/or towers...");

            var topologyData = (ITopologyProcessingResult)g.GetProcessingResult("Topology");

            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.BuildSimpleJunction)
                {
                   
                    List<CIMIdentifiedObject> neighbours = new List<CIMIdentifiedObject>();
                    neighbours.AddRange(obj.Neighbours);

                    if (neighbours.Count > 1)
                    {
                        // Create junction equipment container
                        CIMEquipmentContainer junction = new CIMEquipmentContainer(g.ObjectManager) { Name = obj.Name, ExternalId = obj.ExternalId, Coords = obj.Coords, ClassType = CIMClassEnum.Enclosure, VoltageLevel = 400 };
                        junction.mRID = obj.mRID;
                        junction.SetPSRType(CIMMetaDataManager.Repository, obj.GetPSRType(CIMMetaDataManager.Repository));

                        int nextDerivedGuidCounter = 3;

                        g.IndexObject(junction);

                        // Create CN
                        CIMConnectivityNode cn = new CIMConnectivityNode(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(obj.mRID, nextDerivedGuidCounter), ClassType = CIMClassEnum.ConnectivityNode, EquipmentContainerRef = junction, VoltageLevel = 400 };
                        cn.EquipmentContainerRef = junction;

                        nextDerivedGuidCounter += 3;

                        // Copy vertex id from old t-junction punkt to CN in new t-juction
                        int vertexId = g.ObjectManager.AdditionalObjectAttributes(obj).Vertex1Id;
                        g.ObjectManager.AdditionalObjectAttributes(cn).Vertex1Id = vertexId;

                        // Create busbar
                        CIMConductingEquipment busbar = new CIMConductingEquipment(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(obj.mRID, nextDerivedGuidCounter), ClassType = CIMClassEnum.BusbarSection, EquipmentContainerRef = junction, VoltageLevel = 400 };

                        // Add busbar to container
                        junction.Children.Add(busbar);

                        // Connect CN and busbar
                        cn.AddNeighbour(busbar);
                        busbar.AddNeighbour(cn);

                        // junction (busbar) kan have mange naboer
                        nextDerivedGuidCounter += 10;

                        // Create bay and disconnector for each cable going to the junction

                        neighbours.Sort((io1, io2) => io1.mRID.CompareTo(io2.mRID));

                        int bayCounter = 1;

                        foreach (var n in neighbours)
                        {
                            // Fjern existerende forbindelse til junction (dumt punkt)
                            obj.RemoveNeighbour(n);
                            n.RemoveNeighbour(obj);

                            var bay = new CIMEquipmentContainer(g.ObjectManager) { Name = "" + bayCounter, mRID = GUIDHelper.CreateDerivedGuid(obj.mRID, nextDerivedGuidCounter), ClassType = CIMClassEnum.Bay, VoltageLevel = 400, EquipmentContainerRef = junction };
                            nextDerivedGuidCounter += 3;
                            junction.Children.Add(bay);

                            var dis = new CIMConductingEquipment(g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(obj.mRID, nextDerivedGuidCounter), VoltageLevel = 400, ClassType = CIMClassEnum.Disconnector, EquipmentContainerRef = bay };
                            nextDerivedGuidCounter += 3;
                            bay.Children.Add(dis);

                            // Connect disconnector to CN
                            dis.AddNeighbour(cn);
                            cn.AddNeighbour(dis);

                            // Connect disconnector to cable
                            dis.AddNeighbour(n);
                            n.AddNeighbour(dis);

                            bayCounter++;
                        }


                        // Slet junction punkt
                        g.ObjectManager.Delete(obj);


                        // Create DAX node - ugly should be refactored!
                        var daxNode = new DAXElectricNode(g.ObjectManager) { Name = junction.Name, Description = junction.Description, CIMObjectId = junction.InternalId, ClassType = junction.ClassType, Coords = junction.Coords, VoltageLevel = junction.VoltageLevel };
                        ((TopologyProcessingResult)topologyData)._daxNodeByCimObj.Add(junction, daxNode);
                        
                    }
                }
            }
        }


    }
}
