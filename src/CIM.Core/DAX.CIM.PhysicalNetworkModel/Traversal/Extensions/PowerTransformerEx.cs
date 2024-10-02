using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PhysicalNetworkModel.Traversal.Extensions
{
    public static class PowerTransformerEx
    {
       /// <summary>
       /// Get the power transformer ends (windings)
       /// </summary>
       /// <param name="pt"></param>
       /// <param name="context"></param>
       /// <returns></returns>
        public static List<PowerTransformerEnd> GetEnds(this PowerTransformer pt, CimContext context)
        {
            context = context;

            return context.GetPowerTransformerEnds(pt);
        }
    }
}
