﻿using System;
using System.Xml.Serialization;

namespace CIM.PhysicalNetworkModel.Changes
{
    [Serializable]
    [XmlType(Namespace = "http://daxgrid.net/PhysicalNetworkModel_0_1")]
    public class ObjectReverseModification : ChangeSetMember
    {
        public PropertyModification[] Modifications { get; set; }
    }
}
