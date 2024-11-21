using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM.Processing.Line
{
    /// <summary>
    /// Represents a simple line (strækning) going between exactly two nodes.
    /// In the CIM standard a line is an equipment container and can contain any object that may connect more than two nodes.
    /// This class is a more restrict implementation used for SCADA systemet etc. that cannot handle the flexibility of the line concept as defined in the CIM standard.
    /// </summary>
    public class DAXSimpleLine
    {
        public string Name { get; set; }
        public CIMEquipmentContainer FromBay { get; set; }
        public CIMEquipmentContainer ToBay { get; set; }
        public List<DAXLineRelation> Children = new List<DAXLineRelation>();
    }
}
