using CIM.PhysicalNetworkModel;

namespace DAX.IO.CIM.Processing
{
    public interface IDAXSerializeable
    {
        byte[] Serialize(CIMMetaDataRepository repository, List<CIMIdentifiedObject> cimObjects, CIMGraph graph);
    }
}
