using CIM.PhysicalNetworkModel;
using DAX.IO.CIM.Processing;
using DAX.Util;

namespace DAX.IO.CIM.Serialization.CIM100
{
    public class CIM100Serializer : IDAXInitializeable, IDAXSerializeable
    {
        private List<CIMIdentifiedObject> _cimObjects;
        private CIMMetaDataRepository _metaDataRepository;

        private CoordinateSystem _coordSys = new CoordinateSystem() { mRID = "3455e814-5187-4d7e-9164-ca94797b72bd", crsUrn = "urn:ogc:def:crs:EPSG::25832", name = "ETRS89 / UTM zone 32N" };

        private Dictionary<Guid, ConnectivityNode> _connectivityNodes = new Dictionary<Guid, ConnectivityNode>();
        private Dictionary<Guid, object> _cimObjectHasBeenProcessed = new Dictionary<Guid, object>();
        private Dictionary<Guid, Dictionary<int, VoltageLevel>> _substationVoltageLevelsByMrid = new Dictionary<Guid,Dictionary<int, VoltageLevel>>();

        private Dictionary<string, Guid> _manufacturerNameToGuid = new Dictionary<string, Guid>();
        private Dictionary<string, Guid> _assetModelNameToGuid = new Dictionary<string, Guid>();


        bool hasUsagePointRelation = false;
        bool busExtension = true;

        bool _includeEquipment;
        bool _includeAsset;
        bool _includeLocation;

        public CIM100Serializer()
        {
        }

        public CIM100Serializer(CIMMetaDataRepository repository, CIMEquipmentContainer substation)
        {
            _metaDataRepository = repository;
            _cimObjects = substation.Children;
            _cimObjects.Add(substation);
        }

        public CIM100Serializer(CIMMetaDataRepository repository, List<CIMIdentifiedObject> cimObjects)
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
            throw new NotImplementedException();
        }

        #endregion

        public IEnumerable<IdentifiedObject> GetIdentifiedObjects(CIMMetaDataRepository repository, List<CIMIdentifiedObject> cimObjects, bool includeEquipment, bool includeAsset, bool includeLocation)
        {
            _metaDataRepository = repository;
            _cimObjects = cimObjects;
            _includeLocation = includeLocation;
            _includeAsset = includeAsset;
            _includeEquipment = includeEquipment;

            if (_includeLocation)
                yield return _coordSys;

            foreach (var cimObj in _cimObjects)
            {
                foreach (var identifiedObject in ProcessRootObject(cimObj))
                {
                    yield return identifiedObject;
                }
            }
        }

        private IEnumerable<IdentifiedObject> ProcessRootObject(CIMIdentifiedObject cimObj)
        {
            if (cimObj.ClassType == CIMClassEnum.Substation)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var substation = cimObj as CIMEquipmentContainer;
                    Substation xmlObj = new Substation();

                    var asset = MapAsset(substation, xmlObj);
                    if (asset != null)
                    {
                        // Add maintainance date, if set on substation object
                        if (substation.ContainsPropertyValue("cim.asset.lastmaintenancedate"))
                        {
                            var dateValStr = substation.GetPropertyValueAsString("cim.asset.lastmaintenancedate");

                            DateTime dateVal;

                            if (DateTime.TryParse(dateValStr, out dateVal))
                            {
                                asset.lastMaintenanceDateSpecified = true;
                                asset.lastMaintenanceDate = dateVal;
                            }
                        }

                        yield return asset;
                    }

                    var assetInfo = MapDummyAssetInfo(substation, xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;


                    foreach (var identifiedObject in MapLocation(substation, xmlObj))
                        yield return identifiedObject;

                    MapIdentifiedObjectFields(substation, xmlObj);

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapSubstation(substation, xmlObj))
                        yield return identifiedObject;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.Enclosure)
            {
                if (!CheckIfProcessed(cimObj, false))
                {

                    var enclosure = cimObj as CIMEquipmentContainer;
                    Substation xmlObj = new Substation();

                    var asset = MapAsset(enclosure, xmlObj);
                    if (asset != null)
                        yield return asset;

                    foreach (var identifiedObject in MapLocation(enclosure, xmlObj))
                        yield return identifiedObject;

                    var assetInfo = MapDummyAssetInfo(enclosure, xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;


                    MapIdentifiedObjectFields(enclosure, xmlObj);

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapEnclosure(enclosure, xmlObj))
                        yield return identifiedObject;


                }
            }
            else if (cimObj.ClassType == CIMClassEnum.EnergyConsumer)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var consumer = cimObj as CIMConductingEquipment;

                    EnergyConsumer xmlConsumer = new EnergyConsumer();

                    var asset = MapAsset(consumer, xmlConsumer);
                    if (asset != null)
                        yield return asset;
                    

                    foreach (var identifiedObject in MapLocation(consumer, xmlConsumer))
                        yield return identifiedObject;

                    xmlConsumer.mRID = cimObj.mRID.ToString();
                    MapIdentifiedObjectFields(cimObj, xmlConsumer);
                    MapConductingEquipmentFields(consumer, xmlConsumer);

                    if (_includeEquipment)
                        yield return xmlConsumer;

                    // Map terminals
                    foreach (var identifiedObject in MapTerminals(consumer, xmlConsumer))
                        yield return identifiedObject;

                    if (!hasUsagePointRelation)
                    {
                        UsagePointExt xmlUsagePoint = new UsagePointExt();

                        // Copy information to usage point
                        xmlUsagePoint.mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 10).ToString();
                        CheckIfProcessed(xmlUsagePoint.mRID, xmlUsagePoint);

                        xmlUsagePoint.name = xmlConsumer.name;
                        xmlUsagePoint.description = xmlConsumer.description;

                        // Relate usagepoint to energy consumer                
                        xmlUsagePoint.Equipments = new UsagePointEquipments() { @ref = xmlConsumer.mRID };

                        
                        // Installation id
                        if (cimObj.ContainsPropertyValue("cim.installationid"))
                            xmlUsagePoint.installationId = cimObj.GetPropertyValueAsString("cim.installationid");

                        // Meter id
                        if (cimObj.ContainsPropertyValue("cim.meterid"))
                            xmlUsagePoint.meterId = cimObj.GetPropertyValueAsString("cim.meterid");

                        // Yearly usage
                        if (cimObj.ContainsPropertyValue("cim.yearlyusage"))
                        {
                            Double yearlyUsage;

                            if (Double.TryParse(cimObj.GetPropertyValueAsString("cim.yearlyusage"), out yearlyUsage))
                            {
                                xmlUsagePoint.yearlyUsage = new ApparentPower() { Value = yearlyUsage, multiplier = UnitMultiplier.k, unit = UnitSymbol.Wh };
                            }
                        }

                        if (_includeEquipment)
                            yield return xmlUsagePoint;
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

