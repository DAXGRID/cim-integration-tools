using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickGraph;
using QuickGraph.Algorithms;
using DAX.IO.CIM.DataModel;
using DAX.NetworkModel.CIM;
using DAX.Util;
using DAX.IO.CIM.Processing;

namespace DAX.IO.CIM
{
    public class CIMGraph
    {
        private int _nextVertexId = 1;
        private int _overlapCounter;
        private Dictionary<string, List<string>> _dublicateLists = new Dictionary<string, List<string>>();

        public int StatVertexOverlaps {
            get { return _overlapCounter; }
        }

        public Dictionary<string, List<string>> StatNameDublicates
        {
            get { return _dublicateLists; }
        }

        // CIM Object Manager
        private CIMObjectManager _objectManager = new CIMObjectManager();

        public CIMObjectManager ObjectManager
        {
            get { return _objectManager; }
        }

        // Graph related variables
        private UndirectedGraph<int, Edge<int>> _g = new UndirectedGraph<int, Edge<int>>();
        private Dictionary<int, CIMIdentifiedObject> _cimObjByVertexId = new Dictionary<int, CIMIdentifiedObject>();
        private Dictionary<string, CIMIdentifiedObject> _edgeObjects = new Dictionary<string, CIMIdentifiedObject>();

        // Dictionaries used for looking up CIM objects
        private Dictionary<string, CIMEquipmentContainer> _cimEquipmentContainers = new Dictionary<string, CIMEquipmentContainer>();
        private Dictionary<string, CIMIdentifiedObject> _cimObjectByMRID = new Dictionary<string, CIMIdentifiedObject>();
        private Dictionary<string, CIMIdentifiedObject> _cimObjByName = new Dictionary<string, CIMIdentifiedObject>();
        private Dictionary<string, CIMIdentifiedObject> _cimObjByExternalId = new Dictionary<string, CIMIdentifiedObject>();

        public Dictionary<string, CIMIdentifiedObject> getAllCIMObjectsByExtarnalID()
        {
            return _cimObjByExternalId;
        }
        // Graph Processing Results
        private Dictionary<string, IGraphProcessingResult> _processingResults = new Dictionary<string, IGraphProcessingResult>();

        public CIMGraph()
        {
            // FUSK Skal refactors. 
            _topologyProcessingResult = new TopologyProcessingResult(this);
            _processingResults.Add("Topology", _topologyProcessingResult);
        }

        public IGraphProcessingResult GetProcessingResult(string name)
        {
            if (_processingResults.ContainsKey(name))
                return _processingResults[name];
            else
                throw new DAXGraphException("No processing result found by name '" + name + "'. Please check configured graph processing order and dependencies.");
        }

        public void AddProcessingResult(string name, IGraphProcessingResult result)
        {
            if (_processingResults.ContainsKey(name))
                throw new DAXGraphException("Processing result with name ='" + name + "' already exists.");
            else
                _processingResults.Add(name, result);
        }

        // Topology processing result - needed here, because feeders are identified by CIMGraphWriter who build the CIM Graph
        private TopologyProcessingResult _topologyProcessingResult;

        // Set by CIMGraphWriter. If set, the graph processing errors will be logged to a tabel in a database
        internal TableLogger? TableLogger;

        #region DAX node functionality

        /*
        public List<DAXElectricNode> DAXNodes
        {
            get { return _daxNodeByCimObj.Values.ToList(); }
        }
         * */


        #endregion

        #region CIM object graph functionality

        public List<CIMIdentifiedObject> CIMObjects
        {
            get { return ObjectManager.GetObjects(); }
        }

        public CIMIdentifiedObject? GetCIMObjectByName(string name)
        {
            if (_cimObjByName.ContainsKey(name))
                return _cimObjByName[name];
            else
                return null;
        }

        public CIMIdentifiedObject? GetCIMObjectByMrid(string mrid)
        {
            if (_cimObjectByMRID.ContainsKey(mrid))
                return _cimObjectByMRID[mrid];
            else
                return null;
        }

