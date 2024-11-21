using DAX.CIM.Serialization.NetSam1_3.Equipment;
using DAX.IO.CIM;
using DAX.IO.CIM.Processing;
using DAX.Util;
using System.Xml.Serialization;

namespace DAX.IO.Serialization.NetSam_1_3
{
    public class NetSamSerializer : IDAXInitializeable, IDAXSerializeable
    {
        private List<CIMIdentifiedObject> _cimObjects;
        private CIMMetaDataRepository _metaDataRepository;

        private CoordinateSystem _coordSys = new CoordinateSystem() { mRID = Guid.NewGuid().ToString(), crsUrn = "urn:ogc:def:crs:EPSG::25832", name = "ETRS89 / UTM zone 32N" };
        private Dictionary<int, BaseVoltage> _baseVoltages = new Dictionary<int, BaseVoltage>();
        
        // Auto generated on the fly (guid not stable)
        private Dictionary<string, PSRType> _psrTypes = new Dictionary<string, PSRType>();
        private Dictionary<string, AssetOwner> _assetOwners = new Dictionary<string, AssetOwner>();
        private Dictionary<string, Maintainer> _assetMaintainers = new Dictionary<string, Maintainer>();
        
        private List<Location> _locations = new List<Location>();
        private List<PositionPoint> _positions = new List<PositionPoint>();
        private List<Substation> _substations = new List<Substation>();
        private List<VoltageLevel> _voltageLevels = new List<VoltageLevel>();
        private List<BayExt> _bays = new List<BayExt>();
        private List<LoadBreakSwitch> _loadBreakSwitches = new List<LoadBreakSwitch>();
        private List<Breaker> _breakers = new List<Breaker>();
        private List<Fuse> _fuses = new List<Fuse>();
        private List<Disconnector> _disconnectors = new List<Disconnector>();
        private List<PowerTransformer> _powerTransformers = new List<PowerTransformer>();
        private List<BusbarSection> _busbarSections = new List<BusbarSection>();
        private List<Terminal> _terminals = new List<Terminal>();
        private Dictionary<Guid, ACLineSegmentExt> _acLineSegments = new Dictionary<Guid, ACLineSegmentExt>();
        private Dictionary<Guid, ConnectivityNode> _connectivityNodes = new Dictionary<Guid, ConnectivityNode>();
        private List<PowerTransformerEndExt> _powerTransformerEnds = new List<PowerTransformerEndExt>();
        private List<EnergyConsumer> _energyConsumers = new List<EnergyConsumer>();
        private List<UsagePoint> _usagePoints = new List<UsagePoint>();
        private List<PetersenCoil> _coils = new List<PetersenCoil>();
        private List<SynchronousMachine> _syncGens = new List<SynchronousMachine>();
        private List<AsynchronousMachine> _asyncGens = new List<AsynchronousMachine>();
        private List<Asset> _assets = new List<Asset>();
        private List<RatioTapChanger> _tapChangers = new List<RatioTapChanger>();
        private List<ExternalNetworkInjection> _extNetworkInjections = new List<ExternalNetworkInjection>();
        private List<LinearShuntCompensator> _compensators = new List<LinearShuntCompensator>();
        private List<FaultIndicatorExt> _faultIndicators = new List<FaultIndicatorExt>();
        private List<CurrentTransformerExt> _currentTransformers = new List<CurrentTransformerExt>();

        // Topology serialization
        private TopologySerializationParameters _topoParameters = null;
        private Dictionary<Guid, object> _cimObjectHasBeenProcessed = new Dictionary<Guid, object>();

        bool hasUsagePointRelation = false;
        bool busExtension = false;

        public NetSamSerializer()
        {
        }

        public NetSamSerializer(CIMMetaDataRepository repository, CIMEquipmentContainer substation)
        {
            _metaDataRepository = repository;
            _cimObjects = substation.Children;
            _cimObjects.Add(substation);
        }

        public NetSamSerializer(CIMMetaDataRepository repository, List<CIMIdentifiedObject> cimObjects)
        {
            _metaDataRepository = repository;
            _cimObjects = cimObjects;
        }


        #region IDAXSerializer Members


        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
            foreach (var param in parameters)
            {
                if (param.Name.ToLower() == "energyconsumerusagepointrelation" && param.Value.ToLower() == "true")
                    hasUsagePointRelation = true;

                if (param.Name.ToLower() == "busextension" && param.Value.ToLower() == "true")
                    busExtension = true;
            }
        }

        public byte[] Serialize(CIMMetaDataRepository repository, List<CIMIdentifiedObject> cimObjects, CIMGraph graph)
        {
            _metaDataRepository = repository;
            _cimObjects = cimObjects;

            var env = GetEnvelope(); 

            MemoryStream ms = new MemoryStream();

            XmlSerializer xmlSerializer = new XmlSerializer(env.GetType());
            xmlSerializer.Serialize(ms, env);

            return ms.ToArray();
        }

        public object Serialize(CIMMetaDataRepository repository, List<CIMIdentifiedObject> cimObjects, TopologySerializationParameters topoParameters = null)
        {
            _metaDataRepository = repository;
            _cimObjects = cimObjects;
            _topoParameters = topoParameters;

            return GetEnvelope();
        }

        public object Serialize(CIMMetaDataRepository repository, CIMEquipmentContainer substation, TopologySerializationParameters topoParameters = null)
        {
            _metaDataRepository = repository;
            _topoParameters = topoParameters;
            _cimObjects = new List<CIMIdentifiedObject>();
            _cimObjects.Add(substation);

            return GetEnvelope();
        }

        #endregion


        public ProfileEnvelop GetEnvelope() 
        {
            foreach (var cimObj in _cimObjects)
            {
                ProcessRootObject(cimObj);
            }

            ProfileEnvelop env = new ProfileEnvelop();

            env.CoordinateSystem = new CoordinateSystem[] { _coordSys };

            env.BaseVoltage = _baseVoltages.Values.ToArray();
            _baseVoltages = null;

            env.PSRType = _psrTypes.Values.ToArray();
            _psrTypes = null;

            env.ACLineSegmentExt = _acLineSegments.Values.ToArray();
            _acLineSegments = null;

            env.Location = _locations.ToArray();
            _locations = null;

            env.PositionPoint = _positions.ToArray();
            _positions = null;

            env.Substation = _substations.ToArray();
            _substations = null;

            env.VoltageLevel = _voltageLevels.ToArray();
            _voltageLevels = null;

            env.BayExt = _bays.ToArray();
            _bays = null;

            env.LoadBreakSwitch = _loadBreakSwitches.ToArray();
            _loadBreakSwitches = null;

            env.Breaker = _breakers.ToArray();
            _breakers = null;

            env.Fuse = _fuses.ToArray();
            _fuses = null;

            env.Disconnector = _disconnectors.ToArray();
            _disconnectors = null;

            env.PowerTransformer = _powerTransformers.ToArray();
            _powerTransformers = null;

            env.BusbarSection = _busbarSections.ToArray();
            _busbarSections = null;

            if (_topoParameters == null || !_topoParameters.ExcludeConnectivity)
                env.Terminal = _terminals.ToArray();

            _terminals = null;

            if (_topoParameters == null || !_topoParameters.ExcludeConnectivity)
                env.ConnectivityNode = _connectivityNodes.Values.ToArray();

            _connectivityNodes = null;

            env.PowerTransformerEndExt = _powerTransformerEnds.ToArray();
            _powerTransformerEnds = null;

            env.EnergyConsumer = _energyConsumers.ToArray();
            _energyConsumers = null;

            env.UsagePoint = _usagePoints.ToArray();
            _usagePoints = null;

            env.PetersenCoil = _coils.ToArray();
            _coils = null;

            env.SynchronousMachine = _syncGens.ToArray();
            _syncGens = null;

            env.AsynchronousMachine = _asyncGens.ToArray();
            _asyncGens = null;

            env.Asset = _assets.ToArray();
            _assets = null;

            env.RatioTapChanger = _tapChangers.ToArray();
            _tapChangers = null;

            env.ExternalNetworkInjection = _extNetworkInjections.ToArray();
            _extNetworkInjections = null;

            env.AssetOwner = _assetOwners.Values.ToArray();
            _assetOwners = null;

            env.Maintainer = _assetMaintainers.Values.ToArray();
            _assetMaintainers = null;

            env.LinearShuntCompensator = _compensators.ToArray();
            _compensators = null;

            env.FaultIndicatorExt = _faultIndicators.ToArray();
            _faultIndicators = null;

            env.CurrentTransformerExt = _currentTransformers.ToArray();
            _currentTransformers = null;

            return env;
        }

