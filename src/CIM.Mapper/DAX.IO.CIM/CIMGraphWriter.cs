﻿using DAX.IO.CIM;
using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.NetworkModel.CIM;
using DAX.Util;
using QuickGraph;
using QuickGraph.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;


namespace DAX.IO.Writers
{
    public class CIMGraphWriter : IDaxWriter
    {
        private string _nameAndVersion = "CIMGraphWriter ver. 0.2 (12-03-2018)";

        private TransformationConfig _transConf = null;

        private CIMGraph _g = new CIMGraph();

        public Summary getSummary() { return _g.TableLogger.getSummary(); }

        private Dictionary<string, int> _aaavertexByXYDict = new Dictionary<string, int>();

        private Dictionary<string, List<string>> _invalidCoordsLists = new Dictionary<string, List<string>>();


        //private Dictionary<string, int> _consumerVertexIdByName = new Dictionary<string, int>();

        // Temporary
        private List<DAXFeature> _busbars = new List<DAXFeature>();
        private List<DAXFeature> _aclinesegments = new List<DAXFeature>();
        private List<DAXFeature> _edgeConnectors = new List<DAXFeature>();
        private List<DAXFeature> _energyconsumers = new List<DAXFeature>();
        private List<DAXFeature> _networkConnectionPoints = new List<DAXFeature>();
        private List<DAXFeature> _switches = new List<DAXFeature>();
        private List<DAXFeature> _transformers = new List<DAXFeature>();
        private List<DAXFeature> _equipmentContainers = new List<DAXFeature>();
        private List<DAXFeature> _coils = new List<DAXFeature>();
        private List<DAXFeature> _sync_gens = new List<DAXFeature>();
        private List<DAXFeature> _async_gens = new List<DAXFeature>();
        private List<DAXFeature> _compensators = new List<DAXFeature>();
        private List<DAXFeature> _extNetworkInjections = new List<DAXFeature>();
        private List<DAXFeature> _buildSimpleJunctions = new List<DAXFeature>();
        private List<DAXFeature> _faultIndicators = new List<DAXFeature>();
        private List<DAXFeature> _currentTransformers = new List<DAXFeature>();
        private List<DAXFeature> _potentialTransformers = new List<DAXFeature>();
        private List<DAXFeature> _protectionEquipments = new List<DAXFeature>();
        private List<DAXFeature> _buildNodes = new List<DAXFeature>();
        private List<DAXFeature> _networkEquipments = new List<DAXFeature>();
        private List<DAXFeature> _locationAddresses = new List<DAXFeature>();
        private List<DAXFeature> _usagePoints = new List<DAXFeature>();
        private List<DAXFeature> _assetInfos = new List<DAXFeature>();
        private List<DAXFeature> _manufactures = new List<DAXFeature>();
        private List<DAXFeature> _productAssetModels = new List<DAXFeature>();


        private HashSet<Guid> dubletCheck = new HashSet<Guid>();

        // Writer Parameters
        private int paramRoundDecimals = 3;
        private int paramBufferRadius = 2;

        private string logTableDbConnectionString = null;
        private string logTableName = null;

        private string buildErrorCodeList = null;
        private string doPrecheckconnectivity = null;
        private bool _tool = false;
        private bool _fast = false;

        // Genereal parameters
        private double bayRadius;
        private double enclosureRadius;
        private double consumerRadius;
        private double danglingRadius;
        private double parentCheckRadius = 10;

        private double tolerance;

        private string summaryFileName;

        // Build CIM schematic
        private bool buildCIM = false;
        private Dictionary<string, CIMEquipmentContainer> _substationByXY = new Dictionary<string, CIMEquipmentContainer>();

        // Midlertidig TME hack til brug for generering af CIM indre skematik
        public Dictionary<CIMIdentifiedObject, CIMEquipmentContainer> _substationByACLineConnector = new Dictionary<CIMIdentifiedObject, CIMEquipmentContainer>();

        // Test
        private int _testBusbarVertexId = 0;

        #region IDaxWriter Members

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, TransformationConfig transConfig, List<ConfigParameter> parameters = null)
        {
            _transConf = transConfig;

            if (parameters != null)
            {
                foreach (ConfigParameter configParam in parameters)
                {
                    if (configParam.Name.ToLower() == "rounddecimals")
                        paramRoundDecimals = Convert.ToInt32(configParam.Value);
                    if (configParam.Name.ToLower() == "logdbconnectionstring")
                        logTableDbConnectionString = Configuration.GetConnectionString(configParam.Value);
                    if (configParam.Name.ToLower() == "logdbtablename")
                        logTableName = configParam.Value;

                    if (configParam.Name.ToLower() == "builderrorcodelist")
                        buildErrorCodeList = configParam.Value;
                    if (configParam.Name.ToLower() == "precheckconnectivity")
                        doPrecheckconnectivity = configParam.Value;
                    if (configParam.Name.ToLower() == "tool")
                        _tool = configParam.Value.ToLower().Equals("true");
                    if (configParam.Name.ToLower() == "fast")
                        _fast = configParam.Value.ToLower().Equals("true");


                    // Generel parameters
                    if (configParam.Name.ToLower() == "bayradius")
                        bayRadius = Double.Parse(configParam.Value);
                    if (configParam.Name.ToLower() == "enclosureradius")
                        enclosureRadius = Double.Parse(configParam.Value);
                    if (configParam.Name.ToLower() == "consumerradius")
                        consumerRadius = Double.Parse(configParam.Value);
                    if (configParam.Name.ToLower() == "danglingradius")
                        danglingRadius = Double.Parse(configParam.Value);

                    if (configParam.Name.ToLower() == "tolerance")
                        tolerance = Double.Parse(configParam.Value);

                    if (configParam.Name.ToLower() == "summaryfilename")
                        summaryFileName = configParam.Value;
                }
            }

            // Create metadata repository
            CIMMetaDataManager.Repository = new CIMMetaDataRepository();

        }
        public double GetBayClosenessRadius()
        {
            return bayRadius;
        }
        public double GetEnclosureClosenessRadius()
        {
            return enclosureRadius;
        }
        public double GetConsumerClosenessRadius()
        {
            return consumerRadius;
        }
        public double GetDanglingClosenessRadius()
        {
            return danglingRadius;
        }
        public double GetTolerance()
        {
            if (tolerance == null)
                tolerance = 0.000001D;
            return tolerance;
        }

        public string GetSummaryFileName()
        {
            return summaryFileName;
        }

        public double GetMaxRadiusOfCloseness()
        {
            double r = GetBayClosenessRadius();
            r = Math.Max(r, GetConsumerClosenessRadius());
            r = Math.Max(r, GetEnclosureClosenessRadius());
            r = Math.Max(r, GetDanglingClosenessRadius());

            return r;
        }

        public void Open(string connectionStringOrUrl)
        {
        }

        public void OpenDataSet(string dataSetName)
        {
        }

        public void CloseDataSet(string dataSetName)
        {
        }

        public void WriteFeature(DAXFeature feature, DataSetMappingGuide dsGuide = null)
        {

            //string className = feature.ClassName.ToLower();
            string className = feature.ClassName;

            if (className == "aclinesegment")
            {
                Guid globalid = Guid.Parse(feature["cim.mrid"].ToString());

                if (!dubletCheck.Contains(globalid))
                {
                    _aclinesegments.Add(feature);
                    dubletCheck.Add(globalid);
                }
                else
                {

                }
            }
            else if (className == "busbarsection")
                _busbars.Add(feature);
            else if (className == "connectivityedge")
                _edgeConnectors.Add(feature);
            else if (className == "energyconsumer")
                _energyconsumers.Add(feature);
            else if (className == "networkconnectionpoint")
                _networkConnectionPoints.Add(feature);
            else if (className == "loadbreakswitch" || className == "breaker" || className == "fuse" || className == "disconnector" || className == "grounddisconnector")
                _switches.Add(feature);
            else if (className == "switch")
            {
                feature.ClassName = feature["cim.class"].ToString();
                if (feature.ClassName == "Disconnecter")
                    feature.ClassName = "disconnector";

                _switches.Add(feature);
            }

            else if (className == "powertransformer")
            {
                Guid globalid = Guid.Parse(feature["cim.mrid"].ToString());

                if (!dubletCheck.Contains(globalid))
                {
                    _transformers.Add(feature);
                    dubletCheck.Add(globalid);
                }
                else
                {
                }
            }
            else if (className == "petersencoil")
                _coils.Add(feature);
            else if (className == "linearshuntcompensator")
                _compensators.Add(feature);
            else if (className == "synchronousmachine")
                _sync_gens.Add(feature);
            else if (className == "asynchronousmachine")
                _async_gens.Add(feature);
            else if (className == "externalnetworkinjection")
                _extNetworkInjections.Add(feature);
            else if (className == "substation" || className == "bay" || className == "enclosure" || className == "equipmentcontainer")
                _equipmentContainers.Add(feature);
            else if (className == "buildsimplejunction")
                _buildSimpleJunctions.Add(feature);
            else if (className == "faultindicator")
                _faultIndicators.Add(feature);
            else if (className == "currenttransformer")
                _currentTransformers.Add(feature);
            else if (className == "potentialtransformer")
                _potentialTransformers.Add(feature);
            else if (className == "protectionequipment")
                _protectionEquipments.Add(feature);
            else if (className == "buildnode")
                _buildNodes.Add(feature);
            else if (className == "networkequipment")
                _networkEquipments.Add(feature);
            else if (className == "locationaddress")
                _locationAddresses.Add(feature);
            else if (className == "usagepoint")
                _usagePoints.Add(feature);
            else if (className == "cableinfo")
                _assetInfos.Add(feature);
            else if (className == "overheadwireinfo")
                _assetInfos.Add(feature);
            else if (className == "switchinfo")
                _assetInfos.Add(feature);
            else if (className == "busbarsectioninfo")
                _assetInfos.Add(feature);
            else if (className == "manufacturer")
                _manufactures.Add(feature);
            else if (className == "productassetmodel")
                _productAssetModels.Add(feature);
        }

        public void Close()
        {
        }

        public string DataSourceTypeName()
        {
            return _nameAndVersion;
        }

        public string DataSourceName()
        {
            return "CIMGraphWriter";
        }

        public string JobName()
        {
            return "CIMGraphWriterJob";
        }

        public DAXMetaData GetMetaData()
        {
            DAXMetaData metaData = new DAXMetaData();

            // ACLineSegment
            DAXClassDef fcDef = metaData.AddOrGetFeatureClassDefinition(null, "ACLineSegment", "ACLineSegment");
            AddCommonMetaDataAttributes(metaData, "ACLineSegment");
            AddMetaDataAttribute(metaData, "ACLineSegment", "cim.length");
            AddMetaDataAttribute(metaData, "ACLineSegment", "cim.asset.type");

            // BusbarSection
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "BusbarSection", "BusbarSection");
            AddCommonMetaDataAttributes(metaData, "BusbarSection");

