using DAX.IO.CIM.Processing;
using DAX.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    class InformiBayPositionProcessor : IGraphProcessor
    {
        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        
        }

        public void Run(CIMGraph g, CimErrorLogger tableLogger)
        {
            Logger.Log(LogLevel.Debug, "InformiBayPositionProcessor: Calculating bay position from GIS coordinates...");


            // Find all stations and enclosures
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.Substation || obj.ClassType == CIMClassEnum.Enclosure)
                {
                    CIMEquipmentContainer container = obj as CIMEquipmentContainer;

                    Dictionary<int, List<CIMEquipmentContainer>> bays = new Dictionary<int, List<CIMEquipmentContainer>>();

                    foreach (var child in container.Children)
                    {
                        if (child.ClassType == CIMClassEnum.Bay && child.Coords != null && child.Coords.Length == 4)
                        {
                            if (!bays.ContainsKey(child.VoltageLevel))
                                bays[child.VoltageLevel] = new List<CIMEquipmentContainer>();

                            bays[child.VoltageLevel].Add((CIMEquipmentContainer)child);
                        }
                    }


                    foreach (var voltageLevel in bays.Values)
                    {
                        List<CIMEquipmentContainer> sortedBayList = voltageLevel.OrderBy(b => b.Coords[2]).ToList();

                        int i = 1;

                        foreach (var bay in sortedBayList)
                        {
                            bay.SetPropertyValue("cim.order", i);
                            i++;
                        }
                    }
                }
            }
        }
    }
}
