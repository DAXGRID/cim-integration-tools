using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PowerFactoryExporter
{
    /// <summary>
    /// Power Factory specific extra information that needed to be exported
    /// </summary>
    public class MappingContext
    {
        /// <summary>
        /// Power Factory CGMES import requires that all connectivity nodes are inside a voltage level
        /// </summary>
        public Dictionary<CIM.PhysicalNetworkModel.ConnectivityNode, PhysicalNetworkModel.VoltageLevel> ConnectivityNodeToVoltageLevel = new Dictionary<PhysicalNetworkModel.ConnectivityNode, PhysicalNetworkModel.VoltageLevel>();
    }
}
