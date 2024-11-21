using DAX.IO.CIM.Processing;
using DAX.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{


    public class MissingBaseVoltageProcessor : IGraphProcessor
    {
        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

        public void Run(CIMGraph g, TableLogger tableLogger)
        {
            Logger.Log(LogLevel.Info, "MissingBaseVoltageProcessor: Transfer voltage level to components who miss them from other componentes they are electrically connected to.");

            foreach (var obj in g.CIMObjects)
            {
                if (obj.VoltageLevel == 0)
                {
                    if (obj.ClassType == CIMClassEnum.EnergyConsumer)
                    {
                        // Take voltagelevel from AC line segment going to consumer
                        var test = obj.GetNeighboursNeighbors(CIMClassEnum.ACLineSegment);

                        if (test.Count > 0)
                            obj.VoltageLevel = test[0].VoltageLevel;
                    }
                }
            }
        }
    }
}
