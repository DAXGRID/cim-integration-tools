using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMClassAttributeDef
    {
        public int Id { get; set; }
        public CIMAttributeDef AttributeDef { get; set; }

        // FIX: Attribute defining constraints goes here
    }
}