        public CIMIdentifiedObject? GetCIMObjectByExternalId(string externalId)
        {
            if (_cimObjByExternalId.ContainsKey(externalId))
                return _cimObjByExternalId[externalId];
            else
                return null;
        }

     
        public CIMIdentifiedObject GetCIMObjectByVertexId(int vertexId)
        {
            if (_cimObjByVertexId.ContainsKey(vertexId))
                return _cimObjByVertexId[vertexId];

            return null;
        }

        public int AddCIMObjectToVertex(CIMIdentifiedObject obj, bool dublicate = false)
        {
            if (obj is CIMEquipmentContainer)
            {
                IndexObject(obj);
                AddEquipmentContainer((CIMEquipmentContainer)obj);
                return 0;
            }
            else
            {
                if (!dublicate)
                {
                    if (obj.mRID != Guid.Empty)
                    {
                        if (!_cimObjectByMRID.ContainsKey(obj.mRID.ToString()))
                            _cimObjectByMRID.Add(obj.mRID.ToString(), obj);
                        else
                        {
                            Logger.Log(LogLevel.Warning, "Dublicated mRID: " + obj.ToString());
                            ObjectManager.Delete(obj);
                            return 0;
                        }

                    }
                }

                TryPairWithEquipmentContainer(obj);
                IndexObject(obj);

                // Add object to graph vertex
                _g.AddVertex(_nextVertexId);
                _cimObjByVertexId.Add(_nextVertexId, obj);

                _nextVertexId++;

                if (obj.ClassType == CIMClassEnum.EnergyConsumer)
                {
                    var daxNode = new DAXElectricNode(ObjectManager);
                    daxNode.ClassType = obj.ClassType;
                    daxNode.CIMObjectId = obj.InternalId;
                    daxNode.Coords = obj.Coords;
                    daxNode.Name = obj.Name;
                    daxNode.Description = obj.Description;
                    _topologyProcessingResult.AddDAXNodeByCIMObject(obj, daxNode);
                }

                return _nextVertexId - 1;
            }
        }

        public int AddCIMObjectToExistingVertex(CIMIdentifiedObject obj, int vertexId)
        {
            if (_cimObjByVertexId.ContainsKey(vertexId))
            {
                var existingCimObject = _cimObjByVertexId[vertexId];

                if (existingCimObject.ClassType != CIMClassEnum.ConnectivityNode)
                {
                    _overlapCounter++;

                    string errorText = "CIM object " + obj.IdString() + " cannot be added to the graph because CIM object " + existingCimObject.IdString() + " is already attached to graph vertex with id=" + vertexId;
                    //Logger.Log(LogLevel.Warning, errorText);
                    TableLogger.Log(Severity.Error, (int)GeneralErrors.ComponentOverlayAnotherComponent, errorText, obj);
                    
                    //tableLogger.Log(2, 4, errorText, obj.Coords[0], obj.Coords[1], obj.ExternalId, obj.ClassType.ToString(), obj.mRID.ToString());
                }
                else
                {
                    if (obj.ClassType != CIMClassEnum.ConnectivityNode)
                    {
                        // Just replace the connectionnode with this component
                        _cimObjByVertexId[vertexId] = obj;
                        TryPairWithEquipmentContainer(obj);
                        IndexObject(obj);
                    }
                    else
                    {
                        // Do nothing. The existing connectivity node will be used
                    }
                }
            }
            else
            {
            }

            return vertexId;
        }

