using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMConductingEquipment : CIMPowerSystemResource
    {
        public CIMConductingEquipment(CIMObjectManager objectgManager)
            : base(objectgManager)
        {
        }

        public IEnumerable<CIMTerminal> Terminals
        {
            get
            {
                List<CIMTerminal> terminals = new List<CIMTerminal>();
                int endNumber = 1;

                while (true)
                {
                    if (ContainsPropertyValue($"terminal.{endNumber}.id"))
                    {
                        // Get terminal id
                        var terminalIdStr = GetPropertyValueAsString($"terminal.{endNumber}.id");

                        Guid terminalMrid = Guid.Empty;

                        if (Guid.TryParse(terminalIdStr, out Guid result))
                            terminalMrid = result;

                        // Get connectivity node id
                        var cnIdStr = GetPropertyValueAsString($"cim.terminal.{endNumber}");

                        Guid cnMrid = Guid.Empty;

                        if (Guid.TryParse(cnIdStr, out Guid result2))
                            cnMrid = result2;

                        CIMConnectivityNode cn = null;

                        foreach (var neighbor in Neighbours)
                        {
                            if (neighbor.mRID == cnMrid)
                                cn = neighbor as CIMConnectivityNode;
                        }

                        terminals.Add(new CIMTerminal() { mRID = result, ConnectivityNode = cn, EndNumber = endNumber });
                    }
                    else
                    {
                        break;
                    }

                    endNumber++;
                }

                return terminals.AsEnumerable();

                /*

                // Add a terminal for each connectivity node neighbor
                if (Neighbours.Count < 3)
                {
                    foreach (var neighbour in Neighbours)
                    {
                        if (neighbour is CIMConnectivityNode)
                        {
                            var cn = (CIMConnectivityNode)neighbour;

                            
                            if (cn.mRID == Guid.Empty)
                            {
                                terminals.Add(new CIMTerminal() { mRID = GUIDHelper.CreateDerivedGuid(this.mRID, endNumber), ConnectivityNode = null, EndNumber = endNumber });
                            }
                            else
                            {
                                terminals.Add(new CIMTerminal() { mRID = GUIDHelper.CreateDerivedGuid(this.mRID, endNumber), ConnectivityNode = cn, EndNumber = endNumber });
                            }
                            
                            endNumber++;
                        }
                    }
                }
                else
                {
                    if (this.ClassType == CIMClassEnum.BusbarSection ||
                        this.ClassType == CIMClassEnum.EnergyConsumer ||
                        this.ClassType == CIMClassEnum.SynchronousMachine ||
                        this.ClassType == CIMClassEnum.AsynchronousMachine)
                    {
                        terminals.Add(new CIMTerminal() { mRID = GUIDHelper.CreateDerivedGuid(this.mRID, 1), ConnectivityNode = null, EndNumber = 1 });
                    }
                    else
                    {
                        terminals.Add(new CIMTerminal() { mRID = GUIDHelper.CreateDerivedGuid(this.mRID, 1), ConnectivityNode = null, EndNumber = 1 });
                        terminals.Add(new CIMTerminal() { mRID = GUIDHelper.CreateDerivedGuid(this.mRID, 2), ConnectivityNode = null, EndNumber = 2 });
                    }
                }

                return terminals.AsEnumerable();
            

                */


            }
        }

        public double Length()
        {
            double len = 0;
            if (Coords != null && Coords.Length > 3)
            {
                for (int i = 0; i < (Coords.Length - 2); i+=2)
                {
                    double startX = Coords[i];
                    double startY = Coords[i + 1];

                    double endX = Coords[i + 2];
                    double endY = Coords[i + 3];

                    len += Math.Sqrt(Math.Pow((endY - startY), 2) + Math.Pow((endX - startX), 2));
                }
            }

            return len;
        }


    }
}