        private void ProcessRootObject(CIMIdentifiedObject cimObj)
        {
            if (cimObj.ClassType == CIMClassEnum.Substation)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var substation = cimObj as CIMEquipmentContainer;
                    Substation xmlObj = new Substation();

                    MapIdentifiedObjectFields(substation, xmlObj);
                    MapSubstation(substation, xmlObj);

                    _substations.Add(xmlObj);
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.Enclosure)
            {
                if (!CheckIfProcessed(cimObj, false))
                {

                    var enclosure = cimObj as CIMEquipmentContainer;
                    Substation xmlObj = new Substation();

                    MapIdentifiedObjectFields(enclosure, xmlObj);
                    MapEnclosure(enclosure, xmlObj);

                    _substations.Add(xmlObj);
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.EnergyConsumer)
            {
                if (!CheckIfProcessed(cimObj, false))
                {

                    var consumer = cimObj as CIMConductingEquipment;

                    EnergyConsumer xmlConsumer = new EnergyConsumer();
                    

                    MapIdentifiedObjectFields(consumer, xmlConsumer);
                    MapConductingEquipmentFields(consumer, xmlConsumer);

                    xmlConsumer.mRID = cimObj.mRID.ToString();

                    // Map terminals to energy consumer
                    MapTerminals(consumer, xmlConsumer);


                    if (!hasUsagePointRelation)
                    {
                        // Clear info on energy consumer - hvorfor - giver ingen mening?
                        //xmlConsumer.name = null;
                        //xmlConsumer.description = null;

                        UsagePoint xmlUsagePoint = new UsagePoint();

                        // Copy information to usage point
                        xmlUsagePoint.mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 10).ToString();
                        CheckIfProcessed(xmlUsagePoint.mRID, xmlUsagePoint);

                        xmlUsagePoint.name = xmlConsumer.name;
                        xmlUsagePoint.description = xmlConsumer.description;
                      
                        // Relate usagepoint to energy consumer                
                        xmlUsagePoint.Equipments = new UsagePointEquipments() { @ref = xmlConsumer.mRID };

                        _usagePoints.Add(xmlUsagePoint);
                    }
                    else
                    {
                        var ecUsagePointList = cimObj.GetPropertyValue("usagepoints") as List<CIMIdentifiedObject>;

                        if (ecUsagePointList != null)
                        {
                            foreach (var up in ecUsagePointList)
                            {
                                UsagePoint xmlUsagePoint = new UsagePoint();
                                xmlUsagePoint.mRID = up.mRID.ToString();
                                xmlUsagePoint.name = up.Name;
                                xmlUsagePoint.description = up.Description;
                                // Relate usagepoint to energy consumer                
                                xmlUsagePoint.Equipments = new UsagePointEquipments() { @ref = xmlConsumer.mRID };
                                _usagePoints.Add(xmlUsagePoint);
                            }
                        }
                    }

                    if (cimObj.ContainsPropertyValue("cim.numberofcustomers"))
                    {
                        int nCustomers = cimObj.GetPropertyValueAsInt("cim.numberofcustomers").Value;
                        xmlConsumer.customerCount = "" + nCustomers;
                    }

                    _energyConsumers.Add(xmlConsumer);
                    
                }
                
            }
            // Cables outside substation
            else if (cimObj.ClassType == CIMClassEnum.ACLineSegment && cimObj.EquipmentContainerRef == null && TopologyShouldElementBeIncluded(cimObj))
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ce = cimObj as CIMConductingEquipment;
                    ACLineSegmentExt xmlObj = new ACLineSegmentExt();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapACLineSegmentFields(ce, (ACLineSegmentExt)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    _acLineSegments.Add(ce.mRID, (ACLineSegmentExt)xmlObj);
                }
            }
            // Indfødninger 
            else if (cimObj.ClassType == CIMClassEnum.ExternalNetworkInjection && TopologyShouldElementBeIncluded(cimObj))
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ce = cimObj as CIMConductingEquipment;
                    ExternalNetworkInjection xmlObj = new ExternalNetworkInjection();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapExternalNetworkInjectionFields(ce, (ExternalNetworkInjection)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    _extNetworkInjections.Add((ExternalNetworkInjection)xmlObj);
                }

            }
        }

        private IdentifiedObject ProcessLeafObject(CIMIdentifiedObject cimObj, EquipmentContainer equipmentContainer, int equipmentContainerVoltageLevel, Dictionary<int, VoltageLevel> substationVoltageLevels)
        {
            IdentifiedObject xmlObj = null;

            
            if (cimObj is CIMConductingEquipment)
            {
                var ce = cimObj as CIMConductingEquipment;

                if (ce.ClassType == CIMClassEnum.ACLineSegment && TopologyShouldElementBeIncluded(cimObj))
                {
                    if (!CheckIfProcessed(cimObj, false))
                    {
                        xmlObj = new ACLineSegmentExt();

                        MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                        MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                        MapACLineSegmentFields(ce, (ACLineSegmentExt)xmlObj);
                        MapTerminals(ce, (ConductingEquipment)xmlObj);

                        _acLineSegments.Add(ce.mRID, (ACLineSegmentExt)xmlObj);
                    }
                }
                else if (ce.ClassType == CIMClassEnum.LoadBreakSwitch && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new LoadBreakSwitch();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    ((LoadBreakSwitch)xmlObj).normalOpen = (bool)ce.GetPropertyValue("cim.normalopen");

                    _loadBreakSwitches.Add((LoadBreakSwitch)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.Breaker && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new Breaker();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    // breaking capacity
                    if (cimObj.ContainsPropertyValue("cim.breakingcapacity"))
                    {
                        string valueStr = cimObj.GetPropertyValue("cim.breakingcapacity").ToString();

                        if (Int32.TryParse(valueStr, out var valueInt))
                        {
                            ((Breaker)xmlObj).breakingCapacity = new CurrentFlow() { unit = UnitSymbol.A, unitSpecified = true, Value = "" + valueInt };
                        }
                    }

                    _breakers.Add((Breaker)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.Fuse && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new Fuse();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    _fuses.Add((Fuse)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.Disconnector && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new Disconnector();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    _disconnectors.Add((Disconnector)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.FaultIndicatorExt && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new FaultIndicatorExt();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);

                    var faultIndicator = (FaultIndicatorExt)xmlObj;

                    // Equipment container
                    if (cimObj.EquipmentContainerRef != null)
                    {
                        faultIndicator.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                    }

                    // Connect to termianl
                    if (ce.ContainsPropertyValue("cim.terminal"))
                    {
                        faultIndicator.Terminal = new AuxiliaryEquipmentTerminal() { @ref = ce.GetPropertyValueAsString("cim.terminal") };
                    }

                    // Reset kind
                    if (ce.ContainsPropertyValue("cim.resetkind"))
                    {
                        var val = ce.GetPropertyValueAsString("cim.resetkind").ToLower().Trim();
                        
                        FaultIndicatorResetKind resetKind = FaultIndicatorResetKind.manual;
                        
                        if (val == "automatisk" || val == "automatic")
                            resetKind = FaultIndicatorResetKind.automatic;

                        faultIndicator.resetKind = resetKind;
                        faultIndicator.resetKindSpecified = true;
                    }

                    _faultIndicators.Add((FaultIndicatorExt)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.CurrentTransformer && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new CurrentTransformerExt();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);

                    var currentTransformer = (CurrentTransformerExt)xmlObj;

                    // Equipment container
                    if (cimObj.EquipmentContainerRef != null)
                    {
                        currentTransformer.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                    }

                    // Connect to termianl
                    if (ce.ContainsPropertyValue("cim.terminal"))
                    {
                        currentTransformer.Terminal = new AuxiliaryEquipmentTerminal() { @ref = ce.GetPropertyValueAsString("cim.terminal") };
                    }

                    // maximumcurrent
                    if (ce.ContainsPropertyValue("cim.maximumcurrent"))
                        currentTransformer.maximumCurrent = new CurrentFlow() { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = ce.GetPropertyValueAsString("cim.maximumcurrent") };

                    _currentTransformers.Add((CurrentTransformerExt)xmlObj);
                }

                else if (ce.ClassType == CIMClassEnum.PowerTransformer && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new PowerTransformer();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    _powerTransformers.Add((PowerTransformer)xmlObj);
                    
                    // Create terminal and winding for each neighbor
                    bool firstEndFound = false;

                    PowerTransformerEndExt firstEnd = null;

                    foreach (var cimTerminal in ((CIMConductingEquipment)cimObj).Terminals) 
                    {
                        if (cimTerminal.ConnectivityNode == null)
                        {
                            Logger.Log(LogLevel.Warning, "Expected to find conducting equipment connected to terminal: 1 on PowerTransformer with ExternalId=" + cimObj.ExternalId + " mRID=" + cimObj.mRID + " in substation: " + cimObj.EquipmentContainerRef.Name);
                            continue;
                        }

                        var acSegments = cimTerminal.ConnectivityNode.GetNeighbours(cimObj);
                                             

                        if (acSegments.Count > 0)
                        {
                            // Create xml connectivity node
                            var xmlCn = CreateOrGetConnectivityNode(cimTerminal.ConnectivityNode.mRID, (CIMConductingEquipment)cimObj);

                            // Create xml terminal
                            var xmlTerminal = CreateTerminal(cimTerminal, (ConductingEquipment)xmlObj, xmlCn, cimTerminal.EndNumber);

                            // Create xml power transformer end
                            PowerTransformerEndExt xmlTe = new PowerTransformerEndExt()
                            {
                                mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, cimTerminal.EndNumber + 10).ToString(),
                                PowerTransformer = new PowerTransformerEndPowerTransformer() { @ref = xmlObj.mRID },
                                endNumber = "" + cimTerminal.EndNumber,
                                Terminal = new TransformerEndTerminal() { @ref = xmlTerminal.mRID },
                                BaseVoltage = new TransformerEndBaseVoltage() { @ref = CreateOrGetBaseVoltage(acSegments[0].VoltageLevel).mRID }
                            };

                            CheckIfProcessed(xmlTe.mRID, xmlTe);

                            if (!firstEndFound)
                            {
                                firstEnd = xmlTe;
                                firstEndFound = true;
                            }



                            ////////////
                            // Transfer all electric parameters

                            // Fælles for begge viklinger
                            if (ce.ContainsPropertyValue("cim.v1.rateds"))
                                xmlTe.ratedS = new ApparentPower() { unit = UnitSymbol.VA, unitSpecified = true, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v1.rateds")) };

                             if (ce.ContainsPropertyValue("ext.loss"))
                                xmlTe.loss = new KiloActivePower { unit = UnitSymbol.W, unitSpecified = true, multiplier = UnitMultiplier.none, Value = ce.GetPropertyValueAsString("ext.loss") };

                            if (ce.ContainsPropertyValue("ext.losszero"))
                                xmlTe.lossZero = new KiloActivePower { unit = UnitSymbol.W, unitSpecified = true, multiplier = UnitMultiplier.none, Value = ce.GetPropertyValueAsString("ext.losszero") };

           
                            // Vinkling 1
                            if (cimTerminal.EndNumber == 1)
                            {
                                if (ce.ContainsPropertyValue("cim.v1.nominalvoltage"))
                                    xmlTe.nominalVoltage = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v1.nominalvoltage")) };

                                if (ce.ContainsPropertyValue("cim.v1.phaseangleclock"))
                                    xmlTe.phaseAngleClock = ce.GetPropertyValueAsString("cim.v1.phaseangleclock");

                                if (ce.ContainsPropertyValue("cim.v1.ratedu"))
                                    xmlTe.ratedU = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v1.ratedu")) };
                                
                                if (ce.ContainsPropertyValue("ext.v1.uk"))
                                    xmlTe.uk = new PerCent() { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v1.uk")) };

                                if (ce.ContainsPropertyValue("ext.v1.excitingcurrentzero"))
                                    xmlTe.excitingCurrentZero = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v1.excitingcurrentzero")) };

                            }

                            // Vinkling 2
                            if (cimTerminal.EndNumber == 2)
                            {
                                if (ce.ContainsPropertyValue("cim.v2.nominalvoltage"))
                                    xmlTe.nominalVoltage = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v2.nominalvoltage")) };

                                if (ce.ContainsPropertyValue("cim.v2.phaseangleclock"))
                                    xmlTe.phaseAngleClock = ce.GetPropertyValueAsString("cim.v2.phaseangleclock");

                                if (ce.ContainsPropertyValue("cim.v2.ratedu"))
                                    xmlTe.ratedU = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v2.ratedu")) };

                                if (ce.ContainsPropertyValue("ext.v2.uk"))
                                    xmlTe.uk = new PerCent() { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v2.uk")) };

                                if (ce.ContainsPropertyValue("ext.v2.excitingcurrentzero"))
                                    xmlTe.excitingCurrentZero = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v2.excitingcurrentzero")) };

                            }

                             _powerTransformerEnds.Add(xmlTe);
                        }
                        else
                        {
                            Logger.Log(LogLevel.Warning, "Expected to find conducting equipment connected to terminal: " + cimTerminal.EndNumber + " on PowerTransformer with ExternalId=" + cimObj.ExternalId + " mRID=" + cimObj.mRID + " in substation: " + cimObj.EquipmentContainerRef.Name);
                        }
                    }

                    if (firstEnd != null)
                    {
                        // Trin kobler
                        RatioTapChanger tap = new RatioTapChanger() { TransformerEnd = new RatioTapChangerTransformerEnd() { @ref = firstEnd.mRID } };

                        tap.mRID = GUIDHelper.CreateDerivedGuid(ce.mRID, 100).ToString();
                        CheckIfProcessed(tap.mRID, tap);

                        if (ce.ContainsPropertyValue("tap.lowstep"))
                            tap.lowStep = ce.GetPropertyValueAsString("tap.lowstep");

                        if (ce.ContainsPropertyValue("tap.highstep"))
                            tap.highStep = ce.GetPropertyValueAsString("tap.highstep");

                        if (ce.ContainsPropertyValue("tap.ltcflag"))
                        {
                            string val = ce.GetPropertyValueAsString("tap.ltcflag").ToLower();

                            tap.ltcFlag = false;

                            if (val != null && (val == "1" || val == "true" || val == "yes"))
                                tap.ltcFlag = true;
                        }

                        if (ce.ContainsPropertyValue("tap.neutralstep"))
                            tap.neutralStep = ce.GetPropertyValueAsString("tap.neutralstep");


                        if (ce.ContainsPropertyValue("tap.normalstep"))
                            tap.normalStep = ce.GetPropertyValueAsString("tap.normalstep");

                        if (ce.ContainsPropertyValue("tap.stepvoltageincrement"))
                        {
                            tap.stepVoltageIncrement = new PerCent() { Value = Convert.ToSingle(ce.GetPropertyValueAsString("tap.stepvoltageincrement")) };
                        }

                        if (ce.ContainsPropertyValue("tap.neutralu"))
                            tap.neutralU = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("tap.neutralu")) };

                        _tapChangers.Add(tap);
                    }
                    
                }
                else if (ce.ClassType == CIMClassEnum.BusbarSection && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new BusbarSection();
                    var xmlBusObj = xmlObj as BusbarSection;

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);


                    if (ce.ContainsPropertyValue("cim.ipmax"))
                        ((BusbarSection)xmlObj).ipMax = new CurrentFlow() { unit = UnitSymbol.A, unitSpecified = true, Value = ce.GetPropertyValueAsString("cim.ipmax") };

                    // Make sure busbar points to voltage level, not substation
                    if (substationVoltageLevels != null && substationVoltageLevels.ContainsKey(ce.VoltageLevel))
                        xmlBusObj.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = substationVoltageLevels[ce.VoltageLevel].mRID };

                    _busbarSections.Add((BusbarSection)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.PetersenCoil && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new PetersenCoil();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    if (ce.ContainsPropertyValue("cim.nominalu"))
                        ((PetersenCoil)xmlObj).nominalU = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.nominalu")) };

                    if (ce.ContainsPropertyValue("cim.offsetcurrent"))
                        ((PetersenCoil)xmlObj).offsetCurrent = new CurrentFlow() { unit = UnitSymbol.A, unitSpecified = true, multiplierSpecified = true, multiplier = UnitMultiplier.none, Value = ce.GetPropertyValueAsString("cim.offsetcurrent") };

                    if (ce.ContainsPropertyValue("cim.positioncurrent"))
                        ((PetersenCoil)xmlObj).positionCurrent = new CurrentFlow() { unit = UnitSymbol.A, unitSpecified = true, multiplierSpecified = true, multiplier = UnitMultiplier.none, Value = ce.GetPropertyValueAsString("cim.positioncurrent") };
                    
                    if (ce.ContainsPropertyValue("cim.xgroundmin"))
                        ((PetersenCoil)xmlObj).xGroundMin = new Reactance { unit = UnitSymbol.ohm, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.xgroundmin")) };

                    if (ce.ContainsPropertyValue("cim.xgroundmax"))
                        ((PetersenCoil)xmlObj).xGroundMax = new Reactance { unit = UnitSymbol.ohm, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.xgroundmax")) };

                    if (ce.ContainsPropertyValue("cim.xgroundnominal"))
                        ((PetersenCoil)xmlObj).xGroundNominal = new Reactance { unit = UnitSymbol.ohm, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.xgroundnominal")) };

                    if (ce.ContainsPropertyValue("cim.r"))
                        ((PetersenCoil)xmlObj).r = new Resistance { unit = UnitSymbol.ohm, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.r")) };

                    ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.automaticPositioning;

                    if (ce.ContainsPropertyValue("cim.mode"))
                    {
                        string mode = ce.GetPropertyValueAsString("cim.mode").ToLower();
                        if (mode.Contains("manual") || mode.Contains("manuel"))
                            ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.manual;
                        else if (mode.Contains("automatic"))
                            ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.automaticPositioning;
                        else if (mode.Contains("fixed"))
                            ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.@fixed;
                    }

                    _coils.Add((PetersenCoil)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.SynchronousMachine && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new SynchronousMachine();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    if (ce.ContainsPropertyValue("cim.ratedu"))
                        ((SynchronousMachine)xmlObj).ratedU = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedu")) };

                    if (ce.ContainsPropertyValue("cim.rateds"))
                        ((SynchronousMachine)xmlObj).ratedS = new ApparentPower() { unit = UnitSymbol.VA, unitSpecified = true, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.rateds")) };
                    
                    if (ce.ContainsPropertyValue("cim.ratedpowerfactor")) 
                    {
                        ((SynchronousMachine)xmlObj).ratedPowerFactor = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedpowerfactor"));
                        ((SynchronousMachine)xmlObj).ratedPowerFactorSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.maxq"))
                        ((SynchronousMachine)xmlObj).maxQ = new ReactivePower { unit = UnitSymbol.VAr, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxq")) };

                    if (ce.ContainsPropertyValue("cim.minq"))
                        ((SynchronousMachine)xmlObj).minQ = new ReactivePower { unit = UnitSymbol.VAr, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minq")) };

                    if (ce.ContainsPropertyValue("cim.qpercent"))
                        ((SynchronousMachine)xmlObj).qPercent = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.qpercent")) };

                    if (ce.ContainsPropertyValue("cim.referencepriority"))
                        ((SynchronousMachine)xmlObj).referencePriority = ce.GetPropertyValueAsString("cim.referencepriority");

                    if (ce.ContainsPropertyValue("cim.ikk"))
                        ((SynchronousMachine)xmlObj).ikk = new CurrentFlow { unit = UnitSymbol.A, unitSpecified = true, multiplier = UnitMultiplier.none, Value = ce.GetPropertyValueAsString("cim.ikk") };

                    if (ce.ContainsPropertyValue("cim.mu")) {
                        ((SynchronousMachine)xmlObj).mu = Convert.ToSingle(ce.GetPropertyValueAsString("cim.mu"));
                        ((SynchronousMachine)xmlObj).muSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.r"))
                        ((SynchronousMachine)xmlObj).r = new Resistance { unit = UnitSymbol.ohm, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.r")) };
                    
                    if (ce.ContainsPropertyValue("cim.r0"))
                        ((SynchronousMachine)xmlObj).r0 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.r0")) };

                    if (ce.ContainsPropertyValue("cim.r2"))
                        ((SynchronousMachine)xmlObj).r2 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.r2")) };


                    if (ce.ContainsPropertyValue("cim.shortcircuitrotortype"))
                    {
                        string type = ce.GetPropertyValueAsString("cim.shortcircuitrotortype").ToLower();

                        if (type.Contains("salientpole1"))
                            ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.salientPole1;
                        else if (type.Contains("salientpole2"))
                            ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.salientPole2;
                        else if (type.Contains("turboseries1"))
                            ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.turboSeries1;
                        else if (type.Contains("turboseries2"))
                            ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.turboSeries2;
                    }

                    if (ce.ContainsPropertyValue("cim.voltageregulationrange"))
                        ((SynchronousMachine)xmlObj).voltageRegulationRange = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.voltageregulationrange")) };
                    
                    if (ce.ContainsPropertyValue("cim.x0"))
                        ((SynchronousMachine)xmlObj).x0 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.x0")) };

                    if (ce.ContainsPropertyValue("cim.x2"))
                        ((SynchronousMachine)xmlObj).x2 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.x2")) };

                     if (ce.ContainsPropertyValue("cim.satdirectsubtransx"))
                         ((SynchronousMachine)xmlObj).satDirectSubtransX = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.satdirectsubtransx")) };

                    _syncGens.Add((SynchronousMachine)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.AsynchronousMachine && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new AsynchronousMachine();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    if (ce.ContainsPropertyValue("cim.ratedu"))
                        ((AsynchronousMachine)xmlObj).ratedU = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedu")) };

                    if (ce.ContainsPropertyValue("cim.rateds"))
                        ((AsynchronousMachine)xmlObj).ratedS = new ApparentPower() { unit = UnitSymbol.VA, unitSpecified = true, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.rateds")) };

                    if (ce.ContainsPropertyValue("cim.ratedpowerfactor"))
                    {
                        ((AsynchronousMachine)xmlObj).ratedPowerFactor = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedpowerfactor"));
                        ((AsynchronousMachine)xmlObj).ratedPowerFactorSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.nominalfrequency"))
                        ((AsynchronousMachine)xmlObj).nominalFrequency = new Frequency() { unit = UnitSymbol.Hz, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.nominalfrequency")) };

                    if (ce.ContainsPropertyValue("cim.nominelspeed"))
                        ((AsynchronousMachine)xmlObj).nominalSpeed = new RotationSpeed() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.nominelspeed")) };

                    if (ce.ContainsPropertyValue("cim.converterfeddrive"))
                    {
                        string boolStr = ce.GetPropertyValueAsString("cim.converterfeddrive");

                        if (boolStr == "1")
                            ((AsynchronousMachine)xmlObj).converterFedDrive = true;
                        else
                            ((AsynchronousMachine)xmlObj).converterFedDrive = false;
                    }

                    if (ce.ContainsPropertyValue("cim.efficiency"))
                        ((AsynchronousMachine)xmlObj).efficiency = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.efficiency")) };

                    if (ce.ContainsPropertyValue("cim.iairratio"))
                        ((AsynchronousMachine)xmlObj).iaIrRatio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.iairratio"));

                    if (ce.ContainsPropertyValue("cim.polepairnumber"))
                        ((AsynchronousMachine)xmlObj).polePairNumber = ce.GetPropertyValueAsString("cim.polepairnumber");

                    if (ce.ContainsPropertyValue("cim.reversible"))
                    {
                        string boolStr = ce.GetPropertyValueAsString("cim.reversible");

                        if (boolStr == "1")
                            ((AsynchronousMachine)xmlObj).reversible = true;
                        else
                            ((AsynchronousMachine)xmlObj).reversible = false;
                    }

                    if (ce.ContainsPropertyValue("cim.rxlockedrotorratio"))
                        ((AsynchronousMachine)xmlObj).rxLockedRotorRatio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.rxlockedrotorratio"));

                    _asyncGens.Add((AsynchronousMachine)xmlObj);
                }
                else if (ce.ClassType == CIMClassEnum.LinearShuntCompensator && TopologyShouldElementBeIncluded(cimObj))
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new LinearShuntCompensator();

                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapTerminals(ce, (ConductingEquipment)xmlObj);

                    if (ce.ContainsPropertyValue("cim.nomu"))
                        ((LinearShuntCompensator)xmlObj).nomU = new Voltage() { unit = UnitSymbol.V, unitSpecified = true, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.nomu")) };

                    if (ce.ContainsPropertyValue("cim.normalsections"))
                        ((LinearShuntCompensator)xmlObj).normalSections = ce.GetPropertyValueAsString("cim.normalsections");

                    if (ce.ContainsPropertyValue("cim.maximumsections"))
                        ((LinearShuntCompensator)xmlObj).maximumSections = ce.GetPropertyValueAsString("cim.maximumsections");

                    if (ce.ContainsPropertyValue("cim.bpersection"))
                        ((LinearShuntCompensator)xmlObj).bPerSection = new Susceptance() { Value = ce.GetPropertyValueAsString("cim.bpersection") };

                    if (ce.ContainsPropertyValue("cim.gpersection"))
                        ((LinearShuntCompensator)xmlObj).gPerSection = new  Conductance() { Value = ce.GetPropertyValueAsString("cim.gpersection") };

                    if (ce.ContainsPropertyValue("cim.b0persection"))
                        ((LinearShuntCompensator)xmlObj).b0PerSection = new Susceptance() { Value = ce.GetPropertyValueAsString("cim.b0persection") };

                    if (ce.ContainsPropertyValue("cim.g0persection"))
                        ((LinearShuntCompensator)xmlObj).g0PerSection = new Conductance() { Value = ce.GetPropertyValueAsString("cim.g0persection") };

                    _compensators.Add((LinearShuntCompensator)xmlObj);
                }


                // Set equipment container (if not bay)
                if (xmlObj != null && !(xmlObj is BusbarSection) && xmlObj is ConductingEquipment && equipmentContainer != null)
                    ((ConductingEquipment)xmlObj).EquipmentContainer = new EquipmentEquipmentContainer() { @ref = equipmentContainer.mRID };

            }

            return xmlObj;
        }

        private bool CheckIfProcessed(CIMObject cimObj, bool throwErrorIfAlreadyProcessed = true)
        {
            if (_cimObjectHasBeenProcessed.ContainsKey(cimObj.mRID))
            {
                var existingCimObj = _cimObjectHasBeenProcessed[cimObj.mRID];

                if (throwErrorIfAlreadyProcessed)
                {
                    Logger.Log(LogLevel.Warning, "Dublicate mRID checking failed: " + cimObj.ToString() + " mRID: " + cimObj.mRID + " clash with: " + existingCimObj);
                    throw new Exception(cimObj.mRID + " already processed. Error in serialization. Existing obj: " + existingCimObj.ToString());
                }

                return true;
            }
            else
                _cimObjectHasBeenProcessed.Add(cimObj.mRID, cimObj);

            return false;
        }

        private bool CheckIfProcessed(string mRID, object obj)
        {
            var guid = Guid.Parse(mRID);
            if (_cimObjectHasBeenProcessed.ContainsKey(guid))
            {
                var existingCimObj = _cimObjectHasBeenProcessed[guid];

                throw new Exception(mRID + " already processed. Error in serialization. Existing obj: " + existingCimObj.ToString());
            }
            else
                _cimObjectHasBeenProcessed.Add(guid, obj);

            return false;
        }

        private void MapIdentifiedObjectFields(CIMIdentifiedObject cimObj, PowerSystemResource xmlObj)
        {
            // mRID
            if (cimObj.mRID == Guid.Empty)
                cimObj.mRID = Guid.NewGuid();

            xmlObj.mRID = cimObj.mRID.ToString();

            // PSRType
            var psrType = CreateOrGetPSRType(cimObj.GetPSRType(_metaDataRepository));
            if (psrType != null)
                xmlObj.PSRType = new PowerSystemResourcePSRType() { @ref = psrType.mRID };

            // Name
            if (cimObj.Name != null)
                xmlObj.name = cimObj.Name;

            // Description
            if (cimObj.Description != null)
                xmlObj.description = cimObj.Description;

            // Location, include coords only on aclinesegments, energyconsumer and substations
            double[] coords = null;
            if (cimObj.ClassType == CIMClassEnum.ACLineSegment || cimObj.ClassType == CIMClassEnum.Substation || cimObj.ClassType == CIMClassEnum.Enclosure || cimObj.ClassType == CIMClassEnum.EnergyConsumer)
                coords = cimObj.Coords;

            if (coords != null)
            {
                var location = CreateLocation(coords);
                xmlObj.Location = new PowerSystemResourceLocation() { @ref = location.mRID };
            }


            // Check if asset information
            bool containsAssetInfo = false;
            Asset asset = new Asset() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 20).ToString() };
            

            if (cimObj.ContainsPropertyValue("cim.asset.name"))
            {
                containsAssetInfo = true;
                asset.name = cimObj.GetPropertyValueAsString("cim.asset.name");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.description"))
            {
                containsAssetInfo = true;
                asset.description = cimObj.GetPropertyValueAsString("cim.asset.description");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.type"))
            {
                containsAssetInfo = true;
                asset.type = cimObj.GetPropertyValueAsString("cim.asset.type");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.lotnumber"))
            {
                containsAssetInfo = true;
                asset.lotNumber = cimObj.GetPropertyValueAsString("cim.asset.lotnumber");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.serialnumber"))
            {
                containsAssetInfo = true;
                asset.serialNumber = cimObj.GetPropertyValueAsString("cim.asset.serialnumber").Trim();

                if (asset.serialNumber == "\0")
                    asset.serialNumber = null;
            }

            if (cimObj.ContainsPropertyValue("cim.asset.installationdate"))
            {
                containsAssetInfo = true;
                asset.lifecycle = new LifecycleDate();
                asset.lifecycle.installationDateSpecified = true;
                asset.lifecycle.installationDate = Convert.ToDateTime(cimObj.GetPropertyValueAsString("cim.asset.installationdate"));
            }

            // Asset organisation roles
            List<AssetOrganisationRoles> orgRoles = new List<AssetOrganisationRoles>();

            if (cimObj.ContainsPropertyValue("cim.asset.owner"))
            {
                string name = cimObj.GetPropertyValueAsString("cim.asset.owner");

                if (name != null)
                {
                    containsAssetInfo = true;

                    orgRoles.Add(new AssetOrganisationRoles() { @ref = CreateOrGetAssetOwner(name).mRID });
                }
            }

            if (cimObj.ContainsPropertyValue("cim.asset.maintainer"))
            {
                string name = cimObj.GetPropertyValueAsString("cim.asset.maintainer");

                if (name != null)
                {
                    containsAssetInfo = true;

                    orgRoles.Add(new AssetOrganisationRoles() { @ref = CreateOrGetAssetMaintainer(name).mRID });
                }
            }

            if (orgRoles.Count > 0)
                asset.OrganisationRoles = orgRoles.ToArray();

            if (containsAssetInfo)
            {
                xmlObj.Assets = new PowerSystemResourceAssets() { @ref = asset.mRID };
                _assets.Add(asset);
                CheckIfProcessed(asset.mRID, "Asset related to " + cimObj.ToString());
            }
        }

        private void MapConductingEquipmentFields(CIMIdentifiedObject cimObj, ConductingEquipment xmlObj)
        {
            CIMConductingEquipment cimCe = cimObj as CIMConductingEquipment;

            // Voltage level
            if (cimObj.VoltageLevel > 0)
            {
                var vl = CreateOrGetBaseVoltage(cimObj.VoltageLevel);
                xmlObj.BaseVoltage = new ConductingEquipmentBaseVoltage() { @ref = vl.mRID };
            }

            // Equipment container
            if (cimObj.EquipmentContainerRef != null)
            {
                xmlObj.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
            }
        }
        

        private void MapACLineSegmentFields(CIMIdentifiedObject ce, ACLineSegmentExt xmlObj)
        {
            ////////////
            // Transfer all electric parameters

            double lenKm = Convert.ToDouble(ce.GetPropertyValueAsString("cim.length")) / 1000;

            if (ce.ContainsPropertyValue("cim.length"))
                ((ACLineSegmentExt)xmlObj).length = new Length() {  multiplier= UnitMultiplier.none, unit = UnitSymbol.m, unitSpecified = true, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.length")) };

            if (ce.ContainsPropertyValue("cim.bch"))
                ((ACLineSegmentExt)xmlObj).bch = new Susceptance() { Value = (Convert.ToDouble(ce.GetPropertyValueAsString("cim.bch")) * lenKm).ToString(), multiplierSpecified = true, multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.b0ch"))
                ((ACLineSegmentExt)xmlObj).b0ch = new Susceptance() { Value = (Convert.ToDouble(ce.GetPropertyValueAsString("cim.b0ch")) * lenKm).ToString(), multiplierSpecified = true, multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.gch"))
                ((ACLineSegmentExt)xmlObj).gch = new Conductance { Value = (Convert.ToDouble(ce.GetPropertyValueAsString("cim.gch")) * lenKm).ToString(),unitSpecified = true, multiplier = UnitMultiplier.micro, multiplierSpecified = true };

            if (ce.ContainsPropertyValue("cim.g0ch"))
                ((ACLineSegmentExt)xmlObj).g0ch = new Conductance { Value = (Convert.ToDouble(ce.GetPropertyValueAsString("cim.g0ch")) * lenKm).ToString(), unitSpecified = true, multiplier = UnitMultiplier.micro, multiplierSpecified = true };

            if (ce.ContainsPropertyValue("cim.r"))
                ((ACLineSegmentExt)xmlObj).r = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r")) * lenKm), unitSpecified = true, unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.r0"))
                ((ACLineSegmentExt)xmlObj).r0 = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r0")) * lenKm), unitSpecified = true, unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x"))
                ((ACLineSegmentExt)xmlObj).x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x")) * lenKm), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x0"))
                ((ACLineSegment)xmlObj).x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x0")) * lenKm), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.c"))
                ((ACLineSegmentExt)xmlObj).c = new Capacitance { Value = (Convert.ToDouble(ce.GetPropertyValueAsString("cim.c")) * lenKm).ToString(), multiplierSpecified = true, multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.c0"))
                ((ACLineSegmentExt)xmlObj).c0 = new Capacitance { Value = (Convert.ToDouble(ce.GetPropertyValueAsString("cim.c0")) * lenKm).ToString(), multiplierSpecified = true, multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.maximumcurrent"))
                ((ACLineSegmentExt)xmlObj).maximumCurrent = new CurrentFlow { unit = UnitSymbol.A, unitSpecified = true, multiplier = UnitMultiplier.none, multiplierSpecified = true, Value = ce.GetPropertyValueAsString("cim.maximumcurrent") };
        }

        private void MapExternalNetworkInjectionFields(CIMIdentifiedObject ce, ExternalNetworkInjection xmlObj)
        {
            ////////////
            // Transfer all electric parameters

            double lenKm = Convert.ToDouble(ce.GetPropertyValueAsString("cim.length")) / 1000;

            ((ExternalNetworkInjection)xmlObj).ikSecond = false;
            ((ExternalNetworkInjection)xmlObj).ikSecondSpecified = true;

            if (ce.ContainsPropertyValue("cim.iksecond"))
            {
                string val = ce.GetPropertyValueAsString("cim.iksecond");

                if (val != null && (val == "1" || val.ToLower() == "true" || val.ToLower() == "yes"))
                    ((ExternalNetworkInjection)xmlObj).ikSecond = true;
            }

            if (ce.ContainsPropertyValue("cim.maxinitialsymshccurrent"))
                ((ExternalNetworkInjection)xmlObj).maxInitialSymShCCurrent = new CurrentFlow { unit = UnitSymbol.A, unitSpecified = true, multiplier = UnitMultiplier.k, multiplierSpecified = true, Value = ce.GetPropertyValueAsString("cim.maxinitialsymshccurrent") };
      
            if (ce.ContainsPropertyValue("cim.maxr0tox0ratio"))
                ((ExternalNetworkInjection)xmlObj).maxR0ToX0Ratio =  Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxr0tox0ratio"));

            if (ce.ContainsPropertyValue("cim.maxr1tox1ratio"))
                ((ExternalNetworkInjection)xmlObj).maxR1ToX1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxr1tox1ratio"));

            if (ce.ContainsPropertyValue("cim.maxz0toz1ratio"))
                ((ExternalNetworkInjection)xmlObj).maxZ0ToZ1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxz0toz1ratio"));

            if (ce.ContainsPropertyValue("cim.mininitialsymshccurrent"))
                ((ExternalNetworkInjection)xmlObj).minInitialSymShCCurrent = new CurrentFlow { unit = UnitSymbol.A, unitSpecified = true, multiplier = UnitMultiplier.k, multiplierSpecified = true, Value = ce.GetPropertyValueAsString("cim.mininitialsymshccurrent") };

            if (ce.ContainsPropertyValue("cim.minr0tox0ratio"))
                ((ExternalNetworkInjection)xmlObj).minR0ToX0Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minr0tox0ratio"));

            if (ce.ContainsPropertyValue("cim.minr1tox1ratio"))
                ((ExternalNetworkInjection)xmlObj).minR1ToX1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minr1tox1ratio"));

            if (ce.ContainsPropertyValue("cim.minz0toz1ratio"))
                ((ExternalNetworkInjection)xmlObj).minZ0ToZ1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minz0toz1ratio"));

            if (ce.ContainsPropertyValue("cim.voltagefactor"))
                ((ExternalNetworkInjection)xmlObj).voltageFactor = new PU() { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.voltagefactor")) };

            if (ce.ContainsPropertyValue("cim.governorscd"))
                ((ExternalNetworkInjection)xmlObj).governorSCD = new ActivePowerPerFrequency { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.governorscd")) };

            if (ce.ContainsPropertyValue("cim.maxp"))
                ((ExternalNetworkInjection)xmlObj).maxP = new ActivePower() { unit = UnitSymbol.W, unitSpecified = true, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxp")) };

            if (ce.ContainsPropertyValue("cim.maxq"))
                ((ExternalNetworkInjection)xmlObj).maxQ = new ReactivePower { unit = UnitSymbol.VAr, unitSpecified = true, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxq")) };

            if (ce.ContainsPropertyValue("cim.minp"))
                ((ExternalNetworkInjection)xmlObj).minP = new ActivePower() { unit = UnitSymbol.W, unitSpecified = true, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minp")) };

            if (ce.ContainsPropertyValue("cim.minq"))
                ((ExternalNetworkInjection)xmlObj).minQ = new ReactivePower { unit = UnitSymbol.VAr, unitSpecified = true, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minq")) };


        }

        private void MapSwitchEquipmentFields(CIMIdentifiedObject cimObj, Switch xmlObj)
        {
            if (cimObj.ContainsPropertyValue("cim.normalopen"))
            {
                xmlObj.normalOpen = (bool)cimObj.GetPropertyValue("cim.normalopen");

                var str = cimObj.GetPropertyValue("cim.normalopen");

                if (xmlObj.normalOpen == true)
                {
                }
            }

            if (cimObj.ContainsPropertyValue("cim.ratedcurrent"))
            {
                string valueStr = cimObj.GetPropertyValue("cim.ratedcurrent").ToString();
                int valueInt;

                if (Int32.TryParse(valueStr, out valueInt))
                {
                    xmlObj.ratedCurrent = new CurrentFlow() { unit = UnitSymbol.A, unitSpecified = true, Value = "" + valueInt };
                }
            }

          
        }

        private void MapSubstation(CIMEquipmentContainer cimObj, Substation xmlObj)
        {
            Dictionary<int, VoltageLevel> substationVoltageLevels = new Dictionary<int, VoltageLevel>();

            // Create voltage levels
            int voltageLevelCounter = 0;

            foreach (var child in cimObj.Children)
            {
                if (child.VoltageLevel > 0)
                {
                    if (!substationVoltageLevels.ContainsKey(child.VoltageLevel)) 
                    {
                        voltageLevelCounter++;

                        var baseVoltage = CreateOrGetBaseVoltage(child.VoltageLevel);

                        substationVoltageLevels[child.VoltageLevel] = new VoltageLevel() { BaseVoltage = new VoltageLevelBaseVoltage() { @ref = baseVoltage.mRID }, mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, voltageLevelCounter).ToString(), name = baseVoltage.name, EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = xmlObj.mRID } };

                        CheckIfProcessed(substationVoltageLevels[child.VoltageLevel].mRID, substationVoltageLevels[child.VoltageLevel]);
                        
                        _voltageLevels.Add(substationVoltageLevels[child.VoltageLevel]);
                    }
                }
            }

            // Process substation children
            foreach (var child in cimObj.Children)
            {
                // Bay
                if (child.ClassType == CIMClassEnum.Bay)
                {
                    CIMEquipmentContainer bay = child as CIMEquipmentContainer;

                    var xmlBay = new BayExt() { mRID = bay.mRID.ToString(), name = bay.Name, description = bay.Description };
                    xmlBay.order = bay.GetPropertyValueAsString("cim.order");

                    if (substationVoltageLevels.ContainsKey(bay.VoltageLevel))
                        xmlBay.VoltageLevel = new BayVoltageLevel() { @ref = substationVoltageLevels[bay.VoltageLevel].mRID };

                    _bays.Add(xmlBay);

                    // Process bay children
                    foreach (var bayChild in bay.Children)
                    {
                        ProcessLeafObject(bayChild, xmlBay, child.VoltageLevel, substationVoltageLevels);
                    }
                }
                else
                {
                   ProcessLeafObject(child, xmlObj, cimObj.VoltageLevel, substationVoltageLevels);
                }
            }

        }

        private void MapEnclosure(CIMEquipmentContainer cimObj, Substation xmlObj)
        {
            // Always 400 volt voltage level
            var baseVoltage = CreateOrGetBaseVoltage(400);
            var voltageLevel = new VoltageLevel() { BaseVoltage = new VoltageLevelBaseVoltage() { @ref = baseVoltage.mRID }, mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID,1).ToString(), name = baseVoltage.name, EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = xmlObj.mRID } };
            CheckIfProcessed(voltageLevel.mRID, voltageLevel);
            _voltageLevels.Add(voltageLevel);

            // Process substation children
            foreach (var child in cimObj.Children)
            {
                // Bay
                if (child.ClassType == CIMClassEnum.Bay)
                {
                    CIMEquipmentContainer bay = child as CIMEquipmentContainer;
                    var xmlBay = new BayExt() { mRID = bay.mRID.ToString(), name = bay.Name, description = bay.Description, VoltageLevel = new BayVoltageLevel() { @ref = voltageLevel.mRID } };
                    xmlBay.order = bay.GetPropertyValueAsString("cim.order");

                    _bays.Add(xmlBay);

                    // Process bay children
                    foreach (var bayChild in bay.Children)
                    {
                        ProcessLeafObject(bayChild, xmlBay, child.VoltageLevel, null);
                    }
                }
                else
                {
                    ProcessLeafObject(child, xmlObj, cimObj.VoltageLevel, null);
                }
            }

        }

        private void MapTerminals(CIMConductingEquipment cimObj, ConductingEquipment xmlObj)
        {
            int endNumber = 1;

            foreach (var cimTerminal in cimObj.Terminals)
            {
                var mrid = cimTerminal.mRID.ToString();

                if (endNumber > 2)
                {
                    // No objects in CIM has more than 2 terminals
                    return;
                }

                // Create connectivity node
                ConnectivityNode xmlCn = null;

                if (cimTerminal.ConnectivityNode != null)
                    xmlCn = CreateOrGetConnectivityNode(cimTerminal.ConnectivityNode.mRID, cimObj);

                // Create terminal
                var terminal = CreateTerminal(cimTerminal, (ConductingEquipment)xmlObj, xmlCn, cimTerminal.EndNumber);

                endNumber++;
            }
        }

        private BaseVoltage CreateOrGetBaseVoltage(int voltageLevel)
        {
            if (_baseVoltages.ContainsKey(voltageLevel))
                return _baseVoltages[voltageLevel];
            else
            {
                string name = null;

                if (voltageLevel < 1000)
                {
                    name = "0," + (voltageLevel / 100) + " kV";
                }
                else
                {
                    name = (voltageLevel / 1000) + " kV";
                }

                _baseVoltages[voltageLevel] = new BaseVoltage() { mRID = Guid.NewGuid().ToString(), name = name, nominalVoltage = new Voltage() { multiplier = UnitMultiplier.none, unit = UnitSymbol.V, Value = voltageLevel, unitSpecified = true } };

                return _baseVoltages[voltageLevel];
            }
        }

        private PSRType CreateOrGetPSRType(string psrType)
        {
            if (psrType == null)
                return null;

            if (_psrTypes.ContainsKey(psrType))
                return _psrTypes[psrType];
            else
            {
                _psrTypes[psrType] = new PSRType() { mRID = Guid.NewGuid().ToString(), name = psrType };
                return _psrTypes[psrType];
            }
        }

        private AssetOwner CreateOrGetAssetOwner(string ownerName)
        {
            if (ownerName == null)
                return null;

            if (_assetOwners.ContainsKey(ownerName))
                return _assetOwners[ownerName];
            else
            {
                _assetOwners[ownerName] = new AssetOwner() { mRID = Guid.NewGuid().ToString(), name = ownerName };
                return _assetOwners[ownerName];
            }
        }

        private Maintainer CreateOrGetAssetMaintainer(string maintainerName)
        {
            if (maintainerName == null)
                return null;

            if (_assetMaintainers.ContainsKey(maintainerName))
                return _assetMaintainers[maintainerName];
            else
            {
                _assetMaintainers[maintainerName] = new Maintainer() { mRID = Guid.NewGuid().ToString(), name = maintainerName };
                return _assetMaintainers[maintainerName];
            }
        }

        private Location CreateLocation(double[] coords)
        {
            Location loc = null;

            if (!busExtension)
            {
                loc = new Location() { mRID = Guid.NewGuid().ToString() };
                loc.CoordinateSystem = new LocationCoordinateSystem() { @ref = _coordSys.mRID };

                _locations.Add(loc);

                if (coords != null)
                {

                    int seqNo = 1;
                    for (int i = 0; i < coords.Length; i += 2)
                    {
                        double x = ((double)coords[i]);
                        string xStr = Convert.ToString(x).Replace(',', '.');
                        double y = ((double)coords[i + 1]);
                        string yStr = Convert.ToString(y).Replace(',', '.');

                        PositionPoint point = new PositionPoint() { Location = new PositionPointLocation() { @ref = loc.mRID }, sequenceNumber = "" + seqNo, xPosition = xStr, yPosition = yStr };
                        _positions.Add(point);

                        seqNo++;
                    }

                }
            }

            return loc;
        }

        private Terminal CreateTerminal(CIMTerminal cimTerminal, ConductingEquipment eq, ConnectivityNode cn, int seqNo, PhaseCode phases = PhaseCode.ABC)
        {
            CheckIfProcessed(cimTerminal);

            var terminal = new Terminal() { 
                mRID = cimTerminal.mRID.ToString(),
                ConductingEquipment = new TerminalConductingEquipment() { @ref = eq.mRID },
                sequenceNumber = seqNo + ""
            };

            if (cn != null)
                terminal.ConnectivityNode = new TerminalConnectivityNode() { @ref = cn.mRID };


            if (phases != PhaseCode.ABC) {
                terminal.phases = phases;
                terminal.phasesSpecified = true;
            }
            
            _terminals.Add(terminal);

            return terminal;
        }

        private ConnectivityNode CreateOrGetConnectivityNode(Guid mRID, CIMConductingEquipment cimObj)
        {
            if (_connectivityNodes.ContainsKey(mRID))
                return _connectivityNodes[mRID];
            else
            {
                _connectivityNodes[mRID] = new ConnectivityNode() { mRID = mRID.ToString() };
                
                CheckIfProcessed(mRID.ToString(), "Connectivity node: " + mRID + " CIM Obj: " + cimObj.ToString());
                return _connectivityNodes[mRID];
            }
        }

        #region Topology extension

        private bool TopologyShouldElementBeIncluded(CIMIdentifiedObject cimObj)
        {
            if (_topoParameters == null)
                return true;
            else if (cimObj.ClassType == CIMClassEnum.Substation ||
                cimObj.ClassType == CIMClassEnum.Enclosure ||
                cimObj.ClassType == CIMClassEnum.BusbarSection ||
                cimObj.ClassType == CIMClassEnum.EnergyConsumer ||
                cimObj.ClassType == CIMClassEnum.PowerTransformer)
                return true;
            else if (cimObj.ClassType == CIMClassEnum.ACLineSegment && !_topoParameters.ExcludeACLineSegments)
                return true;
            else if (cimObj.ClassType == CIMClassEnum.Breaker ||
                cimObj.ClassType == CIMClassEnum.LoadBreakSwitch ||
                cimObj.ClassType == CIMClassEnum.Disconnector ||
                cimObj.ClassType == CIMClassEnum.Fuse) 
            {
                // If we should include all switches, just include it
                if (!_topoParameters.ExcludeSwitches)
                    return true;
                // If start of feeder, we must include switch
                else if (_topoParameters.TopologyProcessingResult != null)
                {
                    var topoInfo = _topoParameters.TopologyProcessingResult.GetDAXTopologyInfoByCIMObject((CIMConductingEquipment)cimObj);
                    if (topoInfo != null && topoInfo.IsStartOfFeeder)
                        return true;
                }
            }
            
            return false;
        }

        private CIMConductingEquipment TopologyFilterParents(CIMConductingEquipment cimObj)
        {
            var parent = cimObj;

            if (cimObj != null && _topoParameters != null && _topoParameters.TopologyProcessingResult != null)
            {
                var topoInfo = _topoParameters.TopologyProcessingResult.GetDAXTopologyInfoByCIMObject(cimObj);

                if (topoInfo != null)
                {
                    if (topoInfo.IsStartOfFeeder)
                        return cimObj;

                    // To handle switch-aclinesegment-switch situation.
                    for (int i = 0; i < 4; i++)
                    {
                        // Bypass acline segments, if options says to do so
                        if (_topoParameters.ExcludeACLineSegments)
                        {
                            if (parent.ClassType == CIMClassEnum.ACLineSegment)
                            {
                                var nextTopoInfo = _topoParameters.TopologyProcessingResult.GetDAXTopologyInfoByCIMObject(parent);

                                while (nextTopoInfo != null && parent.ClassType == CIMClassEnum.ACLineSegment)
                                {
                                    nextTopoInfo = _topoParameters.TopologyProcessingResult.GetDAXTopologyInfoByCIMObject(parent);

                                    if (nextTopoInfo != null && nextTopoInfo.IsStartOfFeeder)
                                        return parent;

                                    if (nextTopoInfo != null &&
                                        nextTopoInfo.Parents != null &&
                                        nextTopoInfo.Parents.Count > 0 &&
                                        nextTopoInfo.Parents[0] != null)
                                        parent = nextTopoInfo.Parents[0];
                                    else
                                        nextTopoInfo = null;
                                }
                            }
                        }


                        // Bypass switches segments, if options says to do so
                        if (_topoParameters.ExcludeSwitches)
                        {
                            if (parent.ClassType == CIMClassEnum.Breaker ||
                                parent.ClassType == CIMClassEnum.LoadBreakSwitch ||
                                parent.ClassType == CIMClassEnum.Disconnector ||
                                parent.ClassType == CIMClassEnum.Fuse)
                            {
                                var nextTopoInfo = _topoParameters.TopologyProcessingResult.GetDAXTopologyInfoByCIMObject(parent);

                                while (nextTopoInfo != null &&
                                    (
                                    parent.ClassType == CIMClassEnum.Breaker ||
                                    parent.ClassType == CIMClassEnum.LoadBreakSwitch ||
                                    parent.ClassType == CIMClassEnum.Disconnector ||
                                    parent.ClassType == CIMClassEnum.Fuse
                                    ))
                                {

                                    if (nextTopoInfo != null && nextTopoInfo.IsStartOfFeeder)
                                        return parent;

                                    nextTopoInfo = _topoParameters.TopologyProcessingResult.GetDAXTopologyInfoByCIMObject(parent);

                                    if (nextTopoInfo != null &&
                                        nextTopoInfo.Parents != null &&
                                        nextTopoInfo.Parents.Count > 0 &&
                                        nextTopoInfo.Parents[0] != null)
                                        parent = nextTopoInfo.Parents[0];
                                    else
                                        nextTopoInfo = null;
                                }
                            }
                        }
                    }
                }
            }


            return parent;

        }

        #endregion
    }

    public class TopologySerializationParameters
    {
        public ITopologyProcessingResult TopologyProcessingResult { get; set; }
        public bool ExcludeACLineSegments { get; set; }
        public bool ExcludeSwitches { get; set; }
        public bool ExcludeConnectivity { get; set; }
    }
}