        public void AddCIMObjectToEdge(int fromVertexId, int toVertexId, CIMIdentifiedObject ci)
        {
            var fromVertexObj = _cimObjByVertexId[fromVertexId];
            var toVertexObj = _cimObjByVertexId[toVertexId];

            if (ci.ClassType == CIMClassEnum.ACLineSegment || ci.ClassType == CIMClassEnum.ACLineSegmentExt)
            {
                ci.SetPropertyValue("FromCn", fromVertexObj);
                ci.SetPropertyValue("ToCn", toVertexObj);
            }

            TryPairWithEquipmentContainer(ci);
            IndexObject(ci);

            _g.AddEdge(new Edge<int>(fromVertexId, toVertexId));

            string edgeKey = fromVertexId + ":" + toVertexId;

            if (!_edgeObjects.ContainsKey(edgeKey))
            {
                _edgeObjects.Add(edgeKey, ci);
                fromVertexObj.AddNeighbour(ci);
                toVertexObj.AddNeighbour(ci);
                ci.AddNeighbour(fromVertexObj);
                ci.AddNeighbour(toVertexObj);

                ObjectManager.AdditionalObjectAttributes(ci).Vertex1Id = fromVertexId;

                //ci.Vertex1Id = fromVertexId;
                ObjectManager.AdditionalObjectAttributes(ci).Vertex2Id = toVertexId;

            }
            else
            {
                // Parrallel acls - we just add them as normal - CIM supports this 
                fromVertexObj.AddNeighbour(ci);
                toVertexObj.AddNeighbour(ci);
                ci.AddNeighbour(fromVertexObj);
                ci.AddNeighbour(toVertexObj);

                ObjectManager.AdditionalObjectAttributes(ci).Vertex1Id = fromVertexId;

                //ci.Vertex1Id = fromVertexId;
                ObjectManager.AdditionalObjectAttributes(ci).Vertex2Id = toVertexId;

            }

        }

        public void AddFeeder(CreateFeederInfo feederInfo, CIMIdentifiedObject down)
        {
            var feeder = new DAXElectricFeeder();




            if (feederInfo.ConnectivityNode.EquipmentContainerRef != null)
            {
                feeder.VoltageLevel = feederInfo.ConnectivityNode.EquipmentContainerRef.VoltageLevel;
                feeder.Name = feederInfo.ConnectivityNode.EquipmentContainerRef.Name;
                feeder.Bay = feederInfo.ConnectivityNode.EquipmentContainerRef;
            }

            if (feeder.Name == "03HATT: 30244")
            {

            }

                feeder.UpstreamCIMObjectId = feederInfo.ConnectivityNode.InternalId;
            feeder.DownstreamCIMObjectId = down.InternalId;
            ObjectManager.AdditionalObjectAttributes(feederInfo.ConnectivityNode).IsFeederEntryObject = true;

            var ec = feederInfo.ConnectivityNode.GetEquipmentContainerRoot();

            if (ec != null)
            {
                DAXElectricNode node = _topologyProcessingResult._daxNodeByCimObj[ec];
                feeder.Node = node;

                if (!feederInfo.IsTransformerFeeder)
                {
                    // Check if feeder already added - will ocour with parallel cables going aout from feeder
                    if (node.Feeders != null && feeder.Bay != null)
                    {
                        foreach (var existingFeeder in node.Feeders)
                        {
                            if (existingFeeder.Bay == feeder.Bay)
                                return; // Do nothing
                        }
                    }

                    node.AddFeeder(feeder);
                }
                else
                {
                    var transformer = new DAXElectricTransformer(ObjectManager) { Name = feederInfo.Transformer.Name, Node = node, CIMObjectId = feederInfo.Transformer.InternalId };
                    feeder.Transformer = transformer;

                    if (!_topologyProcessingResult._daxTransformerByCimObj.ContainsKey(feederInfo.Transformer))
                        _topologyProcessingResult._daxTransformerByCimObj.Add(feederInfo.Transformer, transformer);

                    transformer.FeederObjectId = feederInfo.ConnectivityNode.InternalId;

                    node.AddTransformer(transformer);

                    feeder.IsTransformerFeeder = true;
                }
            }
            else
                Logger.Log(LogLevel.Warning, "Cannot find subtation og enclosure for CIM object " + feederInfo.ConnectivityNode.IdString());

          

            // This is ugly, but feeders are identified here at this moment, not later (well, could be done, but need to refactor a lot)
            _topologyProcessingResult.DAXFeeders.Add(feeder);
        }

        #endregion
     

       


