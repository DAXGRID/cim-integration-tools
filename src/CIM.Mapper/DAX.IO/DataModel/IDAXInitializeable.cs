using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;

namespace DAX.IO
{
    public interface IDAXInitializeable
    {
        void Initialize(string name, List<ConfigParameter> parameters = null);
    }
}
