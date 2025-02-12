using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMClassDef
    {
        public int Id { get; set; }

        private Dictionary<int, CIMClassAttributeDef> AttributeDefById;

        public CIMClassAttributeDef GetClassAttributeDefById(int id)
        {
            if (AttributeDefById.ContainsKey(id))
                return AttributeDefById[id];

            return null;
        }
    }
}
