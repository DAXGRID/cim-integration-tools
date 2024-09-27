using System;
using System.Xml.Serialization;

namespace CIM.Change
{
    public class ObjectCreation : ChangeSetMember
    {
        public IdentifiedObject Object { get; set; }
    }
}
