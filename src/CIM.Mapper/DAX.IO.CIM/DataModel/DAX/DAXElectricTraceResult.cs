using DAX.IO.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.NetworkModel.CIM
{
    public class DAXTraceResult
    {
        public string NodeId { get; set; }

        public string Name { get; set; }

        public List<DAXTraceItem> UpstreamTrace { get; set; }

    }

    public class DAXTraceItem
    {
        public string NodeId { get; set; }

        public CIMClassEnum ClassType { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public string Details { get; set; }

        public double[] Coords { get; set; }

        public int VoltageLevel { get; set; }

        public string BranchInfo { get; set; }

        public double Length { get; set; }

        public int CIMObjectId = 0;

        public override string ToString()
        {
            return ClassType.ToString() + " " + Name + " " + Details;
        }

    }
}
