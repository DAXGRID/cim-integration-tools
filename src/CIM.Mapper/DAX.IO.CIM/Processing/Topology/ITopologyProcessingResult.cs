using DAX.NetworkModel.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM.Processing
{
    public interface ITopologyProcessingResult : IGraphProcessingResult
    {
        List<DAXElectricNode> DAXNodes { get; }

        List<DAXElectricFeeder> DAXFeeders { get; }
        
        DAXElectricNode GetDAXNodeByExternalId(string externalId);

        DAXElectricNode GetDAXNodeByName(string name);

        DAXElectricNode GetDAXNodeByCIMObject(CIMIdentifiedObject cimObj, bool trace = true);

        DAXTopologyInfo GetDAXTopologyInfoByCIMObject(CIMConductingEquipment cimObj);

        List<DAXElectricFeeder> GetDAXFeedersByCIMObject(CIMIdentifiedObject cimObj);
    }
}
