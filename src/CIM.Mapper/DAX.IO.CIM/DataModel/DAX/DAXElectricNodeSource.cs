using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.NetworkModel.CIM
{
    public class DAXElectricNodeSource
    {
        public DAXElectricFeeder Feeder;

        public int UpstreamCIMObjectId;

        public List<DAXTraceItem> Trace;
    }
}