            // Energy consumer
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "EnergyConsumer", "EnergyConsumer");
            AddCommonMetaDataAttributes(metaData, "EnergyConsumer");
            AddMetaDataAttribute(metaData, "EnergyConsumer", "dax.searchaddress");

            // ConnectivityNode
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "ConnectivityNode", "ConnectivityNode");
            AddCommonMetaDataAttributes(metaData, "ConnectivityNode");

            // ConnectivityEdge
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "ConnectivityEdge", "ConnectivityEdge");
            AddCommonMetaDataAttributes(metaData, "ConnectivityEdge");

            // LoadBreakswitch
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "LoadBreakSwitch", "LoadBreakSwitch");
            AddCommonMetaDataAttributes(metaData, "LoadBreakSwitch");
            /*
            AddMetaDataAttribute(metaData, "LoadBreakSwitch", "cim.asset.type");
            AddMetaDataAttribute(metaData, "LoadBreakSwitch", "cim.open");
            AddMetaDataAttribute(metaData, "LoadBreakSwitch", "cim.normalopen");
            AddMetaDataAttribute(metaData, "LoadBreakSwitch", "cim.ratedcurrent");
            AddMetaDataAttribute(metaData, "LoadBreakSwitch", "dax.closed");
            AddMetaDataAttribute(metaData, "LoadBreakSwitch", "dax.normalclosed");
            */

            // Breaker
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Breaker", "Breaker");
            AddCommonMetaDataAttributes(metaData, "Breaker");
            /*
            AddMetaDataAttribute(metaData, "Breaker", "cim.asset.type");
            AddMetaDataAttribute(metaData, "Breaker", "cim.open");
            AddMetaDataAttribute(metaData, "Breaker", "cim.normalOpen");
            AddMetaDataAttribute(metaData, "Breaker", "cim.ratedcurrent");
            AddMetaDataAttribute(metaData, "Breaker", "dax.closed");
            AddMetaDataAttribute(metaData, "Breaker", "dax.normalclosed");
            */

            // Fuse
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Fuse", "Fuse");
            AddCommonMetaDataAttributes(metaData, "Fuse");
            /*
            AddMetaDataAttribute(metaData, "Fuse", "cim.ratedcurrent");
            AddMetaDataAttribute(metaData, "Fuse", "cim.asset.type");
            */

            // Disconnector
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Disconnector", "Disconnector");
            AddCommonMetaDataAttributes(metaData, "Disconnector");
            /*
            AddMetaDataAttribute(metaData, "Disconnector", "cim.ratedcurrent");
            AddMetaDataAttribute(metaData, "Disconnector", "cim.asset.type");
            */


            // Substation
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Substation", "Substation");
            AddCommonMetaDataAttributes(metaData, "Substation");

            // PowerTransformer
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "PowerTransformer", "PowerTransformer");
            AddCommonMetaDataAttributes(metaData, "PowerTransformer");

            // Enclosure
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Enclosure", "Enclosure");
            AddCommonMetaDataAttributes(metaData, "Enclosure");
            AddMetaDataAttribute(metaData, "Enclosure", "cim.asset.type");

            // Bay
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Bay", "Bay");
            AddCommonMetaDataAttributes(metaData, "Bay");

            // PetersenCoil
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "PetersenCoil", "PetersenCoil");
            AddCommonMetaDataAttributes(metaData, "PetersenCoil");

            // SynchronousMachine
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "SynchronousMachine", "SynchronousMachine");
            AddCommonMetaDataAttributes(metaData, "SynchronousMachine");

            // AsynchronousMachine
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "AsynchronousMachine", "AsynchronousMachine");
            AddCommonMetaDataAttributes(metaData, "AsynchronousMachine");

            // ExternalNetworkInjection
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "ExternalNetworkInjection", "ExternalNetworkInjection");
            AddCommonMetaDataAttributes(metaData, "ExternalNetworkInjection");

            // Simple Junction
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "BuildSimpleJunction", "BuildSimpleJunction");
            AddCommonMetaDataAttributes(metaData, "BuildSimpleJunction");

            // Fault indicator
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "FaultIndicator", "FaultIndicator");
            AddCommonMetaDataAttributes(metaData, "FaultIndicator");

            // Current Transformer
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "CurrentTransformer", "CurrentTransformer");
            AddCommonMetaDataAttributes(metaData, "CurrentTransformer");

            // Potential Transformer
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "PotentialTransformer", "PotentialTransformer");
            AddCommonMetaDataAttributes(metaData, "PotentialTransformer");

            // Protection Equipment
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "ProtectionEquipment", "ProtectionEquipment");
            AddCommonMetaDataAttributes(metaData, "ProtectionEquipment");

            // Build node
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "BuildNode", "BuildNode");
            AddCommonMetaDataAttributes(metaData, "BuildNode");

            // EquipmentContainer
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "EquipmentContainer", "EquipmentContainer");
            AddCommonMetaDataAttributes(metaData, "EquipmentContainer");

            // NetworkEquipment
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "NetworkEquipment", "NetworkEquipment");
            AddCommonMetaDataAttributes(metaData, "NetworkEquipment");

            // LocationAddress
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "LocationAddress", "LocationAddress");
            AddCommonMetaDataAttributes(metaData, "LocationAddress");

            // UsagePoint
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "NetworkConnectionPoint", "NetworkConnectionPoint");
            AddCommonMetaDataAttributes(metaData, "NetworkConnectionPoint");

            // UsagePoint
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "UsagePoint", "UsagePoint");
            AddCommonMetaDataAttributes(metaData, "UsagePoint");

            // CableInfo
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "CableInfo", "CableInfo");
            AddCommonMetaDataAttributes(metaData, "CableInfo");

            // OverheadWireInfo
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "OverheadWireInfo", "OverheadWireInfo");
            AddCommonMetaDataAttributes(metaData, "OverheadWireInfo");

            // Switch
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Switch", "Switch");
            AddCommonMetaDataAttributes(metaData, "Switch");

            // SwitchInfo
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "SwitchInfo", "SwitchInfo");
            AddCommonMetaDataAttributes(metaData, "SwitchInfo");

            // BusbarSectionInfo
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "BusbarSectionInfo", "BusbarSectionInfo");
            AddCommonMetaDataAttributes(metaData, "BusbarSectionInfo");

            // Manufacturer
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "Manufacturer", "Manufacturer");
            AddCommonMetaDataAttributes(metaData, "Manufacturer");

            // ProductAssetModel
            fcDef = metaData.AddOrGetFeatureClassDefinition(null, "ProductAssetModel", "ProductAssetModel");
            AddCommonMetaDataAttributes(metaData, "ProductAssetModel");


            metaData.CanHandleAllAttributes = true;


            return metaData;
        }

        public void Commit()
        {
            bool graphLoadedSuccessfull = false;

            try
            {
                System.Diagnostics.Stopwatch sw1 = new System.Diagnostics.Stopwatch();

                sw1.Start();
                Logger.Log(LogLevel.Info, "CIMGraphWriter: " + VersionInfo.CIMAdapterVersion + " Starting CIM processing...");

                _g.TableLogger = new TableLogger();

                // Setup table logger if specificed in config
                if (logTableDbConnectionString != null || buildErrorCodeList != null)
                {
                    Logger.Log(LogLevel.Debug, "CIMGraphWriter: Initialize table logger...");

                    if (buildErrorCodeList != null && buildErrorCodeList == "yes")
                        _g.TableLogger.constructErrorCodeList(true);
                }

                _g.ObjectManager.Clear();

                ////////////////////////////////////////////////////////
                // Substations and enclousures

                int nEquipmentContainers = 0;
                int nSubstations = 0;
                int nEnclosures = 0;
                int nBay = 0;

                foreach (var feature in _equipmentContainers)
                {
                    if (feature.ClassName == "substation" || feature.ClassName == "enclosure" || feature.ClassName == "equipmentcontainer")
                    {
                        var ec = new CIMEquipmentContainer(_g.ObjectManager);

                        ec.SetClass(feature.ClassName);
                        ec.SetPropertyValue("dax.equipmentcontainertype", ec.ClassType.ToString());

                        MapCommonFields(feature, ec);
                        CopyAttributes(feature, ec);

                        if (ec.Name == "Vrå")
                        {
                        }

                        if (feature.Coordinates != null && feature.Coordinates.Length == 1)
                        {
                            MapCoords(feature, ec);
                            _g.AddCIMObjectToVertex(ec);


                            if (buildCIM)
                            {
                                var key = CreateStationVertexCoordKey(feature.Coordinates[0].X, feature.Coordinates[0].Y);

                                _substationByXY[key] = ec;

                                if (ec.Name != null)
                                    ec.Name = ec.Name.Trim();
                            }

                        }
                        else
                            //Logger.Log(LogLevel.Warning, "CIM object " + ec.IdString() + " has invalid coordinates.");
                            AddToInvalidCoordList(ec.ClassType.ToString(), ec.ExternalId);

                        if (feature.ClassName == "substation")
                            nSubstations++;
                        else if (feature.ClassName == "equipmentcontainer")
                            nEquipmentContainers++;
                        else
                            nEnclosures++;
                    }
                }

                ////////////////////////////////////////////////////////
                // Bays

                foreach (var feature in _equipmentContainers)
                {
                    if (feature.ClassName == "bay")
                    {
                        var ec = new CIMEquipmentContainer(_g.ObjectManager);
                        ec.SetClass(feature.ClassName);
                        ec.SetPropertyValue("dax.equipmentcontainertype", "Bay");

                        MapCommonFields(feature, ec);
                        CopyAttributes(feature, ec);

                        if (ec.ExternalId == "253854")
                        {
                        }

                        // Overfør minY og maxY (benyttes til check af løse ender)
                        double minX = 99999999999999;
                        double maxX = 0;


                        double minY = 99999999999999;
                        double maxY = 0;


                        foreach (var coord in feature.Coordinates)
                        {
                            if (coord.X < minX)
                                minX = coord.X;

                            if (coord.X > maxX)
                                maxX = coord.X;

                            if (coord.Y < minY)
                                minY = coord.Y;

                            if (coord.Y > maxY)
                                maxY = coord.Y;

                        }

                        ec.Coords = new double[] { Math.Round(minY, paramRoundDecimals), Math.Round(maxY, paramRoundDecimals), Math.Round(minX, paramRoundDecimals), Math.Round(maxX, paramRoundDecimals) };

                        _g.AddCIMObjectToVertex(ec);

                        nBay++;
                    }
                }

                Logger.Log(LogLevel.Info, "" + nEquipmentContainers + " equipment containers imported.");
                Logger.Log(LogLevel.Info, "" + nSubstations + " substations (EquipmentContainer) imported.");
                Logger.Log(LogLevel.Info, "" + nEnclosures + " enclosures (EquipmentContainer) imported.");
                Logger.Log(LogLevel.Info, "" + nBay + " bays (EquipmentContainer) imported.");

                _equipmentContainers = null;

                ////////////////////////////////////////////////////////
                // Asset infos

                foreach (var feature in _assetInfos)
                {
                    var ci = new CIMIdentifiedObject(_g.ObjectManager);
                    ci.SetClass(feature.ClassName);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);

                    _g.CIMObjects.Add(ci);
                }

                Logger.Log(LogLevel.Info, "" + _assetInfos.Count + " asset infos imported.");

                _assetInfos = null;

                ////////////////////////////////////////////////////////
                // Manufactures and product asset models

                foreach (var feature in _manufactures)
                {
                    var ci = new CIMIdentifiedObject(_g.ObjectManager);
                    ci.SetClass(feature.ClassName);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);

                    _g.CIMObjects.Add(ci);
                }

                Logger.Log(LogLevel.Info, "" + _manufactures.Count + " manufactures imported.");

                _manufactures = null;


                foreach (var feature in _productAssetModels)
                {
                    var ci = new CIMIdentifiedObject(_g.ObjectManager);
                    ci.SetClass(feature.ClassName);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);

                    _g.CIMObjects.Add(ci);
                }

                Logger.Log(LogLevel.Info, "" + _productAssetModels.Count + " product asset models imported.");

                _productAssetModels = null;


                ////////////////////////////////////////////////////////
                // Build nodes

                foreach (var feature in _buildNodes)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(feature.ClassName);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);
                }

                Logger.Log(LogLevel.Info, "" + _buildNodes.Count + " build nodes imported.");

                _buildNodes = null;




                ////////////////////////////////////////////////////////
                // Switches

                foreach (var feature in _switches)
                {
                  
                    


                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    if (!ci.ContainsPropertyValue("dax.ref.equipmentcontainertype"))
                        ci.SetPropertyValue("dax.ref.equipmentcontainertype", "Bay");

                    // handle disconnectinglink in classname
                    if (feature.ClassName.ToLower() == "disconnectinglink")
                    {
                        feature.ClassName = "Disconnector";
                        feature["cim.psrtype"] = "DisconnectingLink";
                    }

                    ci.SetClass(feature.ClassName);

                    if (ci.ClassType == CIMClassEnum.Disconnector)
                    {
                    }

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);



                    bool normalOpen = false;

                    if (feature.ContainsKey("dax.closed"))
                    {
                        string closedVal = feature["dax.closed"].ToString();
                        if (closedVal == "1")
                            ci.SetPropertyValue("cim.open", false);
                        else
                            ci.SetPropertyValue("cim.open", true);

                        feature.Remove("dax.closed");
                    }

                    if (feature.ContainsKey("dax.normalclosed"))
                    {
                        string closedVal = feature["dax.normalclosed"].ToString();
                        if (closedVal == "1")
                        {
                            ci.SetPropertyValue("cim.normalopen", false);
                            normalOpen = false;
                        }
                        else
                        {
                            ci.SetPropertyValue("cim.normalopen", true);
                            normalOpen = true;
                        }
                        feature.Remove("dax.normalclosed");
                    }

                    if (feature.ContainsKey("cim.normalopen"))
                    {
                        string normalopen = feature["cim.normalopen"].ToString();
                        if (normalopen.ToLower() == "inde")
                        {
                            normalOpen = false;
                        }
                        else if (normalopen.ToLower() == "ude")
                        {
                            normalOpen = true;
                        }
                        else
                        {
                            normalOpen = false;
                        }
                    }

                    if (normalOpen == false)
                        ci.SetPropertyValue("cim.normalopen", false);
                    else
                        ci.SetPropertyValue("cim.normalopen", true);


                    if (!feature.ContainsKey("cim.terminal.1"))
                    {
                        if (normalOpen == false)
                        {
                            AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                        }
                        else
                        {
                            AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci, 1);
                            AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci, 2);
                        }
                    }
                    else
                        TerminalBasedConnectivity_AddSwitchToGraph(feature, ci);


                }

                Logger.Log(LogLevel.Info, "" + _switches.Count + " switch devices imported.");

                _switches = null;


                ////////////////////////////////////////////////////////
                // Build Simple Junctions
                foreach (var feature in _buildSimpleJunctions)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.BuildSimpleJunction);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);

                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _buildSimpleJunctions.Count + " simple junctions imported.");

                _buildSimpleJunctions = null;


                ////////////////////////////////////////////////////////
                // Fault indicators
                foreach (var feature in _faultIndicators)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.FaultIndicatorExt);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);

                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _faultIndicators.Count + " Fault Indicators imported.");

                _faultIndicators = null;


                ////////////////////////////////////////////////////////
                // Current transformers
                foreach (var feature in _currentTransformers)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.CurrentTransformer);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);

                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _currentTransformers.Count + " Current Transformers imported.");

                _currentTransformers = null;


                ////////////////////////////////////////////////////////
                // Potential transformers
                foreach (var feature in _potentialTransformers)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.PotentialTransformer);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);
                }

                Logger.Log(LogLevel.Info, "" + _potentialTransformers.Count + " Potential Transformers imported.");

                _potentialTransformers = null;

                ////////////////////////////////////////////////////////
                // Protection Equipments (relays)

                foreach (var feature in _protectionEquipments)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.ProtectionEquipment);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);

                    var swRel = ci.GetPropertyValueAsString("cim.ref.protectiveswitch");

                    if (swRel != null)
                    {
                        var sw = _g.GetCIMObjectByMrid(swRel);

                        if (sw != null)
                        {

                        }
                    }
                }

                Logger.Log(LogLevel.Info, "" + _protectionEquipments.Count + " Protection Equipments (relays) imported.");

                _protectionEquipments = null;



                ////////////////////////////////////////////////////////
                // Power Transformers

                foreach (var feature in _transformers)
                {
                    var ci = new CIMPowerTransformer(_g.ObjectManager);
                    ci.SetPropertyValue("dax.ref.equipmentcontainertype", "Substation");
                    ci.SetClass(CIMClassEnum.PowerTransformer);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                    {
                        TerminalBasedConnectivity_AddObjectToGraph(feature, ci);

                        if (feature.ContainsKey("cim.numberofcustomers"))
                        {
                            int nCustomers = ci.GetPropertyValueAsInt("cim.numberofcustomers").Value;

                            AddCustomerObjectToPowerTransformer(ci, nCustomers);
                        }
                    }
                }

                Logger.Log(LogLevel.Info, "" + _transformers.Count + " power transformers imported.");

                ////////////////////////////////////////////////////////
                // Coils

                foreach (var feature in _coils)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.PetersenCoil);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        TerminalBasedConnectivity_AddObjectToGraph(feature, ci);

                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _coils.Count + " petersen coils imported.");

             
                ////////////////////////////////////////////////////////
                // Compensators

                foreach (var feature in _compensators)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.LinearShuntCompensator);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);

                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _compensators.Count + " LinearShuntCompensator imported.");


                ////////////////////////////////////////////////////////
                // Sync generators

                foreach (var feature in _sync_gens)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.SynchronousMachine);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);
                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _sync_gens.Count + " synchronous machines imported.");

                ////////////////////////////////////////////////////////
                // Async generators

                foreach (var feature in _async_gens)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.AsynchronousMachine);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);
                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _async_gens.Count + " asynchronous machines imported.");


                ////////////////////////////////////////////////////////
                // External Network Injections

                foreach (var feature in _extNetworkInjections)
                {
                    var ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.ExternalNetworkInjection);

                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);

                    if (!feature.ContainsKey("cim.terminal.1"))
                        AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                    else
                        AddVertexToGraph(feature["cim.terminal.1"].ToString(), ci);
                    //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);
                }

                Logger.Log(LogLevel.Info, "" + _extNetworkInjections.Count + " external network injections imported.");


                ////////////////////////////////////////////////////////
                // Energy consumers

                int consumerCounter = 0;
                foreach (var feature in _energyconsumers)
                {
                    if (feature.ContainsKey("cim.terminal.1") && feature.GetAttributeAsString("cim.terminal.1") != "0")
                    {
                        TerminalBasedConnectivity_AddSingleTerminalEquipmentToGraph(feature, true);
                        consumerCounter++;
                    }
                    else
                    {
                        var ci = new CIMConductingEquipment(_g.ObjectManager);
                        ci.SetClass(CIMClassEnum.EnergyConsumer);
                        MapCommonFields(feature, ci);
                        CopyAttributes(feature, ci);

                        // Only add if coordinates exists
                        if (feature.Coordinates != null && feature.Coordinates.Length == 1)
                        {
                            MapCoords(feature, ci);

                            AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ci);

                            consumerCounter++;
                        }
                        else
                            AddToInvalidCoordList(ci.ClassType.ToString(), ci.ExternalId);
                    }
                }

                Logger.Log(LogLevel.Info, "" + consumerCounter + " energy consumers imported.");

                _energyconsumers = null;





                ////////////////////////////////////////////////////////
                // Usage Points

                int usagePointCounter = 0;
                foreach (var feature in _usagePoints)
                {
                    if (feature.GetAttributeAsString("cim.networkconnectionpointid") == null)
                    {
                        Logger.Log(LogLevel.Warning, $"No cim.networkconnectionpointid found on usage point with id: {feature.GetAttributeAsString("cim.mrid")}");
                    }
                    else
                    {


                        var ci = new CIMIdentifiedObject(_g.ObjectManager);
                        ci.SetClass(CIMClassEnum.UsagePoint);
                        MapCommonFields(feature, ci);
                        CopyAttributes(feature, ci);

                        if (ci.mRID == Guid.Parse("061E2937-9614-4FA1-9180-3B7A82F70FF0"))
                        {

                        }

                        var ecRel = ci.GetPropertyValueAsString("cim.ref.energyconsumer");

                        if (ecRel != null)
                        {
                            var ec = _g.GetCIMObjectByName(ecRel);

                            if (ec != null)
                            {
                                var ecUsagePointList = ec.GetPropertyValue("usagepoints") as List<CIMIdentifiedObject>;

                                if (ecUsagePointList == null)
                                    ecUsagePointList = new List<CIMIdentifiedObject>();

                                ecUsagePointList.Add(ci);

                                ec.SetPropertyValue("usagepoints", ecUsagePointList);
                            }
                        }
                    }

                }

                Logger.Log(LogLevel.Info, "" + usagePointCounter + " usage points imported.");

                _usagePoints = null;

                ////////////////////////////////////////////////////////
                // Network Equipments

                foreach (var feature in _networkEquipments)
                {
                    CIMConductingEquipment ne = new CIMConductingEquipment(_g.ObjectManager);
                    ne.SetClass(feature.ClassName);

                    MapCommonFields(feature, ne);
                    CopyAttributes(feature, ne);
                    MapCoords(feature, ne);

                    if (feature.Coordinates != null && feature.Coordinates.Length == 1)
                    {
                        if (!feature.ContainsKey("cim.terminal.1"))
                            AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ne);
                        else
                            AddVertexToGraph(feature["cim.terminal.1"].ToString(), ne);
                        //var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, ne);
                    }
                    else if (feature.Coordinates != null && feature.Coordinates.Length > 1)
                    {
                        CoordBasedConnectivity_AddACLineSegmentToGraph(feature, ne);
                    }
                    else
                        //Logger.Log(LogLevel.Warning, "NetworkEquipment " + ne.ToString() + " has invalid coordinates. Will not be imported.");
                        AddToInvalidCoordList(ne.ClassType.ToString(), ne.ExternalId);
                }

                Logger.Log(LogLevel.Info, "" + _networkEquipments.Count + " network equipments imported.");

                _networkEquipments = null;

                sw1.Stop();
                Logger.Log(LogLevel.Debug, "Commit before precheckconnectivity: " + sw1.ElapsedMilliseconds + " milli seconds.");



                ////////////////////////////////////////////////////////
                // Busbar sections
                foreach (var feature in _busbars)
                {
                    if (feature.ContainsKey("cim.terminal.1") && feature.GetAttributeAsString("cim.terminal.1") != "0")
                        TerminalBasedConnectivity_AddSingleTerminalEquipmentToGraph(feature, true);
                    else
                        CoordBasedConnectivity_AddBusbarSectionToGraph(feature, true);
                }

                Logger.Log(LogLevel.Info, "" + _busbars.Count + " busbar sections imported.");

                _busbars = null;


                ////////////////////////////////////////////////////////
                // Edge connectors

                int reversing = 0;
                foreach (var feature in _edgeConnectors)
                {
                    CIMConnectivityNode cn = new CIMConnectivityNode(_g.ObjectManager);
                    cn.SetClass(CIMClassEnum.ConnectivityEdge);

                    MapCommonFields(feature, cn);
                    CopyAttributes(feature, cn);
                    MapCoords(feature, cn);

                    if (cn.ExternalId == "2759849")
                    {
                    }

                    // Reverse cables with wrong direction (the highest Y coordinat must be the first coordinate)
                    if (cn.Coords.Length == 4)
                    {
                        //
                        // Cable is pointing down
                        if (cn.Coords[1] < cn.Coords[3])
                        {
                            double x = cn.Coords[0];
                            double y = cn.Coords[1];
                            cn.Coords[0] = cn.Coords[2];
                            cn.Coords[1] = cn.Coords[3];
                            cn.Coords[2] = x;
                            cn.Coords[3] = y;

                            // JL: Det er vel nok at den skriver hvor mange den har reversed
                            //Logger.Log(LogLevel.Info, "Reversing cartographic cable: " + ci.ExternalId);
                            reversing++;
                        }
                        //if (CIMHelper.equalWithTolerance(ci.Coords[1], ci.Coords[3], GetTolerance()))
                        //  Logger.Log(LogLevel.Info, "Cartographic cable: " + ci.ExternalId + " has no horizontal extent.");

                    }


                    ProcessEdgeConnector(feature, cn, true);
                }

                Logger.Log(LogLevel.Info, "Reversed " + reversing + " cables");
                Logger.Log(LogLevel.Info, "" + _edgeConnectors.Count + " edge connectors imported.");

                _edgeConnectors = null;


                ////////////////////////////////////////////////////////
                // AC line segments

                foreach (var feature in _aclinesegments)
                {
                    CIMConductingEquipment ci = new CIMConductingEquipment(_g.ObjectManager);
                    ci.SetClass(CIMClassEnum.ACLineSegment);


                    MapCommonFields(feature, ci);
                    CopyAttributes(feature, ci);
                    MapCoords(feature, ci);


                    // debug
                    if (ci.mRID == Guid.Parse("43E2A656-B2AA-428B-8018-F540ACF3A724"))
                    {
                    }


                    if (feature.Coordinates != null && feature.Coordinates.Length > 1)
                    {
                        if (feature.ContainsKey("cim.terminal.1"))
                            TerminalBasedConnectivity_AddACLineSegmentToGraph(feature, ci);
                        else
                            CoordBasedConnectivity_AddACLineSegmentToGraph(feature, ci);
                    }
                    else
                        //Logger.Log(LogLevel.Warning, "ACLineSegment " + ci.ToString() + " has invalid coordinates. Will not be imported.");
                        AddToInvalidCoordList(ci.ClassType.ToString(), ci.ExternalId);
                }

                Logger.Log(LogLevel.Info, "" + _aclinesegments.Count + " AC line segments imported.");

                _aclinesegments = null;


                ////////////////////////////////////////////////////////
                // Network connection points

                int networkConnectionPointCount = 0;
                foreach (var feature in _networkConnectionPoints)
                {
                    // Only add if coordinates exists
                    if (feature.Coordinates != null && feature.Coordinates.Length == 1)
                    {
                        var vertexId = FindVertexId(feature.Coordinates[0].X, feature.Coordinates[0].Y);

                        if (vertexId > 0)
                        {
                            var ci = new CIMConductingEquipment(_g.ObjectManager);
                            ci.SetClass(CIMClassEnum.NetworkConnectionPoint);
                            MapCommonFields(feature, ci);
                            CopyAttributes(feature, ci);
                            MapCoords(feature, ci);

                            ci.SetPropertyValue("vertexid", vertexId);
                        }
                        else
                        {
                            Logger.Log(LogLevel.Warning, $"No vertex found for network connection point with id: {feature.GetAttributeAsString("cim.mrid")}");
                        }

                        networkConnectionPointCount++;
                    }

                }

                Logger.Log(LogLevel.Info, "" + networkConnectionPointCount + " network connection points imported.");

                _networkConnectionPoints = null;



                ////////////////////////////////////////////////////////
                // Location addresses

                foreach (var feature in _locationAddresses)
                {
                    CIMConductingEquipment la = new CIMConductingEquipment(_g.ObjectManager);
                    la.SetClass(feature.ClassName);

                    MapCommonFields(feature, la);
                    CopyAttributes(feature, la);
                    MapCoords(feature, la);

                    if (feature.Coordinates != null && feature.Coordinates.Length == 1)
                    {
                        var vertexId = AddVertexToGraph(feature.Coordinates[0].X, feature.Coordinates[0].Y, la);
                    }
                    else if (feature.Coordinates != null && feature.Coordinates.Length > 1)
                    {
                        CoordBasedConnectivity_AddACLineSegmentToGraph(feature, la);
                    }
                    else
                        //Logger.Log(LogLevel.Warning, "LocationAddress " + la.ToString() + " has invalid coordinates. Will not be imported.");
                        AddToInvalidCoordList(la.ClassType.ToString(), la.ExternalId);
                }

                Logger.Log(LogLevel.Info, "" + _locationAddresses.Count + " location addresses equipments imported.");

                _locationAddresses = null;

                if (_invalidCoordsLists.Count > 0)
                {
                    foreach (var dublist in _invalidCoordsLists)
                    {
                        string invalidCoordObjIds = "";
                        foreach (var dub in dublist.Value)
                        {
                            invalidCoordObjIds += dub;

                            if (invalidCoordObjIds != "")
                                invalidCoordObjIds += ",";

                        }

                        Logger.Log(LogLevel.Warning, dublist.Key + "(" + dublist.Value.Count + ") invalid coords object list: " + invalidCoordObjIds);
                    }
                }


                // Fix connectivity
                if (doPreCheck())
                {
                    Logger.Log(LogLevel.Debug, "CIMGraphWriter: Pre-checking GIS connectivity for errors...");
                    PreCheckConnectivity();
                }
                else
                    Logger.Log(LogLevel.Debug, "CIMGraphWriter: Skipping pre-checking GIS connectivity for errors...");

                RunAllGraphProcessors(_transConf);




                Logger.Log(LogLevel.Info, "CIMGraphWriter: Finish loading data into in-memory CIM graph.");
                graphLoadedSuccessfull = true;

                // FIX: Waiting for NRGi to say go... on this
                //EmitGraphToBus(_g).Wait();    

                string summaryFileName = GetSummaryFileName();
                if (summaryFileName != null && summaryFileName.Length > 0)
                {
                    String summary = _g.TableLogger.DumpSummary(LogLevel.Info);

                    try
                    {
                        System.IO.File.WriteAllText("summary.txt", summary);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogLevel.Warning, e.StackTrace);
                    }

                }

            }
            finally
            {
               
            }

        }


        /*
        async private Task EmitGraphToBus(CIMGraph graph)
        {
            
            // Emit CIM XML to bus
            Logger.Log(LogLevel.Debug, "CIMGraphWriter: Trying to emit CIM XML to the bus...");

            using (var activator = new BuiltinHandlerActivator())
            {
                try
                {
                    var bus = Configure
                        .With(activator)
                        .AsClient()
                        .Options(o =>
                        {
                            o.EnableDataBus().StoreInSqlServer("RebusSagas", "DataBus");
                          //  .StoreInFileSystem(@"c:\temp\cim\databus");
                        })
                        .Start();

                    var serializer = _transConf.InitializeSerializer("NetSam") as IDAXSerializeable;

                    var cimXmlBytes = serializer.Serialize(CIMMetaDataManager.Repository, graph.CIMObjects, graph);

                    var attachment = await bus.Advanced.DataBus.CreateAttachment(new MemoryStream(cimXmlBytes));

                    var message = new NewEquipmentSnapshotCreated
                    {
                        AttachmentId = attachment.Id
                    };

                    await bus.Publish(message);

                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warning, "CIMGraphWriter: Cannot emit CIM XML to Rebus. " + ex.Message);
                }
            }
            

        }
        */

        private void AddCustomerObjectToPowerTransformer(CIMConductingEquipment tr, int numberOfCustomers)
        {
            var st = tr.EquipmentContainerRef;

            if (st != null && st.GetPSRType(CIMMetaDataManager.Repository) == "SecondarySubstation")
            {
                int nextDerivedGuidCounter = 25;
                var bay = new CIMEquipmentContainer(_g.ObjectManager) { Name = "LV Bay " + tr.Name, mRID = GUIDHelper.CreateDerivedGuid(tr.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.Bay, VoltageLevel = 400, EquipmentContainerRef = st};

                nextDerivedGuidCounter += 1;
                st.Children.Add(bay);

                // Create disconnecting link between power transformer and energy consumber object
                var disLink = new CIMConductingEquipment(_g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(tr.mRID, nextDerivedGuidCounter, true), VoltageLevel = 400, ClassType = CIMClassEnum.Disconnector, EquipmentContainerRef = bay };
                disLink.SetPSRType(CIMMetaDataManager.Repository, "DisconnectingLink");
                nextDerivedGuidCounter += 3;
                disLink.SetPropertyValue("cim.normalopen", false);
                bay.Children.Add(disLink);

                // Connect dislink to transformer
                var t2 = tr.Terminals.ToList()[1];
                t2.ConnectivityNode.AddNeighbour(disLink);
                disLink.AddNeighbour(t2.ConnectivityNode);

                // Create connectivity node between dislink and energy consumer
                CIMConnectivityNode cn = new CIMConnectivityNode(_g.ObjectManager) { Name = "auto generated", mRID = GUIDHelper.CreateDerivedGuid(tr.mRID, nextDerivedGuidCounter, true), ClassType = CIMClassEnum.ConnectivityNode, VoltageLevel = 400 };
                nextDerivedGuidCounter += 1;
                cn.AddNeighbour(disLink);
                disLink.AddNeighbour(cn);

                // Create energy consumer object
                var ec = new CIMConductingEquipment(_g.ObjectManager) { Name = "Transformer Customers", mRID = GUIDHelper.CreateDerivedGuid(tr.mRID, nextDerivedGuidCounter, true), VoltageLevel = 400, ClassType = CIMClassEnum.EnergyConsumer };
                ec.SetPropertyValue("cim.numberofcustomers", numberOfCustomers);
                ec.AddNeighbour(cn);
                cn.AddNeighbour(ec);

            }

        }

        private void RunAllGraphProcessors(TransformationConfig config)
        {
            System.Diagnostics.Stopwatch sw1 = new System.Diagnostics.Stopwatch();

            var configSettings = Configuration.GetConfiguration();

            string[] runProcessors = null;

            if (configSettings != null && configSettings["RunProcessors"] != null)
            {
                string runProcessorsParam = configSettings["RunProcessors"];
                runProcessors = runProcessorsParam.Replace(" ","").ToLower().Split(',');
                Logger.Log(LogLevel.Debug, "RunAllGraphProcessors: Found param: " + runProcessorsParam);
            }

            sw1.Start();
            if (config.GraphProcessors != null)
            {
                foreach (var graphProcessorConfig in config.GraphProcessors)
                {
                    bool run = true;
                    var proc = config.InitializeGraphProcessor(graphProcessorConfig);

                    if (runProcessors != null)
                    {
                        run = false;

                        if (runProcessors.Contains(graphProcessorConfig.ClassName.ToLower()))
                            run = true;
                    }

                    if (run)
                    {
                        Logger.Log(LogLevel.Info, "Running " + ((IGraphProcessor)proc).GetType().Name + "...");
                        ((IGraphProcessor)proc).Run(_g, _g.TableLogger);
                    }
                }
            }
            sw1.Stop();
            Logger.Log(LogLevel.Debug, "RunAllGraphProcessors: " + sw1.ElapsedMilliseconds + " milli seconds.");

            if (_g.StatVertexOverlaps > 0)
            {
                Logger.Log(LogLevel.Warning, "" + _g.StatVertexOverlaps + " object overlaps!");
            }

            if (_g.StatNameDublicates.Count > 0)
            {
                foreach (var dublist in _g.StatNameDublicates)
                {
                    string dublicates = "";
                    foreach (var dub in dublist.Value)
                    {
                        dublicates += dub;

                        if (dublicates != "")
                            dublicates += ",";

                    }

                    Logger.Log(LogLevel.Warning, dublist.Key + "(" + dublist.Value.Count + ") name dublicates: " + dublicates);
                }
            }


            Logger.Log(LogLevel.Info, "CIMGraphWriter: Finish running graph processors. Graph ready for use.");
        }

        private bool doPreCheck()
        {
            if (doPrecheckconnectivity == null)
                return false;
            return doPrecheckconnectivity.ToLower().Equals("yes");
        }

        public string GetResult()
        {
            throw new NotImplementedException();
        }

        public CIMGraph GetCIMGraph()
        {
            return _g;
        }

        public ConfigParameter GetParameterByName(string name)
        {
            throw new NotImplementedException();
        }

        #endregion

        public void DoNotLogToTable()
        {
            Logger.Log(LogLevel.Info, "Logging to database table disabled.");

            logTableDbConnectionString = null;
            logTableName = null;
        }

        public void DoNotRunPreCheckConnectivity()
        {
            Logger.Log(LogLevel.Info, "Pre check connectivity disabled.");
            doPrecheckconnectivity = null;
        }


        private void AddMetaDataAttribute(DAXMetaData metaData, string className, string fieldName)
        {
            DAXAttributeDef attrDef = metaData.AddOrGetAttributeDefinition(className, fieldName, fieldName);
            attrDef.AttributeType = DAXAttributeType.String;
            attrDef.Length = 255;
        }

        private void AddCommonMetaDataAttributes(DAXMetaData metaData, string className)
        {
            AddMetaDataAttribute(metaData, className, "dax.externalid");
            AddMetaDataAttribute(metaData, className, "cim.mrid");
            AddMetaDataAttribute(metaData, className, "cim.psrtype");
            AddMetaDataAttribute(metaData, className, "dax.voltagelevel");
            AddMetaDataAttribute(metaData, className, "cim.name");
            AddMetaDataAttribute(metaData, className, "cim.description");
            AddMetaDataAttribute(metaData, className, "dax.ref.equipmentcontainertype");
            AddMetaDataAttribute(metaData, className, "cim.ref.equipmentcontainer");
        }

        private void MapCoords(DAXFeature feature, CIMIdentifiedObject obj)
        {
            int coordIndex = 0;
            int destCoordIndex = 0;
            if (feature.Coordinates != null)
            {
                obj.Coords = new double[feature.Coordinates.Length * 2];

                foreach (var coord in feature.Coordinates)
                {
                    obj.Coords[destCoordIndex] = Math.Round(feature.Coordinates[coordIndex].X, paramRoundDecimals);
                    obj.Coords[destCoordIndex + 1] = Math.Round(feature.Coordinates[coordIndex].Y, paramRoundDecimals);
                    coordIndex++;
                    destCoordIndex = destCoordIndex + 2;
                }
            } else
            {
                string SDfg = "sdhfg";
            }
        }

        private bool MapCoordsBusbar(DAXFeature feature, CIMIdentifiedObject obj, double tolerance)
        {
            obj.Coords = new double[4];
            bool notmalformed = true;

            if (feature.Coordinates != null && feature.Coordinates.Length > 0)
            {
                double maxx = feature.Coordinates[0].X;
                double minx = feature.Coordinates[0].X;

                double y = feature.Coordinates[0].Y;

                foreach (var coord in feature.Coordinates)
                {
                    maxx = Math.Max(coord.X, maxx);
                    minx = Math.Min(coord.X, minx);

                    if (Math.Abs(y - coord.Y) > tolerance)
                        notmalformed = false;

                }
                obj.Coords[0] = minx;
                obj.Coords[1] = y;
                obj.Coords[2] = maxx;
                obj.Coords[3] = y;
            }
            else
                return false;
            return notmalformed;
        }

        private void MapCommonFields(DAXFeature feature, CIMIdentifiedObject cimObj)
        {
            // ExternalId
            if (feature.ContainsKey("dax.externalid"))
            {
                cimObj.ExternalId = feature.GetAttributeAsString("dax.externalid");
                feature.Remove("dax.externalid");
            }

            // mRID
            if (feature.ContainsKey("cim.mrid"))
            {
                cimObj.mRID = Guid.Parse(feature.GetAttributeAsString("cim.mrid"));
                feature.Remove("cim.mrid");
            }


            // Voltage level
            if (feature.ContainsKey("dax.voltagelevel"))
            {
                int voltageLevel = 0;
                if (!Int32.TryParse(feature.GetAttributeAsString("dax.voltagelevel"), out voltageLevel))
                    Logger.Log(LogLevel.Warning, "Cannot parse voltagelevel='" + feature.GetAttributeAsString("dax.voltagelevel") + "' processing CIM object " + cimObj.IdString());

                cimObj.VoltageLevel = voltageLevel;
                feature.Remove("dax.voltagelevel");
            }

            // Name
            if (feature.ContainsKey("cim.name"))
            {
                cimObj.Name = feature.GetAttributeAsString("cim.name");
                feature.Remove("cim.name");
            }


            // Description
            if (feature.ContainsKey("cim.description"))
            {
                cimObj.Description = feature.GetAttributeAsString("cim.description");
                feature.Remove("cim.description");
            }

            // PSRType
            string psrType = feature.GetAttributeAsString("cim.psrtype");

            if (psrType != null)
            {
                cimObj.SetPSRType(CIMMetaDataManager.Repository, psrType);
                feature.Remove("cim.psrtype");
            }
        }

        private void CopyAttributes(DAXFeature feature, CIMIdentifiedObject cimObj)
        {
            foreach (var a in feature)
            {
                cimObj.SetPropertyValue(a.Key, a.Value);
            }
        }

        private void ProcessEdgeConnector(DAXFeature feat, CIMIdentifiedObject cimObj, bool checkCoordinates = false)
        {
            int fromVertexId = -1;
            int toVertexId = -1;

            var x1 = feat.Coordinates[0].X;
            var y1 = feat.Coordinates[0].Y;
            var x2 = feat.Coordinates[feat.Coordinates.Length - 1].X;
            var y2 = feat.Coordinates[feat.Coordinates.Length - 1].Y;

            if (!feat.ContainsKey("cim.terminal.1"))
                fromVertexId = ProcessEdgeConnectorVertex(feat, cimObj, x1, y1, x2, y2, 1);
            else
                fromVertexId = ProcessEdgeConnectorVertex(feat, cimObj, feat["cim.terminal.1"].ToString());

            if (!feat.ContainsKey("cim.terminal.2"))
                toVertexId = ProcessEdgeConnectorVertex(feat, cimObj, x2, y2, x1, y1, 2);
            else
                fromVertexId = ProcessEdgeConnectorVertex(feat, cimObj, feat["cim.terminal.2"].ToString());

            if (checkCoordinates)
            {
                if (feat.Coordinates[feat.Coordinates.Length - 1].Y > feat.Coordinates[0].Y)
                {
                    var tempVertex = toVertexId;
                    toVertexId = fromVertexId;
                    fromVertexId = tempVertex;
                    cimObj.coordFlipped = true;
                }
            }

            _g.AddCIMObjectToEdge(fromVertexId, toVertexId, cimObj);
        }

        private int ProcessEdgeConnectorVertex(DAXFeature feat, CIMIdentifiedObject cimObj, double x, double y, double otherEndX, double otherEndY, int no)
        {
            int vertexId = 0;

            int normalVertexId = FindVertexId(x, y);

            // Normal vertex
            if (normalVertexId > 0)
                vertexId = normalVertexId;
            else
            {
                // Check om det er tale om åben bryder
                int openSwitchVertexId = FindVertexId(x, y, 1);

                if (openSwitchVertexId > 0)
                {
                    // Hvis pågældende vertex er højest (dvs. tegnet oven fra og ned), da forbind til terminal 2
                    if (y > otherEndY)
                        vertexId = FindVertexId(x, y, 2);
                    // Ellers forbind til terminal 1
                    else
                        vertexId = openSwitchVertexId;
                }
                else
                // Create new vertex
                {
                    var cn = new CIMConnectivityNode(_g.ObjectManager);

                    //cn.mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, no);

                    cn.ExternalId = cimObj.ExternalId;

                    if (cn.ExternalId == "2759849")
                    {
                    }

                    CopyAttributes(feat, cn);
                    vertexId = AddVertexToGraph(x, y, cn);
                }
            }

            return vertexId;
        }

        private int ProcessEdgeConnectorVertex(DAXFeature feat, CIMIdentifiedObject cimObj, string cnId)
        {
            int vertexId = 0;

            int normalVertexId = FindVertexId(cnId);

            // Normal vertex
            if (normalVertexId > 0)
                vertexId = normalVertexId;
            else
            {
                // Check om det er tale om åben bryder
                int openSwitchVertexId = FindVertexId(cnId, 1);

                if (openSwitchVertexId > 0)
                {
                    vertexId = openSwitchVertexId;
                }
                else
                // Create new vertex
                {
                    var cn = new CIMConnectivityNode(_g.ObjectManager);

                    cn.ExternalId = cimObj.ExternalId;

                    if (cn.ExternalId == "2759849")
                    {
                    }

                    CopyAttributes(feat, cn);
                    vertexId = AddVertexToGraph(cnId, cn);
                }
            }

            return vertexId;
        }

        private void TerminalBasedConnectivity_AddACLineSegmentToGraph(DAXFeature feat, CIMConductingEquipment acLineSegment, bool checkCoordinates = false)
        {
            // From CN
            var fromCnId = feat.GetAttributeAsString("cim.terminal.1");
            var fromVertextId = TerminalBasedConnectivity_CreateCnIfNotExists(fromCnId, acLineSegment.ExternalId);
            var fromCn = _g.GetCIMObjectByVertexId(fromVertextId) as CIMConnectivityNode;

            // To CN
            var toCnId = feat.GetAttributeAsString("cim.terminal.2");
            var toVertextId = TerminalBasedConnectivity_CreateCnIfNotExists(toCnId, acLineSegment.ExternalId);
            var toCn = _g.GetCIMObjectByVertexId(toVertextId) as CIMConnectivityNode;

            // ACLS
            _g.AddCIMObjectToEdge(fromVertextId, toVertextId, acLineSegment);
        }

        private void TerminalBasedConnectivity_AddSingleTerminalEquipmentToGraph(DAXFeature feat, bool checkCoordinates = false)
        {
            if (feat.ContainsKey("cim.terminal.1"))
            {
                var ce = new CIMConductingEquipment(_g.ObjectManager);
                ce.SetClass(feat.ClassName);
                MapCommonFields(feat, ce);
                CopyAttributes(feat, ce);

                if (feat.Coordinates != null && feat.Coordinates.Length == 1)
                {
                    MapCoords(feat, ce);
                }

                
                if (ce.mRID == Guid.Parse("34916164-07d6-4955-813c-827013699867"))
                {

                }

                // CN
                var cnId = feat.GetAttributeAsString("cim.terminal.1");

                if (cnId == "394985")
                {

                }

                var fromVertextId = TerminalBasedConnectivity_CreateCnIfNotExists(cnId, ce.ExternalId);
                var cn = _g.GetCIMObjectByVertexId(fromVertextId) as CIMConnectivityNode;



                var vertexId = _g.AddCIMObjectToVertex(ce);
                TerminalBasedConnectity_Pair(ce, cn);
            }
            else
                throw new Exception("Conducting equipment has no cim.terminal.1 " + feat.GetStringDetailed());
        }

        private void TerminalBasedConnectivity_AddSwitchToGraph(DAXFeature feat, CIMConductingEquipment sw)
        {
            bool? normalOpen = feat["cim.normalopen"] as bool?;

            // From CN
            var fromCnId = feat.GetAttributeAsString("cim.terminal.1");
            var fromVertextId = TerminalBasedConnectivity_CreateCnIfNotExists(fromCnId, sw.ExternalId);
            var fromCn = _g.GetCIMObjectByVertexId(fromVertextId) as CIMConnectivityNode;

            // To CN
            var toCnId = feat.GetAttributeAsString("cim.terminal.2");
            var toVertextId = TerminalBasedConnectivity_CreateCnIfNotExists(toCnId, sw.ExternalId);
            var toCn = _g.GetCIMObjectByVertexId(toVertextId) as CIMConnectivityNode;
            
            var vertexId = _g.AddCIMObjectToVertex(sw);

            TerminalBasedConnectity_Pair(sw, fromCn);
            TerminalBasedConnectity_Pair(sw, toCn);

        }

        private void TerminalBasedConnectivity_AddObjectToGraph(DAXFeature feat, CIMConductingEquipment ci)
        {
            // From CN
            var fromCnId = feat.GetAttributeAsString("cim.terminal.1");
            var fromVertextId = TerminalBasedConnectivity_CreateCnIfNotExists(fromCnId, ci.ExternalId);
            var fromCn = _g.GetCIMObjectByVertexId(fromVertextId) as CIMConnectivityNode;

            // To CN
            var toCnId = feat.GetAttributeAsString("cim.terminal.2");
            var toVertextId = TerminalBasedConnectivity_CreateCnIfNotExists(toCnId, ci.ExternalId);
            var toCn = _g.GetCIMObjectByVertexId(toVertextId) as CIMConnectivityNode;

            if (toCnId != fromCnId)
            {

                var vertexId = _g.AddCIMObjectToVertex(ci);

                TerminalBasedConnectity_Pair(ci, fromCn);
                TerminalBasedConnectity_Pair(ci, toCn);
            }
            else
            {
                Logger.Log(LogLevel.Warning, "Conducting equipment terminals point to the same connectivity node. Will be skipped! " + feat.ClassName + " mRID=" + ci.mRID);
            }

        }

        private int TerminalBasedConnectivity_CreateCnIfNotExists(string cnId, string externalId)
        {
            var vertexId = FindVertexId(cnId);
            if (vertexId == 0)
            {
                // create cn at lonely end
                var cn = new CIMConnectivityNode(_g.ObjectManager);
                cn.SetClass(CIMClassEnum.ConnectivityNode);
                cn.ExternalId = externalId;

                if (cnId != "0")
                    cn.mRID = Guid.Parse("ab4d5a94-bbe0-4fec-a3d1-" + cnId.PadLeft(12, '0'));
                else
                    cn.mRID = Guid.NewGuid();

                if (cnId != "0")
                {
                    vertexId = AddVertexToGraph(cnId, cn);
                    AddVertexIdToIndex(vertexId, cnId);
                }
                else // løs ende
                {
                    vertexId = AddVertexToGraph(cn.mRID.ToString(), cn);
                }
            }

            return vertexId;
        }

        private void TerminalBasedConnectity_Pair(CIMConductingEquipment ci, CIMConnectivityNode cn)
        {
            cn.AddNeighbour(ci);
            ci.AddNeighbour(cn);
        }

        private void CoordBasedConnectivity_AddACLineSegmentToGraph(DAXFeature feat, CIMConductingEquipment acLineSegment, bool checkCoordinates = false)
        {
            int fromVertexId = -1;
            int toVertexId = -1;

            if (!feat.ContainsKey("cim.terminal.1"))
                fromVertexId = FindVertexId(feat.Coordinates[0].X, feat.Coordinates[0].Y);
            else
                fromVertexId = FindVertexId(feat["cim.terminal.1"].ToString());

            if (!feat.ContainsKey("cim.terminal.2"))
                toVertexId = FindVertexId(feat.Coordinates[feat.Coordinates.Length - 1].X, feat.Coordinates[feat.Coordinates.Length - 1].Y);
            else
                toVertexId = FindVertexId(feat["cim.terminal.2"].ToString());

            // Manage from point

            if (fromVertexId > 0)
            {
                var feederObj = CoordBasedConnectivity_GetFeederVertexFromACLineSegment(acLineSegment, fromVertexId, toVertexId);

                if (feederObj != null)
                {
                    var cn = _g.GetCIMObjectByVertexId(fromVertexId);

                    // Add feeder to substation
                    _g.AddFeeder(feederObj, cn);
                    _g.ObjectManager.AdditionalObjectAttributes(acLineSegment).IsFeederExitObject = true;
                }
            }
            else
            {
                var cn = new CIMConnectivityNode(_g.ObjectManager);
                cn.SetClass(CIMClassEnum.ConnectivityNode);
                cn.ExternalId = acLineSegment.ExternalId;

                if (!feat.ContainsKey("cim.terminal.1"))
                    fromVertexId = AddVertexToGraph(feat.Coordinates[0].X, feat.Coordinates[0].Y, cn);
                else
                    fromVertexId = AddVertexToGraph(feat["cim.terminal.1"].ToString(), cn);

                //fromVertexId = AddVertexToGraph(feat.Coordinates[0].X, feat.Coordinates[0].Y, cn);
            }

            // Manage to point
            if (toVertexId > 0)
            {
                var feederObj = CoordBasedConnectivity_GetFeederVertexFromACLineSegment(acLineSegment, toVertexId, fromVertexId);
                if (feederObj != null)
                {
                    // Add feeder to substation
                    var cn = _g.GetCIMObjectByVertexId(toVertexId);

                    _g.AddFeeder(feederObj, cn);
                    _g.ObjectManager.AdditionalObjectAttributes(acLineSegment).IsFeederExitObject = true;
                }
            }
            else
            {
                var cn = new CIMConnectivityNode(_g.ObjectManager);
                cn.SetClass(CIMClassEnum.ConnectivityNode);
                cn.ExternalId = acLineSegment.ExternalId;

                if (!feat.ContainsKey("cim.terminal.2"))
                    toVertexId = AddVertexToGraph(feat.Coordinates[feat.Coordinates.Length - 1].X, feat.Coordinates[feat.Coordinates.Length - 1].Y, cn);
                else
                    toVertexId = AddVertexToGraph(feat["cim.terminal.2"].ToString(), cn);

                // toVertexId = AddVertexToGraph(feat.Coordinates[feat.Coordinates.Length - 1].X, feat.Coordinates[feat.Coordinates.Length - 1].Y, cn);
            }

            _g.AddCIMObjectToEdge(fromVertexId, toVertexId, acLineSegment);
        }
        
        private CreateFeederInfo CoordBasedConnectivity_GetFeederVertexFromACLineSegment(CIMConductingEquipment acLineSegment, int acLineSegmentVertexIdToCheck, int acLineSegmentOtherEndVertexId)
        {
            // hack virkede ikke
            //if (acLineSegment.ContainsPropertyValue("cim.ref.equipmentcontainer"))
            //    return null;

            var endToCheckCimObj = _g.GetCIMObjectByVertexId(acLineSegmentVertexIdToCheck);
            var otherEndCimObj = _g.GetCIMObjectByVertexId(acLineSegmentOtherEndVertexId);

            if (endToCheckCimObj != null)
            {
                // Find connector (kartografik kabel) som sidder på knudepunktet tilhørende kablet
                List<CIMIdentifiedObject> connectors = endToCheckCimObj.GetNeighbours(CIMClassEnum.ConnectivityNode, CIMClassEnum.ConnectivityEdge);

                List<CIMIdentifiedObject> connectorsWithBay = new List<CIMIdentifiedObject>();

                // Remove connectors that has no bay
                foreach (var con in connectors)
                {
                    if (con.EquipmentContainerRef != null && con.EquipmentContainerRef.ClassType == CIMClassEnum.Bay)
                        connectorsWithBay.Add(con);
                }

                // Check if connector is placed in bay
                if (connectorsWithBay.Count > 0 && connectorsWithBay[0].EquipmentContainerRef != null && connectorsWithBay[0].EquipmentContainerRef.ClassType == CIMClassEnum.Bay)
                {
                    CIMIdentifiedObject connector = connectorsWithBay[0];
                    var bay = connector.EquipmentContainerRef;

                    // Check if bay is places in station
                    if (bay.EquipmentContainerRef != null && bay.EquipmentContainerRef.ClassType == CIMClassEnum.Substation)
                    {
                        var substation = bay.EquipmentContainerRef;


                        // Hvis der er tale om trafo forbindelseskabel
                        if (otherEndCimObj != null && otherEndCimObj.ClassType == CIMClassEnum.PowerTransformer)
                        {
                            // Hvis det er den øverste, så lav feeder point
                            if (bay.VoltageLevel == substation.VoltageLevel)
                            {
                                return new CreateFeederInfo() { ConnectivityNode = connector, IsTransformerFeeder = true, Transformer = otherEndCimObj };
                            }
                        }
                        else
                        {
                            // Check if bay voltage level is lower than substation voltage level (then is's feeder)
                            if (bay.VoltageLevel < substation.VoltageLevel)
                            {
                                return new CreateFeederInfo() { ConnectivityNode = connector, IsTransformerFeeder = false };
                            }
                        }
                    }
                   
                }
                // Måske Trafo på horn
                else
                {
                    // Hvis der er tale om trafo forbindelseskabel
                    if (endToCheckCimObj != null && endToCheckCimObj.EquipmentContainerRef != null && endToCheckCimObj.ClassType == CIMClassEnum.PowerTransformer)
                    {
                        // Hvis det er den øverste, så lav feeder point
                        if (endToCheckCimObj.EquipmentContainerRef.VoltageLevel == acLineSegment.VoltageLevel)
                        {
                            //return new CreateFeederInfo() { ConnectivityNode = acLineSegment, IsTransformerFeeder = true, Transformer = endToCheckCimObj };
                        }
                    }
                }

            }

            return null;
        }

        private void CoordBasedConnectivity_AddBusbarSectionToGraph(DAXFeature feat, bool checkCoordinates = false)
        {
            var busbar = new CIMConductingEquipment(_g.ObjectManager);
            busbar.SetClass("BusbarSection");
            MapCommonFields(feat, busbar);
            CopyAttributes(feat, busbar);

            MapCoordsBusbar(feat, busbar, GetTolerance());
        
            // Create busbar vertex
            int busBarVertexId = _g.AddCIMObjectToVertex(busbar);

            string type = busbar.GetPropertyValueAsString("dax.ref.equipmentcontainertype");

            // SK 2
            if (busbar.mRID == Guid.Parse("be37cd82-da88-4348-a67a-19ab150299bc"))
            {
            }

            // SK 3
            if (busbar.mRID == Guid.Parse("b22d7e47-8988-4356-89c0-683cbbae71a8"))
            {
            }


            // Search switches FROM END
            int fromEndSearchVertexId = -1;

            if (FindVertexId(feat.Coordinates[0].X, feat.Coordinates[0].Y) > 0)
                fromEndSearchVertexId = FindVertexId(feat.Coordinates[0].X, feat.Coordinates[0].Y);

            // Search open switches also
            if (FindVertexId(feat.Coordinates[0].X, feat.Coordinates[0].Y, 1) > 0)
                fromEndSearchVertexId = FindVertexId(feat.Coordinates[0].X, feat.Coordinates[0].Y, 1);

            if (fromEndSearchVertexId > 0)
            {
                var cimObjFound = _g.GetCIMObjectByVertexId(fromEndSearchVertexId);

                if (cimObjFound.ClassType == CIMClassEnum.Disconnector || cimObjFound.ClassType == CIMClassEnum.LoadBreakSwitch || cimObjFound.ClassType == CIMClassEnum.Breaker)
                {
                    // Create a connectivity edge between switch and busbar
                    CIMConnectivityNode ce = new CIMConnectivityNode(_g.ObjectManager);
                    ce.SetClass(CIMClassEnum.ConnectivityEdge);
                    //ce.mRID = GUIDHelper.CreateDerivedGuid(busbar.mRID, 1);

                    // Connect switch to cn
                    cimObjFound.AddNeighbour(ce);
                    ce.AddNeighbour(cimObjFound);

                    // Hack. Set switch parent to same parrent as busbar if null. This because switch many times has no parent in GIS.
                    if (cimObjFound.EquipmentContainerRef == null)
                    {
                        cimObjFound.EquipmentContainerRef = busbar.EquipmentContainerRef;
                        if (busbar.EquipmentContainerRef != null)
                            busbar.EquipmentContainerRef.Children.Add(cimObjFound);
                    }

                    // Hack. Set switch voltage level to same as busbar if null.
                    if (cimObjFound.VoltageLevel == 0)
                    {
                        cimObjFound.VoltageLevel = busbar.VoltageLevel;
                    }

                    // Connect busbar to cn
                    busbar.AddNeighbour(ce);
                    ce.AddNeighbour(busbar);
                }
                else
                {
                    Logger.Log(LogLevel.Warning, "Busbar mRID=" + busbar.mRID + " overlaps another component " + cimObjFound.ToString() + " that is not a disconnector, loadbreaker og breaker");
                }
            }


            // Search switches TO END
            int toEndSearchVertexId = -1;

            if (FindVertexId(feat.Coordinates[feat.Coordinates.Length - 1].X, feat.Coordinates[feat.Coordinates.Length - 1].Y) > 0)
                toEndSearchVertexId = FindVertexId(feat.Coordinates[feat.Coordinates.Length - 1].X, feat.Coordinates[feat.Coordinates.Length - 1].Y);

            // Search open switches also
            if (FindVertexId(feat.Coordinates[feat.Coordinates.Length - 1].X, feat.Coordinates[feat.Coordinates.Length - 1].Y, 1) > 0)
                toEndSearchVertexId = FindVertexId(feat.Coordinates[feat.Coordinates.Length - 1].X, feat.Coordinates[feat.Coordinates.Length - 1].Y, 1);

            if (toEndSearchVertexId > 0)
            {
                var cimObjFound = _g.GetCIMObjectByVertexId(toEndSearchVertexId);

                if (cimObjFound.ClassType == CIMClassEnum.Disconnector || cimObjFound.ClassType == CIMClassEnum.LoadBreakSwitch || cimObjFound.ClassType == CIMClassEnum.Breaker)
                {
                    // Create a connectivity edge between switch and busbar
                    CIMConnectivityNode ce = new CIMConnectivityNode(_g.ObjectManager);
                    ce.SetClass(CIMClassEnum.ConnectivityEdge);
                    //ce.mRID = GUIDHelper.CreateDerivedGuid(busbar.mRID, 1);

                    // Connect switch to cn
                    cimObjFound.AddNeighbour(ce);
                    ce.AddNeighbour(cimObjFound);

                    // Hack. Set switch parent to same parrent as busbar if null. This because switch many times has no parent in GIS.
                    if (cimObjFound.EquipmentContainerRef == null)
                    {
                        cimObjFound.EquipmentContainerRef = busbar.EquipmentContainerRef;
                        if (busbar.EquipmentContainerRef != null)
                            busbar.EquipmentContainerRef.Children.Add(cimObjFound);
                    }

                    // Hack. Set switch voltage level to same as busbar if null.
                    if (cimObjFound.VoltageLevel == 0)
                    {
                        cimObjFound.VoltageLevel = busbar.VoltageLevel;
                    }

                    // Connect busbar to cn
                    busbar.AddNeighbour(ce);
                    ce.AddNeighbour(busbar);
                }
                else
                {
                    Logger.Log(LogLevel.Warning, "Busbar mRID=" + busbar.mRID + " overlaps another component " + cimObjFound.ToString() + " that is not a disconnector, loadbreaker og breaker");
                }
            }




            // Index all intermediate busbar coords to busbar vertex, to support IG style busbar connections (kartografiske kabler som er snappet ind til skinnens vertices)
            for (int i = 1; i < feat.Coordinates.Length - 1; i++)
            {
                var coord = feat.Coordinates[i];

                int vertexId = FindVertexId(coord.X, coord.Y);

                if (vertexId <= 0)
                    AddVertexIdToIndex(busBarVertexId, coord.X, coord.Y);
                else
                {
                    //Logger.Log(LogLevel.Warning, "Intermediate Busbar coordinate: " + feat.ToString() + " belonging to CIM object " + busbar.IdString() + " overlaps another CIM object. Will be ignored!");
                    // TODO: Log til table - ingen checker denne log for denne type fejl

                }
            }
        }
        
        private int AddVertexToGraph(double X, double Y, CIMIdentifiedObject obj, int no = 0)
        {
            // If vertex already exists in graph, just tell graph to assign the CIM object to the vertex. It's up to the graph to decide if it's legal or not.
            if (FindVertexId(X, Y, no) > 0)
            {
                int vertexId = FindVertexId(X, Y, no);

                _g.AddCIMObjectToExistingVertex(obj, vertexId);

                if (no == 0 || no == 1)
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex1Id = vertexId;
                else
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex2Id = vertexId;

                return vertexId;
            }
            else
            {
                // Create new graph vertex to hold CIM object
                bool dublicate = false;
                if (no > 1)
                    dublicate = true;

                int vertexId = _g.AddCIMObjectToVertex(obj, dublicate);

                // Set vertex 1 and 2 depending on the vertex no (open switches are represented by two vertex ids).
                if (no == 0 || no == 1)
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex1Id = vertexId;
                else
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex2Id = vertexId;

                AddVertexIdToIndex(vertexId, X, Y, no);

                return vertexId;
            }
        }

        private int AddVertexToGraph(string cnId, CIMIdentifiedObject obj, int no = 0)
        {
            // If vertex already exists in graph, just tell graph to assign the CIM object to the vertex. It's up to the graph to decide if it's legal or not.
            if (FindVertexId(cnId, no) > 0)
            {
                int vertexId = FindVertexId(cnId, no);

                _g.AddCIMObjectToExistingVertex(obj, vertexId);

                if (no == 0 || no == 1)
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex1Id = vertexId;
                else
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex2Id = vertexId;

                return vertexId;
            }
            else
            {
                // Create new graph vertex to hold CIM object
                int vertexId = _g.AddCIMObjectToVertex(obj);

                // Set vertex 1 and 2 depending on the vertex no (open switches are represented by two vertex ids).
                if (no == 0 || no == 1)
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex1Id = vertexId;
                else
                    _g.ObjectManager.AdditionalObjectAttributes(obj).Vertex2Id = vertexId;

                AddVertexIdToIndex(vertexId, cnId, no);

                return vertexId;
            }
        }

        private string CreateStationVertexCoordKey(double x, double y)
        {
            var xRound2 = Math.Floor(x);
            var yRound2 = Math.Floor(y);
            return xRound2 + ":" + yRound2;
        }

        private void AddVertexIdToIndex(int vertexId, double x, double y, int no = 0)
        {
            string key = GetVertexKey(x, y, no);

            if (!_aaavertexByXYDict.ContainsKey(key))
                _aaavertexByXYDict.Add(GetVertexKey(x, y, no), vertexId);
        }

        private void AddVertexIdToIndex(int vertexId, string cnId, int no = 0)
        {
            string key = GetVertexKey(cnId, no);

            if (!_aaavertexByXYDict.ContainsKey(key))
                _aaavertexByXYDict.Add(GetVertexKey(cnId, no), vertexId);
        }


        private int FindVertexId(string cnId, int no = 0)
        {
            var key = GetVertexKey(cnId, no);

            if (_aaavertexByXYDict.ContainsKey(key))
                return _aaavertexByXYDict[key];

            return 0;
        }

        private int FindVertexId(double x, double y, int no = 0)
        {
            string key = GetVertexKey(x, y, no);

            // First try if we can get a "direct hit"
            if (_aaavertexByXYDict.ContainsKey(key))
                return _aaavertexByXYDict[key];

            double roundedX = RoundVertexCoordinate(x);
            double roundedY = RoundVertexCoordinate(y);
            double incr = 1.0 / Math.Pow(10, paramRoundDecimals);

            if (paramBufferRadius > 0)
            {
                int steps = 2;
                for (int offset = 1; offset < (paramBufferRadius + 1); offset++)
                {

                    // Top right corner
                    double checkX = Math.Round(roundedX + (incr * offset), paramRoundDecimals);
                    double checkY = Math.Round(roundedY + (incr * offset), paramRoundDecimals);

                    key = GetVertexKey(checkX, checkY, no);

                    if (_aaavertexByXYDict.ContainsKey(key))
                        return _aaavertexByXYDict[key];


                    // From top right to bottom right
                    for (int c = 0; c < steps; c++)
                    {
                        checkX = Math.Round(checkX - incr, paramRoundDecimals);

                        key = GetVertexKey(checkX, checkY, no);

                        if (_aaavertexByXYDict.ContainsKey(key))
                            return _aaavertexByXYDict[key];
                    }

                    // From bottom right to bottom left
                    for (int c = 0; c < steps; c++)
                    {
                        checkY = Math.Round(checkY - incr, paramRoundDecimals);

                        key = GetVertexKey(checkX, checkY, no);

                        if (_aaavertexByXYDict.ContainsKey(key))
                            return _aaavertexByXYDict[key];
                    }

                    // From bottom left to top left
                    for (int c = 0; c < steps; c++)
                    {
                        checkX = Math.Round(checkX + incr, paramRoundDecimals);

                        key = GetVertexKey(checkX, checkY, no);

                        if (_aaavertexByXYDict.ContainsKey(key))
                            return _aaavertexByXYDict[key];
                    }

                    // From top left to top right
                    for (int c = 0; c < steps; c++)
                    {
                        checkY = Math.Round(checkY + incr, paramRoundDecimals);

                        key = GetVertexKey(checkX, checkY, no);

                        if (_aaavertexByXYDict.ContainsKey(key))
                            return _aaavertexByXYDict[key];
                    }

                    steps = steps + 2;

                }

            }


            return 0;
        }

        private string GetVertexKey(double x, double y, int no)
        {
            string vertexKey = RoundVertexCoordinate(x) + ":" + RoundVertexCoordinate(y);

            if (no > 0)
                vertexKey += ":" + no;

            return vertexKey;
        }

        private string GetVertexKey(string cnId, int no)
        {
            string vertexKey = cnId + ":" + no;
            return vertexKey;
        }

        private double RoundVertexCoordinate(double coordinate)
        {
            //var pow = Math.Pow(10, paramRoundDecimals);
            //return Math.Truncate(coordinate * pow) / pow;
            return Math.Round(coordinate, paramRoundDecimals);
        }

        private static bool isTopCoordFirst(CIMIdentifiedObject obj)
        {
            if (obj.Coords.Length == 4)
            {
                //
                // Compare y coords
                if (obj.Coords[1] > obj.Coords[3])
                    return true;
            }
            return false;
        }

        private static bool isTop(CIMIdentifiedObject obj, bool firstVertice)
        {
            if (firstVertice && isTopCoordFirst(obj))
                return true;
            if (!firstVertice && !isTopCoordFirst(obj))
                return true;
            return false;
        }


        private void PreCheckConnectivity()
        {
            int danglingEndCount = 0;

            // Check imported CIM objects for diverse errors
            System.Diagnostics.Stopwatch sw1 = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch sw2 = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch sw3 = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch sw4 = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch sw5 = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch sw6 = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch sw7 = new System.Diagnostics.Stopwatch();
            System.Diagnostics.Stopwatch sw8 = new System.Diagnostics.Stopwatch();

            sw6.Start();
            int topDown = 0;
            int topUp = 0;
            int vertical = 0;
            foreach (var obj in _g.CIMObjects)
            {
                if (obj.ExternalId == "716166")
                {
                }

                // Check objects that must have a parent (that sit inside substation)
                if (obj.ClassType == CIMClassEnum.Breaker || 
                    obj.ClassType == CIMClassEnum.LoadBreakSwitch || 
                    obj.ClassType == CIMClassEnum.Disconnector || 
                    obj.ClassType == CIMClassEnum.Fuse || 
                    obj.ClassType == CIMClassEnum.PowerTransformer ||
                    obj.ClassType == CIMClassEnum.LinearShuntCompensator ||
                    obj.ClassType == CIMClassEnum.NonlinearShuntCompensator ||
                    obj.ClassType == CIMClassEnum.AsynchronousMachine ||
                    obj.ClassType == CIMClassEnum.SynchronousMachine ||
                    obj.ClassType == CIMClassEnum.FaultIndicator ||
                    obj.ClassType == CIMClassEnum.BusbarSection ||
                    obj.ClassType == CIMClassEnum.PetersenCoil ||
                    obj.ClassType == CIMClassEnum.Bay ||
                    (obj.ClassType == CIMClassEnum.ConnectivityEdge && obj.Coords != null && obj.Coords.Length > 0 && obj.Coords[0] > 0) ||
                    (obj.ClassType == CIMClassEnum.ACLineSegment && obj.GetPropertyValueAsString("dax.istransformercable") != null)
                )
                {
                    // Check if component has a parrent
                    if (obj.EquipmentContainerRef == null)
                    {
                        var theErr = GeneralErrors.ComponentHasNoParent;
                        _g.TableLogger.Log(Severity.Error, (short)theErr, GeneralErrorToString.getString(theErr), obj);
                    }
                    else 
                    {
                        var rootContainer = obj.GetEquipmentContainerRoot();
                        if (rootContainer != null && rootContainer.Coords != null && obj.Coords != null)
                        {
                            if (rootContainer.Coords.Length == 2)
                            {
                                double objX = -1;
                                double objY = -1;

                                if (obj.ClassType == CIMClassEnum.BusbarSection && obj.Coords.Length == 4)
                                {
                                    objX = obj.Coords[0];
                                    objY = obj.Coords[1];
                                }
                                else if (obj.ClassType == CIMClassEnum.Bay && obj.Coords.Length == 4)
                                {
                                    objX = obj.Coords[2];
                                    objY = obj.Coords[0];
                                }
                                else if (obj.ClassType == CIMClassEnum.ACLineSegment && obj.Coords.Length > 2)
                                {
                                    objX = obj.Coords[0];
                                    objY = obj.Coords[1];
                                }
                                else if (obj.Coords.Length == 2)
                                {
                                    objX = obj.Coords[0];
                                    objY = obj.Coords[1];
                                }

                                // If we find some useful coords to checke on both child and parent, then check them
                                if (objX != -1)
                                {

                                    double dist = CIMHelper.FindDist(rootContainer.Coords[0], rootContainer.Coords[1], objX, objY);

                                    if (dist > parentCheckRadius)
                                    {
                                        var theErr = GeneralErrors.ComponentHasParentFarAway;
                                        _g.TableLogger.Log(Severity.Error, (short)theErr, GeneralErrorToString.getString(theErr), obj);
                                    }
                                }

                            }
                        }
                        else
                        {
                            var theErr = GeneralErrors.ComponentHasNoRootParent;
                            _g.TableLogger.Log(Severity.Error, (short)theErr, GeneralErrorToString.getString(theErr), obj);

                        }

                    }
                }

                // Check if objects has voltagelevel
                if ((obj.ClassType == CIMClassEnum.BusbarSection ||
                    obj.ClassType == CIMClassEnum.Bay ||
                    obj.ClassType == CIMClassEnum.ACLineSegment ||
                    obj.ClassType == CIMClassEnum.Substation ||
                    obj.ClassType == CIMClassEnum.Enclosure) && obj.VoltageLevel == 0)
                {
                    var theErr = GeneralErrors.ComponentHasNoVoltageLevel;
                    _g.TableLogger.Log(Severity.Error, (short)theErr, GeneralErrorToString.getString(theErr), obj);
                }
              
                // Check line connectors
                if ((obj.ClassType == CIMClassEnum.ACLineSegment || obj.ClassType == CIMClassEnum.ConnectivityEdge) && obj.Coords != null)
                {
                    bool isError = true;

                    var neighbors = obj.GetNeighbours();

                    // Check if more than two neighbors
                    if (neighbors.Count > 2)
                    {
                    }

                    if (neighbors.Count < 2)
                    {
                    }

                    // Check if the line segment has neighbor in both ends
                    bool firstVertex = true;

                    bool onTopEdgeBay = false;
                    bool onBottomEdgeBay = false;

                    //
                    // Check if Cartographics cabel is malformed in some way
                    if (obj.ClassType == CIMClassEnum.ConnectivityEdge)
                    {

                        if (obj.Coords.Length == 4 & !isTopCoordFirst(obj))
                        {
                            if (CIMHelper.equalWithTolerance(obj.Coords[1], obj.Coords[3], GetTolerance()))
                            {
                                vertical++;
                                // JL: Er lovligt nok. Så der skal ikke være varning
                                //Logger.Log(LogLevel.Warning, "Cartographic cable: " + obj.ExternalId + " is Vertical!");
                                isError = false;
                            }
                            else
                            {
                                topDown++;
                                Logger.Log(LogLevel.Warning, "Cartographic cable: " + obj.ExternalId + " is top down!");
                                isError = false;
                            }
                        }
                        else
                        {
                            topUp++;

                        }
                    }

                    if (isError)
                        foreach (var neighbor in neighbors)
                        {
                            sw1.Start();
                            if (!_fast)
                            {
                                //
                                // if neighbor.Neighbours.Count() < 2 then the cable is not connected
                                if (neighbor.Neighbours.Count() >= 2 && neighbor.ClassType != CIMClassEnum.BusbarSection)
                                {
                                    if (obj.ClassType == CIMClassEnum.ConnectivityEdge) // Cartographic cable
                                    {


                                        double[] coor = { obj.Coords[0], obj.Coords[1] };

                                        if (!firstVertex)
                                        {
                                            //
                                            // Second coordinates is the bottom of the cartographic cable
                                            coor[0] = obj.Coords[obj.Coords.Length - 2];
                                            coor[1] = obj.Coords[obj.Coords.Length - 1];
                                        }

                                        // Check if Dalle laske skab skrammel
                                        // Horizontal cartographics cable
                                        // isError = false; 
                                        CIMIdentifiedObject busBarSection = null;

                                        //
                                        // The test takes way too long time: 530.000*70 milli seconds ~= 600 minutes
                                        //busBarSection = GetCIMGraph().IsCartographicCableOnBusBarSection(coor, obj, GetTolerance());

                                        if (busBarSection != null)
                                        {
                                            var theErr = GeneralErrors.DanglingConnectivityEdgeOnBusbarSection;
                                            _g.TableLogger.Log(Severity.Error, (short)theErr, GeneralErrorToString.getString(theErr), obj, obj.Coords[0], obj.Coords[1], "", busBarSection);
                                        }
                                    }

                                }
                            }
                            sw1.Stop();

                            if (neighbor.Neighbours.Count() < 2 && 
                            neighbor.ClassType != CIMClassEnum.EnergyConsumer && 
                            neighbor.ClassType != CIMClassEnum.BusbarSection &&
                            neighbor.ClassType != CIMClassEnum.AsynchronousMachine &&
                            neighbor.ClassType != CIMClassEnum.SynchronousMachine &&
                            neighbor.ClassType != CIMClassEnum.LinearShuntCompensator &&
                            neighbor.ClassType != CIMClassEnum.NonlinearShuntCompensator &&
                            neighbor.ClassType != CIMClassEnum.NetworkEquipment &&
                            neighbor.ClassType != CIMClassEnum.PetersenCoil)
                        {
                            sw2.Start();

                            //
                            // First coordinates is the top of the cartographic cable
                            double x = obj.Coords[0];
                            double y = obj.Coords[1];

                            if (!firstVertex)
                            {
                                //
                                // Second coordinates is the bottom of the cartographic cable
                                x = obj.Coords[obj.Coords.Length - 2];
                                y = obj.Coords[obj.Coords.Length - 1];
                            }

                            // Check if Dalle laske skab skrammel
                            // Horizontal cartographics cable
                            if (obj.Coords.Length == 4 && obj.Coords[1] == obj.Coords[3])
                                isError = false;

                            // If inside bay check if y coord is on edge of bay
                            if (obj.EquipmentContainerRef != null && obj.EquipmentContainerRef.ClassType == CIMClassEnum.Bay)
                            {

                                // Bay only has three significant digits
                                //
                                var yRound = Math.Round(y, paramRoundDecimals);
                                var bay = obj.EquipmentContainerRef;
                                double bayMinY = bay.Coords[0];
                                double bayMaxY = bay.Coords[1];

                                // Sidder på øverste kant
                                if (firstVertex && CIMHelper.equalWithTolerance(yRound, bayMaxY, GetTolerance()))
                                {
                                    onTopEdgeBay = true;
                                    //    if (obj.EquipmentContainerRef.EquipmentContainerRef != null)
                                    //    {
                                    //        var root = obj.EquipmentContainerRef.EquipmentContainerRef;

                                    //        // Hvis en station og bay sidder på primær side, så er det ok
                                    //        if (root.ClassType == CIMClassEnum.Substation && bay.VoltageLevel == root.VoltageLevel)
                                    //            isError = false;
                                    //    }
                                }

                                // Sidder på nederste felt kant
                                if (!firstVertex && CIMHelper.equalWithTolerance(yRound, bayMinY, GetTolerance()))
                                {
                                    onBottomEdgeBay = true;
                                    //if (obj.EquipmentContainerRef.EquipmentContainerRef != null)
                                    //{
                                    //    var root = obj.EquipmentContainerRef.EquipmentContainerRef;

                                    //    // Hvis det er et skab, så er det ok. I så fald der er kabler, så vil de få "dangling error"
                                    //    if (root.ClassType == CIMClassEnum.Enclosure)
                                    //        isError = false;

                                    //    // Hvis en station og bay sidder på sekundær side, så er det ok
                                    //    if (root.ClassType == CIMClassEnum.Substation && bay.VoltageLevel < root.VoltageLevel)
                                    //        isError = false;
                                    //}
                                }
                            }

                            sw2.Stop();
                            //
                            // Only gets here (and mark endpoint as error)
                            //     if this is true -> (neighbor.Neighbours.Count() < 2 && neighbor.ClassType != CIMClassEnum.EnergyConsumer && neighbor.ClassType != CIMClassEnum.BusbarSection)
                            if (isError)
                            {
                                if (obj.ClassType == CIMClassEnum.ACLineSegment)
                                {
                                    sw3.Start();
                                    _allDanglers.Add(new Tuple<CIMIdentifiedObject, double, double>(obj, x, y));

                                    if (_visitedDangling.ContainsKey(obj.InternalId))
                                        _visitedDangling[obj.InternalId] = GeneralErrors.DanglingACLineSegmentDouble;
                                    else
                                        _visitedDangling.Add(obj.InternalId, GeneralErrors.noError);
                                    sw3.Stop();
                                }
                                else
                                {
                                    sw4.Start();
                                    // Cartographic cables
                                    // Current end is not connected
                                    //
                                    // onTopEdgeBay is true if the cart. cable is inside bay and top edge is on bay top edge. Unless there is something else wrong, the cable's good
                                    // onBottomEdgeBay is true if the cart. cable is inside bay and bottom edge is on bay top edge. Unless there is something else wrong, the cable's good

                                    GeneralErrors theErr = GeneralErrors.DanglingConnectivityEdge;
                                    string KompunderMarkering = "";
                                    CIMIdentifiedObject theCon = obj.GetEquipmentContainerRoot();
                                    if (theCon != null)
                                    {
                                        if (theCon.ClassType == CIMClassEnum.Enclosure)
                                            KompunderMarkering = "Enclosure: " + theCon.InternalId.ToString();
                                        else if (theCon.ClassType == CIMClassEnum.Substation)
                                            KompunderMarkering = "Substation: " + theCon.InternalId.ToString();
                                    }

                                    CIMIdentifiedObject busBarSection = null;
                                    double[] coor = { x, y };
                                    sw7.Start();
                                     /*
                                    if (!_fast)
                                    {
                                        busBarSection = GetCIMGraph().IsCartographicCableOnBusBarSection(coor, obj, GetTolerance());
                                    }
                                    */
                                    sw7.Stop();
                                    if (busBarSection != null)
                                       theErr = GeneralErrors.DanglingConnectivityEdgeOnBusbarSection;

                                    if (theCon == null)
                                       theErr = GeneralErrors.DanglingConnectivityNotKompunder;
                                    else if (busBarSection == null)
                                    {
                                        isError = false;

                                        // If Top and not on top bay edge then error
                                        if (firstVertex && !onTopEdgeBay)
                                            isError = true;

                                        // If Bottom and not on bottom bay then error
                                        if (!firstVertex && !onBottomEdgeBay)
                                            isError = true;
                                    }

                                    // Check if dax.checkconnectivity is set to false on the bay, and if so don't log any connectivity error
                                    if (isError && obj.EquipmentContainerRef != null)
                                    {
                                        if (obj.EquipmentContainerRef.ContainsPropertyValue("dax.checkconnectivity"))
                                        {
                                            string val = obj.EquipmentContainerRef.GetPropertyValueAsString("dax.checkconnectivity");

                                            if (val != null && (val == "0" || val == "no" || val == "false"))
                                                isError = false;
                                        }
                                    }

                                    if (isError)
                                        _g.TableLogger.Log(Severity.Error, (short)theErr, GeneralErrorToString.getString(theErr), obj, x, y, KompunderMarkering, busBarSection);
                                    sw4.Stop();
                                }
                                danglingEndCount++;
                            } // End if (isError)
                        } // End if (neighbor.Neighbours.Count() < 2 && neighbor.ClassType != CIMClassEnum.EnergyConsumer && neighbor.ClassType != CIMClassEnum.BusbarSection)

                        firstVertex = false;
                    }
                }
            }
            sw5.Start();
            analyseDanglingCables();
            sw5.Stop();
            sw6.Stop();
            Logger.Log(LogLevel.Debug, "Searching for busbar sections: " + sw1.ElapsedMilliseconds + " milli seconds.");
            Logger.Log(LogLevel.Debug, "First test: " + sw2.ElapsedMilliseconds + " milli seconds.");
            Logger.Log(LogLevel.Debug, "ACLineSegment: " + sw3.ElapsedMilliseconds + " milli seconds.");
            Logger.Log(LogLevel.Debug, "Cartographic cabels: " + sw4.ElapsedMilliseconds + " milli seconds.");
            Logger.Log(LogLevel.Debug, "Cartographic cabels (search for busbar): " + sw7.ElapsedMilliseconds + " milli seconds.");
            Logger.Log(LogLevel.Debug, "analyseDanglingCables: " + sw5.ElapsedMilliseconds + " milli seconds.");
            Logger.Log(LogLevel.Debug, "All: " + sw6.ElapsedMilliseconds + " milli seconds.");

            Logger.Log(LogLevel.Debug, "Cartographic cable top down: " + topDown + ".");
            Logger.Log(LogLevel.Debug, "Vertical cartographic cable: " + vertical + ".");
            Logger.Log(LogLevel.Debug, "Cartographic cable top up: " + topUp + ".");
            
        }

        private void AddToInvalidCoordList(string objType, string objId)
        {

            if (!_invalidCoordsLists.ContainsKey(objType))
                _invalidCoordsLists[objType] = new List<string>();

            _invalidCoordsLists[objType].Add(objId);
        }

        private void analyseDanglingCables()
        {

            foreach (var theDangler in _allDanglers)
            {
                GeneralErrors theErr = GeneralErrors.DanglingACLineSegment;
                if (_visitedDangling[theDangler.Item1.InternalId] != GeneralErrors.noError)
                    theErr = _visitedDangling[theDangler.Item1.InternalId];
                /*
                else if (!_fast)
                {
                    double[] coor = { theDangler.Item2, theDangler.Item3 };

                    List<Tuple<CIMIdentifiedObject, double>> closeBys = GetCIMGraph().GetCIMObjectsCloseToCoor(coor, GetMaxRadiusOfCloseness());
                    if (isBayCloseBy(closeBys, GetBayClosenessRadius()))
                        theErr = GeneralErrors.DanglingACLineSegmentCloseToBay;
                    else if (isConsumerCloseBy(closeBys, GetConsumerClosenessRadius()))
                        theErr = GeneralErrors.DanglingACLineSegmentCloseToConsumer;
                    else if (isOtherDanglingCloseBy(theDangler.Item1, closeBys, GetDanglingClosenessRadius(), coor))
                        theErr = GeneralErrors.DanglingACLineSegmentCloseToOtherDangling;
                    else if (isEnclosureCloseBy(closeBys, GetEnclosureClosenessRadius()))
                        theErr = GeneralErrors.DanglingACLineSegmentCloseToEnclosure;

                }
                */

                _g.TableLogger.Log(Severity.Error, (short)theErr, GeneralErrorToString.getString(theErr), theDangler.Item1, theDangler.Item2, theDangler.Item3);
            }
        }

        private bool isOtherDanglingCloseBy(CIMIdentifiedObject theCIM, List<Tuple<CIMIdentifiedObject, double>> closeBys, double radius, double[] coor)
        {
            foreach (var theDangler in _allDanglers)
            {
                if (theDangler.Item1.InternalId != theCIM.InternalId)
                {
                    double dist = CIMHelper.FindDist(coor[0], coor[1], theDangler.Item2, theDangler.Item3);
                    if (dist < radius)
                        return true;
                }
            }
            return false;
        }


        private List<Tuple<CIMIdentifiedObject, double, double>> _allDanglers = new List<Tuple<CIMIdentifiedObject, double, double>>();

        private SortedDictionary<int, GeneralErrors> _visitedDangling = new SortedDictionary<int, GeneralErrors>();

        //
        // TODO: The distance to the enclosure is undefined
        // The distance to components is used as substitute, but is that good enough?
        private bool isEnclosureCloseBy(List<Tuple<CIMIdentifiedObject, double>> closeBys, double radius)
        {
            CIMIdentifiedObject theContainer = null;

            foreach (Tuple<CIMIdentifiedObject, double> theCIM_Dist in closeBys)
            {
                if (theCIM_Dist.Item2 < radius)
                    if (theCIM_Dist.Item1.ClassType == CIMClassEnum.Fuse || theCIM_Dist.Item1.ClassType == CIMClassEnum.LoadBreakSwitch)
                    {
                        theContainer = theCIM_Dist.Item1.GetEquipmentContainerRoot();
                        if (theContainer != null && theContainer.ClassType == CIMClassEnum.Substation)
                        {
                            CIMClassEnum sd = CIMClassEnum.SynchronousMachine;
                            string sdgh = theContainer.ToString();
                        }

                        if (theContainer != null && theContainer.ClassType == CIMClassEnum.Enclosure)
                            return true;
                    }
            }
            return false;
        }

        //
        // TODO Only available consumers
        private bool isConsumerCloseBy(List<Tuple<CIMIdentifiedObject, double>> closeBys, double radius)
        {

            foreach (Tuple<CIMIdentifiedObject, double> theCIM_Dist in closeBys)
            {
                if (theCIM_Dist.Item2 < radius)
                    if (theCIM_Dist.Item1.ClassType == CIMClassEnum.EnergyConsumer)
                        return true;
            }
            return false;
        }

        private bool isBayCloseBy(List<Tuple<CIMIdentifiedObject, double>> closeBys, double radius)
        {
            foreach (Tuple<CIMIdentifiedObject, double> theCIM_Dist in closeBys)
            {
                if (theCIM_Dist.Item2 < radius)
                {
                    CIMIdentifiedObject theCIM = theCIM_Dist.Item1.EquipmentContainerRef;
                    if (theCIM != null && theCIM.ClassType == CIMClassEnum.Bay)
                        return true;
                }
            }
            return false;
        }

        public CIMIdentifiedObject getCIMType(List<Tuple<CIMIdentifiedObject, double>> theCIMs, CIMClassEnum p)
        {
            foreach (Tuple<CIMIdentifiedObject, double> theCIM_dist in theCIMs)
            {
                if (theCIM_dist.Item1.ClassType == p)
                    return theCIM_dist.Item1;
            }
            return null;
        }

        public bool containsCIMType(List<Tuple<CIMIdentifiedObject, double>> theCIMs, CIMClassEnum p)
        {
            return getCIMType(theCIMs, p) != null;
        }


        public List<ErrorCode> getErrorCodeList()
        {
            return _g.TableLogger.getErrorCodeList();
        }
    }
}