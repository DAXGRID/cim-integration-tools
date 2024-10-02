using System;
using System.Xml.Serialization;

namespace CIM.Differ.Change
{
    public class ObjectCreation : ChangeSetMember
    {
        public IdentifiedObject Object { get; set; }
    }
}
