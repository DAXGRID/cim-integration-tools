using DAX.IO.CIM.Processing;
using DAX.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public class AddMissingConnectivityNodesProcessor : IGraphProcessor
    {
        public void Run(CIMGraph g, TableLogger tableLogger)
        {
            Logger.Log(LogLevel.Debug, "AddMissingConnectivityNodeProcessor: Processing ...");



            // Add missing connectivity nodes
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ACLineSegment && !g.ObjectManager.IsDeleted(obj))
                {
                    List<CIMIdentifiedObject> neighborsToBeRemoved = new List<CIMIdentifiedObject>();
                    List<CIMIdentifiedObject> neighborsToBeAdded = new List<CIMIdentifiedObject>();

                    foreach (var neighbor in obj.Neighbours)
                    {
                        if (neighbor.ClassType != CIMClassEnum.ConnectivityNode)
                        {
                            if (obj.ExternalId == "2759849")
                            {
                            }

                            var newCn = new CIMConnectivityNode(g.ObjectManager) { ExternalId = obj.ExternalId };
                            newCn.AddNeighbour(obj);
                            newCn.AddNeighbour(neighbor);
                            neighborsToBeRemoved.Add(neighbor);
                            neighborsToBeAdded.Add(newCn);
                            neighbor.RemoveNeighbour(obj);
                            neighbor.AddNeighbour(newCn);
                        }
                    }

                    foreach (var d in neighborsToBeRemoved)
                        obj.RemoveNeighbour(d);

                    obj.AddNeighbour(neighborsToBeAdded);
                }

            }
        }

        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

    }
}
