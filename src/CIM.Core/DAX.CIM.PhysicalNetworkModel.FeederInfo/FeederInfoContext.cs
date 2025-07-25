﻿using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CIM.PhysicalNetworkModel.FeederInfo
{
    public class FeederInfoContext
    {
        readonly Dictionary<ConnectivityNode, ConnectionPoint> _connectionPoints = new Dictionary<ConnectivityNode, ConnectionPoint>();
        readonly Dictionary<Substation, List<ConnectionPoint>> _stConnectionPoints = new Dictionary<Substation, List<ConnectionPoint>>();
        readonly Dictionary<ConductingEquipment, ConductingEquipmentFeederInfo> _conductingEquipmentFeeders = new Dictionary<ConductingEquipment, ConductingEquipmentFeederInfo>();
     
        readonly CimContext _cimContext;

        private bool _treatTJunctionsAsCabinets = false;

        public FeederInfoContext(CimContext cimContext)
        {
            _cimContext = cimContext;
        }

        public List<Feeder> GetFeeders()
        {
            Dictionary<string, Feeder> result = new Dictionary<string, Feeder>();

            foreach (var connectionPoint in _connectionPoints)
            {
                foreach (var feeder in connectionPoint.Value.Feeders)
                {
                    if (!result.ContainsKey(feeder.ConductingEquipment.mRID))
                    {
                        result.Add(feeder.ConductingEquipment.mRID, feeder);
                    }
                }
            }

            return result.Values.ToList();
        }

        public Dictionary<ConductingEquipment, ConductingEquipmentFeederInfo> GetFeederInfos()
        {
            return _conductingEquipmentFeeders;
        }

        public Dictionary<ConductingEquipment, List<Feeder>> GetConductionEquipmentFeeders()
        {
            Dictionary<ConductingEquipment, List<Feeder>> result = new Dictionary<ConductingEquipment, List<Feeder>>();

            foreach (var feederInfo in _conductingEquipmentFeeders)
            {
                result.Add(feederInfo.Key, feederInfo.Value.Feeders);
            }

            return result;
        }
        
        public void CreateFeederObjects(bool treatTJunctionsAsCabinets = false)
        {
            _treatTJunctionsAsCabinets = treatTJunctionsAsCabinets;

            CreateConnectionPointsAndFeeders();
            SubstationInternalPowerTransformerTrace();
            TraceAllFeeders();
            FixTJunctionCustomers();
        }

        public List<Feeder> GeConductingEquipmentFeeders(ConductingEquipment ce)
        {
            if (_conductingEquipmentFeeders.ContainsKey(ce))
                return _conductingEquipmentFeeders[ce].Feeders;
            else
                return new List<Feeder>();
        }

        public ConductingEquipmentFeederInfo GeConductingEquipmentFeederInfo(ConductingEquipment ce)
        {
            if (_conductingEquipmentFeeders.ContainsKey(ce))
                return _conductingEquipmentFeeders[ce];
            else
                return new ConductingEquipmentFeederInfo();
        }

        public List<ConnectionPoint> GetSubstationConnectionPoints(Substation st)
        {
            return _stConnectionPoints[st];
        }

        private void CreateConnectionPointsAndFeeders()
        {
            // Create feeder connection points in all substations
            foreach (var obj in _cimContext.GetAllObjects())
            {
                if (obj is Substation)
                {
                    var st = obj as Substation;

                    List<Equipment> stEquipments = new List<Equipment>();

                    stEquipments = st.GetEquipments(_cimContext);

                    // For each equipment inside substation
                    foreach (var stEq in stEquipments)
                    {
                        // If conducting equipment
                        if (stEq is ConductingEquipment)
                        {
                            var stCi = stEq as ConductingEquipment;

                            // For each conducting equipment terminal connection
                            foreach (var ciTc in _cimContext.GetConnections(stCi))
                            {
                                // For each connectivity node terminal connection
                                foreach (var cnTc in _cimContext.GetConnections(ciTc.ConnectivityNode))
                                {
                                    // If connected to some conducting outside station we have a connection point
                                    if (!cnTc.ConductingEquipment.IsInsideSubstation(_cimContext))
                                    {
                                        var cp = CreateConnectionPoint(ConnectionPointKind.Line, cnTc.ConnectivityNode, st, stEq.GetBay(_cimContext, false));

                                        // Don't add feeders where bays are connected to an external network injection
                                        if (!(cnTc.ConductingEquipment is ExternalNetworkInjection))
                                            CreateFeeder(cp, cnTc.ConductingEquipment);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Create trafo connection points in all substation
            foreach (var obj in _cimContext.GetAllObjects())
            {
                if (obj is PowerTransformer)
                {
                    var trafo = obj as PowerTransformer;
                    var st = trafo.GetSubstation(_cimContext, false);

                    // Check if local transformer, then don't create connection point
                    //if (trafo.GetNeighborConductingEquipments(_cimContext).Exists(o => o.BaseVoltage == st.GetPrimaryVoltageLevel(_cimContext)))
                    if (trafo.name != null && !trafo.name.ToLower().Contains("lokal"))
                    {
                        var trafoConnections = _cimContext.GetConnections(trafo);

                        // Find terminal 2
                        if (trafoConnections.Exists(c => c.Terminal.sequenceNumber == "2"))
                        {
                            var trafoTerminal2Connection = trafoConnections.First(c => c.Terminal.sequenceNumber == "2");

                            if (!_connectionPoints.ContainsKey(trafoTerminal2Connection.ConnectivityNode))
                            {
                                var cp = CreateConnectionPoint(ConnectionPointKind.PowerTranformer, trafoTerminal2Connection.ConnectivityNode, st, null);
                                CreateFeeder(cp, trafo);
                            }
                            else
                            {

                            }
                        }
                    }
                }
            }

            // Create connection points on all external network injections
            foreach (var obj in _cimContext.GetAllObjects())
            {
                if (obj is ExternalNetworkInjection)
                {
                    var source = obj as ExternalNetworkInjection;
                    var sourceConnections = _cimContext.GetConnections(source);

                    // Find terminal 1
                    if (sourceConnections.Exists(c => c.Terminal.sequenceNumber == "1"))
                    {
                        var extTerminal2Connection = sourceConnections.First(c => c.Terminal.sequenceNumber == "1");

                      
                            var cp = CreateConnectionPoint(ConnectionPointKind.ExternalNetworkInjection, extTerminal2Connection.ConnectivityNode, null, null);
                            CreateFeeder(cp, source);
                      
                    }
                }
            }
        }

        private ConnectionPoint CreateConnectionPoint(ConnectionPointKind kind, ConnectivityNode cn, Substation st, Bay bay)
        {
            if (!_connectionPoints.ContainsKey(cn))
            {
                var newCp = new ConnectionPoint() { Kind = kind, ConnectivityNode = cn, Substation = st, Bay = bay };
                _connectionPoints[cn] = newCp;

                // Add to station dict
                if (st != null)
                {
                    if (!_stConnectionPoints.ContainsKey(st))
                        _stConnectionPoints[st] = new List<ConnectionPoint>();

                    _stConnectionPoints[st].Add(newCp);

                    if (st.ConnectionPoints == null)
                        st.ConnectionPoints = new List<ConnectionPoint>();

                    st.ConnectionPoints.Add(newCp);
                }

                return newCp;
            }
            else
                return _connectionPoints[cn];
        }

        private void CreateFeeder(ConnectionPoint connectionPoint, ConductingEquipment conductingEquipment)
        {
            // Don't create feeder if conducting equipment already feederized
            if (connectionPoint.Feeders.Count(c => c.ConductingEquipment == conductingEquipment) > 0)
                return;

            // External Network Injection
            if (connectionPoint != null &&
                conductingEquipment != null &&
                conductingEquipment is ExternalNetworkInjection)
            {
                var feeder = new Feeder()
                {
                    ConnectionPoint = connectionPoint,
                    ConductingEquipment = conductingEquipment,
                    FeederType = FeederType.NetworkInjection,
                    VoltageLevel = FeederVoltageLevel.HighVoltage
                };

                connectionPoint.AddFeeder(feeder);
            }

            // Primary substation
            if (connectionPoint != null &&
                conductingEquipment != null &&
                connectionPoint.Substation != null && 
                connectionPoint.Substation.PSRType == "PrimarySubstation" && 
                ((conductingEquipment.BaseVoltage < connectionPoint.Substation.GetPrimaryVoltageLevel(_cimContext)) || conductingEquipment.BaseVoltage < 20000)
                )
            {
                var feeder = new Feeder()
                {
                    ConnectionPoint = connectionPoint,
                    ConductingEquipment = conductingEquipment,
                    FeederType = FeederType.PrimarySubstation,
                    VoltageLevel = FeederVoltageLevel.MediumVoltage
                };

                connectionPoint.AddFeeder(feeder);
            }

            // Secondary substation
            if (connectionPoint != null &&
               conductingEquipment != null &&
               connectionPoint.Substation != null &&
               connectionPoint.Substation.PSRType == "SecondarySubstation" &&
               conductingEquipment.BaseVoltage < connectionPoint.Substation.GetPrimaryVoltageLevel(_cimContext))
            {
                var feeder = new Feeder()
                {
                    ConnectionPoint = connectionPoint,
                    ConductingEquipment = conductingEquipment,
                    FeederType = FeederType.SecondarySubstation,
                    VoltageLevel = FeederVoltageLevel.LowVoltage
                };

                connectionPoint.AddFeeder(feeder);
            }

            // Cable box (and t-junctions if treated as cable boxed)
            if (connectionPoint != null &&
                conductingEquipment != null &&
                connectionPoint.Substation != null &&
                (connectionPoint.Substation.PSRType == "CableBox" || (connectionPoint.Substation.PSRType == "T-Junction" && _treatTJunctionsAsCabinets)))
            {
                // We need to do a trace to figure out if it's a customer feeder
                var traceResult = conductingEquipment.Traverse(
                    _cimContext,
                    ci => (!ci.IsInsideSubstation(_cimContext) || (!_treatTJunctionsAsCabinets && ci.IsInsideSubstation(_cimContext) && ci.GetSubstation(_cimContext, true).PSRType == "T-Junction")),
                    cn => (!cn.IsInsideSubstation(_cimContext) || (!_treatTJunctionsAsCabinets && cn.IsInsideSubstation(_cimContext) && cn.GetSubstation(_cimContext, true).PSRType == "T-Junction")),
                    false
                ).ToList();

                // If we hit som energy consumers, it's a cablebox feeding a customer type of feeder
                if (traceResult.Count(io => io is EnergyConsumer) > 0)
                {
                    var feeder = new Feeder()
                    {
                        ConnectionPoint = connectionPoint,
                        ConductingEquipment = conductingEquipment,
                        FeederType = FeederType.CableBox,
                        VoltageLevel = FeederVoltageLevel.LowVoltage
                    };
                    connectionPoint.AddFeeder(feeder);
                }
            }
        }

        private void SubstationInternalPowerTransformerTrace()
        {
            // Trace all power transformers internal in the substation to enrich connectivity points
            foreach (var obj in _cimContext.GetAllObjects())
            {
                if (obj is PowerTransformer)
                {
                    var pt = obj as PowerTransformer;

                    if (!(pt.name != null && pt.name.ToLower().Contains("lokal")))
                    {
                        var traceResult = pt.Traverse(
                        _cimContext,
                        ce => ce.IsInsideSubstation(_cimContext) &&
                        !ce.IsOpen() &&
                        ce.BaseVoltage < pt.GetSubstation(_cimContext, false).GetPrimaryVoltageLevel(_cimContext),
                        null,
                        false
                     ).ToList();

                        foreach (var cimObj in traceResult)
                        {
                            if (cimObj is ConnectivityNode && _connectionPoints.ContainsKey((ConnectivityNode)cimObj))
                            {
                                var cp = _connectionPoints[(ConnectivityNode)cimObj];
                                cp.PowerTransformer = pt;
                            }
                        }
                    }
                }
            }
        }

        private void TraceAllFeeders()
        {
            foreach (var cp in _connectionPoints.Values)
            {
                ////////////////////////////////////////////////////////////////
                // Line and external network injection feeders 

                if (cp.Kind == ConnectionPointKind.Line || cp.Kind == ConnectionPointKind.ExternalNetworkInjection)
                {
                    foreach (var feeder in cp.Feeders)
                    {
                        HashSet<string> nodeTypesToPass = new HashSet<string>();

                        if (feeder.FeederType == FeederType.NetworkInjection)
                        {
                            // Pass both primary and secondary, because injection might be placed on secondary side in a primary substation, like Konstants's Anholt injection
                            nodeTypesToPass.Add("PrimarySubstation");
                            nodeTypesToPass.Add("SecondarySubstation");
                            nodeTypesToPass.Add("Tower");
                        }
                        else if (feeder.FeederType == FeederType.PrimarySubstation)
                        {
                            // Don't trace a line feeder, unless transformer feeds the connection point
                            if (feeder.ConnectionPoint.PowerTransformer == null)
                                break;

                            nodeTypesToPass.Add("SecondarySubstation");
                            nodeTypesToPass.Add("Tower");
                        }
                        else if (feeder.FeederType == FeederType.SecondarySubstation)
                        {
                            // Don't trace a line feeder, unless transformer feeds the connection point
                            if (feeder.ConnectionPoint.PowerTransformer == null)
                                break;

                            nodeTypesToPass.Add("CableBox");
                            nodeTypesToPass.Add("Tower");
                            nodeTypesToPass.Add("T-Junction");
                        }
                        else if (feeder.FeederType == FeederType.CableBox)
                        { 
                            // Pass t-junctions, unless they should be treated as cable boxes
                            if (!_treatTJunctionsAsCabinets)
                                nodeTypesToPass.Add("T-Junction");
                        }

                        // Regarding the trace:
                        // We need a node check on both conducting equipment and connectivity nodes, 
                        // because otherwise we risk running through the node (from one feeder to another)
                        // if feeder cables are connected directly to a cn/busbar inside the node.
                        // We include the power transformer in the trace, no matter if a base voltage is specified.
                        // We need the transformer, and sometimes the base voltage is not set. That's why.
                        var traceResult = feeder.ConductingEquipment.TraverseWithHopInfo(
                            _cimContext,
                            ce =>
                                (
                                    ce.BaseVoltage == feeder.ConductingEquipment.BaseVoltage
                                    ||
                                    ce is PowerTransformer // because power transformers sometimes has no base voltage
                                )
                                &&
                                !ce.IsOpen()
                                &&
                                (
                                    (ce.IsInsideSubstation(_cimContext) && nodeTypesToPass.Contains(ce.GetSubstation(_cimContext, true).PSRType))
                                    ||
                                    !ce.IsInsideSubstation(_cimContext)
                                ),
                            cn =>
                                (
                                    (cn.IsInsideSubstation(_cimContext) && nodeTypesToPass.Contains(cn.GetSubstation(_cimContext, true).PSRType))
                                    ||
                                    !cn.IsInsideSubstation(_cimContext)
                                )
                                ,
                            true
                            ).ToList();

                        int energyConsumerCount = 0;

                        bool customerCableFound = false;
                        Guid customerCableId = Guid.Empty;

                        int traversalOrder = 0;

                        // store index to busbars
                        Dictionary<string, IdentifiedObjectWithHopInfo> busbarHopsById = new Dictionary<string, IdentifiedObjectWithHopInfo>();

                        foreach (var cimObj in traceResult)
                        {
                            if (cimObj.IdentifiedObject is BusbarSection)
                            {
                                busbarHopsById.Add(cimObj.IdentifiedObject.mRID, cimObj);
                            }
                        }

                        HashSet<BusbarSection> busbarProcessed = new HashSet<BusbarSection>();

                        foreach (var cimObj in traceResult)
                        {
                            traversalOrder++;

                            if (cimObj.IdentifiedObject is EnergyConsumer)
                                energyConsumerCount++;

                            // If we hit a customer cable, save it for feeder info
                            if (cimObj.IdentifiedObject is ACLineSegment)
                            {
                                var acls = cimObj.IdentifiedObject as ACLineSegment;

                                if (acls.PSRType != null && acls.PSRType.ToLower().Contains("customer") && customerCableFound == false)
                                {
                                    customerCableFound = true;
                                    customerCableId = Guid.Parse(acls.mRID);
                                }
                                else if (acls.PSRType != null && acls.PSRType.ToLower().Contains("customer") && customerCableFound == true)
                                {
                                    // We keep the first id
                                }
                                else
                                {
                                    // reset the id (because we´re out of the customer part of the net)
                                    customerCableFound = false;
                                    customerCableId = Guid.Empty;
                                }
                            }

                            if (cimObj.IdentifiedObject is ConnectivityNode)
                            {
                                var connections = _cimContext.GetConnections((ConnectivityNode)cimObj.IdentifiedObject);
                                var busbar = connections.FirstOrDefault(c => c.ConductingEquipment is BusbarSection).ConductingEquipment;

                                if (busbar != null)
                                {
                                    int stationHop = 0;

                                    if (busbarHopsById.ContainsKey(busbar.mRID))
                                        stationHop = busbarHopsById[busbar.mRID].stationHop;

                                    busbarProcessed.Add((BusbarSection)busbar);

                                    AssignFeederToConductingEquipment(busbar, feeder, traversalOrder, stationHop, customerCableId);
                                }
                            }


                            if (cimObj.IdentifiedObject is ConductingEquipment)
                            {
                                var ce = cimObj.IdentifiedObject as ConductingEquipment;

                                if (ce is BusbarSection)
                                {
                                    if (!busbarProcessed.Contains(ce))
                                        AssignFeederToConductingEquipment(ce, feeder, traversalOrder, cimObj.stationHop, customerCableId);
                                }
                                else
                                    AssignFeederToConductingEquipment(ce, feeder, traversalOrder, cimObj.stationHop, customerCableId);

                                // If a busbar or powertransformer inside substation container add feeder to substation as well
                                if ((ce is BusbarSection || ce is PowerTransformer) && ce.IsInsideSubstation(_cimContext))
                                {
                                    var st = ce.GetSubstation(_cimContext, false);

                                    AssignFeederToSubstation(st, feeder);
                                }
                            }
                        }

                        if (feeder.ConnectionPoint.PowerTransformer != null)
                        {
                            feeder.ConnectionPoint.PowerTransformer.EnergyConsumerCount += energyConsumerCount;
                        }
                    }
                }

                ////////////////////////////////////////////////////////////////
                // power transformer feeders
                if (cp.Kind == ConnectionPointKind.PowerTranformer)
                {
                    foreach (var feeder in cp.Feeders)
                    {
                        var pt = feeder.ConductingEquipment as PowerTransformer;

                        var traceResult = pt.Traverse(
                           _cimContext,
                           ce =>
                           ce.IsInsideSubstation(_cimContext) &&
                           !ce.IsOpen() &&
                           ce.BaseVoltage < pt.GetSubstation(_cimContext, true).GetPrimaryVoltageLevel(_cimContext),
                           null,
                           true
                        ).ToList();

                        foreach (var cimObj in traceResult)
                        {
                            int traversalOrder = 0;

                            if (cimObj is ConductingEquipment)
                            {
                                traversalOrder++;

                                var ce = cimObj as ConductingEquipment;

                                // We don't want to add feeder to power transformers and ac line segments outsit station.
                                if (!(ce is PowerTransformer) && !(ce is ACLineSegment && !ce.IsInsideSubstation(_cimContext)))
                                    AssignFeederToConductingEquipment(ce, feeder, traversalOrder, 0, Guid.Empty);
                            }
                        }
                    }
                } 
            }
        }

        private void AssignFeederToConductingEquipment(ConductingEquipment ce, Feeder feeder, int traversalOrder, int substationHop, Guid customerCableId)
        {
            if (!_conductingEquipmentFeeders.ContainsKey(ce))
            {
                _conductingEquipmentFeeders[ce] = new ConductingEquipmentFeederInfo() { SubstationHop = substationHop, TraversalOrder=traversalOrder, FirstCustomerCableId = customerCableId };
                _conductingEquipmentFeeders[ce].Feeders.Add(feeder);
            }
            else
            {
                var ceFeeders = _conductingEquipmentFeeders[ce].Feeders;

                // Only add feeder if conducting equipment not already addede to a feeder attached to the same connectivity node
                if (ceFeeders.Count(f => f.ConnectionPoint == feeder.ConnectionPoint) == 0)
                {
                    if (!ceFeeders.Contains(feeder))
                        ceFeeders.Add(feeder);
                }
            }

            // Add to internal feeder list
            if (ce.InternalFeeders == null)
                ce.InternalFeeders = new List<Feeder>();

            // Only add feeder if conducting equipment not already added to a feeder attached to the same connectivity node
            if (ce.InternalFeeders.Count(f => f.ConnectionPoint == feeder.ConnectionPoint) == 0)
            {
                if (!ce.InternalFeeders.Contains(feeder))
                    ce.InternalFeeders.Add(feeder);
            }
        }

        private void AssignFeederToSubstation(Substation st, Feeder feeder)
        {
            // Add to internal feeder list
            if (st.InternalFeeders == null)
                st.InternalFeeders = new List<Feeder>();

            // Only add feeder if conducting equipment not already added to a feeder attached to the same connectivity node
            if (st.InternalFeeders.Count(f => f.ConnectionPoint == feeder.ConnectionPoint) == 0)
            {
                if (!st.InternalFeeders.Contains(feeder))
                    st.InternalFeeders.Add(feeder);
            }
        }

        private void FixTJunctionCustomers()
        {
            foreach (var cif in _conductingEquipmentFeeders)
            {
                if (cif.Key is EnergyConsumer)
                {
                    var ec = cif.Key as EnergyConsumer;
                    var conductingfeederInfo = cif.Value;

                    var cableBoxFeeders = conductingfeederInfo.Feeders.FindAll(f => f.FeederType == FeederType.CableBox);


                    // If count is 1 then the feeder is fine.
                    // If count is > 2 something rottet, and we should not mess it up any further
                    if (cableBoxFeeders.Count == 2)
                    {
                        // Trace customer
                        HashSet<string> nodeTypesToPass = new HashSet<string>();
                        nodeTypesToPass.Add("T-Junction");
                        nodeTypesToPass.Add("Tower");
                        nodeTypesToPass.Add("CableBox");

                        var traceResult = ec.Traverse(
                            _cimContext,
                       ce =>
                           ce.BaseVoltage == ec.BaseVoltage
                           &&
                           !ce.IsOpen()
                           &&
                           (
                               (ce.IsInsideSubstation(_cimContext) && nodeTypesToPass.Contains(ce.GetSubstation(_cimContext, true).PSRType))
                               ||
                               !ce.IsInsideSubstation(_cimContext)
                           ),
                       cn =>
                           (
                               (cn.IsInsideSubstation(_cimContext) && nodeTypesToPass.Contains(cn.GetSubstation(_cimContext, true).PSRType))
                               ||
                               !cn.IsInsideSubstation(_cimContext)
                           )
                           ,
                       true
                       ).ToList();

                        // we want to searh for substation backwards
                        traceResult.Reverse();

                        Substation cableBoxToKeep = null;

                        bool startToLookForCabinet = false;

                        foreach (var traceObj in traceResult)
                        {
                            if (traceObj.IsInsideSubstation(_cimContext) && traceObj.GetSubstation(_cimContext, true).PSRType == "SecondarySubstation")
                                startToLookForCabinet = true;

                            if (startToLookForCabinet && traceObj.IsInsideSubstation(_cimContext) && traceObj.GetSubstation(_cimContext, true).PSRType == "CableBox")
                            {
                                var cableBox = traceObj.GetSubstation(_cimContext, true);

                                if (cableBoxFeeders.Exists(o => o.ConnectionPoint.Substation == cableBox))
                                {
                                    if (traceObj is Switch && ((Switch)traceObj).normalOpen != true)
                                    {
                                        if (cableBoxToKeep == null)
                                            cableBoxToKeep = cableBox;
                                    }
                                }
                            }
                        }

                        if (cableBoxToKeep != null)
                        {
                            List<Feeder> feedersToRemove = new List<Feeder>();
                            // Remove all cable box feeders but this one
                            foreach (var feeder in conductingfeederInfo.Feeders)
                            {
                                if (feeder.FeederType == FeederType.CableBox && feeder.ConnectionPoint.Substation != cableBoxToKeep)
                                    feedersToRemove.Add(feeder);
                            }

                            foreach (var feederToRemove in feedersToRemove)
                                conductingfeederInfo.Feeders.Remove(feederToRemove);
                        }

                    }
                }
            }
        }
    }
}
