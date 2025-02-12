using DAX.IO.CIM.Processing;
using DAX.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public class InformiConnectivityProcessor : IGraphProcessor
    {
        public void Run(CIMGraph g, CimErrorLogger tableLogger)
        {
            Logger.Log(LogLevel.Debug, "InformiConnectivityProcessor: Transforming Informi GIS connectivity to CIM connectivity...");

            ProcessAuxEquipments(g, tableLogger);

            // Clean up edge connectors
            foreach (var obj in g.CIMObjects)
            {
                // Processer connectivity node og edge
                if ((obj.ClassType == CIMClassEnum.ConnectivityNode || obj.ClassType == CIMClassEnum.ConnectivityEdge) && !g.ObjectManager.IsDeleted(obj))
                {
                    var connectivityNodeNeighbors = obj.GetNeighbours(CIMClassEnum.ConnectivityEdge, CIMClassEnum.ConnectivityNode);

                    bool keepSearchingForEdges = true;

                    while (keepSearchingForEdges && connectivityNodeNeighbors.Count > 0)
                    {
                        foreach (var connectivityNodeNeighbor in connectivityNodeNeighbors)
                        {
                            // SK 2
                            if (connectivityNodeNeighbor.mRID == Guid.Parse("be37cd82-da88-4348-a67a-19ab150299bc"))
                            {
                            }

                            // SK 3
                            if (connectivityNodeNeighbor.mRID == Guid.Parse("b22d7e47-8988-4356-89c0-683cbbae71a8"))
                            {
                            }

                            // Nabo skal også peger på to ting
                            if (connectivityNodeNeighbor.Neighbours.Count() == 2)
                            {
                                var otherEnd = connectivityNodeNeighbor.Neighbours.ElementAt(0);

                                // Hvis otherEnd peger på nodens nabo
                                if (otherEnd == obj)
                                    otherEnd = connectivityNodeNeighbor.Neighbours.ElementAt(1);

                                if (obj != connectivityNodeNeighbor)
                                {

                                    // Fjern edge fra nodens naboer
                                    obj.RemoveNeighbour(connectivityNodeNeighbor);

                                    // Indæt edge i nodes nabo liste i stedet for
                                    obj.AddNeighbour(otherEnd);

                                    // overfør feeder attributter
                                    g.ObjectManager.AdditionalObjectAttributes(otherEnd).IsFeederEntryObject = g.ObjectManager.AdditionalObjectAttributes(connectivityNodeNeighbor).IsFeederEntryObject;
                                    g.ObjectManager.AdditionalObjectAttributes(otherEnd).IsFeederExitObject = g.ObjectManager.AdditionalObjectAttributes(connectivityNodeNeighbor).IsFeederExitObject;
                                    g.ObjectManager.AdditionalObjectAttributes(otherEnd).IsTransformerFeederObject = g.ObjectManager.AdditionalObjectAttributes(connectivityNodeNeighbor).IsTransformerFeederObject;


                                    // SLET: sec forb kabel på byg som mister forbindelse til trafo
                                    if (otherEnd.mRID == Guid.Parse("FDAB0C12-76B4-4AD2-B545-B2F48DD4103B") || obj.mRID == Guid.Parse("FDAB0C12-76B4-4AD2-B545-B2F48DD4103B"))
                                    {
                                    }

                                    // Fjern edge fra otherend
                                    otherEnd.RemoveNeighbour(connectivityNodeNeighbor);

                                    // Tilfør node til otherend istedet for
                                    otherEnd.AddNeighbour(obj);

                                    g.ObjectManager.Delete(connectivityNodeNeighbor);
                                }
                            }
                            else
                                keepSearchingForEdges = false;
                        }

                        connectivityNodeNeighbors = obj.GetNeighbours(CIMClassEnum.ConnectivityEdge, CIMClassEnum.ConnectivityNode);
                    }
                }
            }

            // Process busbar sections
            foreach (var obj in g.CIMObjects)
            {
                // Processer connectivity node
                if (obj.ClassType == CIMClassEnum.BusbarSection)
                {
                    // SK 2
                    if (obj.mRID == Guid.Parse("be37cd82-da88-4348-a67a-19ab150299bc"))
                    {
                    }

                    // SK 3
                    if (obj.mRID == Guid.Parse("b22d7e47-8988-4356-89c0-683cbbae71a8"))
                    {
                    }

                    // Check om der går kabler direkte til skinnen.
                    var acLinesConnectedToBusBar = obj.GetNeighbours(CIMClassEnum.ACLineSegment);

                    if (acLinesConnectedToBusBar.Count > 0)
                    {
                        foreach (var acLineSegment in acLinesConnectedToBusBar)
                        {
                            var connectionPoints = obj.GetNeighbours(CIMClassEnum.ConnectivityNode, CIMClassEnum.ConnectivityEdge);

                            if (connectionPoints.Count > 0)
                            {
                                var connectionPoint = connectionPoints[0];

                                // Add ac line segment to connection Point
                                connectionPoint.AddNeighbour(acLineSegment);

                                // Remove ac line segment from busbar
                                obj.RemoveNeighbour(acLineSegment);

                                // Add connectionpoint to ac line segment
                                acLineSegment.AddNeighbour(connectionPoint);

                                // Remove busbar from ac line segment
                                acLineSegment.RemoveNeighbour(obj);
                            }
                            else
                            {
                                Logger.Log(LogLevel.Warning, "Dangling AC line segment found on " + obj.IdString());
                            }
                        }
                    }

                    if (obj.Neighbours.Count() > 1)
                    {
                        if (!(obj.Neighbours.ElementAt(0).ClassType != CIMClassEnum.ConnectivityNode && obj.Neighbours.ElementAt(0).ClassType != CIMClassEnum.ConnectivityEdge))
                        {
                            var busbarCnToKeep = obj.Neighbours.ElementAt(0);
                            busbarCnToKeep.SetClass(CIMClassEnum.ConnectivityNode);

                            List<CIMIdentifiedObject> cnToRemoveFromBusbar = new List<CIMIdentifiedObject>();

                            for (int i = 1; i < obj.Neighbours.Count(); i++)
                            {
                                var busBarCnToDelete = obj.Neighbours.ElementAt(i);
                                g.ObjectManager.Delete(busBarCnToDelete);
                                cnToRemoveFromBusbar.Add(busBarCnToDelete);

                                // Move all neighbours to first cn
                                foreach (var neighbourNeighbour in busBarCnToDelete.Neighbours)
                                {
                                    // Flyt nabo nabo undtagen samleskinnen
                                    if (neighbourNeighbour != obj && neighbourNeighbour != busBarCnToDelete)
                                    {
                                        neighbourNeighbour.RemoveNeighbour(busBarCnToDelete);
                                        neighbourNeighbour.AddNeighbour(busbarCnToKeep);

                                        // Flyt fra busbar cn to be deleted to busbar cn to keep
                                        if (busBarCnToDelete != busbarCnToKeep)
                                        {
                                            busbarCnToKeep.AddNeighbour(neighbourNeighbour);
                                        }
                                    }
                                }
                            }

                            // delte busbar cn's
                            foreach (var cn in cnToRemoveFromBusbar)
                                obj.RemoveNeighbour(cn);
                        }
                        else
                        {

                        }
                    }
                }
            }

            // Convert connectivity edges to connectivity nodes
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ConnectivityEdge)
                {
                    obj.ClassType = CIMClassEnum.ConnectivityNode;
                }
            }

            // Add missing connectivity nodes
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ACLineSegment && !g.ObjectManager.IsDeleted(obj))
                {
                    List<CIMIdentifiedObject> neighborsToBeRemoved = new List<CIMIdentifiedObject>();
                    List<CIMIdentifiedObject> neighborsToBeAdded = new List<CIMIdentifiedObject>();

                    // SLET: sec forb kabel på byg som mister forbindelse til trafo
                    if (obj.mRID == Guid.Parse("FDAB0C12-76B4-4AD2-B545-B2F48DD4103B"))
                    {
                    }

                    foreach (var neighbor in obj.Neighbours)
                    {
                        if (neighbor.ClassType != CIMClassEnum.ConnectivityNode)
                        {
                            var newCn = new CIMConnectivityNode(g.ObjectManager) { ExternalId = obj.ExternalId };

                            if (newCn.ExternalId == "2759849")
                            {
                            }

                            newCn.AddNeighbour(obj);
                            newCn.AddNeighbour(neighbor);
                            neighborsToBeRemoved.Add(neighbor);
                            neighborsToBeAdded.Add(newCn);
                            neighbor.RemoveNeighbour(obj);
                            neighbor.AddNeighbour(newCn);
                        }
                    }

                    foreach (var d in neighborsToBeRemoved)
                        obj.RemoveNeighbour(d);

                    obj.AddNeighbour(neighborsToBeAdded);
                }

                else if (obj.ClassType == CIMClassEnum.PowerTransformer && !g.ObjectManager.IsDeleted(obj))
                {
                    // SLET: trafo loss 60 kv connection
                    if (obj.mRID == Guid.Parse("f06617c6-b615-478b-94dc-444cf0fa2e60"))
                    {
                    }


                    if (obj.EquipmentContainerRef != null && obj.Neighbours.Count > 1)
                    {
                        List<CIMIdentifiedObject> newNeighborList = new List<CIMIdentifiedObject>();

                        CIMConnectivityNode primaryCN = null;
                        CIMConnectivityNode secondaryCN = null;
                        CIMIdentifiedObject primaryLineSegment = null;
                        CIMIdentifiedObject secondaryLineSegment = null;
                        CIMIdentifiedObject coilConnection = null;

                        foreach (var neighbor in obj.Neighbours)
                        {
                            // if cable directly connected to trafo
                            if (neighbor.ClassType == CIMClassEnum.ACLineSegment)
                            {
                                if (neighbor.VoltageLevel == obj.EquipmentContainerRef.VoltageLevel)
                                    primaryLineSegment = neighbor;
                                else
                                    secondaryLineSegment = neighbor;
                            }

                            // if cn, that has connections to cables, that's also ok
                            if (neighbor.GetNeighbours(CIMClassEnum.ACLineSegment).Count > 0)
                            {
                                var n2 = neighbor.GetNeighbours(CIMClassEnum.ACLineSegment)[0];

                                if (n2.VoltageLevel == obj.EquipmentContainerRef.VoltageLevel)
                                    primaryLineSegment = n2;
                                else
                                    secondaryLineSegment = n2;
                            }


                            // TO SUPPORT Verdo / build node that has CN's
                            if (neighbor.ClassType == CIMClassEnum.ConnectivityNode)
                            {
                                var cnNeighbords = neighbor.GetNeighbours(obj);

                                if (cnNeighbords.Count == 1)
                                {
                                    var realNeighbor = cnNeighbords[0];

                                    if (realNeighbor.VoltageLevel == obj.EquipmentContainerRef.VoltageLevel)
                                        primaryLineSegment = realNeighbor;
                                    else
                                        secondaryLineSegment = realNeighbor;
                                }
                            }
                            

                            /* Noget bras
                            if (neighbor.ClassType == CIMClassEnum.ConnectivityNode)
                            {
                                coilConnection = neighbor;
                            }
                             */ 
                        }

                        if (primaryLineSegment != null)
                        {
                            if (primaryLineSegment.GetNeighbours(CIMClassEnum.ConnectivityNode).Count != 2)
                            {

                                // Create CN representing primary side
                                var cn = new CIMConnectivityNode(g.ObjectManager) { ExternalId = obj.ExternalId };
                                cn.mRID = GUIDHelper.CreateDerivedGuid(cn.mRID, 1);

                                // Connect CN to power transformer
                                cn.AddNeighbour(obj);
                                // Connect CN to power transformer
                                obj.AddNeighbour(cn);

                                // Disconnect cable from powertransformer
                                primaryLineSegment.RemoveNeighbour(obj);
                                obj.RemoveNeighbour(primaryLineSegment);

                                // Connect cable to cn
                                cn.AddNeighbour(primaryLineSegment);
                                primaryLineSegment.AddNeighbour(cn);


                                // Add CN to new neighbor list
                                newNeighborList.Add(cn);

                                primaryCN = cn;
                            }
                        }
                        else
                        {
                            // TODO: Log til table - ingen checker denne log for denne type fejl
                            //Logger.Log(LogLevel.Warning, GeneralErrorToString.getString(GeneralErrors.PowerTransformerHasNoConnectionToPrimarySide) + ", ExternalID: " + obj.ExternalId);

                            // tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerHasNoConnectionToPrimarySide, "Power transformer has no cable connected to the primary side", obj);
                        }

                        if (secondaryLineSegment != null)
                        {
                            if (secondaryLineSegment.GetNeighbours(CIMClassEnum.ConnectivityNode).Count != 2)
                            {

                                // Create CN representing secondary side
                                var cn = new CIMConnectivityNode(g.ObjectManager) { ExternalId = obj.ExternalId };
                                cn.mRID = GUIDHelper.CreateDerivedGuid(cn.mRID, 2);

                                // Connect CN to power transformer
                                cn.AddNeighbour(obj);
                                obj.AddNeighbour(cn);

                                // Disconnect cable from powertransformer
                                secondaryLineSegment.RemoveNeighbour(obj);
                                obj.RemoveNeighbour(secondaryLineSegment);

                                // Connect cable to cn
                                cn.AddNeighbour(secondaryLineSegment);
                                secondaryLineSegment.AddNeighbour(cn);

                                // Add CN to new neighbor list
                                newNeighborList.Add(cn);

                                secondaryCN = cn;
                            }
                        }
                        else
                        {
                            //   Logger.Log(LogLevel.Warning, GeneralErrorToString.getString(GeneralErrors.PowerTransformerHasNoConnectionToSecoundarySide) + ", ExternalID: " + obj.ExternalId);
                            //   tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerHasNoConnectionToSecoundarySide, "Transformer has no connection to secondary side", obj);
                        }

                        // Handle peterson coil connection
                        if (coilConnection != null)
                        {
                            // Disconnect coil connection from powertransformer
                            coilConnection.RemoveNeighbour(obj);
                            obj.RemoveNeighbour(coilConnection);

                            // Connect object in the other end of coil connection to primary cn
                            if (primaryCN != null && coilConnection.Neighbours.Count == 1 && coilConnection.Neighbours[0].ClassType != CIMClassEnum.PowerTransformer)
                            {
                                primaryCN.AddNeighbour(coilConnection.Neighbours[0]);
                                coilConnection.Neighbours[0].AddNeighbour(primaryCN);

                                // Disconnect coil connecton object from coil connection and delete coil connection
                                coilConnection.Neighbours[0].RemoveNeighbour(coilConnection);
                                g.ObjectManager.Delete(coilConnection);

                                // Now we should have a coil (or switch inbetween coil and transformer) connected to the cn representing the primary side of transformer
                            }
                        }

                        // add new neighbor list to transformer
                        if (newNeighborList.Count == 2)
                            obj.Neighbours = newNeighborList;
                    }
                    else
                    {
                        tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerHasNoConnections, "Power transformer has no parent or has only one connection", obj);
                    }
                }

            }

            // Check if objects har right amount of terminals
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ConnectivityNode)
                {
                }
                else if (obj.ClassType == CIMClassEnum.PowerTransformer && !g.ObjectManager.IsDeleted(obj))
                {
                    if (obj.Neighbours.Count() != 2)
                    {
                        // TODO: Log til table - ingen checker denne log for denne type fejl
                        //Logger.Log(LogLevel.Warning, "CIM object " + obj.IdString() + " has potentially wrong number of terminals. Expected 2");

                        // TODO: Giver p.t. støj - fejl check skal finpudses
                        //tableLogger.Log(Severity.Error, (int)GeneralErrors.WrongNumberOfTerminals, "Wrong number of terminals. Expected 2", obj);
                    }
                }
                else if (obj.ClassType == CIMClassEnum.BusbarSection && !g.ObjectManager.IsDeleted(obj))
                {
                    if (obj.Neighbours.Count() > 1)
                    {
                        Logger.Log(LogLevel.Warning, "CIM object " + obj.IdString() + " has more than one terminal!");
                    }
                }
                else
                {
                    if (obj.Neighbours.Count() > 2 && obj.ClassType != CIMClassEnum.BuildNode)
                    {
                        
                        //Logger.Log(LogLevel.Warning, "CIM object " + obj.IdString() + " has potentially wrong number of terminals. Expected 2");
                        //tableLogger.Log(Severity.Error, (int)GeneralErrors.WrongNumberOfTerminals, "Object has more that 2 terminals. Excepted no more than two.", obj);
                    }
                }
            }

            ProcessPowerTransformers(g, tableLogger);

            FixConnectivityNodesInSeries(g, tableLogger);

            ProcessEnergyConsumersInSeries(g, tableLogger);

            ProcessCoilsWithMoreThanOneTerminal(g, tableLogger);

            DeleteACLSThatHasTerminalsConnectedToEachOther(g, tableLogger);

            RemoveDublicatedNeighbors(g, tableLogger);

            SortNeighborsToCreateStabelTerminal(g, tableLogger);

            EnsureStableConnectivityNodeMRID(g, tableLogger);

            CheckPowerTransformers(g, tableLogger);
        }

        private void SortNeighborsToCreateStabelTerminal(CIMGraph g, CimErrorLogger tableLogger)
        {
            foreach (var obj in g.CIMObjects.Where(o => o is CIMConductingEquipment))
            {
                // ACLineSegment
                if (obj.ClassType == CIMClassEnum.ACLineSegment)
                {
                    // kabel mellem to t-muffer som fucker op
                    if (obj.mRID == Guid.Parse("900671b7-e0a4-4e57-ada1-6ecda5b805a3"))
                    {
                    }
                    
                    // Only sort if the ACLS is connected in both end
                        if (obj.Neighbours.Count == 2 && obj.Coords != null && obj.Coords.Length > 3)
                    {
                        var n1CoordsEnd = new DAXCoordinate() { X = obj.Coords[0], Y = obj.Coords[1] };
                        var n2CoordsEnd = new DAXCoordinate() { X = obj.Coords[obj.Coords.Length - 2], Y = obj.Coords[obj.Coords.Length - 1] };

                        // Distance from end 1 to neighbors of CN 1
                        var distE1N1 = ShortestDistanceToNeighbor(n1CoordsEnd, GetBoundaryCoordinates(obj.Neighbours[0], obj.Neighbours[1], obj));

                        // Distance from end 1 to neighbors of CN 1
                        var distE2N1 = ShortestDistanceToNeighbor(n2CoordsEnd, GetBoundaryCoordinates(obj.Neighbours[0], obj.Neighbours[1], obj));

                        // If we hit a dangling end (dist will return 9999999), test the other end
                        if (distE1N1 == distE2N1)
                        {
                            // Then try distance to CN 2 instead
                            distE2N1 = ShortestDistanceToNeighbor(n1CoordsEnd, GetBoundaryCoordinates(obj.Neighbours[1], obj.Neighbours[0],obj));
                            distE1N1 = ShortestDistanceToNeighbor(n2CoordsEnd, GetBoundaryCoordinates(obj.Neighbours[1], obj.Neighbours[0],obj));
                        }

                        if (distE1N1 == distE2N1 && !(obj.Neighbours[0].Neighbours.Count == 1 && obj.Neighbours[1].Neighbours.Count == 1))
                        //if (distE1N1 == distE2N1)
                        {
                            tableLogger.Log(Severity.Error, (int)GeneralErrors.WrongNumberOfTerminals, "Cannot decide what should be connected to which terminal. That will give problem with unstable mRID's in delta, so the object is removed from CIM.", obj);
                            g.ObjectManager.Delete(obj);
                        }

                        // If End 2 has less distance to neighbors of first CN, then swap
                        if (distE2N1 < distE1N1)
                        {
                            List<CIMIdentifiedObject> newNeighborsSorted = new List<CIMIdentifiedObject>();
                            newNeighborsSorted.Add(obj.Neighbours[1]);
                            newNeighborsSorted.Add(obj.Neighbours[0]);

                            obj.Neighbours = newNeighborsSorted;
                        }
                    }
                }
                // Busbars
                else if (obj.ClassType == CIMClassEnum.BusbarSection)
                {
                }
                // Switch equipment
                else if (obj.ClassType == CIMClassEnum.Disconnector ||
                    obj.ClassType == CIMClassEnum.Fuse ||
                    obj.ClassType == CIMClassEnum.LoadBreakSwitch ||
                    obj.ClassType == CIMClassEnum.Breaker ||
                    obj.ClassType == CIMClassEnum.PowerTransformer
                    )
                {
                    if (obj.Neighbours.Count == 2)
                    {
                        if (obj.Neighbours.Count(o => o is CIMConnectivityNode) == 2)
                        {
                            // Power Transformer
                            if (obj.ClassType == CIMClassEnum.PowerTransformer)
                            {
                                // Make sure terminal 1 is connected to the primary side on at rafo
                                var t1n = obj.Neighbours[0].GetNeighbours(CIMClassEnum.ACLineSegment);
                                var t2n = obj.Neighbours[1].GetNeighbours(CIMClassEnum.ACLineSegment);

                                if (t1n.Count > 0 && t2n.Count > 0 && t1n[0].ClassType == CIMClassEnum.ACLineSegment && t2n[0].ClassType == CIMClassEnum.ACLineSegment)
                                {
                                    // Hvis ac line segment på terminal 1 har mindre spændingsniveau end den på terminal 2, da but om
                                    if (t1n[0].VoltageLevel < t2n[0].VoltageLevel)
                                    {
                                        List<CIMIdentifiedObject> newNeighborsSorted = new List<CIMIdentifiedObject>();
                                        newNeighborsSorted.Add(obj.Neighbours[1]);
                                        newNeighborsSorted.Add(obj.Neighbours[0]);

                                        obj.Neighbours = newNeighborsSorted;
                                    }
                                }
                            }
                            // Switches
                            else
                            {

                                int c1Rank = GetCnRanking((CIMConductingEquipment)obj, (CIMConnectivityNode)obj.Neighbours[0]);
                                int c2Rank = GetCnRanking((CIMConductingEquipment)obj, (CIMConnectivityNode)obj.Neighbours[1]);

                                // Check if we need to termina 1 and 2
                                if (c1Rank < c2Rank)
                                {
                                    List<CIMIdentifiedObject> newNeighborsSorted = new List<CIMIdentifiedObject>();
                                    newNeighborsSorted.Add(obj.Neighbours[1]);
                                    newNeighborsSorted.Add(obj.Neighbours[0]);

                                    obj.Neighbours = newNeighborsSorted;
                                }
                                else if (c1Rank == c2Rank)
                                {
                                    var c1MridRank = GetCnHighestMRID((CIMConductingEquipment)obj, (CIMConnectivityNode)obj.Neighbours[0]);
                                    var c2MridRank = GetCnHighestMRID((CIMConductingEquipment)obj, (CIMConnectivityNode)obj.Neighbours[1]);

                                    if (c1MridRank == c2MridRank)
                                    {
                                        //System.Diagnostics.Debug.WriteLine("'" + obj.mRID + "',");
                                        tableLogger.Log(Severity.Error, (int)GeneralErrors.WrongNumberOfTerminals, "Cannot decide what should be connected to which terminal. That will give problem with unstable mRID's in delta, so the object is removed from CIM.", obj);
                                        g.ObjectManager.Delete(obj);
                                    }

                                    if (c1MridRank < c2MridRank)
                                    {
                                        List<CIMIdentifiedObject> newNeighborsSorted = new List<CIMIdentifiedObject>();
                                        newNeighborsSorted.Add(obj.Neighbours[1]);
                                        newNeighborsSorted.Add(obj.Neighbours[0]);

                                        obj.Neighbours = newNeighborsSorted;
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new DAXGraphException("the Conducting Equipment neighbors contains non connectivity nodes. " + obj.ToString());
                        }
                    }
                    else
                    {
                        if (obj.Neighbours.Count == 0)
                        {
                            // Create two CN's with no mRID (will result in terminals with no CN connection)
                            obj.Neighbours.Add(new CIMConnectivityNode(g.ObjectManager));
                            obj.Neighbours.Add(new CIMConnectivityNode(g.ObjectManager));
                        }
                        else if (obj.Neighbours.Count == 1)
                        {
                            // Create additional termianl 2 (will result in terminal with no CN connection)
                            obj.Neighbours.Add(new CIMConnectivityNode(g.ObjectManager));
                        }
                        else
                        {
                             tableLogger.Log(Severity.Error, (int)GeneralErrors.WrongNumberOfTerminals, "Conducting equipment has " + obj.Neighbours.Count + " terminals. This is not allowed.", obj);

                            // More than 2 terminals, something i rotten. We just remove them.
                            obj.Neighbours.Clear();

                            // Create two CN's with no mRID (will result in terminals with no CN connection)
                            obj.Neighbours.Add(new CIMConnectivityNode(g.ObjectManager));
                            obj.Neighbours.Add(new CIMConnectivityNode(g.ObjectManager));
                        }
                    }
                }
            }
        }

        private double ShortestDistanceToNeighbor(DAXCoordinate endToCheck, List<DAXCoordinate> candidates)
        {
            double distanceResult = 9999999;

            double x1 = endToCheck.X;
            double y1 = endToCheck.Y;

            foreach (var candidate in candidates)
            {
                double x2 = candidate.X;
                double y2 = candidate.Y;

                double distance = Math.Sqrt(Math.Pow(Math.Abs(x1 - x2), 2) + Math.Pow(Math.Abs(y1 - y2), 2));

                if (distance < distanceResult)
                    distanceResult = distance;
            }

            return distanceResult;
        }

        private List<DAXCoordinate> GetBoundaryCoordinates(CIMIdentifiedObject endToCheck, CIMIdentifiedObject otherEnd, CIMIdentifiedObject dontInclude)
        {
            List<DAXCoordinate> result = new List<DAXCoordinate>();

            var neighbors = endToCheck.GetNeighbours(dontInclude);

            // Find shared object
            foreach (var nInEndToCheck in endToCheck.GetNeighbours(dontInclude))
            {
                var otherEndNeighbors = otherEnd.GetNeighbours(dontInclude);

                if (otherEndNeighbors.Contains(nInEndToCheck))
                    neighbors.Remove(nInEndToCheck);
            }

            foreach (var n in neighbors)
            {
                var coords = n.Coords;

                // To support autogenerated componenets, if coords is null, take coord from parent structure
                if (coords == null)
                {
                    var root = n.GetEquipmentContainerRoot();
                    if (root != null && root.Coords != null && root.Coords.Length > 1)
                        coords = root.Coords;
                }

                if (coords != null && coords.Length > 1)
                {
                    if (coords.Length == 2)
                        result.Add(new DAXCoordinate() { X = coords[0], Y = coords[1] });
                    else
                    {
                        // add first coordinate
                        result.Add(new DAXCoordinate() { X = coords[0], Y = coords[1] });

                        // add last coordinate
                        result.Add(new DAXCoordinate() { X = coords[coords.Length -2], Y = coords[coords.Length - 1] });
                    }
                    
                  
                }
            }

            return result;
        }

        private int GetCnRanking(CIMConductingEquipment ci, CIMConnectivityNode cn)
        {
            // Let's examine all the equipment the cn is connected to excluding the switch we're analyzing
            var cnNeighbors = cn.GetNeighbours(ci);

            if (cnNeighbors.Count == 0)
                return 0;

            if (cnNeighbors.Count == 1)
            {
                // If we find a busbar it's always the firste priority for terminal sorting
                if (cnNeighbors[0].ClassType == CIMClassEnum.BusbarSection)
                {
                    return 10;
                }
                // Switching Component
                else if (cnNeighbors[0].ClassType == CIMClassEnum.Disconnector ||
                     cnNeighbors[0].ClassType == CIMClassEnum.Fuse ||
                     cnNeighbors[0].ClassType == CIMClassEnum.LoadBreakSwitch ||
                     cnNeighbors[0].ClassType == CIMClassEnum.Breaker)
                {
                    return 8;
                }
                // ACLS
                else if (cnNeighbors[0].ClassType == CIMClassEnum.ACLineSegment ||
                    cnNeighbors[0].ClassType == CIMClassEnum.ACLineSegmentExt)
                {
                    return 6;
                }
                // Ok, we're dealing with something else that above
                else
                {
                    return 4;
                }
            }
            // The CN is connected to multiple neighbors
            else
            {
                // If we find a busbar it's always the firste priority for terminal sorting
                if (cnNeighbors.Exists(c => c.ClassType == CIMClassEnum.BusbarSection))
                {
                    return 10;
                }
                // If a switching device, this is the next priority
                else if (cnNeighbors.Exists(c => c.ClassType == CIMClassEnum.Disconnector ||
                     c.ClassType == CIMClassEnum.Fuse ||
                     c.ClassType == CIMClassEnum.LoadBreakSwitch ||
                     c.ClassType == CIMClassEnum.Breaker))
                {
                    return 8;
                }
                // If a switching device, this is the next priority
                else if (cnNeighbors.Exists(c => c.ClassType == CIMClassEnum.ACLineSegment ||
                     c.ClassType == CIMClassEnum.ACLineSegmentExt))
                {
                    return 6;
                }
                // Ok, we're dealing with something else that above
                else
                {
                    return 4;
                }
            }
        }

        private BigInteger GetCnHighestMRID(CIMConductingEquipment ci, CIMConnectivityNode cn)
        {
            // Let's examine all the equipment the cn is connected to excluding the switch we're analyzing
            var cnNeighbors = cn.GetNeighbours(ci);

            if (cnNeighbors.Count == 0)
                return 0;

            if (cnNeighbors.Count == 1)
            {
                return new BigInteger(cnNeighbors[0].mRID.ToByteArray());
            }
            // The CN is connected to multiple neighbors
            else
            {
                var highestVal = new BigInteger(0);

                foreach (var neighbor in cnNeighbors)
                {
                    var neighborVal = new BigInteger(neighbor.mRID.ToByteArray());

                    if (neighborVal > highestVal)
                        highestVal = neighborVal;
                }

                return highestVal;
            }
        }

        private void DeleteACLSThatHasTerminalsConnectedToEachOther(CIMGraph g, CimErrorLogger tableLogger)
        {
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ACLineSegment)
                {
                    var neighboords = obj.GetNeighbours();
                    HashSet<CIMIdentifiedObject> visited = new HashSet<CIMIdentifiedObject>();

                    bool endsPointToEachOther = false;

                    foreach (var n in neighboords)
                    {
                        if (visited.Contains(n))
                            endsPointToEachOther = true;

                        visited.Add(n);
                    }

                    if (endsPointToEachOther)
                    {
                        tableLogger.Log(Severity.Error, (int)GeneralErrors.UnsupportedACLineSegment, "ACLS terminal is connected to each other", obj);

                        // remove it self from all its neighbors
                        foreach (var neighboor in obj.Neighbours)
                            neighboor.RemoveNeighbour(obj);

                        g.ObjectManager.Delete(obj);

                        var root = obj.GetEquipmentContainerRoot();

                        if (root != null)
                            root.Children.Remove(obj);
                    }

                }

            }
        }

        private void ProcessAuxEquipments(CIMGraph g, CimErrorLogger tableLogger)
        {
            // An aux equipment is connected to a terminal directly, and does not have terminal it self, so we need to disconnect it from the network
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.FaultIndicatorExt)
                {
                    ConnectAuxObjectToObject(obj, CIMClassEnum.ACLineSegment, tableLogger);
                }
                else if (obj.ClassType == CIMClassEnum.CurrentTransformer)
                {
                    ConnectAuxObjectToObject(obj, CIMClassEnum.Switch, tableLogger);
                }
                else if (obj.ClassType == CIMClassEnum.PotentialTransformer)
                {
                    ConnectAuxObjectToObject(obj, CIMClassEnum.Switch, tableLogger);
                }


            }
        }

        private void FixConnectivityNodesInSeries(CIMGraph g, CimErrorLogger tableLogger)
        {
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ConnectivityNode)
                {
                    if (obj.Neighbours.Any(cimObj => cimObj.ClassType == CIMClassEnum.ConnectivityNode))
                    {
                        var cnToRemove = obj.Neighbours.Find(cimObj => cimObj.ClassType == CIMClassEnum.ConnectivityNode);

                        var cnToRemoveNeighbors = cnToRemove.Neighbours.ToArray();
                            
                            
                        // Move all neighbors to this node
                        foreach (var cnToRemoteNeighbor in cnToRemoveNeighbors)
                        {
                            // Remove neighbor relation to cn (to be removed)
                            cnToRemoteNeighbor.RemoveNeighbour(cnToRemove);

                            // Remove cn (to be removed) relation to neighbor
                            cnToRemove.RemoveNeighbour(cnToRemoteNeighbor);

                            // Connect this cn to neighbor 
                            obj.AddNeighbour(cnToRemoteNeighbor);

                            // Connect neighbor to this cn
                            cnToRemoteNeighbor.AddNeighbour(obj);
                        }
                    }
                    
                }
            }
        }

        private void CheckPowerTransformers(CIMGraph g, CimErrorLogger tableLogger)
        {
            foreach (var obj in g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.PowerTransformer)
                {
                    CIMConductingEquipment trafo = obj as CIMConductingEquipment;

                    if (trafo.GetEquipmentContainerRoot() == null)
                        tableLogger.Log(Severity.Error, (int)GeneralErrors.ComponentHasNoParent, "Cannot check power tranformer - has no parent.", obj);
                    else if (trafo.Terminals != null)
                    {
                        var terminals = trafo.Terminals.ToArray();

                        ////////////////////////////////////
                        // Check primary side
                        if (terminals.Length > 0)
                        {
                            var cn = terminals[0].ConnectivityNode;

                            // We expect that terminal 1 (primary side) is connected to a connectivity node
                            if (cn != null)
                            {
                                var acLines = cn.GetNeighbours(CIMClassEnum.ACLineSegment);

                                // We expect one ac line segment connected to primary side connectivity node
                                if (acLines.Count == 1)
                                {
                                    var priAcLine = acLines[0];

                                    // Check that acLine has no busbar sections as neighbor. PSI want a bay + switch inbetween power transformer and primary busbar
                                    var busbarsNeighbors = priAcLine.GetNeighboursNeighbors(CIMClassEnum.BusbarSection);

                                    if (busbarsNeighbors.Count > 0)
                                        tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerPrimaryCableConnectedDirectlyToBusbar, "Power transformer primary cable connected directly to busbar. PSI can not handle this!", obj);

                                    // We expect Ac line voltage level to be equal station voltage level
                                    if (priAcLine.VoltageLevel != trafo.GetEquipmentContainerRoot().VoltageLevel)
                                        tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerPrimaryCableWrongVoltageLevel, "Expected that the power transformer was connected to a primary cable with same voltage level as the substation. Found cable: " + priAcLine.ToString(), obj);

                                    var traceResult = CIMTraversalInsideSubstation(priAcLine);

                                    bool hasPriBusbar = traceResult.Any(cimObj =>
                                        cimObj.ClassType == CIMClassEnum.BusbarSection);

                                    bool hasPriBusbarWithCorrectVoltageLevel = traceResult.Any(cimObj =>
                                        cimObj.ClassType == CIMClassEnum.BusbarSection &&
                                        cimObj.VoltageLevel == obj.EquipmentContainerRef.VoltageLevel);

                                    if (!hasPriBusbarWithCorrectVoltageLevel)
                                    {
                                        if (hasPriBusbar)
                                        {
                                            // Expected connection to primary busbar
                                            tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerExpectedPrimaryBusbar, "Expected that the power transformer was connected to a primary busbar with same voltage level as the substation. Found busbar but not with correct voltage level.", obj);
                                        }
                                        else
                                        {
                                            // Expected connection to primary busbar
                                            tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerExpectedPrimaryBusbar, "Expected that the power transformer was connected to a primary busbar with same voltage level as the substation. No busbars found at all!", obj);
                                        }

                                    }

                                }
                                else
                                {
                                    // Expected one cable to primary side
                                    tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerExpectedOnePrimaryCable, "Expected one cable to primary side. " + acLines.Count + " found!", obj);

                                }
                            }
                            else
                            {
                                // No terminals
                                tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerPrimaryTerminalNotConnected, "Power transformer primary terminal is not connected to a connectivity node", obj);

                            }
                        }
                        else
                        {
                            // No terminals
                            tableLogger.Log(Severity.Error, (int)GeneralErrors.PowerTransformerHasNoTerminals, "Power transformer has no terminals", obj);

                        }


                    }
                }
            }
        }

        private Queue<CIMIdentifiedObject> CIMTraversalInsideSubstation(CIMIdentifiedObject cimObj)
        {
            var station = cimObj.GetEquipmentContainerRoot();

            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();

            if (station != null)
            {

                Queue<CIMIdentifiedObject> Q = new Queue<CIMIdentifiedObject>();
                HashSet<CIMIdentifiedObject> S = new HashSet<CIMIdentifiedObject>();
                Q.Enqueue(cimObj);
                S.Add(cimObj);

                while (Q.Count > 0)
                {
                    CIMConductingEquipment p = Q.Dequeue() as CIMConductingEquipment;
                    traverseOrder.Enqueue(p);

                    foreach (CIMTerminal terminal in p.Terminals)
                    {
                        if (terminal.ConnectivityNode != null)
                        {
                            foreach (var neighbor in terminal.ConnectivityNode.GetNeighbours())
                            {
                                if (neighbor is CIMConductingEquipment)
                                {
                                    if (!S.Contains(neighbor))
                                    {
                                        S.Add(neighbor);

                                        var rootContainer = neighbor.GetEquipmentContainerRoot();

                                        // Er vi inde i station, da forsæt trace
                                        if (rootContainer != null && rootContainer == station)
                                        {
                                           Q.Enqueue(neighbor);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return traverseOrder;
        }

        private void ProcessPowerTransformers(CIMGraph g, CimErrorLogger tableLogger)
        {
            foreach (var obj in g.CIMObjects)
            {
                if (obj.mRID == Guid.Parse("f06617c6-b615-478b-94dc-444cf0fa2e60"))
                {
                }

                if (obj.ClassType == CIMClassEnum.PowerTransformer && obj.Neighbours.Count > 2)
                {
                    
                    
                    Dictionary<int, List<CIMIdentifiedObject>> acLineSegmentCNsByVoltageLevel = new Dictionary<int, List<CIMIdentifiedObject>>();

                    foreach (var cn in obj.Neighbours)
                    {
                        foreach (var n in cn.GetNeighbours(CIMClassEnum.ACLineSegment))
                        {
                            if (!acLineSegmentCNsByVoltageLevel.ContainsKey(n.VoltageLevel))
                                acLineSegmentCNsByVoltageLevel[n.VoltageLevel] = new List<CIMIdentifiedObject>();

                            acLineSegmentCNsByVoltageLevel[n.VoltageLevel].Add(cn);
                        }
                    }

                    foreach (var acLists in acLineSegmentCNsByVoltageLevel.Values)
                    {
                        if (acLists.Count > 1)
                        {
                            // Lad den første CN blive (i = 1)
                            for (int i = 1; i < acLists.Count; i++)
                            {
                                var firstCn = acLists[0];
                                var cn = acLists[i];

                                List<CIMIdentifiedObject> cnNeighbors = new List<CIMIdentifiedObject>();
                                cnNeighbors.AddRange(cn.Neighbours);

                                // flyt forbindelser til CN over til først CN
                                foreach (var cnNeighbor in cnNeighbors)
                                {
                                    if (cnNeighbor.ClassType == CIMClassEnum.PowerTransformer)
                                    {
                                        // Fjerne reference fra obj til CN
                                        cnNeighbor.RemoveNeighbour(cn);
                                        cn.RemoveNeighbour(cnNeighbor);
                                    }

                                    if (cnNeighbor.ClassType == CIMClassEnum.ACLineSegment)
                                    {
                                        // Fjerne reference fra obj til CN
                                        cnNeighbor.RemoveNeighbour(cn);
                                        cn.RemoveNeighbour(cnNeighbor);

                                        // Tilføj reference fra obj til først cn
                                        cnNeighbor.AddNeighbour(firstCn);
                                        firstCn.AddNeighbour(cnNeighbor);
                                    }
                                }
                            }

                        }
                    }


                }

            }
        }

        private void ProcessEnergyConsumersInSeries(CIMGraph g, CimErrorLogger tableLogger)
        {
            foreach (var obj in g.CIMObjects)
            {

                if (obj.ClassType == CIMClassEnum.EnergyConsumer && obj.Neighbours != null && obj.Neighbours.Count > 1)
                {
                    var cns = obj.GetNeighbours(CIMClassEnum.ConnectivityNode);

                    if (cns != null && cns.Count > 1)
                    {
                        var keepCn = cns[0];

                        for (int i = 1; i < cns.Count; i++)
                        {
                            var cnToMode = cns[i];

                            if (cns[i].Neighbours != null)
                            {
                                var cnToModeNeighbors = cns[i].Neighbours.ToArray();
                                // Move everyting to keepCN
                                foreach (var neighbor in cnToModeNeighbors)
                                {

                                    neighbor.RemoveNeighbour(cnToMode);
                                    cnToMode.RemoveNeighbour(neighbor);

                                    if (neighbor.ClassType != CIMClassEnum.EnergyConsumer)
                                    {
                                        keepCn.AddNeighbour(neighbor);
                                        neighbor.AddNeighbour(keepCn);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ProcessCoilsWithMoreThanOneTerminal(CIMGraph g, CimErrorLogger tableLogger)
        {
            foreach (var obj in g.CIMObjects)
            {

                if (obj.ClassType == CIMClassEnum.PetersenCoil && obj.Neighbours != null && obj.Neighbours.Count > 1)
                {
                    var cns = obj.GetNeighbours(CIMClassEnum.ConnectivityNode);

                    if (cns != null && cns.Count > 1)
                    {
                        var keepCn = cns[0];

                        for (int i = 1; i < cns.Count; i++)
                        {
                            var cnToMode = cns[i];

                            if (cns[i].Neighbours != null)
                            {
                                var cnToModeNeighbors = cns[i].Neighbours.ToArray();
                                // Move everyting to keepCN
                                foreach (var neighbor in cnToModeNeighbors)
                                {

                                    neighbor.RemoveNeighbour(cnToMode);
                                    cnToMode.RemoveNeighbour(neighbor);

                                    if (neighbor.ClassType != CIMClassEnum.PetersenCoil)
                                    {
                                        keepCn.AddNeighbour(neighbor);
                                        neighbor.AddNeighbour(keepCn);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ConnectAuxObjectToObject(CIMIdentifiedObject objToConnect, CIMClassEnum cimClassToConnectTo, CimErrorLogger tableLogger)
        {
            if (objToConnect.Name != null && objToConnect.Name == "571313124503002715")
            {

            }


            List<CIMIdentifiedObject> neighborList = new List<CIMIdentifiedObject>();

            // Remove aux eq connection from neighbors
            foreach (var neighbor in objToConnect.Neighbours)
            {
                neighborList.Add(neighbor);
                neighbor.RemoveNeighbour(objToConnect);
            }

            // Remove it from aux eq
            foreach (var neighbor in neighborList)
            {
                objToConnect.RemoveNeighbour(neighbor);
            }


            if (neighborList.Count == 2)
            {
                // Connect neighbors together
                neighborList[0].AddNeighbour(neighborList[1]);
                neighborList[1].AddNeighbour(neighborList[0]);

                // Connect eux eq to conducting equipment
                if (neighborList[0].EquipmentContainerRef != null)
                {
                    // Fix containment if nessesary
                    if (objToConnect.EquipmentContainerRef == null)
                    {
                        objToConnect.EquipmentContainerRef = neighborList[0].EquipmentContainerRef;
                        neighborList[0].EquipmentContainerRef.Children.Add(objToConnect);
                    }

                    if (objToConnect.EquipmentContainerRef != null)
                    {
                        // Switch
                        if (cimClassToConnectTo == CIMClassEnum.Switch)
                        {
                            CIMTerminal terminalFound = null;

                            // Search for breaker
                            foreach (var bayChild in objToConnect.EquipmentContainerRef.Children)
                            {
                                if (bayChild.ClassType == CIMClassEnum.Breaker)
                                {
                                    var childTerminals = ((CIMConductingEquipment)bayChild).Terminals.ToArray();
                                    
                                    if (childTerminals != null && childTerminals.Length > 0)
                                        terminalFound = childTerminals[0];
                                }
                            }

                            if (terminalFound == null)
                            {
                                // Search for load break switch
                                foreach (var bayChild in objToConnect.EquipmentContainerRef.Children)
                                {
                                    if (bayChild.ClassType == CIMClassEnum.LoadBreakSwitch)
                                    {
                                        var childTerminals = ((CIMConductingEquipment)bayChild).Terminals.ToArray();

                                        if (childTerminals != null && childTerminals.Length > 0)
                                            terminalFound = childTerminals[0];
                                    }
                                }
                            }

                            if (terminalFound == null)
                            {
                                // Search for disconnector
                                foreach (var bayChild in objToConnect.EquipmentContainerRef.Children)
                                {
                                    if (bayChild.ClassType == CIMClassEnum.Disconnector)
                                    {
                                        var childTerminals = ((CIMConductingEquipment)bayChild).Terminals.ToArray();

                                        if (childTerminals != null && childTerminals.Length > 0)
                                            terminalFound = childTerminals[0];
                                    }
                                }
                            }

                            if (terminalFound == null)
                            {
                                // Search for busbar
                                foreach (var ctNeighbor in neighborList)
                                {
                                    if (ctNeighbor.Neighbours.Exists(o => o.ClassType == CIMClassEnum.BusbarSection))
                                    {
                                        var busBar = ctNeighbor.Neighbours.Find(o => o.ClassType == CIMClassEnum.BusbarSection);
                                        terminalFound = ((CIMConductingEquipment)busBar).Terminals.ToArray()[0];
                                    }
                                    else
                                    {
                                        // Try see if at VT sits in from of CT and busbar
                                        if (ctNeighbor.Neighbours.Exists(o => o.ClassType == CIMClassEnum.PotentialTransformer))
                                        {
                                            var vt = ctNeighbor.Neighbours.Find(o => o.ClassType == CIMClassEnum.PotentialTransformer);

                                            foreach (var vtNeighbor in vt.Neighbours)
                                            {
                                                if (vtNeighbor.Neighbours.Exists(o => o.ClassType == CIMClassEnum.BusbarSection))
                                                {
                                                    var busBar = vtNeighbor.Neighbours.Find(o => o.ClassType == CIMClassEnum.BusbarSection);
                                                    terminalFound = ((CIMConductingEquipment)busBar).Terminals.ToArray()[0];
                                                }

                                            }
                                        }
                                    }
                                }
                            }



                            if (terminalFound != null)
                            {
                                objToConnect.SetPropertyValue("cim.terminal", terminalFound.mRID);
                            }
                            else
                                cimClassToConnectTo = CIMClassEnum.ACLineSegment;
                        }

                        // ACLineSegment
                        if (cimClassToConnectTo == CIMClassEnum.ACLineSegment)
                        {
                            List<CIMIdentifiedObject> acLinesSearchResult = new List<CIMIdentifiedObject>();

                            foreach (var child in objToConnect.EquipmentContainerRef.Children)
                            {
                                if (child.ClassType == CIMClassEnum.ACLineSegment)
                                    acLinesSearchResult.Add(child);

                                acLinesSearchResult.AddRange(child.GetNeighboursNeighbors(CIMClassEnum.ACLineSegment));
                            }

                            if (acLinesSearchResult.Count > 0)
                            {
                                var acLineToConnect = acLinesSearchResult[0];

                                CIMTerminal terminalFound = null;

                                foreach (var acLineTerminal in ((CIMConductingEquipment)acLineToConnect).Terminals)
                                {
                                    if (acLineTerminal.ConnectivityNode != null)
                                    {
                                        foreach (var acLineTerminalNeighbor in acLineTerminal.ConnectivityNode.Neighbours)
                                        {
                                            // Check if neighbor in bay
                                            foreach (var bayChild in objToConnect.EquipmentContainerRef.Children)
                                            {
                                                if (bayChild == acLineTerminalNeighbor)
                                                    terminalFound = acLineTerminal;
                                            }
                                        }
                                    }
                                }

                                if (terminalFound != null)
                                    objToConnect.SetPropertyValue("cim.terminal", terminalFound.mRID);
                                else
                                    tableLogger.Log(Severity.Error, (int)GeneralErrors.AuxEquipmentCannotFindCableToConnect, "Cannot figure out which AC Line Segment terminal to connect. No terminal belonging to cable: " + acLineToConnect + " is connected to a object inside the bay where the fault indicator is located!", objToConnect);
                            }
                            else
                                tableLogger.Log(Severity.Error, (int)GeneralErrors.AuxEquipmentCannotFindCableToConnect, "Cannot find a cable to connect the aux equipment.", objToConnect);

                        }
                    }
                    else
                        tableLogger.Log(Severity.Error, (int)GeneralErrors.AuxEquipmentCannotFindParent, "No connection to parent. PSI need all aux equipments to have a relation to a bay.", objToConnect);
 

                    /*
                    CIMIdentifiedObject sw1 = null;
                    foreach (var bayChild in neighborList[0].EquipmentContainerRef.Children)
                    {
                        if (bayChild.ClassType == cimClassToConnectTo)
                        {
                            sw1 = bayChild;
                            var terminals = ((CIMConductingEquipment)bayChild).Terminals;
                            var terminal = terminals.FirstOrDefault();
                            if (terminal != null)
                                objToConnect.SetPropertyValue("cim.terminal", terminal.mRID);

                        }
                    }

                    // Check if aux equipment is within same bay as connected equipment
                    if (sw1 != null && sw1.EquipmentContainerRef != objToConnect.EquipmentContainerRef)
                    {
                        // It's not - because something wrong with the relations in GIS. We trust the breaker
                        objToConnect.EquipmentContainerRef.Children.Remove(objToConnect);
                        objToConnect.EquipmentContainerRef = sw1.EquipmentContainerRef;
                        sw1.EquipmentContainerRef.Children.Add(objToConnect);
                    }
                     */

                }
            }
        }

        private void RemoveDublicatedNeighbors(CIMGraph g, CimErrorLogger tableLogger)
        {
            // Ensure no neighbor exists more that one time
            foreach (var cimObj in g.CIMObjects)
            {
                CIMIdentifiedObject toBeRemoved = null;

                foreach (var neighbor in cimObj.Neighbours)
                {
                    if (cimObj.Neighbours.Count(o => o == neighbor) > 1)
                    {
                        // Remove dublicate
                        toBeRemoved = neighbor;
                    }
                }

                if (toBeRemoved != null)
                    cimObj.Neighbours.Remove(toBeRemoved);
            }
        }

        private void EnsureStableConnectivityNodeMRID(CIMGraph g, CimErrorLogger tableLogger)
        {
            // Ensure Connectivity nodes has stable mRID's
            foreach (var cimObj in g.CIMObjects)
            {
                if (cimObj is CIMConnectivityNode && !g.ObjectManager.IsDeleted(cimObj))
                {
                    var cn = cimObj as CIMConnectivityNode;

                    var cnNeighbors = cn.GetNeighbours();

                    // Order neighbors of CN on mrid
                    var cnNeighborsOrdered = cn.GetNeighbours().OrderBy(o => o.mRID);

                    if (cnNeighbors.Count == 0)
                    {
                        //Don't log these - polutes the log
                        //Logger.Log(LogLevel.Warning, "Connectivity node with no neighbors not allowed. " + cn.ToString());
                        g.ObjectManager.Delete(cn);
                    }
                    else if (!(cnNeighbors[0] is CIMConductingEquipment))
                    {
                        //Logger.Log(LogLevel.Warning, "Connectivity node is connected to non conducting equipment. This is not allowed. " + cn.ToString());
                        g.ObjectManager.Delete(cn);
                    }
                    else
                    {
                        // We want to take mrid from busbar if such exist as neighbor
                        if (cnNeighbors.Exists(c => c.ClassType == CIMClassEnum.BusbarSection))
                        {
                            var busBar = cnNeighborsOrdered.First(c => c.ClassType == CIMClassEnum.BusbarSection) as CIMConductingEquipment;

                            if (busBar.Neighbours.IndexOf(cn) >= 0)
                            {
                                var terminal = busBar.Terminals.First(o => o.EndNumber == (busBar.Neighbours.IndexOf(cn) + 1));
                                cn.mRID = GUIDHelper.CreateDerivedGuid(terminal.mRID, 555, true);
                            }
                            else
                            {
                                Logger.Log(LogLevel.Warning, "Connectivity is sick around: " + busBar.ToString());
                                g.ObjectManager.Delete(cn);
                            }
                        }
                        // Or trafo
                        else if (cnNeighbors.Exists(c => c.ClassType == CIMClassEnum.PowerTransformer))
                        {
                            var pt = cnNeighborsOrdered.First(c => c.ClassType == CIMClassEnum.PowerTransformer) as CIMConductingEquipment;

                            if (pt.Neighbours.IndexOf(cn) >= 0)
                            {
                                var terminal = pt.Terminals.First(o => o.EndNumber == (pt.Neighbours.IndexOf(cn) + 1));
                                cn.mRID = GUIDHelper.CreateDerivedGuid(terminal.mRID, 555, true);
                            }
                            else
                            {
                                Logger.Log(LogLevel.Warning, "Connectivity node don't fit any terminals: " + cn.ToString() + " " + pt.ToString());
                                g.ObjectManager.Delete(cn);
                            }
                        }
                        // Or switch
                        else if (cnNeighbors.Exists(c =>
                            c.ClassType == CIMClassEnum.Disconnector ||
                            c.ClassType == CIMClassEnum.Fuse ||
                            c.ClassType == CIMClassEnum.LoadBreakSwitch ||
                            c.ClassType == CIMClassEnum.Breaker ||
                            c.ClassType == CIMClassEnum.Switch
                        ))
                        {
                            var sw = cnNeighborsOrdered.First(c =>
                                c.ClassType == CIMClassEnum.Disconnector ||
                                c.ClassType == CIMClassEnum.Fuse ||
                                c.ClassType == CIMClassEnum.LoadBreakSwitch ||
                                c.ClassType == CIMClassEnum.Breaker ||
                                c.ClassType == CIMClassEnum.Switch) as CIMConductingEquipment;

                            if (sw.Neighbours.IndexOf(cn) >= 0)
                            {
                                var terminal = sw.Terminals.First(o => o.EndNumber == (sw.Neighbours.IndexOf(cn) + 1));
                                cn.mRID = GUIDHelper.CreateDerivedGuid(terminal.mRID, 555, true);
                            }
                            else
                            {
                                Logger.Log(LogLevel.Warning, "Connectivity is sick around: " + sw.ToString());
                                g.ObjectManager.Delete(cn);
                            }
                        }
                        // Or ACLS
                        else if (cnNeighbors.Exists(c => c.ClassType == CIMClassEnum.ACLineSegment))
                        {
                            var acls = cnNeighborsOrdered.First(c => c.ClassType == CIMClassEnum.ACLineSegment) as CIMConductingEquipment;

                            if (acls.mRID == Guid.Parse("C5AAF1A0-77DE-462C-9A3B-D6CF499D275D"))
                            {
                            }

                            if (acls.Neighbours.IndexOf(cn) >= 0 && acls.Neighbours.IndexOf(cn) < 2)
                            {
                                var terminal = acls.Terminals.First(o => o.EndNumber == (acls.Neighbours.IndexOf(cn) + 1));
                                cn.mRID = GUIDHelper.CreateDerivedGuid(terminal.mRID, 555, true);
                            }
                            else
                            {
                                Logger.Log(LogLevel.Warning, "Connectivity is sick around: " + acls.ToString());
                                g.ObjectManager.Delete(cn);
                            }
                        }
                        else
                        {
                            Logger.Log(LogLevel.Warning, "Connectivity node had bad neighbors! Expected busbar, powertranformer, switch or acls! " + cn.ToString());
                            g.ObjectManager.Delete(cn);
                        }
                    }
                }
            }

        }

        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

    }
}
