﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PhysicalNetworkModel
{
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://daxgrid.net/PhysicalNetworkModel_0_1")]
    public enum CableOuterJacketKind
    {

        /// <remarks/>
        insulating,

        /// <remarks/>
        linearLowDensityPolyethylene,

        /// <remarks/>
        none,

        /// <remarks/>
        other,

        /// <remarks/>
        polyethylene,

        /// <remarks/>
        pvc,

        /// <remarks/>
        semiconducting,
    }
}
