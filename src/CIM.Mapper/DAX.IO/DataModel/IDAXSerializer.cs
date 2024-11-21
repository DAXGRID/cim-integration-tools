using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;

namespace DAX.IO
{
    public interface IDAXSerializer
    {
        void Initialize(List<ConfigParameter> parameters = null);
    }
}
