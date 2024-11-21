using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM.Processing.Line
{
    public class DAXBayInfo
    {
        public DAXSimpleLine Line { get; set; }
        public CIMEquipmentContainer OtherEndBay { get; set; }
        public CIMIdentifiedObject OtherEndNode { get; set; }
        public List<CIMConductingEquipment> Customers { get; set; }
        public int NumberOfCables { get; set; }
        public string OtherEndGeneratedName { get; set; }
    }
}
