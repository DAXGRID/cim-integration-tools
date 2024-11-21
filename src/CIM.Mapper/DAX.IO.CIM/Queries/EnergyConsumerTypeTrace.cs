using DAX.IO.CIM.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DAX.IO.CIM.Queries
{
    public class EnergyConsumerTypeTrace
    {
        private CIMGraph _g;

        public EnergyConsumerTypeTrace(CIMGraph graph)
        {
            _g = graph;
        }

        public List<EnergyConsumerTypeTraceInfo> Run()
        {
            List<EnergyConsumerTypeTraceInfo> result = new List<EnergyConsumerTypeTraceInfo>();
                
            foreach (var cimObj in _g.CIMObjects)
            {
                if (cimObj.ClassType == CIMClassEnum.EnergyConsumer)
                {
                    // Find end
                    var traceResult = TraceUntilFirstContainer(cimObj);

                    var lastObject = traceResult.ToArray()[traceResult.Count - 1];

                    var rootContainer = lastObject.GetEquipmentContainerRoot();

                    var ti = new EnergyConsumerTypeTraceInfo() { ECName = cimObj.Name, ECDescription = cimObj.Description, ECVoltageLevel = cimObj.VoltageLevel };

                    if (rootContainer == null)
                    {
                    }
                    else
                    {
                        ti.ContainerPSRType = rootContainer.GetPSRType(CIMMetaDataManager.Repository);
                        ti.ContainerNavn = rootContainer.Name;
                    }

                    result.Add(ti);
                }
            }

            return result;
        }


        private Queue<CIMIdentifiedObject> TraceUntilFirstContainer(CIMIdentifiedObject root)
        {
            // Trace variables
            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Stack<CIMIdentifiedObject> stack = new Stack<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> visited = new HashSet<CIMIdentifiedObject>();
            stack.Push(root);
            visited.Add(root);

            while (stack.Count > 0)
            {
                CIMIdentifiedObject p = stack.Pop();

                traverseOrder.Enqueue(p);

                var neighbours = p.Neighbours;

                foreach (CIMIdentifiedObject n in neighbours)
                {
                    if (!visited.Contains(n))
                    {
                        visited.Add(n);

                        if (n.GetEquipmentContainerRoot() == null)
                        {
                            stack.Push(n);
                        }
                        else
                        {
                            traverseOrder.Enqueue(n);
                            return traverseOrder;
                        }
                    }
                }
            }

            return traverseOrder;
        }
    }

    [DataContract]
    public class EnergyConsumerTypeTraceInfo
    {
        [DataMember, XmlAttribute]
        public string ECName { get; set; }

        [DataMember, XmlAttribute]
        public int ECVoltageLevel { get; set; }

        [DataMember, XmlAttribute]
        public string ECDescription { get; set; }

        [DataMember, XmlAttribute]
        public string ContainerPSRType { get; set; }

        [DataMember, XmlAttribute]
        public string ContainerNavn { get; set; }

        public override string ToString()
        {
            return ECName + " " + ECVoltageLevel + " " + ECDescription + " " + ContainerNavn + " " + ContainerPSRType;
        }
    }
}