        public IList<CIMIdentifiedObject> ShortestPath(int sourceVertexId, int targetVertexId)
        {
            List<CIMIdentifiedObject> cimPath = new List<CIMIdentifiedObject>();

            if (sourceVertexId > 0 && targetVertexId > 0)
            {
                Func<Edge<int>, double> cityDistances = e => 1; // constant cost

                TryFunc<int, IEnumerable<Edge<int>>> tryGetPath = _g.ShortestPathsDijkstra(cityDistances, sourceVertexId);

                IEnumerable<Edge<int>> path;
                tryGetPath(targetVertexId, out path);

                if (path != null)
                {
                    int currentFromVertex = sourceVertexId;
                    cimPath.Add(_cimObjByVertexId[currentFromVertex]);

                    foreach (var edge in path)
                    {
                        string edgeKey = edge.Source + ":" + edge.Target;
                        CIMIdentifiedObject eObj = _edgeObjects[edgeKey];

                        // Add edge object
                        cimPath.Add(eObj);

                        // Add vertex object
                        if (edge.Source == currentFromVertex)
                        {
                            currentFromVertex = edge.Target;
                            cimPath.Add(_cimObjByVertexId[currentFromVertex]);
                        }
                        else
                        {
                            currentFromVertex = edge.Source;
                            cimPath.Add(_cimObjByVertexId[currentFromVertex]);
                        }

                    }
                }
            }
            return cimPath;
        }


        #region Private helper functions

        private void AddEquipmentContainer(CIMEquipmentContainer ec)
        {
            string key = ec.ExternalId;

            if (ec.ContainsPropertyValue("dax.equipmentcontainertype"))
            {
                key = ec.GetPropertyValueAsString("dax.equipmentcontainertype") + ":" + key;
            }

            if (!_cimEquipmentContainers.ContainsKey(key))
            {
                _cimEquipmentContainers.Add(key, ec);
                _cimObjectByMRID.Add(ec.mRID.ToString(), ec);

                //ec.VoltageLevel = ConvertVoltageLevel(ec);

                TryPairWithEquipmentContainer(ec);

                if (ec.ClassType == CIMClassEnum.Substation || ec.ClassType == CIMClassEnum.Enclosure)
                {
                    var daxNode = new DAXElectricNode(ObjectManager) { Name = ec.Name, Description = ec.Description, CIMObjectId = ec.InternalId, ClassType = ec.ClassType, Coords = ec.Coords, VoltageLevel = ec.VoltageLevel };
                    _topologyProcessingResult._daxNodeByCimObj.Add(ec, daxNode);
                }
            }
            else
            {

            }
        
        }

        private void TryPairWithEquipmentContainer(CIMIdentifiedObject obj)
        {
            // Only pair with container, if not allready paired
            if (obj.EquipmentContainerRef == null)
            {
                string cimRefEc = obj.GetPropertyValueAsString("cim.ref.equipmentcontainer");
                string ctype = obj.GetPropertyValueAsString("dax.ref.equipmentcontainertype");

                if (obj.ExternalId == "17063")
                {
                }

                if (ctype != null && cimRefEc == null)
                {
                    obj.RemoveProperty("dax.ref.equipmentcontainertype");

                    string cimRefEcSubstation = obj.GetPropertyValueAsString("cim.ref.substation");
                    string cimRefEcBay = obj.GetPropertyValueAsString("cim.ref.bay");

                    if (cimRefEcBay != null)
                    {
                    }

                    // Prøv at finde bay først
                    if (cimRefEcBay != null && _cimEquipmentContainers.ContainsKey("Bay:" + cimRefEcBay))
                    {
                        CIMEquipmentContainer ec = _cimEquipmentContainers["Bay:" + cimRefEcBay];
                        ec.Children.Add(obj);
                        obj.EquipmentContainerRef = ec;
                        obj.EquipmentContainerRef.Children.Add(obj);
                    }
                    else if (cimRefEcSubstation != null)
                    {
                        // Find container
                        string key = ctype + ":" + cimRefEcSubstation;

                        if (_cimEquipmentContainers.ContainsKey(key))
                        {
                            CIMEquipmentContainer ec = _cimEquipmentContainers[key];
                            obj.EquipmentContainerRef = ec;
                            obj.EquipmentContainerRef.Children.Add(obj);
                        }
                        else
                        {
                            // try to sbstation lookup by mrid
                            string mridClean = cimRefEcSubstation.ToLower().Replace("{", "").Replace("}", "");

                            if (_cimObjectByMRID.ContainsKey(mridClean))
                            {
                                obj.EquipmentContainerRef = _cimObjectByMRID[mridClean] as CIMEquipmentContainer;
                                obj.EquipmentContainerRef.Children.Add(obj);
                            }

                        }
                    }
                    else if (cimRefEc != null)
                    {

                    }
                }
                else
                {
                    string cimRefEcBay = obj.GetPropertyValueAsString("cim.ref.bay");

                    // Prøv at finde bay først
                    if (cimRefEcBay != null && _cimEquipmentContainers.ContainsKey("Bay:" + cimRefEcBay))
                    {
                        obj.RemoveProperty("cim.ref.bay");

                        CIMEquipmentContainer ec = _cimEquipmentContainers["Bay:" + cimRefEcBay];
                        ec.Children.Add(obj);
                        obj.EquipmentContainerRef = ec;
                    }
                    else if (cimRefEc != null)
                    {
                        string key = cimRefEc;

                        obj.RemoveProperty("cim.ref.equipmentcontainer");

                        if (ctype != null)
                        {
                            obj.RemoveProperty("dax.ref.equipmentcontainertype");
                            key = ctype + ":" + key;
                        }

                        if (_cimEquipmentContainers.ContainsKey(key))
                        {
                            CIMEquipmentContainer ec = _cimEquipmentContainers[key];
                            ec.Children.Add(obj);
                            obj.EquipmentContainerRef = ec;
                        }
                    }
                }
            }
        }

