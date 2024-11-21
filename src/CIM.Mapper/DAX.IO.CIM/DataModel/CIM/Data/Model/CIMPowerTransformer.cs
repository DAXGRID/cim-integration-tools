using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    /// <summary>
    /// CIM Power Transformer.
    /// </summary>
    public class CIMPowerTransformer : CIMConductingEquipment
    {
        public CIMPowerTransformer(CIMObjectManager objectManager)
            : base(objectManager)
        {
        }

    }
}
