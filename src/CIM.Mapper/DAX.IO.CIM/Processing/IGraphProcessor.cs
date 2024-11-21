using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM.Processing
{
    public interface IGraphProcessor : IDAXInitializeable
    {
        void Run(CIMGraph g, TableLogger tableLogger);
    }
}
