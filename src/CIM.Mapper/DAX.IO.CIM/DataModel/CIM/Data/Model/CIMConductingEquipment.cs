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

                    //throw new DAXGraphException("Fatal error. Conducting equipment has too many terminals. " + this.ToString());
                }

                /*

                // If a switch check if we should create missing terminals
                if (Neighbours.Count != 2 && (this.ClassType == CIMClassEnum.Breaker || this.ClassType == CIMClassEnum.LoadBreakSwitch || this.ClassType == CIMClassEnum.Disconnector || this.ClassType == CIMClassEnum.Fuse))
                {
                    for (int i = 0; i < 2 - Neighbours.Count; i++)
                    {
                        terminals.Add(new CIMTerminal() { mRID = GUIDHelper.CreateDerivedGuid(this.mRID, endNumber), ConnectivityNode = null, EndNumber = endNumber });
                        endNumber++;
                    }
                }

                // Make sure terminal 1 is connected to the primary side on at rafo

                if (this.ClassType == CIMClassEnum.PowerTransformer && terminals.Count > 1)
                {
                    var t1n = terminals[0].ConnectivityNode.GetNeighbours(CIMClassEnum.ACLineSegment);
                    var t2n = terminals[1].ConnectivityNode.GetNeighbours(CIMClassEnum.ACLineSegment);

                    if (t1n.Count > 0 && t2n.Count > 0 && t1n[0].ClassType == CIMClassEnum.ACLineSegment && t2n[0].ClassType == CIMClassEnum.ACLineSegment)
                    {
                        // Hvis ac line segment på terminal 1 har mindre spændingsniveau end den på terminal 2, da but om
                        if (t1n[0].VoltageLevel < t2n[0].VoltageLevel)
                        {
                            var temp = terminals[0];
                            terminals[0] = terminals[1];
                            terminals[1] = temp;
                        }
                    }
                }

                // Add mising terminals to trafo
                if (this.ClassType == CIMClassEnum.PowerTransformer && terminals.Count < 2)
                {
                    var tCount = terminals.Count;

                    for (int i = 0; i < 2 - tCount; i++)
                    {
                        terminals.Add(new CIMTerminal() { mRID = GUIDHelper.CreateDerivedGuid(this.mRID, endNumber), ConnectivityNode = null, EndNumber = endNumber });
                        endNumber++;
                    }
                }

                // Add mising connectivity nodes
                foreach (var terminal in terminals)
                {
                    if (terminal.ConnectivityNode == null)
                    {
                        terminal.ConnectivityNode = new CIMConnectivityNode(this.ObjectManager);

                        terminal.ConnectivityNode.AddNeighbour(this);
                        this.AddNeighbour(terminal.ConnectivityNode);
                        terminal.ConnectivityNode.mRID = GUIDHelper.CreateDerivedGuid(terminal.mRID, 555, true);
                    }
                }

                */

                return terminals.AsEnumerable();
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