                                if (_includeEquipment)
                                    yield return xmlUsagePoint;
                            }
                        }
                    }
                }
            }
            // Cables outside substation
            else if (cimObj.ClassType == CIMClassEnum.ACLineSegment && cimObj.EquipmentContainerRef == null)
            {
                bool hasTerminalsConnectedToEachOther = false;

                var neighboors = cimObj.GetNeighbours();

                if (neighboors.Count == 2 && neighboors[0] == neighboors[1])
                {
                    hasTerminalsConnectedToEachOther = true;
                }

                if (!CheckIfProcessed(cimObj, false))
                {
                    var ce = cimObj as CIMConductingEquipment;
                    ACLineSegmentExt xmlObj = new ACLineSegmentExt();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;

                    foreach (var identifiedObject in MapLocation(ce, (PowerSystemResource)xmlObj))
                        yield return identifiedObject;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapACLineSegmentFields(ce, (ACLineSegmentExt)xmlObj);

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
            }
            // Indfødninger 
            else if (cimObj.ClassType == CIMClassEnum.ExternalNetworkInjection)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ce = cimObj as CIMConductingEquipment;
                    ExternalNetworkInjection xmlObj = new ExternalNetworkInjection();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    foreach (var identifiedObject in MapLocation(ce, (PowerSystemResource)xmlObj))
                        yield return identifiedObject;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapExternalNetworkInjectionFields(ce, (ExternalNetworkInjection)xmlObj);

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.CableInfo)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    CableInfoExt xmlObj = new CableInfoExt();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);



                    // Outer jacket kind
                    if (ci.ContainsPropertyValue("cim.outerjacketkind"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.outerjacketkind");

                        if (Enum.TryParse(val, out CableOuterJacketKind enumVal))
                        {
                            xmlObj.outerJacketKind = enumVal;
                            xmlObj.outerJacketKindSpecified = true;
                        }
                    }

                    // isolation material
                    if (ci.ContainsPropertyValue("cim.isolation.material"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.isolation.material");

                        if (Enum.TryParse(val, out WireInsulationKind enumVal))
                        {
                            xmlObj.insulationMaterial = enumVal;
                            xmlObj.insulationMaterialSpecified = true;
                        }
                    }


                    // Conductor count
                    if (ci.ContainsPropertyValue("cim.conductor.count"))
                    {
                        var val = ci.GetPropertyValueAsInt("cim.conductor.count");

                        if (val != null)
                            xmlObj.conductorCount = val.Value;
                    }

                    // Conductor material
                    if (ci.ContainsPropertyValue("cim.conductor.material"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.conductor.material");

                        if (Enum.TryParse(val, out WireMaterialKind enumVal))
                        {
                            xmlObj.material = enumVal;
                            xmlObj.materialSpecified = true;
                        }
                    }

                    // Conductor size
                    if (ci.ContainsPropertyValue("cim.conductor.size"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.conductor.size");

                        if (val != null)
                        {
                            xmlObj.conductorCrossSectionalArea = val.Value;
                            xmlObj.conductorCrossSectionalAreaSpecified = true;
                        }
                    }

                    // Shield material
                    if (ci.ContainsPropertyValue("cim.shield.material"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.shield.material");

                        if (Enum.TryParse(val, out CableShieldMaterialKind enumVal))
                        {
                            xmlObj.shieldMaterial = enumVal;
                            xmlObj.shieldMaterialSpecified = true;
                        }
                    }

                    // Shield size
                    if (ci.ContainsPropertyValue("cim.shield.size"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.shield.size");

                        if (val != null)
                        {
                            xmlObj.shieldCrossSectionalArea = val.Value;
                            xmlObj.shieldCrossSectionalAreaSpecified = true;
                        }
                    }

                    // Product asset model reference
                    if (ci.ContainsPropertyValue("cim.ref.productassetmodel"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.ref.productassetmodel");

                        if (val != null)
                        {
                            xmlObj.AssetModel = new AssetInfoAssetModel() { @ref = val };
                        }
                    }

                    MapWireInfoFields(ci, xmlObj);

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.OverheadWireInfo)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    OverheadWireInfoExt xmlObj = new OverheadWireInfoExt();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);

                    // Rated current
                    if (ci.ContainsPropertyValue("cim.ratedcurrent"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedcurrent");

                        if (val != null)
                        {
                            xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    // Conductor count
                    if (ci.ContainsPropertyValue("cim.conductor.count"))
                    {
                        var val = ci.GetPropertyValueAsInt("cim.conductor.count");

                        if (val != null)
                            xmlObj.conductorCount = val.Value;
                    }

                    // Conductor material
                    if (ci.ContainsPropertyValue("cim.conductor.material"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.conductor.material");

                        if (Enum.TryParse(val, out WireMaterialKind enumVal))
                        {
                            xmlObj.material = enumVal;
                            xmlObj.materialSpecified = true;
                        }
                    }

                    // Conductor size
                    if (ci.ContainsPropertyValue("cim.conductor.size"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.conductor.size");

                        if (val != null)
                        {
                            xmlObj.conductorCrossSectionalArea = val.Value;
                            xmlObj.conductorCrossSectionalAreaSpecified = true;
                        }
                    }

                    MapWireInfoFields(ci, xmlObj);

                    // Product asset model reference
                    if (ci.ContainsPropertyValue("cim.ref.productassetmodel"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.ref.productassetmodel");

                        if (val != null)
                        {
                            xmlObj.AssetModel = new AssetInfoAssetModel() { @ref = val };
                        }
                    }

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.SwitchInfo)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    SwitchInfoExt xmlObj = new SwitchInfoExt();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);

                    // Product asset model reference
                    if (ci.ContainsPropertyValue("cim.ref.productassetmodel"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.ref.productassetmodel");

                        if (val != null)
                        {
                            xmlObj.AssetModel = new AssetInfoAssetModel() { @ref = val };
                        }
                    }


                    // Rated current
                    if (ci.ContainsPropertyValue("cim.ratedcurrent"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedcurrent");

                        if (val != null)
                        {
                            xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    // Rated voltage
                    if (ci.ContainsPropertyValue("cim.ratedvoltage"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedvoltage");

                        if (val != null)
                        {
                            xmlObj.ratedVoltage = new Voltage() { multiplier = UnitMultiplier.none, unit = UnitSymbol.V, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    // Breaking Capacity
                    if (ci.ContainsPropertyValue("cim.ratedbreakingcurrent"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedbreakingcurrent");

                        if (val != null)
                        {
                            xmlObj.ratedBreakingCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    // Making Current
                    if (ci.ContainsPropertyValue("cim.ratedmakingcurrent"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedmakingcurrent");

                        if (val != null)
                        {
                            xmlObj.ratedMakingCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    // Withstand current 1 sec
                    if (ci.ContainsPropertyValue("cim.ratedwithstandcurrent1sec"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedwithstandcurrent1sec");

                        if (val != null)
                        {
                            xmlObj.ratedWithstandCurrent1sec = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    // Withstand current 3 sec
                    if (ci.ContainsPropertyValue("cim.ratedwithstandcurrent3sec"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedwithstandcurrent3sec");

                        if (val != null)
                        {
                            xmlObj.ratedWithstandCurrent3sek = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.BusbarSectionInfo)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    BusbarSectionInfo xmlObj = new BusbarSectionInfo();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);

                    // Product asset model reference
                    if (ci.ContainsPropertyValue("cim.ref.productassetmodel"))
                    {
                        var val = ci.GetPropertyValueAsString("cim.ref.productassetmodel");

                        if (val != null)
                        {
                            xmlObj.AssetModel = new AssetInfoAssetModel() { @ref = val };
                        }
                    }


                    // Rated current
                    if (ci.ContainsPropertyValue("cim.ratedcurrent"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedcurrent");

                        if (val != null)
                        {
                            xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    // Rated voltage
                    if (ci.ContainsPropertyValue("cim.ratedvoltage"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedvoltage");

                        if (val != null)
                        {
                            xmlObj.ratedVoltage = new Voltage() { multiplier = UnitMultiplier.none, unit = UnitSymbol.V, Value = Convert.ToDouble(val.Value) };
                        }
                    }

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.Manufacturer)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    Manufacturer xmlObj = new Manufacturer();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.ProductAssetModel)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    ProductAssetModel xmlObj = new ProductAssetModel();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);

                    var manufacturerMrid = ci.GetPropertyValueAsString("cim.ref.manufacturer");

                    if (manufacturerMrid != null)
                        xmlObj.Manufacturer = new ProductAssetModelManufacturer() { @ref = manufacturerMrid };

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.ProtectionEquipment)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    var protectiveSwitchMrid = ci.GetPropertyValueAsString("cim.ref.protectiveswitch");
                    var currenttransformerMrid = ci.GetPropertyValueAsString("cim.ref.currenttransformer");
                    var potentialtransformerMrid = ci.GetPropertyValueAsString("cim.ref.potentialtransformer");

                    if (protectiveSwitchMrid != null)
                    {
                        ProtectionEquipmentExt protectionEq = new ProtectionEquipmentExt();
                        MapIdentifiedObjectFields(ci, (IdentifiedObject)protectionEq);
                        protectionEq.ProtectedSwitches = new ProtectionEquipmentProtectedSwitches[] { new ProtectionEquipmentProtectedSwitches() { @ref = protectiveSwitchMrid  } };
                        
                        if (currenttransformerMrid != null)
                            protectionEq.CurrentTransformers = new ProtectionEquipmentExtCurrentTransformers[] { new ProtectionEquipmentExtCurrentTransformers() { @ref = currenttransformerMrid } };

                        if (potentialtransformerMrid != null)
                            protectionEq.PotentialTransformers = new ProtectionEquipmentExtPotentialTransformers[] { new ProtectionEquipmentExtPotentialTransformers() { @ref = potentialtransformerMrid } };

                        var asset = MapAsset(ci, protectionEq);

                        if (asset != null)
                            yield return asset;

                        var assetInfo = MapDummyAssetInfo(ci, protectionEq);

                        if (assetInfo != null)
                            yield return assetInfo;


                        if (_includeEquipment)
                            yield return protectionEq;

                    }
                }
            }
        }

        private void MapWireInfoFields(CIMIdentifiedObject ce, WireInfoExt xmlObj)
        {
            if (ce.ContainsPropertyValue("cim.bch"))
                ((WireInfoExt)xmlObj).bch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.bch")), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.b0ch"))
                ((WireInfoExt)xmlObj).b0ch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.b0ch")), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.gch"))
                ((WireInfoExt)xmlObj).gch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.gch")), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.g0ch"))
                ((WireInfoExt)xmlObj).g0ch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.g0ch")), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.r"))
                ((WireInfoExt)xmlObj).r = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.r0"))
                ((WireInfoExt)xmlObj).r0 = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r0"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x"))
                ((WireInfoExt)xmlObj).x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x0"))
                ((WireInfoExt)xmlObj).x0 = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x0"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            // Rated current
            if (ce.ContainsPropertyValue("cim.ratedcurrent"))
            {
                var val = ce.GetPropertyValueAsDecimal("cim.ratedcurrent");

                if (val != null)
                {
                    xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value) };
                }
            }

            // Rated voltage
            if (ce.ContainsPropertyValue("cim.ratedvoltage"))
            {
                var val = ce.GetPropertyValueAsDecimal("cim.ratedvoltage");

                if (val != null)
                {
                    xmlObj.ratedVoltage = new Voltage() { multiplier = UnitMultiplier.none, unit = UnitSymbol.V, Value = Convert.ToDouble(val.Value) };
                }
            }

            if (ce.ContainsPropertyValue("cim.ratedwithstandcurrent1sec"))
                ((WireInfoExt)xmlObj).ratedWithstandCurrent1sec = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.ratedwithstandcurrent1sec")) };
        }

        private IEnumerable<IdentifiedObject> ProcessLeafObject(CIMIdentifiedObject cimObj, EquipmentContainer equipmentContainer, int equipmentContainerVoltageLevel, Dictionary<int, VoltageLevel> substationVoltageLevels)
        {
            IdentifiedObject xmlObj = null;

            if (cimObj is CIMConductingEquipment)
            {
                var ce = cimObj as CIMConductingEquipment;

                if (ce.ClassType == CIMClassEnum.ACLineSegment)
                {
                    bool hasTerminalsConnectedToEachOther = false;

                    var neighboors = cimObj.GetNeighbours();

                    if (neighboors.Count == 2 && neighboors[0] == neighboors[1])
                    {
                        hasTerminalsConnectedToEachOther = true;
                    }

                    if (!CheckIfProcessed(cimObj, false))
                    {
                        xmlObj = new ACLineSegmentExt();

                        var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                        if (asset != null)
                            yield return asset;

                        MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                        MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                        MapACLineSegmentFields(ce, (ACLineSegmentExt)xmlObj);

                        if (_includeEquipment)
                            yield return xmlObj;

                        foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                    }
                }
                else if (ce.ClassType == CIMClassEnum.LoadBreakSwitch)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new LoadBreakSwitch();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;


                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);

                    ((LoadBreakSwitch)xmlObj).normalOpen = (bool)ce.GetPropertyValue("cim.normalopen");

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
                else if (ce.ClassType == CIMClassEnum.Breaker)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new Breaker();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);

                    // breaking capacity
                    if (cimObj.ContainsPropertyValue("cim.breakingcapacity"))
                    {
                        string valueStr = cimObj.GetPropertyValue("cim.breakingcapacity").ToString();
                        int valueInt;

                        if (Int32.TryParse(valueStr, out valueInt))
                        {
                            ((Breaker)xmlObj).breakingCapacity = new CurrentFlow() { unit = UnitSymbol.A, Value = valueInt };
                        }
                    }

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
                else if (ce.ClassType == CIMClassEnum.Fuse)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new Fuse();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;



                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
                else if (ce.ClassType == CIMClassEnum.Disconnector)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new Disconnector();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    MapSwitchEquipmentFields(ce, (Switch)xmlObj);

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;

                }
                else if (ce.ClassType == CIMClassEnum.FaultIndicatorExt)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new FaultIndicatorExt();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;


                    var faultIndicator = (FaultIndicatorExt)xmlObj;
                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);

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

                    if (_includeEquipment)
                        yield return xmlObj;
                }
                else if (ce.ClassType == CIMClassEnum.CurrentTransformer)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new CurrentTransformerExt();


                    // create asset info
                    var assetInfo = new CurrentTransformerInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                    if (cimObj.ContainsPropertyValue("cim.primarycurrent"))
                        assetInfo.primaryCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.primarycurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                    if (cimObj.ContainsPropertyValue("cim.secondarycurrent"))
                        assetInfo.secondaryCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.secondarycurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                    ce.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                    // create asset
                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj, true);



                    var currentTransformer = (CurrentTransformerExt)xmlObj;
                    MapIdentifiedObjectFields(ce, (PowerSystemResource)xmlObj);

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
                        currentTransformer.maximumCurrent = new CurrentFlow() { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.maximumcurrent")) };

                    if (_includeEquipment)
                        yield return xmlObj;

                    if (_includeAsset)
                    {
                        if (asset != null)
                            yield return asset;

                        if (assetInfo != null)
                            yield return assetInfo;
                    }
                }
                else if (ce.ClassType == CIMClassEnum.PotentialTransformer)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new PotentialTransformer();


                    // create asset info
                    var assetInfo = new PotentialTransformerInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                    if (cimObj.ContainsPropertyValue("cim.primaryvoltage"))
                        assetInfo.primaryVoltage = new Voltage() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.primaryvoltage")), multiplier = UnitMultiplier.c, unit = UnitSymbol.V };

                    if (cimObj.ContainsPropertyValue("cim.secondaryvoltage"))
                        assetInfo.secondaryVoltage = new Voltage() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.secondaryvoltage")), multiplier = UnitMultiplier.c, unit = UnitSymbol.V };

                    ce.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                    // create asset
                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj, true);

                    var potentialTransformer = (PotentialTransformer)xmlObj;
                    MapIdentifiedObjectFields(ce, (PotentialTransformer)xmlObj);

                    // Equipment container
                    if (cimObj.EquipmentContainerRef != null)
                    {
                        potentialTransformer.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                    }

                    // Connect to termianl
                    if (ce.ContainsPropertyValue("cim.terminal"))
                    {
                        potentialTransformer.Terminal = new AuxiliaryEquipmentTerminal() { @ref = ce.GetPropertyValueAsString("cim.terminal") };
                    }

                   
                    if (_includeEquipment)
                        yield return xmlObj;

                    if (_includeAsset)
                    {
                        if (asset != null)
                            yield return asset;

                        if (assetInfo != null)
                            yield return assetInfo;
                    }
                }

                else if (ce.ClassType == CIMClassEnum.PowerTransformer)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new PowerTransformer();

                    // create asset info
                    var assetInfo = new PowerTransformerInfoExt { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                    if (cimObj.ContainsPropertyValue("ext.thermalrateds"))
                        assetInfo.thermalRatedS = new ApparentPower { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.thermalrateds")) * 1000000, multiplier = UnitMultiplier.c, unit = UnitSymbol.VA };

                    if (cimObj.ContainsPropertyValue("ext.lowerbound"))
                        assetInfo.lowerBound = new PU() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.lowerbound")), multiplier = UnitMultiplier.c, unit = UnitSymbol.none };

                    if (cimObj.ContainsPropertyValue("ext.upperbound"))
                        assetInfo.upperBound = new PU() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.upperbound")), multiplier = UnitMultiplier.c, unit = UnitSymbol.none };

                    assetInfo.hasInternalDeltaWinding = false;

                    if (cimObj.ContainsPropertyValue("ext.hasInternalDeltaWinding"))
                    {
                        if (cimObj.GetPropertyValueAsString("ext.hasInternalDeltaWinding") == "1")
                            assetInfo.hasInternalDeltaWinding = true;
                    }

                    // Add product model reference if exists
                    if (cimObj.ContainsPropertyValue("cim.ref.productassetmodel"))
                    {
                        var assetModelId = cimObj.GetPropertyValueAsString("cim.ref.productassetmodel").ToLower();
                        assetInfo.AssetModel = new AssetInfoAssetModel() { @ref = assetModelId };
                    }

                    ce.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                    yield return assetInfo;


                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);

                    if (_includeEquipment)
                    {
                        yield return xmlObj;

                        // Create terminal and winding for each neighbor
                        bool firstEndFound = false;

                        PowerTransformerEndExt firstEnd = null;

                        var ptTerminals = ((CIMConductingEquipment)cimObj).Terminals.ToList();

                        foreach (var cimTerminal in ptTerminals)
                        {
                            double baseVoltage = 400;

                            if (cimTerminal.ConnectivityNode != null)
                            {
                                var acSegments = cimTerminal.ConnectivityNode.GetNeighbours(CIMClassEnum.ACLineSegment);

                                if (acSegments.Count > 0)
                                {
                                    baseVoltage = acSegments[0].VoltageLevel;
                                }
                            }

                            ConnectivityNode xmlCn = null;

                            if (cimTerminal.ConnectivityNode != null)
                            {
                                // Create xml connectivity node
                                foreach (var identifiedObject in CreateAndYieldConnectivityNodeIfNotExists(cimTerminal.ConnectivityNode.mRID, (CIMConductingEquipment)cimObj))
                                    yield return identifiedObject;

                                xmlCn = GetConnectivityNode(cimTerminal.ConnectivityNode.mRID, (CIMConductingEquipment)cimObj);
                            }

                            // Create xml terminal
                            foreach (var identifiedObject in CreateTerminal(cimTerminal, (ConductingEquipment)xmlObj, xmlCn, cimTerminal.EndNumber))
                                yield return identifiedObject;

                            // Create xml power transformer end
                            PowerTransformerEndExt xmlTe = new PowerTransformerEndExt()
                            {
                                mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, cimTerminal.EndNumber + 10).ToString(),
                                PowerTransformer = new PowerTransformerEndPowerTransformer() { @ref = xmlObj.mRID },
                                endNumber = "" + cimTerminal.EndNumber,
                                Terminal = new TransformerEndTerminal() { @ref = cimTerminal.mRID.ToString() },
                                BaseVoltage = baseVoltage
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
                                xmlTe.ratedS = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v1.rateds")) };

                            if (ce.ContainsPropertyValue("ext.loss"))
                                xmlTe.loss = new KiloActivePower { unit = UnitSymbol.W, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("ext.loss")) };

                            if (ce.ContainsPropertyValue("ext.losszero"))
                                xmlTe.lossZero = new KiloActivePower { unit = UnitSymbol.W, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("ext.losszero")) };

                            if (ce.ContainsPropertyValue("ext.ratingFactor"))
                                xmlTe.ratingFactor = new PerCent() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.ratingFactor")), multiplier = UnitMultiplier.c, unit = UnitSymbol.none };


                            // Vinkling 1
                            if (cimTerminal.EndNumber == 1)
                            {
                                if (ce.ContainsPropertyValue("cim.v1.nominalvoltage"))
                                    xmlTe.nominalVoltage = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v1.nominalvoltage")) };

                                if (ce.ContainsPropertyValue("cim.v1.phaseangleclock"))
                                    xmlTe.phaseAngleClock = ce.GetPropertyValueAsString("cim.v1.phaseangleclock");

                                if (ce.ContainsPropertyValue("cim.v1.connectionkind"))
                                    xmlTe.connectionKind = ce.GetPropertyValueAsString("cim.v1.connectionkind");

                                if (ce.ContainsPropertyValue("cim.v1.grounded"))
                                    xmlTe.grounded = ParseBoolString(ce.GetPropertyValueAsString("cim.v1.grounded"));

                                if (ce.ContainsPropertyValue("cim.v1.ratedu"))
                                    xmlTe.ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v1.ratedu")) };

                                if (ce.ContainsPropertyValue("ext.v1.uk"))
                                    xmlTe.uk = new PerCent() { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v1.uk")) };

                                if (ce.ContainsPropertyValue("ext.v1.excitingcurrentzero"))
                                    xmlTe.excitingCurrentZero = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v1.excitingcurrentzero")) };

                                if (ce.ContainsPropertyValue("cim.v1.r"))
                                    xmlTe.r = new Resistance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.r")) };

                                if (ce.ContainsPropertyValue("cim.v1.x"))
                                    xmlTe.x = new Reactance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.x")) };

                                if (ce.ContainsPropertyValue("cim.v1.g"))
                                    xmlTe.g = new Conductance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.g")) };

                                if (ce.ContainsPropertyValue("cim.v1.b"))
                                    xmlTe.b = new Susceptance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.b")) };

                                if (ce.ContainsPropertyValue("cim.v1.r0"))
                                    xmlTe.r0 = new Resistance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.r0")) };

                                if (ce.ContainsPropertyValue("cim.v1.x0"))
                                    xmlTe.x0 = new Reactance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.x0")) };

                                if (ce.ContainsPropertyValue("cim.v1.g"))
                                    xmlTe.g = new Conductance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.g")) };

                                if (ce.ContainsPropertyValue("cim.v1.b"))
                                    xmlTe.b = new Susceptance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(ce.GetPropertyValueAsString("cim.v1.b")) };

                            }

                            // Vinkling 2
                            if (cimTerminal.EndNumber == 2)
                            {
                                if (ce.ContainsPropertyValue("cim.v2.nominalvoltage"))
                                    xmlTe.nominalVoltage = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v2.nominalvoltage")) };

                                if (ce.ContainsPropertyValue("cim.v2.phaseangleclock"))
                                    xmlTe.phaseAngleClock = ce.GetPropertyValueAsString("cim.v2.phaseangleclock");

                                if (ce.ContainsPropertyValue("cim.v2.connectionkind"))
                                    xmlTe.connectionKind = ce.GetPropertyValueAsString("cim.v2.connectionkind");

                                if (ce.ContainsPropertyValue("cim.v2.grounded"))
                                    xmlTe.grounded = ParseBoolString(ce.GetPropertyValueAsString("cim.v2.grounded"));

                                if (ce.ContainsPropertyValue("cim.v2.ratedu"))
                                    xmlTe.ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.v2.ratedu")) };

                                if (ce.ContainsPropertyValue("ext.v2.uk"))
                                    xmlTe.uk = new PerCent() { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v2.uk")) };

                                if (ce.ContainsPropertyValue("ext.v2.excitingcurrentzero"))
                                    xmlTe.excitingCurrentZero = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("ext.v2.excitingcurrentzero")) };

                                if (ce.ContainsPropertyValue("cim.v2.r"))
                                    xmlTe.r = new Resistance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.v2.r")) };

                                if (ce.ContainsPropertyValue("cim.v2.x"))
                                    xmlTe.x = new Reactance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.v2.x")) };
                            }


                            if (_includeEquipment)
                                yield return xmlTe;
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
                                tap.neutralU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("tap.neutralu")) };

                            if (_includeEquipment)
                                yield return tap;
                        }
                    }
                }
                else if (ce.ClassType == CIMClassEnum.BusbarSection)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new BusbarSectionExt();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;


                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);

                    if (substationVoltageLevels.ContainsKey(cimObj.VoltageLevel))
                        ((BusbarSectionExt)xmlObj).EquipmentContainer = new EquipmentEquipmentContainer() { @ref = substationVoltageLevels[cimObj.VoltageLevel].mRID };
               
                    if (ce.ContainsPropertyValue("cim.ipmax"))
                        ((BusbarSectionExt)xmlObj).ipMax = new CurrentFlow() { unit = UnitSymbol.A, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.ipmax")) };

                    if (ce.ContainsPropertyValue("cim.powerfactormin"))
                    {
                        ((BusbarSectionExt)xmlObj).powerFactorMin = Convert.ToSingle(ce.GetPropertyValueAsString("cim.powerfactormin"));
                        ((BusbarSectionExt)xmlObj).powerFactorMinSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.powerfactormax"))
                    {
                        ((BusbarSectionExt)xmlObj).powerFactorMax = Convert.ToSingle(ce.GetPropertyValueAsString("cim.powerfactormax"));
                        ((BusbarSectionExt)xmlObj).powerFactorMaxSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.sspmin"))
                        ((BusbarSectionExt)xmlObj).sspMin = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.m, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.sspmin")) };

                    if (ce.ContainsPropertyValue("cim.sspmax"))
                        ((BusbarSectionExt)xmlObj).sspMax = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.m, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.sspmax")) };

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;

                    if (_includeEquipment)
                        yield return xmlObj;
                }
                else if (ce.ClassType == CIMClassEnum.PetersenCoil)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new PetersenCoil();

                    // create asset info
                    var assetInfo = new PetersenCoilInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.minimumcurrent"))
                        assetInfo.minimumCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.minimumcurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.maximumcurrent"))
                        assetInfo.maximumCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.maximumcurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.actualcurrent"))
                        assetInfo.actualCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.actualcurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                    ce.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                    // create asset
                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj, true);

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);

                    if (substationVoltageLevels.ContainsKey(cimObj.VoltageLevel))
                        ((PetersenCoil)xmlObj).EquipmentContainer = new EquipmentEquipmentContainer() { @ref = substationVoltageLevels[cimObj.VoltageLevel].mRID };


                    if (ce.ContainsPropertyValue("cim.nominalu"))
                        ((PetersenCoil)xmlObj).nominalU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.nominalu")) };

                    if (ce.ContainsPropertyValue("cim.offsetcurrent"))
                        ((PetersenCoil)xmlObj).offsetCurrent = new CurrentFlow() { unit = UnitSymbol.A,  multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.offsetcurrent")) };

                    if (ce.ContainsPropertyValue("cim.positioncurrent"))
                        ((PetersenCoil)xmlObj).positionCurrent = new CurrentFlow() { unit = UnitSymbol.A,  multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.positioncurrent")) };

                    if (ce.ContainsPropertyValue("cim.xgroundmin"))
                        ((PetersenCoil)xmlObj).xGroundMin = new Reactance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.xgroundmin")) };

                    if (ce.ContainsPropertyValue("cim.xgroundmax"))
                        ((PetersenCoil)xmlObj).xGroundMax = new Reactance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.xgroundmax")) };

                    if (ce.ContainsPropertyValue("cim.xgroundnominal"))
                        ((PetersenCoil)xmlObj).xGroundNominal = new Reactance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.xgroundnominal")) };

                    if (ce.ContainsPropertyValue("cim.r"))
                        ((PetersenCoil)xmlObj).r = new Resistance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.r")) };

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

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;

                    if (_includeAsset)
                    {
                        if (asset != null)
                            yield return asset;

                        if (assetInfo != null)
                            yield return assetInfo;
                    }
                }
                else if (ce.ClassType == CIMClassEnum.SynchronousMachine)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new SynchronousMachine();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    
                    if (ce.ContainsPropertyValue("cim.ratedu"))
                        ((SynchronousMachine)xmlObj).ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedu")) };

                    if (ce.ContainsPropertyValue("cim.rateds"))
                        ((SynchronousMachine)xmlObj).ratedS = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.rateds")) };

                    if (ce.ContainsPropertyValue("cim.ratedpowerfactor"))
                    {
                        ((SynchronousMachine)xmlObj).ratedPowerFactor = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedpowerfactor"));
                        ((SynchronousMachine)xmlObj).ratedPowerFactorSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.maxq"))
                        ((SynchronousMachine)xmlObj).maxQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxq")) };

                    if (ce.ContainsPropertyValue("cim.minq"))
                        ((SynchronousMachine)xmlObj).minQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minq")) };

                    if (ce.ContainsPropertyValue("cim.qpercent"))
                        ((SynchronousMachine)xmlObj).qPercent = new PerCent { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.qpercent")) };

                    if (ce.ContainsPropertyValue("cim.referencepriority"))
                        ((SynchronousMachine)xmlObj).referencePriority = ce.GetPropertyValueAsString("cim.referencepriority");

                    if (ce.ContainsPropertyValue("cim.ikk"))
                        ((SynchronousMachine)xmlObj).ikk = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.ikk")) };

                    if (ce.ContainsPropertyValue("cim.mu"))
                    {
                        ((SynchronousMachine)xmlObj).mu = Convert.ToSingle(ce.GetPropertyValueAsString("cim.mu"));
                        ((SynchronousMachine)xmlObj).muSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.r"))
                        ((SynchronousMachine)xmlObj).r = new Resistance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.r")) };

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

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
                else if (ce.ClassType == CIMClassEnum.AsynchronousMachine)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new AsynchronousMachine();

                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;


                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    
                    if (ce.ContainsPropertyValue("cim.ratedu"))
                        ((AsynchronousMachine)xmlObj).ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedu")) };

                    if (ce.ContainsPropertyValue("cim.rateds"))
                        ((AsynchronousMachine)xmlObj).ratedS = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.rateds")) };

                    if (ce.ContainsPropertyValue("cim.ratedpowerfactor"))
                    {
                        ((AsynchronousMachine)xmlObj).ratedPowerFactor = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedpowerfactor"));
                        ((AsynchronousMachine)xmlObj).ratedPowerFactorSpecified = true;
                    }

                    if (ce.ContainsPropertyValue("cim.nominalfrequency"))
                        ((AsynchronousMachine)xmlObj).nominalFrequency = new Frequency() { unit = UnitSymbol.Hz, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.nominalfrequency")) };

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

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
                else if (ce.ClassType == CIMClassEnum.LinearShuntCompensator)
                {
                    CheckIfProcessed(cimObj);

                    xmlObj = new LinearShuntCompensator();

                    // create asset info
                    var assetInfo = new LinearShuntCompensatorInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                    if (cimObj.ContainsPropertyValue("cim.ratedvoltage"))
                        assetInfo.ratedVoltage = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.ratedvoltage")) };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.minimumreactivepower"))
                        assetInfo.minimumReactivePower = new ReactivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.minimumreactivepower")), multiplier = UnitMultiplier.M, unit = UnitSymbol.VAr };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.maximumreactivepower"))
                        assetInfo.maximumReactivePower = new ReactivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.maximumreactivepower")), multiplier = UnitMultiplier.M, unit = UnitSymbol.VAr };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.actualreactivepower"))
                        assetInfo.actualReactivePower = new ReactivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.actualreactivepower")), multiplier = UnitMultiplier.M, unit = UnitSymbol.VAr };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.loss"))
                        assetInfo.loss = new ActivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.loss")), multiplier = UnitMultiplier.none, unit = UnitSymbol.W };

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.qualityfactory"))
                        assetInfo.qualityFactory = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.assetinfo.qualityfactory"));

                    if (cimObj.ContainsPropertyValue("cim.assetinfo.technology"))
                        assetInfo.technology = cimObj.GetPropertyValueAsString("cim.assetinfo.technology");


                    ce.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                    // create asset
                    var asset = MapAsset(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;
                    
                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapConductingEquipmentFields(ce, (ConductingEquipment)xmlObj);
                    
                    if (ce.ContainsPropertyValue("cim.nomu"))
                        ((LinearShuntCompensator)xmlObj).nomU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.nomu")) };

                    if (ce.ContainsPropertyValue("cim.normalsections"))
                        ((LinearShuntCompensator)xmlObj).normalSections = ce.GetPropertyValueAsString("cim.normalsections");

                    if (ce.ContainsPropertyValue("cim.maximumsections"))
                        ((LinearShuntCompensator)xmlObj).maximumSections = ce.GetPropertyValueAsString("cim.maximumsections");

                    if (ce.ContainsPropertyValue("cim.bpersection"))
                        ((LinearShuntCompensator)xmlObj).bPerSection = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.bpersection")) };

                    if (ce.ContainsPropertyValue("cim.gpersection"))
                        ((LinearShuntCompensator)xmlObj).gPerSection = new Conductance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.gpersection") )};

                    if (ce.ContainsPropertyValue("cim.b0persection"))
                        ((LinearShuntCompensator)xmlObj).b0PerSection = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.b0persection")) };

                    if (ce.ContainsPropertyValue("cim.g0persection"))
                        ((LinearShuntCompensator)xmlObj).g0PerSection = new Conductance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.g0persection")) };

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;

                    if (_includeAsset)
                    {
                        if (asset != null)
                            yield return asset;

                        if (assetInfo != null)
                            yield return assetInfo;
                    }
                }

                // Set equipment container
                if (xmlObj is ConductingEquipment && xmlObj != null && equipmentContainer != null && ((ConductingEquipment)xmlObj).EquipmentContainer == null)
                    ((ConductingEquipment)xmlObj).EquipmentContainer = new EquipmentEquipmentContainer() { @ref = equipmentContainer.mRID };
            }
        }

        private bool CheckIfProcessed(CIMObject cimObj, bool throwErrorIfAlreadyProcessed = true)
        {
            if (_cimObjectHasBeenProcessed.ContainsKey(cimObj.mRID))
            {
                var existingCimObj = _cimObjectHasBeenProcessed[cimObj.mRID];

                if (throwErrorIfAlreadyProcessed)
                {
                    Logger.Log(LogLevel.Warning, "Dublicate mRID checking failed: " + cimObj.ToString() + " mRID: " + cimObj.mRID + " clash with: " + existingCimObj);
                    //throw new Exception(cimObj.mRID + " already processed. Error in serialization. Existing obj: " + existingCimObj.ToString());
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

                return true;
            }
            else
                _cimObjectHasBeenProcessed.Add(guid, obj);

            return false;
        }

        private void MapIdentifiedObjectFields(CIMIdentifiedObject cimObj, PowerSystemResource xmlObj)
        {
            // mRID
            if (cimObj.mRID == Guid.Empty)
                throw new DAXGraphException("mRID cannot be null. " + cimObj.ToString());

            xmlObj.mRID = cimObj.mRID.ToString();

            // PSRType
            var psrType = cimObj.GetPSRType(_metaDataRepository);
            if (psrType != null)
                xmlObj.PSRType = psrType;

            // Name
            if (cimObj.Name != null)
                xmlObj.name = cimObj.Name;

            // Description
            if (cimObj.Description != null)
                xmlObj.description = cimObj.Description;
        }

        private void MapIdentifiedObjectFields(CIMIdentifiedObject cimObj, IdentifiedObject xmlObj)
        {
            // mRID
            if (cimObj.mRID == Guid.Empty)
                throw new DAXGraphException("mRID cannot be null. " + cimObj.ToString());

            xmlObj.mRID = cimObj.mRID.ToString();

            // Name
            if (cimObj.Name != null)
                xmlObj.name = cimObj.Name;

            // Description
            if (cimObj.Description != null)
                xmlObj.description = cimObj.Description;
        }


        private IEnumerable<IdentifiedObject> MapLocation(CIMIdentifiedObject cimObj, PowerSystemResource xmlObj)
        {
            Location location = null;

            // Location, include coords only on aclinesegments, energyconsumer and substations
            double[] coords = null;
            if (cimObj.ClassType == CIMClassEnum.ACLineSegment || cimObj.ClassType == CIMClassEnum.Substation || cimObj.ClassType == CIMClassEnum.Enclosure || cimObj.ClassType == CIMClassEnum.EnergyConsumer)
                coords = cimObj.Coords;

            if (coords != null)
            {
                location = CreateLocation(coords, cimObj);
                xmlObj.Location = new PowerSystemResourceLocation() { @ref = location.mRID };

            }

            if (_includeLocation && location != null)
                yield return location;
        }

        private AssetExt MapAsset(CIMIdentifiedObject cimObj, PowerSystemResource xmlObj, bool forceAssetRecord = false)
        {

            bool containsAssetInfo = false;

            AssetExt asset = new AssetExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 20).ToString() };

            if (cimObj.ContainsPropertyValue("cim.ref.assetinfo"))
            {
                asset.AssetInfo = new AssetAssetInfo() { @ref = cimObj.GetPropertyValueAsString("cim.ref.assetinfo").ToLower() };
                containsAssetInfo = true;
            }
            else
            {
                // If equipment has productassetmodel but no assetinfo, create dummy asset info ref
                if (cimObj.ContainsPropertyValue("cim.ref.productassetmodel"))
                {
                    // Create derived ref from equipment id
                    var derivedAssetInfoId = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200, true).ToString();

                    asset.AssetInfo = new AssetAssetInfo() { @ref = derivedAssetInfoId };
                    containsAssetInfo = true;
                }
            }


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
                asset.serialNumber = cimObj.GetPropertyValueAsString("cim.asset.serialnumber");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.installationdate"))
            {
                containsAssetInfo = true;
                asset.lifecycle = new LifecycleDate();
                asset.lifecycle.installationDateSpecified = true;
                asset.lifecycle.installationDate = Convert.ToDateTime(cimObj.GetPropertyValueAsString("cim.asset.installationdate"));
            }

            if (cimObj.ContainsPropertyValue("cim.asset.owner"))
            {
                string name = cimObj.GetPropertyValueAsString("cim.asset.owner");

                if (name != null)
                {
                    containsAssetInfo = true;
                    asset.owner = name;
                }
            }

            if (cimObj.ContainsPropertyValue("cim.asset.maintainer"))
            {
                string name = cimObj.GetPropertyValueAsString("cim.asset.maintainer");

                if (name != null)
                {
                    containsAssetInfo = true;
                    asset.maintainer = name;
                }
            }

            if (cimObj.ContainsPropertyValue("cim.asset.manufacturer"))
            {
                string name = cimObj.GetPropertyValueAsString("cim.asset.manufacturer");

                if (name != null)
                {
                    containsAssetInfo = true;
                    asset.manufacturerName = name;
                }
            }


            if (cimObj.ContainsPropertyValue("cim.asset.productmodel"))
            {
                string name = cimObj.GetPropertyValueAsString("cim.asset.productmodel");

                if (name != null)
                {
                    containsAssetInfo = true;
                    asset.assetModeName = name;
                }
            }

            if (containsAssetInfo || forceAssetRecord)
            {
                xmlObj.Assets = new PowerSystemResourceAssets() { @ref = asset.mRID };

                if (_includeAsset)
                {
                   return asset;
                }

                CheckIfProcessed(asset.mRID, "Asset related to " + cimObj.ToString());
            }

            return null;
        }

        private AssetInfo MapDummyAssetInfo(CIMIdentifiedObject cimObj, PowerSystemResource xmlObj, bool forceAssetRecord = false)
        {
            // Creates dummy asset info when cim.ref.assetinfo = null and cim.ref.productassetmodel <> null

            bool containsAssetInfo = false;

            AssetExt asset = new AssetExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 20).ToString() };

            if (!cimObj.ContainsPropertyValue("cim.ref.assetinfo"))
            {
                // If equipment has productassetmodel but no assetinfo, create dummy asset info
                if (cimObj.ContainsPropertyValue("cim.ref.productassetmodel"))
                {

                    // Create derived id from equipment id
                    var derivedAssetInfoId = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200, true).ToString();
                    var assetModelId = cimObj.GetPropertyValueAsString("cim.ref.productassetmodel").ToLower();

                    var assetInfo = new AssetInfo() { mRID = derivedAssetInfoId };
                    assetInfo.AssetModel = new AssetInfoAssetModel() { @ref = assetModelId };

                    return assetInfo;
                }

            }

            return null;
        }


        private void MapConductingEquipmentFields(CIMIdentifiedObject cimObj, ConductingEquipment xmlObj)
        {
            CIMConductingEquipment cimCe = cimObj as CIMConductingEquipment;

            // Voltage level
            if (cimObj.VoltageLevel > 0)
            {
                var vl = cimObj.VoltageLevel;
                xmlObj.BaseVoltage = vl;
            }

            // Equipment container
            if (cimObj.EquipmentContainerRef != null)
            {
                
                if ((cimObj.ClassType == CIMClassEnum.Disconnector ||
                    cimObj.ClassType == CIMClassEnum.Fuse ||
                    cimObj.ClassType == CIMClassEnum.LoadBreakSwitch ||
                    cimObj.ClassType == CIMClassEnum.Breaker ||
                    cimObj.ClassType == CIMClassEnum.BusbarSection ||
                    cimObj.ClassType == CIMClassEnum.EnergyConsumer) &&
                    _substationVoltageLevelsByMrid.ContainsKey(cimObj.EquipmentContainerRef.mRID))
                {
                    var subVoltageLevels = _substationVoltageLevelsByMrid[cimObj.EquipmentContainerRef.mRID];

                    if (subVoltageLevels.ContainsKey(cimObj.VoltageLevel))
                    {
                        xmlObj.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = subVoltageLevels[cimObj.VoltageLevel].mRID.ToString() };
                    }
                    else
                        xmlObj.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                }
                else
                    xmlObj.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
            }
        }

        private void MapACLineSegmentFields(CIMIdentifiedObject ce, ACLineSegmentExt xmlObj)
        {
          
                ////////////
                // Transfer all electric parameters

                if (ce.ContainsPropertyValue("cim.length"))
                ((ACLineSegmentExt)xmlObj).length = new Length() { multiplier = UnitMultiplier.none, unit = UnitSymbol.m, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.length")) };

            if (ce.ContainsPropertyValue("cim.bch"))
                ((ACLineSegmentExt)xmlObj).bch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.bch")) ,  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.b0ch"))
                ((ACLineSegmentExt)xmlObj).b0ch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.b0ch")),  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.gch"))
                ((ACLineSegmentExt)xmlObj).gch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.gch")), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.g0ch"))
                ((ACLineSegmentExt)xmlObj).g0ch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.g0ch")), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.r"))
                ((ACLineSegmentExt)xmlObj).r = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.r0"))
                ((ACLineSegmentExt)xmlObj).r0 = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r0"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x"))
                ((ACLineSegmentExt)xmlObj).x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x0"))
                ((ACLineSegment)xmlObj).x0 = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x0"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.c"))
                ((ACLineSegmentExt)xmlObj).c = new Capacitance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.c")),  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.c0"))
                ((ACLineSegmentExt)xmlObj).c0 = new Capacitance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.c0")),  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.maximumcurrent"))
                ((ACLineSegmentExt)xmlObj).maximumCurrent = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.none,  Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.maximumcurrent")) };

            if (ce.ContainsPropertyValue("cim.neutral_r"))
                ((ACLineSegmentExt)xmlObj).neutral_r = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_r"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.neutral_r0"))
                ((ACLineSegmentExt)xmlObj).neutral_r0 = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_r0"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.neutral_x"))
                ((ACLineSegmentExt)xmlObj).neutral_x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_x"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.neutral_x0"))
                ((ACLineSegmentExt)xmlObj).neutral_x0 = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_x0"))), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.ik"))
                ((ACLineSegmentExt)xmlObj).iK = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.k, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.ik")) };
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
                ((ExternalNetworkInjection)xmlObj).maxInitialSymShCCurrent = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.k,  Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.maxinitialsymshccurrent")) };

            if (ce.ContainsPropertyValue("cim.maxr0tox0ratio"))
                ((ExternalNetworkInjection)xmlObj).maxR0ToX0Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxr0tox0ratio"));

            if (ce.ContainsPropertyValue("cim.maxr1tox1ratio"))
                ((ExternalNetworkInjection)xmlObj).maxR1ToX1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxr1tox1ratio"));

            if (ce.ContainsPropertyValue("cim.maxz0toz1ratio"))
                ((ExternalNetworkInjection)xmlObj).maxZ0ToZ1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxz0toz1ratio"));

            if (ce.ContainsPropertyValue("cim.mininitialsymshccurrent"))
                ((ExternalNetworkInjection)xmlObj).minInitialSymShCCurrent = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.k,  Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.mininitialsymshccurrent")) };

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
                ((ExternalNetworkInjection)xmlObj).maxP = new ActivePower() { unit = UnitSymbol.W, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxp")) };

            if (ce.ContainsPropertyValue("cim.maxq"))
                ((ExternalNetworkInjection)xmlObj).maxQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxq")) };

            if (ce.ContainsPropertyValue("cim.minp"))
                ((ExternalNetworkInjection)xmlObj).minP = new ActivePower() { unit = UnitSymbol.W, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minp")) };

            if (ce.ContainsPropertyValue("cim.minq"))
                ((ExternalNetworkInjection)xmlObj).minQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minq")) };


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
                    xmlObj.ratedCurrent = new CurrentFlow() { unit = UnitSymbol.A, Value = valueInt };
                }
            }
        }

        private IEnumerable<IdentifiedObject> MapSubstation(CIMEquipmentContainer cimObj, Substation xmlObj)
        {
            Dictionary<int, VoltageLevel> substationVoltageLevels = new Dictionary<int, VoltageLevel>();

            // Create voltage levels
            int voltageLevelCounter = 0;

            List<int> vlCandidates = new List<int>();

            foreach (var child in cimObj.Children)
                vlCandidates.Add(child.VoltageLevel);

            // Make sure we add transformer cables in satallite stations, lv on local trafo etc
            foreach (var child in cimObj.Children)
            {
                if (child is CIMPowerTransformer)
                {
                    var tfCables = child.GetNeighboursNeighbors(CIMClassEnum.ACLineSegment);

                    foreach (var tfCable in tfCables)
                        vlCandidates.Add(tfCable.VoltageLevel);
                }

                // local trafo voltage level hack - because we don't have any conducting equipment component on the secondary side, we need add 400 volt voltage level here
                if (child is CIMPowerTransformer && child.Name != null && child.Name.ToLower().Contains("lokal"))
                {
                    vlCandidates.Add(400);
                }
            }

            foreach (var vl in vlCandidates)
            {
                if (vl > 0)
                {
                    if (!substationVoltageLevels.ContainsKey(vl))
                    {
                        voltageLevelCounter++;

                        var baseVoltage = vl;

                        substationVoltageLevels[vl] = new VoltageLevel()
                        {
                            BaseVoltage =
                            baseVoltage,
                            mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, voltageLevelCounter).ToString(),
                            name = GetVoltageString(baseVoltage),
                            EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = xmlObj.mRID }
                        };

                        CheckIfProcessed(substationVoltageLevels[vl].mRID, substationVoltageLevels[vl]);

                        if (_includeEquipment)
                            yield return substationVoltageLevels[vl];
                    }
                }
            }

            // Needed to fix switches outside bays relationship
            _substationVoltageLevelsByMrid.Add(cimObj.mRID, substationVoltageLevels);


            // Process substation children
            foreach (var child in cimObj.Children)
            {
                // Bay
                if (child.ClassType == CIMClassEnum.Bay)
                {
                    CIMEquipmentContainer bay = child as CIMEquipmentContainer;

                    var xmlBay = new BayExt();

                    MapIdentifiedObjectFields(bay, xmlBay);

                    xmlBay.order = bay.GetPropertyValueAsString("cim.order");

                    if (substationVoltageLevels.ContainsKey(bay.VoltageLevel))
                        xmlBay.VoltageLevel = new BayVoltageLevel() { @ref = substationVoltageLevels[bay.VoltageLevel].mRID };

                    if (_includeEquipment)
                        yield return xmlBay;

                    // Process bay children
                    foreach (var bayChild in bay.Children)
                    {
                        foreach (var identifiedObject in ProcessLeafObject(bayChild, xmlBay, child.VoltageLevel, substationVoltageLevels))
                        {
                            yield return identifiedObject;
                        }
                    }
                }
                else
                {
                    foreach (var identifiedObject in ProcessLeafObject(child, xmlObj, cimObj.VoltageLevel, substationVoltageLevels))
                    {
                        yield return identifiedObject;
                    }
                }
            }
        }

        private IEnumerable<IdentifiedObject> MapEnclosure(CIMEquipmentContainer cimObj, Substation xmlObj)
        {
            Dictionary<int, VoltageLevel> substationVoltageLevels = new Dictionary<int, VoltageLevel>();

            // Always 400 volt voltage level
            var baseVoltage = 400;
            var voltageLevel = new VoltageLevel()
            {
                BaseVoltage = baseVoltage,
                mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 1).ToString(),
                name = GetVoltageString(baseVoltage),
                EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = xmlObj.mRID }
            };
            CheckIfProcessed(voltageLevel.mRID, voltageLevel);

            if (_includeEquipment)
                yield return voltageLevel;

            substationVoltageLevels.Add(400, voltageLevel);


            // Process substation children
            foreach (var child in cimObj.Children)
            {
                // Bay
                if (child.ClassType == CIMClassEnum.Bay)
                {
                    CIMEquipmentContainer bay = child as CIMEquipmentContainer;

                    var xmlBay = new BayExt();

                    MapIdentifiedObjectFields(bay, xmlBay);

                    xmlBay.order = bay.GetPropertyValueAsString("cim.order");

                    if (substationVoltageLevels.ContainsKey(bay.VoltageLevel))
                        xmlBay.VoltageLevel = new BayVoltageLevel() { @ref = substationVoltageLevels[bay.VoltageLevel].mRID };

                    if (_includeEquipment)
                        yield return xmlBay;

                    // Process bay children
                    foreach (var bayChild in bay.Children)
                    {
                        foreach (var identifiedObject in ProcessLeafObject(bayChild, xmlBay, child.VoltageLevel, substationVoltageLevels))
                        {
                            yield return identifiedObject;
                        }
                    }
                }
                else
                {
                    foreach (var identifiedObject in ProcessLeafObject(child, xmlObj, cimObj.VoltageLevel, substationVoltageLevels))
                    {
                        yield return identifiedObject;
                    }
                }
            }
        }

        private IEnumerable<IdentifiedObject> MapTerminals(CIMConductingEquipment cimObj, ConductingEquipment xmlObj)
        {
            if (_includeEquipment)
            {
                PhaseCode phaseCode = PhaseCode.ABC;

                if (cimObj.ContainsPropertyValue("cim.phasecode"))
                {
                    var phaseCodesStr = cimObj.GetPropertyValueAsString("cim.phasecode").Replace(" ", "").Replace(",", "");
                    PhaseCode.TryParse(phaseCodesStr, out phaseCode);
                }

                int endNumber = 1;

                List<Terminal> terminals = new List<Terminal>();

                foreach (var cimTerminal in cimObj.Terminals)
                {

                    if (endNumber < 3)
                    {

                        // Create connectivity node
                        ConnectivityNode xmlCn = null;

                        if (cimTerminal.ConnectivityNode != null)
                        {
                            foreach (var identifiedObject in CreateAndYieldConnectivityNodeIfNotExists(cimTerminal.ConnectivityNode.mRID, (CIMConductingEquipment)cimObj))
                                yield return identifiedObject;

                            xmlCn = GetConnectivityNode(cimTerminal.ConnectivityNode.mRID, (CIMConductingEquipment)cimObj);
                        }

                        // Create terminal
                        foreach (var identifiedObject in CreateTerminal(cimTerminal, (ConductingEquipment)xmlObj, xmlCn, cimTerminal.EndNumber, phaseCode))
                            yield return identifiedObject;
                    }

                    endNumber++;
                }
            }
        }
               
        private Location CreateLocation(double[] coords, CIMIdentifiedObject cimObj)
        {
            Location loc = null;

            loc = new LocationExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 999, true).ToString() };
            loc.CoordinateSystem = new LocationCoordinateSystem() { @ref = _coordSys.mRID };

            if (coords != null)
            {
                List<Point2D> points = new List<Point2D>();

                for (int i = 0; i < coords.Length; i += 2)
                {
                    double x = ((double)coords[i]);
                    double y = ((double)coords[i + 1]);

                    points.Add(new Point2D(x, y));

                }

                ((LocationExt)loc).coordinates = points.ToArray();
            }

            return loc;
        }

        private IEnumerable<IdentifiedObject> CreateTerminal(CIMTerminal cimTerminal, ConductingEquipment eq, ConnectivityNode cn, int seqNo, PhaseCode phases = PhaseCode.ABC)
        {
            if (_includeEquipment)
            {

                CheckIfProcessed(cimTerminal);

                var terminal = new Terminal()
                {
                    mRID = cimTerminal.mRID.ToString(),
                    ConductingEquipment = new TerminalConductingEquipment() { @ref = eq.mRID },
                    sequenceNumber = seqNo + ""
                };

                if (cn != null)
                    terminal.ConnectivityNode = new TerminalConnectivityNode() { @ref = cn.mRID };

                terminal.phases = phases;
                terminal.phasesSpecified = true;

                yield return terminal;
            }
        }


        private ConnectivityNode GetConnectivityNode(Guid mRID, CIMConductingEquipment cimObj)
        {
            if (_connectivityNodes.ContainsKey(mRID))
            {
               return _connectivityNodes[mRID];
            }
            throw new Exception("The code that call this function, should make sure the connectivity node is created first.");
        }


        private IEnumerable<IdentifiedObject> CreateAndYieldConnectivityNodeIfNotExists(Guid mRID, CIMConductingEquipment cimObj)
        {
            if (cimObj != null && _includeEquipment)
            {
                if (!_connectivityNodes.ContainsKey(mRID))
                {
                    _connectivityNodes[mRID] = new ConnectivityNode() { mRID = mRID.ToString() };

                    CheckIfProcessed(mRID.ToString(), "Connectivity node: " + mRID + " CIM Obj: " + cimObj.ToString());
                    yield return _connectivityNodes[mRID];
                }
            }
        }


        private double ConvertToDouble(string value)
        {
            double doubleValue;

            if (value == null)
                return 0;
            else if (value.Trim() == "")
                return 0;
            else if (!Double.TryParse(value, out doubleValue))
                return 0;
            else
                return Convert.ToDouble(value);
        }


        private string GetVoltageString(int voltageLevel)
        {
            if (voltageLevel < 1000)
            {
                return voltageLevel + " V";
            }
            else
            {
                return (voltageLevel / 1000) + " kV";
            }
        }


        private bool ParseBoolString(string val)
        {
            if (val != null && (val == "1" || val.ToLower() == "true" || val.ToLower() == "yes"))
                return true;
            else
                return false;
        }
    }

    public class TopologySerializationParameters
    {
        public ITopologyProcessingResult TopologyProcessingResult { get; set; }
        public bool ExcludeACLineSegments { get; set; }
        public bool ExcludeSwitches { get; set; }
        public bool ExcludeConnectivity { get; set; }
    }
}
