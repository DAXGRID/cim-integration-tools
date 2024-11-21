using DAX.NetworkModel.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public class NetworkReliabilityProcessingResult : INetworkReliabilityProcessingResult
    {
        public Dictionary<CIMIdentifiedObject, ACLineSegmentFlowInfo> AcLineSegmentFlowInfo = new Dictionary<CIMIdentifiedObject, ACLineSegmentFlowInfo>();
    }

    public class ACLineSegmentFlowInfo 
    {
        public Dictionary<CIMConnectivityNode, List<DAXElectricFeeder>> cnFeeders = new Dictionary<CIMConnectivityNode, List<DAXElectricFeeder>>();
    }
}
