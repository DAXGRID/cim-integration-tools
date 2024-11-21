using DAX.IO.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.NetworkModel.CIM
{
    public class DAXElectricFeeder
    {
        public DAXElectricNode Node;

        public string Name;

        public DAXElectricTransformer Transformer;

        public int DownstreamCIMObjectId;

        public int UpstreamCIMObjectId;

        public int VoltageLevel = 0;

        public List<DAXTraceItem> Trace;

        public bool IsTransformerFeeder = false;

        public CIMEquipmentContainer Bay = null;

        public override string ToString()
        {
            string returnVal = "DAXElectricFeeder: Name='" + Name + "'";
            if (Node != null)
                returnVal += " Node='" + Node.Name + "'";

            return returnVal;
        }

        public string GetDetailLabel()
        {
            var result = "";

            if (Node != null)
            {
                if (Node.ClassType == CIMClassEnum.Substation)
                    result += "St. " + Node.Name;
            }

            if (Transformer != null)
            {
                result += " Tr. " + Transformer.Name;
            }

            if (Name != null)
            {
                result += " Udf. " + Name;
            }

            if (Transformer != null && Transformer.Sources != null && Transformer.Sources.Length > 0)
            {
                if (Transformer.Sources.Length == 1)
                {
                    if (Transformer.Sources[0].Transformer != null &&
                        Transformer.Sources[0].Transformer.Sources != null &&
                        Transformer.Sources[0].Transformer.Sources.Length > 0 &&
                        Transformer.Sources[0].Transformer.Sources[0] == this)
                    {

                        result += " <- feeder loop with " + Transformer.Sources[0].Transformer.Sources[0].Name;
                    }
                    else
                        result += " <- " + Transformer.Sources[0].GetDetailLabel();
                }
                else
                    result += " <- multiple sources";
            }
                                    
            return result;
        }
    }
}