        public void IndexObject(CIMIdentifiedObject obj)
        {
            // Index externalid on all objects except ConnectivityNode and ConnectivityEdge (to save memory)
            if (obj.ClassType != CIMClassEnum.ConnectivityNode && obj.ClassType != CIMClassEnum.ConnectivityEdge)
            {
                string exKey = obj.ClassType.ToString() + ":" + obj.ExternalId;
                if (!_cimObjByExternalId.ContainsKey(exKey))
                    _cimObjByExternalId.Add(exKey, obj);
            }

            // Index name on substations, enclosures and enegyconsumers
            if (obj.Name != null)
            {
                if (obj.ClassType == CIMClassEnum.Substation ||
                    obj.ClassType == CIMClassEnum.Enclosure ||
                    obj.ClassType == CIMClassEnum.EnergyConsumer ||
                    obj.ClassType == CIMClassEnum.AsynchronousMachine ||
                    obj.ClassType == CIMClassEnum.SynchronousMachine)
                {
                    //
                    // Lots of enclosure share name with station
                    // By prefixing the enclosure name with "Enclosure:" 
                    string objName = obj.Name;
                    if (obj.ClassType == CIMClassEnum.Enclosure)
                        objName = "Enclosure:" + obj.Name;
                    if (!_cimObjByName.ContainsKey(objName))
                        _cimObjByName.Add(objName, obj);
                    else
                    {
                        string errorMsg = "Duplicated name '" + obj.Name + "' detected trying to add CIM object " + obj.IdString() + " to name index.";

                        if (!_dublicateLists.ContainsKey(obj.ClassType.ToString()))
                            _dublicateLists[obj.ClassType.ToString()] = new List<string>();

                        _dublicateLists[obj.ClassType.ToString()].Add(objName);


                        //Logger.Log(LogLevel.Warning, errorMsg);
                        TableLogger.Log(Severity.Error, (int)GeneralErrors.NameNotUnique, errorMsg, obj);
                    }
                }
                else if ((obj.ClassType == CIMClassEnum.PetersenCoil || obj.ClassType == CIMClassEnum.LinearShuntCompensator) && obj.EquipmentContainerRef != null)
                {
                    string indexKey = obj.EquipmentContainerRef.Name + "/" + obj.Name;

                    if (!_cimObjByName.ContainsKey(indexKey))
                        _cimObjByName.Add(indexKey, obj);
                }
            }


        }

        #endregion

    }

    public class CreateFeederInfo
    {
        public CIMIdentifiedObject ConnectivityNode;
        public bool IsTransformerFeeder = false;
        public CIMIdentifiedObject Transformer;
    }

    public class TraceHopInfo
    {
        public CIMIdentifiedObject CIMObject { get; set; }
        public string BranchingInfo { get; set; }
    }

}

