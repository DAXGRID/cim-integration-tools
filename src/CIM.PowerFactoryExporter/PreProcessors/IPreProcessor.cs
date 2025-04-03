using CIM.PhysicalNetworkModel;
using CIM.PhysicalNetworkModel.Traversal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PowerFactoryExporter
{
    public interface IPreProcessor
    {
        IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> input);
    }
}
