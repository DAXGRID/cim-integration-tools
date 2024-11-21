using DAX.IO.CIM.Processing;
using DAX.NetworkModel.CIM;

namespace DAX.IO.CIM.Serialization
{
    public static class SerializationHelper
    {
        public static List<CIMIdentifiedObject> SerializeStationAndDown(CIMGraph graph, DAXElectricNode st)
        {
            var topologyData = (ITopologyProcessingResult)graph.GetProcessingResult("Topology");

            List<CIMIdentifiedObject> objectsToSerialize = new List<CIMIdentifiedObject>();

            // tilføj indfødninger
            foreach (var obj in graph.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ExternalNetworkInjection)
                    objectsToSerialize.Add(obj);
            }

            // Add hsp station
            objectsToSerialize.Add(st.CIMObject);

            // All all msp feeders
            if (st.Feeders != null)
            {
                foreach (var mspFeeder in st.Feeders)
                {
                    foreach (var mspObj in mspFeeder.Trace)
                    {
                        objectsToSerialize.Add(graph.ObjectManager.GetCIMObjectById(mspObj.CIMObjectId));

                        if (mspObj.ClassType == CIMClassEnum.Substation)
                        {
                            var mspSt = topologyData.GetDAXNodeByCIMObject((graph.ObjectManager.GetCIMObjectById(mspObj.CIMObjectId)));

                            if (mspSt.Feeders != null)
                            {
                                foreach (var lspFeeder in mspSt.Feeders)
                                {
                                    if (lspFeeder.Trace != null)
                                    {
                                        foreach (var lspObj in lspFeeder.Trace)
                                        {
                                            objectsToSerialize.Add(graph.ObjectManager.GetCIMObjectById(lspObj.CIMObjectId));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }


            return objectsToSerialize;
        }
    }
}
