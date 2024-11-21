using DAX.NetworkModel.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM.Processing
{
    public class TopologyProcessingResult : ITopologyProcessingResult
    {
        // Dictionaries holding DAX objects that are created as part of topology processing
        private CIMGraph _g;
        public List<DAXElectricFeeder> _daxFeeders = new List<DAXElectricFeeder>();
        public Dictionary<CIMIdentifiedObject, DAXElectricNode> _daxNodeByCimObj = new Dictionary<CIMIdentifiedObject, DAXElectricNode>();
        public Dictionary<CIMIdentifiedObject, DAXElectricTransformer> _daxTransformerByCimObj = new Dictionary<CIMIdentifiedObject, DAXElectricTransformer>();
        public Dictionary<CIMConductingEquipment, DAXTopologyInfo> _daxTopoInfoByCimObj = new Dictionary<CIMConductingEquipment, DAXTopologyInfo>();
        public Dictionary<CIMIdentifiedObject, List<DAXElectricFeeder>> _daxFeedersByCimObj = new Dictionary<CIMIdentifiedObject, List<DAXElectricFeeder>>();


        public TopologyProcessingResult(CIMGraph graph)
        {
            _g = graph;
        }

        public List<DAXElectricNode> DAXNodes
        {
            get { return _daxNodeByCimObj.Values.ToList(); }
        }

        public List<DAXElectricFeeder> DAXFeeders
        {
            get { return _daxFeeders; }
        }
        
        public DAXElectricNode GetDAXNodeByExternalId(string externalId)
        {
            if (_g.GetCIMObjectByExternalId(externalId) != null)
                return GetDAXNodeByCIMObject(_g.GetCIMObjectByExternalId(externalId));
            else
                return null;
        }

        public DAXElectricNode GetDAXNodeByName(string name)
        {
            if (_g.GetCIMObjectByName(name) != null)
                return GetDAXNodeByCIMObject(_g.GetCIMObjectByName(name));
            else
                return null;
        }

        public void InitialTraceAllFeeders()
        {
            foreach (var feeder in DAXFeeders)
            {
                if (!feeder.IsTransformerFeeder && feeder.Name == "03HATT: 30244")
                {
                }

                CIMIdentifiedObject startObject = _g.ObjectManager.GetCIMObjectById(feeder.DownstreamCIMObjectId);
                TraceFeeder(startObject, feeder);

                
            }

            // Trace installations connected directly to tranformers
            foreach (var feeder in DAXFeeders)
            {
                if (feeder.IsTransformerFeeder && feeder.Transformer != null)
                {
                    //CIMIdentifiedObject startObject = _g.ObjectManager.GetCIMObjectById(feeder.DownstreamCIMObjectId);

                    Queue<CIMIdentifiedObject> traverseOrder = StationTransformerBFSTraceOutsideStation(feeder);

                    foreach (var cimObj in traverseOrder)
                    {
                        if (cimObj.ClassType == CIMClassEnum.EnergyConsumer)
                        {
                            if (_daxNodeByCimObj.ContainsKey(cimObj))
                            {
                                var daxNode = _daxNodeByCimObj[cimObj];

                                if (daxNode.Sources == null || daxNode.Sources.Length == 0)
                                {
                                    // Has no feeder
                                    daxNode.AddSource(new DAXElectricNodeSource() { Feeder = feeder, UpstreamCIMObjectId = cimObj.InternalId });

                                    // Add to feeder info
                                    if (!_daxFeedersByCimObj.ContainsKey(cimObj))
                                        _daxFeedersByCimObj[cimObj] = new List<DAXElectricFeeder> { feeder };
                                    else
                                        _daxFeedersByCimObj[cimObj].Add(feeder);
                                }

                            }

                        }
                    }

                }
            }

        }

        private void TraceFeeder(CIMIdentifiedObject root, DAXElectricFeeder feeder)
        {

            // Trace feeder
            if (!feeder.IsTransformerFeeder)
            {

                if (CheckIFeederShouldBeTraced(feeder))
                {

                    if (feeder.Name == "101: Lavsp.tavle LEC, Bytoften 1, Inst. 155085")
                    {
                    }

                    Queue<CIMIdentifiedObject> traverseOrder = StationFeederDFSTrace(feeder);

                    while (traverseOrder.Count > 0)
                    {
                        CIMIdentifiedObject p = traverseOrder.Dequeue();

                        var nodeContainer = p.GetEquipmentContainerRoot();


                        // Add Feeder Info
                        if (p is CIMConductingEquipment)
                        {
                            var ci = p as CIMConductingEquipment;
                            if (!_daxFeedersByCimObj.ContainsKey(ci))
                                _daxFeedersByCimObj[ci] = new List<DAXElectricFeeder> { feeder };
                            else
                                _daxFeedersByCimObj[ci].Add(feeder);
                        }


                        // If we hit energy consumer, add feeder to its sources
                        if (p.ClassType == CIMClassEnum.EnergyConsumer)
                            _daxNodeByCimObj[p].AddSource(new DAXElectricNodeSource() { Feeder = feeder, UpstreamCIMObjectId = p.InternalId });
                        // If we hit power transformer add feeder to its sources
                        else if (p.ClassType == CIMClassEnum.PowerTransformer && _daxTransformerByCimObj.ContainsKey(p))
                            _daxTransformerByCimObj[p].AddSource(feeder);
                        // If a substation or enclosure, add feeder to its sources
                        else if (p.ClassType == CIMClassEnum.ConnectivityNode
                            && nodeContainer != null
                            && (nodeContainer.ClassType == CIMClassEnum.Substation || nodeContainer.ClassType == CIMClassEnum.Enclosure))
                        {
                            if (!(nodeContainer.InternalId == feeder.Node.CIMObjectId)
                                && nodeContainer.VoltageLevel < 60000
                                && nodeContainer.VoltageLevel != feeder.Node.VoltageLevel)
                                _daxNodeByCimObj[nodeContainer].AddSource(new DAXElectricNodeSource() { Feeder = feeder, UpstreamCIMObjectId = p.InternalId });
                        }
                    }
                }
            }
            // Transformer trace
            else
            {

                var nodeCimObj = feeder.Node.CIMObject;

                Queue<CIMIdentifiedObject> traverseOrder = StationTransformerBFSTraceInsideStation(feeder);

                while (traverseOrder.Count > 0)
                {
                    CIMIdentifiedObject p = traverseOrder.Dequeue();

                    // If transformer feeder and we are inside the substation container the transformer we're tracing
                    if (p.EquipmentContainerRef != null
                        && p.EquipmentContainerRef.EquipmentContainerRef != null
                        && p.EquipmentContainerRef.EquipmentContainerRef.ClassType == CIMClassEnum.Substation
                        && p.EquipmentContainerRef.EquipmentContainerRef == nodeCimObj)
                    {
                        var daxNode = _daxNodeByCimObj[p.EquipmentContainerRef.EquipmentContainerRef];

                        // Find feeder connected to transformer
                        if (daxNode.Feeders != null)
                        {
                            foreach (var daxNodeFeeder in daxNode.Feeders)
                            {
                                if (daxNodeFeeder.Bay != null && daxNodeFeeder.Bay == p.EquipmentContainerRef)
                                    daxNodeFeeder.Transformer = feeder.Transformer;
                            }
                        }
                    }
                }
            }
        }

        public void AddDAXNodeByCIMObject(CIMIdentifiedObject cimObj, DAXElectricNode daxNode)
        {
            _daxNodeByCimObj.Add(cimObj, daxNode);
        }

        public DAXElectricNode GetDAXNodeByCIMObject(CIMIdentifiedObject cimObj, bool trace = true)
        {
            if (_daxNodeByCimObj.ContainsKey(cimObj))
            {
                var daxNode = _daxNodeByCimObj[cimObj];

                if (trace)
                {
                    // Skabe
                    if (daxNode != null && daxNode.ClassType == CIMClassEnum.Enclosure && daxNode.Feeders == null)
                    {
                        EnclosureTrace(daxNode);
                    }

                    // Stationer
                    if (daxNode != null && daxNode.ClassType == CIMClassEnum.Substation && daxNode.Feeders != null)
                    {
                        foreach (var feeder in daxNode.Feeders)
                        {
                            feeder.Trace = TraceSubstationFeeder(feeder);
                        }
                    }

                    // Sources (både skabe og stationer)
                    if (daxNode != null && daxNode.Sources != null)
                    {
                        foreach (var source in daxNode.Sources)
                        {
                            source.Trace = TraceSource(daxNode, source);
                        }
                    }
                }


                return daxNode;
            }

            return null;
        }

        public List<DAXElectricFeeder> GetDAXFeedersByCIMObject(CIMIdentifiedObject cimObj)
        {
            if (_daxFeedersByCimObj.ContainsKey(cimObj))
            {
                return _daxFeedersByCimObj[cimObj];
            }

            return null;
        }

        public DAXTopologyInfo GetDAXTopologyInfoByCIMObject(CIMConductingEquipment cimObj)
        {
            if (_daxTopoInfoByCimObj.ContainsKey(cimObj))
                return _daxTopoInfoByCimObj[cimObj];
            else
                return null;
        }

        public bool FeederTrace(DAXElectricFeeder feeder)
        {
            feeder.Trace = TraceSubstationFeeder(feeder);
            return true;
        }

        private List<DAXTraceItem> TraceSubstationFeeder(DAXElectricFeeder feeder)
        {
            List<DAXTraceItem> result = new List<DAXTraceItem>();

            if (CheckIFeederShouldBeTraced(feeder))
            {
                Queue<TraceHopInfo> traverseOrder = StationFeederDFSTraceWithHopInfo(feeder);

                HashSet<string> alreadyAdded = new HashSet<string>();

                // Add cables, substations and enclosures to result
                while (traverseOrder.Count > 0)
                {
                    TraceHopInfo hi = traverseOrder.Dequeue();
                    var cimObj = hi.CIMObject;

                    if (cimObj.ClassType == CIMClassEnum.EnergyConsumer || cimObj.ClassType == CIMClassEnum.ACLineSegment)
                    {
                        var ti = CreateTraceItem(cimObj);
                        ti.BranchInfo = hi.BranchingInfo;
                        result.Add(ti);
                    }
                    else if (cimObj.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.EquipmentContainerRef != null)
                    {
                        if (!alreadyAdded.Contains(cimObj.EquipmentContainerRef.EquipmentContainerRef.ExternalId))
                        {
                            var ti = CreateTraceItem(cimObj.EquipmentContainerRef.EquipmentContainerRef);
                            ti.BranchInfo = hi.BranchingInfo;
                            result.Add(ti);

                            alreadyAdded.Add(cimObj.EquipmentContainerRef.EquipmentContainerRef.ExternalId);
                        }
                    }
                }
            }


            return result;
        }

        private List<DAXTraceItem> TraceSource(DAXElectricNode node, DAXElectricNodeSource source)
        {
            List<DAXTraceItem> result = new List<DAXTraceItem>();

            CIMIdentifiedObject fromCimObj = null;
            CIMIdentifiedObject toCimObj = null;

            if (node.ClassType == CIMClassEnum.EnergyConsumer)
            {
                fromCimObj = _g.ObjectManager.GetCIMObjectById(source.UpstreamCIMObjectId);
                toCimObj = _g.ObjectManager.GetCIMObjectById(source.Feeder.DownstreamCIMObjectId);

                if (fromCimObj != null && toCimObj != null)
                {
                    var trace = _g.ShortestPath(_g.ObjectManager.AdditionalObjectAttributes(fromCimObj).Vertex1Id, _g.ObjectManager.AdditionalObjectAttributes(toCimObj).Vertex1Id);

                    AddToUpstreamTrace(trace, ref result);

                    if (source.Feeder.Node != null && source.Feeder.Node.Sources != null && source.Feeder.Node.Sources.Length > 0 && source.Feeder.Node.Transformers != null)
                    {
                        fromCimObj = null;
                        toCimObj = null;

                        if (source.Feeder.Transformer != null && source.Feeder.Transformer.Sources != null && source.Feeder.Transformer.Sources.Length > 0)
                        {
                            fromCimObj = _g.ObjectManager.GetCIMObjectById(source.Feeder.Transformer.FeederObjectId);

                            toCimObj = _g.ObjectManager.GetCIMObjectById(source.Feeder.Transformer.Sources[0].DownstreamCIMObjectId);
                        }

                        if (fromCimObj != null && toCimObj != null)
                        {
                            var trace2 = _g.ShortestPath(_g.ObjectManager.AdditionalObjectAttributes(fromCimObj).Vertex1Id, _g.ObjectManager.AdditionalObjectAttributes(toCimObj).Vertex1Id);
                            AddToUpstreamTrace(trace2, ref result);
                        }
                    }
                }
            }
            else if (node.ClassType == CIMClassEnum.Substation || node.ClassType == CIMClassEnum.Enclosure)
            {
                fromCimObj = _g.ObjectManager.GetCIMObjectById(source.UpstreamCIMObjectId);
                toCimObj = _g.ObjectManager.GetCIMObjectById(source.Feeder.DownstreamCIMObjectId);

                if (fromCimObj != null && toCimObj != null)
                {
                    var trace2 = _g.ShortestPath(_g.ObjectManager.AdditionalObjectAttributes(fromCimObj).Vertex1Id, _g.ObjectManager.AdditionalObjectAttributes(toCimObj).Vertex1Id);
                    AddToUpstreamTrace(trace2, ref result);
                }
            }

            if (result.Count == 0)
                return null;

            return result;

        }

        private void AddToUpstreamTrace(IList<CIMIdentifiedObject> trace, ref List<DAXTraceItem> result)
        {
            HashSet<string> alreadyAdded = new HashSet<string>();

            foreach (var cimObj in trace)
            {
                if (cimObj.ClassType == CIMClassEnum.EnergyConsumer)
                    result.Add(CreateTraceItem(cimObj));
                if (cimObj.ClassType == CIMClassEnum.ACLineSegment)
                    result.Add(CreateTraceItem(cimObj));
                else if (cimObj.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.EquipmentContainerRef != null)
                {
                    if (!alreadyAdded.Contains(cimObj.EquipmentContainerRef.EquipmentContainerRef.ExternalId))
                    {
                        result.Add(CreateTraceItem(cimObj.EquipmentContainerRef.EquipmentContainerRef));
                        alreadyAdded.Add(cimObj.EquipmentContainerRef.EquipmentContainerRef.ExternalId);
                    }
                }
            }

        }

        private DAXTraceItem CreateTraceItem(CIMIdentifiedObject obj)
        {
            if (obj.ClassType == CIMClassEnum.ACLineSegment)
            {
                string name = obj.Name;
                if (name == null || name == "")
                    name = obj.GetPropertyValueAsString("cim.asset.name");

                if (name != null)
                    name = name.Trim();

                string typeb = obj.GetPropertyValueAsString("cim.asset.name");

                if (typeb != null)
                    typeb = typeb.Trim();


                return new DAXTraceItem() { CIMObjectId = obj.InternalId, NodeId = obj.ExternalId, ClassType = obj.ClassType, Name = name, Type = typeb, Details = obj.Description, Coords = obj.Coords, VoltageLevel = obj.VoltageLevel, Length = ((CIMConductingEquipment)obj).Length() };
            }
            else if (obj.ClassType == CIMClassEnum.BusbarSection)
            {
                string name = "";

                string typeb = obj.GetPropertyValueAsString("cim.asset.name");

                double[] coords = null;

                if (obj.EquipmentContainerRef != null && obj.EquipmentContainerRef.ClassType == CIMClassEnum.Enclosure)
                {
                    name = "Skab " + obj.EquipmentContainerRef.Name;
                    coords = obj.EquipmentContainerRef.Coords;
                }
                else if (obj.EquipmentContainerRef != null && obj.EquipmentContainerRef.ClassType == CIMClassEnum.Substation)
                {
                    name = "St. " + obj.EquipmentContainerRef.Name;
                    coords = obj.EquipmentContainerRef.Coords;
                }

                return new DAXTraceItem() { CIMObjectId = obj.InternalId, NodeId = obj.ExternalId, ClassType = obj.ClassType, Name = name, Details = obj.Description, Coords = coords, VoltageLevel = obj.VoltageLevel };
            }
            else if (obj.ClassType == CIMClassEnum.EnergyConsumer)
            {
                string name = "Uknown";

                if (obj.Name != null && obj.Name.Length == 18)
                    name = Convert.ToString(Convert.ToInt32(obj.Name.Substring(10, obj.Name.Length - 11)));

                return new DAXTraceItem() { CIMObjectId = obj.InternalId, NodeId = obj.ExternalId, ClassType = obj.ClassType, Name = name, Details = obj.Description, Coords = obj.Coords, VoltageLevel = obj.VoltageLevel };
            }
            else if (obj.ClassType == CIMClassEnum.Substation)
            {
                return new DAXTraceItem() { CIMObjectId = obj.InternalId, NodeId = obj.ExternalId, ClassType = obj.ClassType, Name = obj.Name, Details = obj.Description, Coords = obj.Coords, VoltageLevel = obj.VoltageLevel };
            }
            else if (obj.ClassType == CIMClassEnum.Enclosure)
            {
                return new DAXTraceItem() { CIMObjectId = obj.InternalId, NodeId = obj.ExternalId, ClassType = obj.ClassType, Name = obj.Name, Details = obj.Description, Coords = obj.Coords, VoltageLevel = obj.VoltageLevel };
            }

            return null;
        }

        // Station transformer trace
        public Queue<CIMIdentifiedObject> StationTransformerBFSTraceInsideStation(DAXElectricFeeder feeder)
        {
            CIMIdentifiedObject root = _g.ObjectManager.GetCIMObjectById(feeder.DownstreamCIMObjectId);

            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Queue<CIMIdentifiedObject> Q = new Queue<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> S = new HashSet<CIMIdentifiedObject>();
            Q.Enqueue(root);
            S.Add(root);

            while (Q.Count > 0)
            {
                CIMIdentifiedObject p = Q.Dequeue();
                traverseOrder.Enqueue(p);

                foreach (CIMIdentifiedObject cimObj in p.GetNeighbours())
                {
                    if (!S.Contains(cimObj))
                    {
                        S.Add(cimObj);

                        if (InsideStationTransformerTraceShouldWeEnqueueObject(feeder, cimObj) && !NormalOpen(cimObj))
                            Q.Enqueue(cimObj);
                        //else traverseOrder.Enqueue(cimObj);
                    }
                }
            }

            return traverseOrder;
        }

        // Station transformer trace
        public Queue<CIMIdentifiedObject> StationTransformerBFSTraceOutsideStation(DAXElectricFeeder feeder)
        {
            CIMIdentifiedObject root = feeder.Transformer.CIMObject;

            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Queue<CIMIdentifiedObject> Q = new Queue<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> S = new HashSet<CIMIdentifiedObject>();
            Q.Enqueue(root);
            S.Add(root);

            while (Q.Count > 0)
            {
                CIMIdentifiedObject p = Q.Dequeue();
                traverseOrder.Enqueue(p);

                foreach (CIMIdentifiedObject cimObj in p.GetNeighbours())
                {
                    if (!S.Contains(cimObj))
                    {
                        S.Add(cimObj);

                        if (OutsideStationTransformerTraceShouldWeEnqueueObject(feeder, cimObj) && !NormalOpen(cimObj))
                            Q.Enqueue(cimObj);
                        //else traverseOrder.Enqueue(cimObj);
                    }
                }
            }

            return traverseOrder;
        }

        private bool InsideStationTransformerTraceShouldWeEnqueueObject(DAXElectricFeeder feeder, CIMIdentifiedObject cimObj)
        {
            bool enqueue = true;

            var node = cimObj.GetEquipmentContainerRoot();

            // if we hit object with parent node <> feeder station don't enqueue
            if (node != null && node != feeder.Node.CIMObject)
                enqueue = false;

            // if we hit a ac line segment object with no parent don't enqueue
            if (cimObj.ClassType == CIMClassEnum.ACLineSegment && node == null)
                enqueue = false;

            // if we hit a peterson coil don't enqueue
            if (cimObj.ClassType == CIMClassEnum.PetersenCoil)
                enqueue = false;

            // if we hit busbar with feeder voltage, don't enque (to prevent tracing on primary side)
            if (cimObj.ClassType == CIMClassEnum.BusbarSection &&
                cimObj.VoltageLevel == feeder.VoltageLevel)
                enqueue = false;

            // if we hit a CN connected to a busbar with feeder voltage, don't enque (to prevent tracing on primary side)
            if (cimObj.ClassType == CIMClassEnum.ConnectivityNode)
            {
                var cnNeighbors = cimObj.GetNeighbours(CIMClassEnum.BusbarSection);

                if (cnNeighbors != null && cnNeighbors.Count == 1)
                {
                    if (cnNeighbors[0].VoltageLevel == feeder.VoltageLevel)
                        enqueue = false;
                }
            }
            
            /*

            // If we hit entry feeder don't enqueue
            if (_g.ObjectManager.AdditionalObjectAttributes(cimObj).IsFeederEntryObject)
                enqueue = false;

            // If we hit AC line segment outside substation don't enqueue
            if (cimObj.ClassType == CIMClassEnum.ACLineSegment && cimObj.EquipmentContainerRef == null)
                enqueue = false;

             * 
            */ 


            return enqueue;
        }

        private bool OutsideStationTransformerTraceShouldWeEnqueueObject(DAXElectricFeeder feeder, CIMIdentifiedObject cimObj)
        {
            bool enqueue = true;

            var node = cimObj.GetEquipmentContainerRoot();

            
            // if we hit a peterson coil don't enqueue
            if (cimObj.ClassType == CIMClassEnum.PetersenCoil)
                enqueue = false;

            // if we hit busbar with feeder voltage, don't enque (to prevent tracing on primary side)
            if (cimObj.ClassType == CIMClassEnum.BusbarSection &&
                cimObj.VoltageLevel == feeder.VoltageLevel)
                enqueue = false;

            // if we hit a CN connected to a busbar with feeder voltage, don't enque (to prevent tracing on primary side)
            if (cimObj.ClassType == CIMClassEnum.ConnectivityNode)
            {
                var cnNeighbors = cimObj.GetNeighbours(CIMClassEnum.BusbarSection);

                if (cnNeighbors != null && cnNeighbors.Count == 1)
                {
                    if (cnNeighbors[0].VoltageLevel == feeder.VoltageLevel)
                        enqueue = false;
                }
            }

            // if we hit voltage greater that tf feeder
            if (cimObj.VoltageLevel >= feeder.VoltageLevel)
                enqueue = false;

            // If we hit annother substation
            if (cimObj.EquipmentContainerRef != null && // Bay
                cimObj.EquipmentContainerRef.EquipmentContainerRef != null && // Substation
                cimObj.EquipmentContainerRef.EquipmentContainerRef.ClassType == CIMClassEnum.Substation &&
                cimObj.EquipmentContainerRef.EquipmentContainerRef != feeder.Node.CIMObject)
            {
                enqueue = false;
            }


            return enqueue;
        }

        // Station feeder BFS trace
        public Queue<CIMIdentifiedObject> StationFeederBFSTrace(DAXElectricFeeder feeder)
        {
            CIMIdentifiedObject root = _g.ObjectManager.GetCIMObjectById(feeder.DownstreamCIMObjectId);

            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Queue<CIMIdentifiedObject> Q = new Queue<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> S = new HashSet<CIMIdentifiedObject>();
            Q.Enqueue(root);
            S.Add(root);

            while (Q.Count > 0)
            {
                CIMIdentifiedObject p = Q.Dequeue();
                traverseOrder.Enqueue(p);

                foreach (CIMIdentifiedObject cimObj in p.GetNeighbours())
                {
                    if (!S.Contains(cimObj))
                    {
                        S.Add(cimObj);

                        if (StationFeederTraceShouldWeEnqueueObject(feeder, cimObj) && !NormalOpen(cimObj))
                            Q.Enqueue(cimObj);
                        else
                            traverseOrder.Enqueue(cimObj);
                    }
                }
            }

            return traverseOrder;
        }

        // Station feeder DFS trace
        private Queue<CIMIdentifiedObject> StationFeederDFSTrace(DAXElectricFeeder feeder)
        {
            //System.Diagnostics.Debug.WriteLine("TRACING FEEDER: " + feeder.Node.Name + " " + feeder.Name);

          

            CIMIdentifiedObject root = _g.ObjectManager.GetCIMObjectById(feeder.DownstreamCIMObjectId);

            // Topology variables
            Dictionary<CIMIdentifiedObject, CIMIdentifiedObject> parentDict = new Dictionary<CIMIdentifiedObject, CIMIdentifiedObject>();

            // Trace variables
            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Stack<CIMIdentifiedObject> stack = new Stack<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> visited = new HashSet<CIMIdentifiedObject>();
            stack.Push(root);
            visited.Add(root);

            int level = 1;

            while (stack.Count > 0)
            {
                CIMIdentifiedObject p = stack.Pop();
                level = level - 1;

                traverseOrder.Enqueue(p);

                var neighbours = p.Neighbours;

                foreach (CIMIdentifiedObject n in neighbours)
                {
                    if (!visited.Contains(n))
                    {
                        visited.Add(n);

                        if (StationFeederTraceShouldWeEnqueueObject(feeder, n) && !NormalOpen(n))
                        {
                            stack.Push(n);

                            level = level + 1;

                         

                            //System.Diagnostics.Debug.WriteLine("P: " + p.ToString());
                            //System.Diagnostics.Debug.WriteLine("O: " + n.ToString());
                            //System.Diagnostics.Debug.WriteLine("");
                        }
                        else
                        {
                            traverseOrder.Enqueue(n);
                        }

                        // Add to parent list
                        if (!parentDict.ContainsKey(n))
                            parentDict[n] = p;
                    }
                }
            }

            ProcessTopologyParentInfo(feeder, traverseOrder, parentDict);

            return traverseOrder;
        }

        private void ProcessTopologyParentInfo(DAXElectricFeeder feeder, Queue<CIMIdentifiedObject> traverseOrder, Dictionary<CIMIdentifiedObject, CIMIdentifiedObject> parentDict)
        {
            CIMIdentifiedObject feederStartObject = null;

            CIMIdentifiedObject lastObject = null;

            foreach (var cimObj in traverseOrder)
            {
                if (InsideFeederBay(feeder, cimObj))
                {
                    // Try find feeder start object (breaker, loadbreaker, disconnetor or fuse) inside feeder bay
                    if (feederStartObject == null)
                    {
                        feederStartObject = GetFeederStartObject(cimObj.EquipmentContainerRef);

                        // If feeder start objec found, set parrent to transformer
                        if (feederStartObject != null)
                        {
                            lastObject = feederStartObject;

                            if (feeder.Transformer != null)
                            {
                                DAXTopologyInfo dti = new DAXTopologyInfo() { IsStartOfFeeder = true };
                                dti.theFeeder = feeder;

                                AddChildToTopoInfo((CIMConductingEquipment)feeder.Transformer.CIMObject, (CIMConductingEquipment)feederStartObject);
                                dti.AddParent((CIMConductingEquipment)feeder.Transformer.CIMObject);

                                if (!_daxTopoInfoByCimObj.ContainsKey((CIMConductingEquipment)feederStartObject))
                                    _daxTopoInfoByCimObj.Add((CIMConductingEquipment)feederStartObject, dti);
                            }
                        }
                    }
                }
                else
                {
                    if (cimObj.ClassType == CIMClassEnum.ACLineSegment && cimObj.VoltageLevel > (feeder.VoltageLevel - 6000))
                    {
                        var parent = GetUseFullParent(parentDict, cimObj);

                        DAXTopologyInfo dti = new DAXTopologyInfo();
                        dti.theFeeder = feeder;

                        if (lastObject != null && lastObject == feederStartObject)
                        {
                            AddChildToTopoInfo((CIMConductingEquipment)lastObject, (CIMConductingEquipment)cimObj);
                            dti.AddParent((CIMConductingEquipment)lastObject);
                        }
                        else
                        {
                            AddChildToTopoInfo((CIMConductingEquipment)parent, (CIMConductingEquipment)cimObj);
                            dti.AddParent((CIMConductingEquipment)parent);
                        }

                        if (!_daxTopoInfoByCimObj.ContainsKey((CIMConductingEquipment)cimObj))
                            _daxTopoInfoByCimObj.Add((CIMConductingEquipment)cimObj, dti);

                        lastObject = cimObj;
                    }
                    else if (cimObj.ClassType == CIMClassEnum.ConnectivityNode)
                    {
                        var busBar = GetBusBar((CIMConnectivityNode)cimObj);

                        if (busBar != null)
                        {
                            var parent = GetUseFullParent(parentDict, cimObj);

                            DAXTopologyInfo dti = new DAXTopologyInfo();
                            dti.theFeeder = feeder;

                            AddChildToTopoInfo((CIMConductingEquipment)parent, (CIMConductingEquipment)busBar);
                            dti.AddParent((CIMConductingEquipment)parent);

                            if (!_daxTopoInfoByCimObj.ContainsKey((CIMConductingEquipment)busBar))
                            _daxTopoInfoByCimObj.Add((CIMConductingEquipment)busBar, dti);

                            lastObject = busBar;
                        }
                    }
                    else if (cimObj.ClassType == CIMClassEnum.PowerTransformer ||
                        cimObj.ClassType == CIMClassEnum.EnergyConsumer ||
                        cimObj.ClassType == CIMClassEnum.Breaker || 
                        cimObj.ClassType == CIMClassEnum.LoadBreakSwitch || 
                        cimObj.ClassType == CIMClassEnum.Disconnector || 
                        cimObj.ClassType == CIMClassEnum.Fuse)
                    {
                        var parent = GetUseFullParent(parentDict, cimObj);

                        DAXTopologyInfo dti = new DAXTopologyInfo();
                        dti.theFeeder = feeder;

                        AddChildToTopoInfo((CIMConductingEquipment)parent, (CIMConductingEquipment)cimObj);
                        dti.AddParent((CIMConductingEquipment)parent);

                        if (!_daxTopoInfoByCimObj.ContainsKey((CIMConductingEquipment)cimObj))
                            _daxTopoInfoByCimObj.Add((CIMConductingEquipment)cimObj, dti);

                        lastObject = cimObj;
                    }
                }
            }
        }

        private void AddChildToTopoInfo(CIMConductingEquipment eq, CIMConductingEquipment child)
        {
            if (eq != null && _daxTopoInfoByCimObj.ContainsKey(eq))
            {
                var topoInfo = _daxTopoInfoByCimObj[eq];
                topoInfo.AddChild(child);
            }
        }

        private CIMIdentifiedObject GetUseFullParent(Dictionary<CIMIdentifiedObject, CIMIdentifiedObject> parentDict, CIMIdentifiedObject cimObj)
        {
            if (parentDict.ContainsKey(cimObj))
            {
                var parent = parentDict[cimObj];

                while (parent != null)
                {
                    // AC line segments
                    if (parent.ClassType == CIMClassEnum.ACLineSegment)
                        return parent;

                    // Busbar sections 
                    if (parent.ClassType == CIMClassEnum.ConnectivityNode)
                    {
                        var busBar = GetBusBar((CIMConnectivityNode)parent);

                         if (busBar != null)
                             return busBar;
                    }

                    // Power transformer
                    if (parent.ClassType == CIMClassEnum.PowerTransformer)
                        return parent;

                    // Switches
                    if (parent.ClassType == CIMClassEnum.Breaker || 
                        parent.ClassType == CIMClassEnum.LoadBreakSwitch || 
                        parent.ClassType == CIMClassEnum.Disconnector || 
                        parent.ClassType == CIMClassEnum.Fuse)
                        return parent;


                    if (parentDict.ContainsKey(parent))
                        parent = parentDict[parent];
                    else
                        parent = null;
                }
            }

            return null;
        }

        private bool InsideFeederBay(DAXElectricFeeder feeder, CIMIdentifiedObject cimObj)
        {
            if (cimObj.EquipmentContainerRef != null && cimObj.EquipmentContainerRef == feeder.Bay)
                return true;
            else
                return false;
        }

        private CIMConductingEquipment GetBusBar(CIMConnectivityNode cn)
        {
            foreach (var n in cn.Neighbours)
            {
                if (n.ClassType == CIMClassEnum.BusbarSection)
                    return (CIMConductingEquipment)n;
            }
            return null;
        }

        private CIMIdentifiedObject GetFeederStartObject(CIMEquipmentContainer bay)
        {
            CIMIdentifiedObject startObj = null;

            if (bay != null)
            {
                startObj = bay.GetFirstChild(CIMClassEnum.Breaker);

                if (startObj == null)
                    startObj = bay.GetFirstChild(CIMClassEnum.Breaker);

                if (startObj == null)
                    startObj = bay.GetFirstChild(CIMClassEnum.LoadBreakSwitch);

                if (startObj == null)
                    startObj = bay.GetFirstChild(CIMClassEnum.Fuse);

                if (startObj == null)
                    startObj = bay.GetFirstChild(CIMClassEnum.Disconnector);
            }

            return startObj;
        }

        // Station feeder DFS trace
        private Queue<TraceHopInfo> StationFeederDFSTraceWithHopInfo(DAXElectricFeeder feeder)
        {
            CIMIdentifiedObject root = _g.ObjectManager.GetCIMObjectById(feeder.DownstreamCIMObjectId);

            Queue<TraceHopInfo> traverseOrder = new Queue<TraceHopInfo>();
            Stack<CIMIdentifiedObject> stack = new Stack<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> visited = new HashSet<CIMIdentifiedObject>();
            stack.Push(root);
            visited.Add(root);

            //Stack<string> levels = new Stack<string>();

            //levels.Push("1");
            //levels.Push("1");

            Stack<CIMIdentifiedObject> visitedStations = new Stack<CIMIdentifiedObject>();

            while (stack.Count > 0)
            {
                CIMIdentifiedObject p = stack.Pop();

                if (!(p.EquipmentContainerRef != null && p.EquipmentContainerRef.EquipmentContainerRef != null && p.EquipmentContainerRef.EquipmentContainerRef == feeder.Node.CIMObject))
                    traverseOrder.Enqueue(new TraceHopInfo() { CIMObject = p, BranchingInfo = Convert.ToString(visitedStations.Count) });

                var neighbours = p.GetNeighbours();

                // Count number of neighbou
                int neighbourCount = 0;
                foreach (CIMIdentifiedObject cimObj in neighbours)
                {
                    if (!visited.Contains(cimObj))
                        neighbourCount++;
                }

                int neighbourCounter = 1;


                // branching checking
                if (p.EquipmentContainerRef != null && p.EquipmentContainerRef.EquipmentContainerRef != null)
                {
                    if (!visitedStations.Contains(p.EquipmentContainerRef.EquipmentContainerRef))
                        visitedStations.Push(p.EquipmentContainerRef.EquipmentContainerRef);
                    else
                    {
                        // If we're moving around in the same station then do nothing
                        if (visitedStations.Peek() == p.EquipmentContainerRef.EquipmentContainerRef)
                        {
                        }
                        else
                        {
                            visitedStations.Pop();
                            // we have been branching
                        }
                    }
                }

                foreach (CIMIdentifiedObject cimObj in neighbours)
                {
                    if (!visited.Contains(cimObj))
                    {
                        visited.Add(cimObj);

                        if (StationFeederTraceShouldWeEnqueueObject(feeder, cimObj) && !NormalOpen(cimObj))
                        {
                            stack.Push(cimObj);
                            neighbourCounter++;
                        }
                        else
                        {
                            // Add the object which stopped the trace too
                            if (!(cimObj.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.EquipmentContainerRef == feeder.Node.CIMObject))
                                traverseOrder.Enqueue(new TraceHopInfo() { CIMObject = cimObj, BranchingInfo = "" + visitedStations.Count });
                        }
                    }
                }
            }

            return traverseOrder;
        }

        private bool NormalOpen(CIMIdentifiedObject cimObj)
        {
            bool? normalOpen = false;

            if (cimObj.ContainsPropertyValue("cim.normalopen"))
                normalOpen = cimObj.GetPropertyValue("cim.normalopen") as bool?;

            return normalOpen.Value;
        }

        private bool StationFeederTraceShouldWeEnqueueObject(DAXElectricFeeder feeder, CIMIdentifiedObject cimObj)
        {
            bool enqueue = true;

            // If normal feeder and we hit something insite feeder substation (the we're going in the wrong direction) don't enqueue
            if (!feeder.IsTransformerFeeder && 
                cimObj.EquipmentContainerRef != null && 
                cimObj.EquipmentContainerRef.EquipmentContainerRef != null && 
                cimObj.EquipmentContainerRef.EquipmentContainerRef.InternalId == feeder.Node.CIMObjectId)
                enqueue = false;

            // If normal feeder and we hit something insite feeder substation (the we're going in the wrong direction) don't enqueue
            // to support stations with only busbar sections
            if (!feeder.IsTransformerFeeder &&
               cimObj.EquipmentContainerRef != null &&
               cimObj.EquipmentContainerRef.InternalId == feeder.Node.CIMObjectId)
                enqueue = false;


            if (!feeder.IsTransformerFeeder
                && cimObj.VoltageLevel > 0
                && cimObj.VoltageLevel != feeder.VoltageLevel
                && cimObj.VoltageLevel != (feeder.VoltageLevel - 5000) // NRGi hack - skal fjernes så snart de får styr på forb kabler subtype
                && cimObj.VoltageLevel != (feeder.VoltageLevel + 5000))// NRGi hack - skal fjernes så snart de får styr på forb kabler subtype
                enqueue = false;

            // If feeder er 400 volt and we reach substation don't enqueue.
            if (feeder.VoltageLevel == 400 &&
                cimObj.EquipmentContainerRef != null && // Bay
                cimObj.EquipmentContainerRef.EquipmentContainerRef != null && // Substation
                cimObj.EquipmentContainerRef.EquipmentContainerRef.ClassType == CIMClassEnum.Substation)
            {
                enqueue = false;
            }

            return enqueue;
        }

        private void EnclosureTrace(DAXElectricNode enclosure)
        {
            CIMIdentifiedObject enCimObj = _g.ObjectManager.GetCIMObjectById(enclosure.CIMObjectId);

            if (enclosure.Sources != null)
            {
                foreach (var source in enclosure.Sources)
                {
                    CIMIdentifiedObject root = _g.ObjectManager.GetCIMObjectById(source.UpstreamCIMObjectId);

                    List<CIMIdentifiedObject> portFeeders = new List<CIMIdentifiedObject>();

                    var neighbours = root.GetNeighbours();

                    Queue<CIMIdentifiedObject> Q = new Queue<CIMIdentifiedObject>();
                    HashSet<CIMIdentifiedObject> S = new HashSet<CIMIdentifiedObject>();
                    Q.Enqueue(root);
                    S.Add(root);

                    while (Q.Count > 0)
                    {
                        CIMIdentifiedObject p = Q.Dequeue();

                        foreach (CIMIdentifiedObject cimObj in p.GetNeighbours())
                        {
                            if (!S.Contains(cimObj))
                            {
                                S.Add(cimObj);

                                if (cimObj.ExternalId == "BUS:181903")
                                {
                                }

                                if (cimObj.ClassType == CIMClassEnum.ACLineSegment && !_g.ObjectManager.AdditionalObjectAttributes(cimObj).IsFeederExitObject)
                                {
                                    // Potential feeder
                                    portFeeders.Add(cimObj);
                                }
                                else if (!NormalOpen(cimObj))
                                    Q.Enqueue(cimObj);
                            }
                        }
                    }

                    foreach (var portFeeder in portFeeders)
                    {
                        var feeder = IG_EnclosureFeederTrace(source, enclosure, portFeeder);

                        if (feeder != null)
                        {
                            if (enclosure.Feeders == null)
                                enclosure.Feeders = new List<DAXElectricFeeder>();

                            enclosure.Feeders.Add(feeder);
                        }
                    }
                }
            }
        }

        private bool EnclosureFeederTraceShouldWeEnqueueObject(DAXElectricNode feeder, CIMIdentifiedObject cimObj)
        {
            bool enqueue = true;
            // If we reach another enclosure don't enqueu
            if (cimObj.EquipmentContainerRef != null && // Bay
                cimObj.EquipmentContainerRef.EquipmentContainerRef != null && // Enclosure
                cimObj.EquipmentContainerRef.EquipmentContainerRef.ClassType == CIMClassEnum.Enclosure &&
                cimObj.EquipmentContainerRef.EquipmentContainerRef.InternalId != feeder.CIMObjectId)
            {
                enqueue = false;
            }

            return enqueue;
        }

        private DAXElectricFeeder IG_EnclosureFeederTrace(DAXElectricNodeSource source, DAXElectricNode enclosure, CIMIdentifiedObject root)
        {
            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Queue<CIMIdentifiedObject> Q = new Queue<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> S = new HashSet<CIMIdentifiedObject>();
            Q.Enqueue(root);
            S.Add(root);

            bool isFeeder = true;

            while (Q.Count > 0)
            {
                CIMIdentifiedObject p = Q.Dequeue();
                traverseOrder.Enqueue(p);

                foreach (CIMIdentifiedObject cimObj in p.GetNeighbours())
                {
                    if (!S.Contains(cimObj))
                    {
                        S.Add(cimObj);

                        var nodeContainer = cimObj.GetEquipmentContainerRoot();

                        // If component is placed inside a different container, it's not a feeder we're tracing
                        if (nodeContainer != null &&
                            nodeContainer.InternalId != enclosure.CIMObjectId)
                            isFeeder = false;

                        // If we hit feeder object (station feeder), it's not a enclosure feeder
                        if (_g.ObjectManager.AdditionalObjectAttributes(cimObj).IsFeederExitObject)
                        {

                        }



                        // If connector inside enclosure, find bay and find fuses
                        if (cimObj.ClassType == CIMClassEnum.ConnectivityNode &&
                            cimObj.EquipmentContainerRef != null &&
                            cimObj.EquipmentContainerRef.EquipmentContainerRef != null &&
                            cimObj.EquipmentContainerRef.EquipmentContainerRef.InternalId == enclosure.CIMObjectId)
                        {
                            foreach (var child in cimObj.EquipmentContainerRef.Children)
                            {
                                // FIX
                            }
                        }


                        if (cimObj.EquipmentContainerRef == null && !NormalOpen(cimObj))
                            Q.Enqueue(cimObj);
                    }
                }
            }

            if (isFeeder)
            {
                DAXElectricFeeder feeder = new DAXElectricFeeder() { Node = enclosure, DownstreamCIMObjectId = root.InternalId, UpstreamCIMObjectId = source.UpstreamCIMObjectId, VoltageLevel = 400, Name = "Kunde udf." };

                List<DAXTraceItem> result = new List<DAXTraceItem>();

                HashSet<string> alreadyAdded = new HashSet<string>();

                // Add cables, substations and enclosures to result
                while (traverseOrder.Count > 0)
                {
                    CIMIdentifiedObject p = traverseOrder.Dequeue();

                    if (p.ClassType == CIMClassEnum.EnergyConsumer)
                        result.Add(CreateTraceItem(p));
                    else if (p.ClassType == CIMClassEnum.ACLineSegment)
                        result.Add(CreateTraceItem(p));
                }

                feeder.Trace = result;

                return feeder;

            }
            else
                return null;
        }

      

        bool CheckIFeederShouldBeTraced(DAXElectricFeeder feeder)
        {
            // Check if breaker og loadbreaker is open in feeder bay.
            bool trace = true;

            if (feeder.Bay != null)
            {
                if (!TraceSwitchEllerEj(feeder.Bay, CIMClassEnum.Disconnector))
                    trace = false;

                if (!TraceSwitchEllerEj(feeder.Bay, CIMClassEnum.Fuse))
                    trace = false;

                if (!TraceSwitchEllerEj(feeder.Bay, CIMClassEnum.LoadBreakSwitch))
                    trace = false;

                if (!TraceSwitchEllerEj(feeder.Bay, CIMClassEnum.Breaker))
                    trace = false;
            }

            return trace;

        }
        
        private bool TraceSwitchEllerEj(CIMEquipmentContainer bay, CIMClassEnum bryderType)
        {
            bool trace = true;

            List<bool> switchStatuses = new List<bool>();

            foreach (var child in bay.Children)
            {
                if (child.ClassType == bryderType)
                {
                    if (child.ContainsPropertyValue("cim.normalopen"))
                    {
                        bool normalOpen = (bool)child.GetPropertyValue("cim.normalopen");
                        switchStatuses.Add(normalOpen);
                    }
                    else
                        switchStatuses.Add(false);
                }
            }

            // Hvis et stk switch, og den er åben, da trace ikke
            if (switchStatuses.Count == 1 && switchStatuses[0] == true)
                trace = false;

            // Hvis to stk switch og de begge er åben, da trace ikke
            if (switchStatuses.Count == 2 && switchStatuses[0] == true && switchStatuses[1] == true)
                trace = false;

            return trace;
        } 

    }
}
