﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CIM.PhysicalNetworkModel;
using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Extensions;
using DAX.IO.CIM;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Linemerge;

namespace CIM.PowerFactoryExporter
{
    /// <summary>
    /// Find ACLS junctions (connectivity nodes) outside substations. 
    /// If the ACLS have the same charateristic, merge them.
    /// Otherwise create a "junction substation".
    /// Required for Power Factory CGMES import to work. 
    /// All ACLS ends must be connected inside a container.
    /// </summary>
    public class ACLSMerger : IPreProcessor
    {
        int _guidOffset = 1000;

        MappingContext _mappingContext;

        public ACLSMerger(MappingContext mappingContext)
        {
            _mappingContext = mappingContext;
        }

        public IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> input)
        {
            HashSet<PhysicalNetworkModel.IdentifiedObject> dropList = new HashSet<IdentifiedObject>();

            List<PhysicalNetworkModel.IdentifiedObject> addList = new List<IdentifiedObject>();

            foreach (var inputCimObject in input)
            {
                // Find low voltage acls's and set default values
                if (inputCimObject is ACLineSegment && ((ACLineSegment)inputCimObject).BaseVoltage < 1000 && !dropList.Contains(inputCimObject) && !inputCimObject.IsInsideSubstation(context))
                {
                    var acls = inputCimObject as ACLineSegment;


                    if (acls.x == null)
                        acls.x = new Reactance() { Value = 0 };

                    if (acls.x0 == null)
                        acls.x0 = new Reactance() { Value = 0 };


                    if (acls.r == null)
                        acls.r = new Resistance() { Value = 0 };

                    if (acls.r0 == null)
                        acls.r0 = new Resistance() { Value = 0 };


                    if (acls.bch == null)
                        acls.bch = new Susceptance() { Value = 0 };

                    if (acls.b0ch == null)
                        acls.b0ch = new Susceptance() { Value = 0 };


                    if (acls.gch == null)
                        acls.gch = new Conductance() { Value = 0 };

                    if (acls.g0ch == null)
                        acls.g0ch = new Conductance() { Value = 0 };
                }
            }

            foreach (var inputCimObject in input)
            {
                // Find connectivity nodes outside substations
                if (inputCimObject is ConnectivityNode && !dropList.Contains(inputCimObject) && !inputCimObject.IsInsideSubstation(context))
                {
                    ConnectivityNode cn = inputCimObject as ConnectivityNode;

                    // Handle that acls cn relationship might have changed
                    var cnNeighborsx = cn.GetNeighborConductingEquipments(context);

                    // If above above low voltage
                    if (cnNeighborsx.Count(o => o.BaseVoltage > 5000) > 0)
                    {
                        // acls <-> acls
                        if (cnNeighborsx.Count(o => o is ACLineSegmentExt) == 2)
                        {
                            var acls = cnNeighborsx.Where(o => o is ACLineSegmentExt).ToList();

                            var acls1 = acls[0] as ACLineSegment;
                            var acls2 = acls[1] as ACLineSegment;

                            // NEVER MERGE
                            bool theSame = false;

                            double acls1len = acls1.length != null ? acls1.length.Value : 0;

                            double acls2len = acls2.length != null ? acls2.length.Value : 0;

                            // Compate bch
                            if (acls1.bch != null && acls2.bch != null && !CompareAclsValue(acls1len, acls1.bch.Value, acls2len, acls2.bch.Value))
                                theSame = false;

                            // Compate b0ch
                            if (acls1.b0ch != null && acls2.b0ch != null && !CompareAclsValue(acls1len, acls1.b0ch.Value, acls2len, acls2.b0ch.Value))
                                theSame = false;

                            // Compare gch
                            if (acls1.gch != null && acls2.gch != null && !CompareAclsValue(acls1len, acls1.gch.Value, acls2len, acls2.gch.Value))
                                theSame = false;

                            // Compare g0ch
                            if (acls1.g0ch != null && acls2.g0ch != null && !CompareAclsValue(acls1len, acls1.g0ch.Value, acls2len, acls2.g0ch.Value))
                                theSame = false;

                            // Compare r
                            if (acls1.r != null && acls2.r != null && !CompareAclsValue(acls1len, acls1.r.Value, acls2len, acls2.r.Value))
                                theSame = false;

                            // Compare r0 
                            if (acls1.r0 != null && acls2.r0 != null && !CompareAclsValue(acls1len, acls1.r0.Value, acls2len, acls2.r0.Value))
                                theSame = false;

                            // Compare x
                            if (acls1.x != null && acls2.x != null && !CompareAclsValue(acls1len, acls1.x.Value, acls2len, acls2.x.Value))
                                theSame = false;

                            // Compare x0
                            if (acls1.x0 != null && acls2.x0 != null && !CompareAclsValue(acls1len, acls1.x0.Value, acls2len, acls2.x0.Value))
                                theSame = false;


                            // If the cables have the same eletrical charastica, merge them
                            if (theSame)
                            {

                                // ACLS 1 will survive, ACLS 2 and the CN will die
                                dropList.Add(cn);
                                dropList.Add(acls2);

                                // drop acls 2 terminals
                                foreach (var tc in context.GetConnections(acls2))
                                {
                                    dropList.Add(tc.Terminal);
                                }

                                var loc1 = context.GetObject<LocationExt>(acls1.Location.@ref);
                                var loc2 = context.GetObject<LocationExt>(acls2.Location.@ref);

                                LineMerger lm = new LineMerger();

                                // Convert to NTS geometries
                                lm.Add(GetGeometry(loc1));
                                lm.Add(GetGeometry(loc2));

                                // Merge the two line strings
                                var mergedLineList = lm.GetMergedLineStrings();

                                if (mergedLineList.Count != 1)
                                    throw new Exception("Cannot merge ACLS: " + acls1.mRID + " and " + acls2.mRID);

                                // Overwrite loc 1 coordinated with merged strings
                                loc1.coordinates = GetPoints((LineString)mergedLineList[0]).ToArray();

                                // Sum length
                                acls1.length.Value += acls2.length.Value;

                                // Sum bch
                                if (acls1.bch != null && acls2.bch != null)
                                    acls1.bch.Value += acls2.bch.Value;

                                // Sum b0ch
                                if (acls1.b0ch != null && acls2.b0ch != null)
                                    acls1.b0ch.Value += acls2.b0ch.Value;

                                // Sum gch
                                if (acls1.gch != null && acls2.gch != null)
                                    acls1.gch.Value += acls2.gch.Value;

                                // Sum g0ch
                                if (acls1.g0ch != null && acls2.g0ch != null)
                                    acls1.g0ch.Value += acls2.g0ch.Value;

                                // Sum r
                                if (acls1.r != null && acls2.r != null)
                                    acls1.r.Value += acls2.r.Value;

                                // Sum r0
                                if (acls1.r0 != null && acls2.r0 != null)
                                    acls1.r0.Value += acls2.r0.Value;

                                // Sum x
                                if (acls1.x != null && acls2.x != null)
                                    acls1.x.Value += acls2.x.Value;

                                // Sum x0
                                if (acls1.x0 != null && acls2.x0 != null)
                                    acls1.x0.Value += acls2.x0.Value;

                                // Find cn in the other end of ACLS 2
                                var acls2otherEndCn = context.GetConnections(acls2).Find(o => o.ConnectivityNode != cn);

                                // Get terminal of ACLS 1 that point to ACLS 2
                                var acls1Terminal = acls1.GetTerminal(acls2, context);

                                // Disconnect ACLS 2 terminals
                                var acls2connections = context.GetConnections(acls2);

                                List<Terminal> terminalsToDisconnect = new List<Terminal>();

                                foreach (var acls2con in acls2connections)
                                {
                                    terminalsToDisconnect.Add(acls2con.Terminal);
                                }

                                foreach (var t2d in terminalsToDisconnect)
                                {
                                    context.DisconnectTerminalFromConnectitityNode(t2d);
                                }

                                // Change terminal of ACLS 1 to point to ACLS 2 other end CN
                                context.ConnectTerminalToAnotherConnectitityNode(acls1Terminal, acls2otherEndCn.ConnectivityNode);
                            }
                            else
                            {
                                // Cable are not the same, we need to add a susbstation to act as a junction

                                // Create muffe station
                                var st = new PhysicalNetworkModel.Substation();
                                st.mRID = Guid.NewGuid().ToString();
                                st.name = "Junction";
                                st.PSRType = "Junction";
                                addList.Add(st);

                                // Create voltage level
                                var vl = new PhysicalNetworkModel.VoltageLevel();
                                vl.mRID = Guid.NewGuid().ToString();
                                vl.BaseVoltage = acls1.BaseVoltage;
                                vl.name = "VL";
                                vl.EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = st.mRID };
                                addList.Add(vl);

                                // Relate cn to voltage level
                                if (_mappingContext.ConnectivityNodeToVoltageLevel.ContainsKey(cn))
                                    _mappingContext.ConnectivityNodeToVoltageLevel.Remove(cn);

                                _mappingContext.ConnectivityNodeToVoltageLevel.Add(cn, vl);

                            }
                        }
                        // <> 2 kabler
                        else
                        {
                            // Create muffe station
                            var st = new PhysicalNetworkModel.Substation();
                            st.mRID = Guid.NewGuid().ToString();
                            st.name = "MUFFE";
                            st.PSRType = "Junction";
                            addList.Add(st);

                            // Create voltage level
                            var vl = new PhysicalNetworkModel.VoltageLevel();
                            vl.mRID = Guid.NewGuid().ToString();
                            vl.BaseVoltage = cnNeighborsx[0].BaseVoltage;
                            vl.name = "VL";
                            vl.EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = st.mRID };
                            addList.Add(vl);

                            // Relate cn to voltage level
                            if (_mappingContext.ConnectivityNodeToVoltageLevel.ContainsKey(cn))
                                _mappingContext.ConnectivityNodeToVoltageLevel.Remove(cn);

                            _mappingContext.ConnectivityNodeToVoltageLevel.Add(cn, vl);

                        }
                    }
                }
            }


            // Handle EC's not connected to a substation
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is EnergyConsumer)
                {
                    var ec = inputCimObject as EnergyConsumer;

                    // ec terminal connections
                    var ecTerminalConnections = context.GetConnections(inputCimObject);

                    if (ecTerminalConnections.Count == 1)
                    {
                        var ecTerminal = ecTerminalConnections[0].Terminal;

                        var ecCn = ecTerminalConnections[0].ConnectivityNode;

                        var ecCnConnections = context.GetConnections(ecCn);

                        var aclsCount = ecCnConnections.Count(o => o.ConductingEquipment is ACLineSegment);

                        if (aclsCount == 1)
                        {
                            var acls = ecCnConnections.First(o => o.ConductingEquipment is ACLineSegment).ConductingEquipment;

                            // find other end of acls
                            var aclsConnections = context.GetConnections(acls);

                            // Can we find another cn (in the other end of the cable)
                            if (aclsConnections.Exists(o => o.ConnectivityNode != ecCn))
                            {
                                var aclsCn = aclsConnections.First(o => o.ConnectivityNode != ecCn).ConnectivityNode;

                         
                                var aclsSt = aclsCn.GetSubstation(context);

                                if (aclsSt == null)
                                {
                                    if (_mappingContext.ConnectivityNodeToVoltageLevel.ContainsKey(aclsCn))
                                    {
                                        ec.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = _mappingContext.ConnectivityNodeToVoltageLevel[aclsCn].mRID };
                                    }
                                    else
                                    {


                                        // Create muffe station
                                        var st = new PhysicalNetworkModel.Substation();
                                        st.mRID = Guid.NewGuid().ToString();
                                        st.name = "Fælles inst";
                                        st.PSRType = "Junction";
                                        addList.Add(st);



                                        // Create voltage level
                                        var vl = new PhysicalNetworkModel.VoltageLevel();
                                        vl.mRID = Guid.NewGuid().ToString();
                                        vl.BaseVoltage = acls.BaseVoltage;
                                        vl.name = "VL";
                                        vl.EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = st.mRID };
                                        addList.Add(vl);

                                        System.Diagnostics.Debug.WriteLine("FÆLLES STIK VL: " + vl.mRID);

                                        // Relate cn to voltage level
                                        if (_mappingContext.ConnectivityNodeToVoltageLevel.ContainsKey(aclsCn))
                                            _mappingContext.ConnectivityNodeToVoltageLevel.Remove(aclsCn);

                                        _mappingContext.ConnectivityNodeToVoltageLevel.Add(aclsCn, vl);

                                        // Add ec to new container
                                        ec.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = vl.mRID };
                                    }

                                    dropList.Add(acls);
                                    dropList.Add(aclsConnections[0].Terminal);
                                    dropList.Add(aclsConnections[1].Terminal);

                                }
                            }
                        }
                    }
                }
            }

            // return objects, except the one dropped
            foreach (var inputObj in input)
            {
                if (!dropList.Contains(inputObj))
                    yield return inputObj;
            }

            // yield added objects,
            foreach (var inputObj in addList)
            {
                yield return inputObj;
            }

        }

        private bool CompareAclsValue(double acls1len, double acls1val, double acls2len, double acls2val)
        {
            double val1 = Math.Round(acls1val / acls1len, 4);
            double val2 = Math.Round(acls2val / acls2len, 4);

            return (val1 == val2);
        }

        private Geometry GetGeometry(PhysicalNetworkModel.LocationExt location)
        {
            List<Coordinate> points = new List<Coordinate>();
            foreach (var locPt in location.coordinates)
                points.Add(new Coordinate(locPt.X, locPt.Y));

            return (new LineString(points.ToArray()));
        }

        private List<PhysicalNetworkModel.Point2D> GetPoints(LineString line)
        {
            List<PhysicalNetworkModel.Point2D> result = new List<Point2D>();

            foreach (var pt in line.Coordinates)
                result.Add(new Point2D() { X = pt.X, Y = pt.Y });

            return result;
        }
    }
}
