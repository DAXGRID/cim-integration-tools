using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PhysicalNetworkModel.Traversal.Extensions
{
    public static class PowerTransformerEndEx
    {
        public static List<TapChanger> GetTapChangers(this PowerTransformerEnd end, CimContext context)
        {
            context = context;

            return context.GetPowerTransformerEndTapChangers(end);
        }
    }
}
