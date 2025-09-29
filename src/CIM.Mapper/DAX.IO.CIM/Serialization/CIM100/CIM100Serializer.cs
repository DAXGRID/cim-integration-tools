using CIM.PhysicalNetworkModel;
using DAX.IO.CIM.Processing;
using DAX.Util;
using System.Globalization;

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

        bool _includeEquipment;
        bool _includeAsset;
        bool _includeLocation;

        private CultureInfo _cultureInfo = new CultureInfo("EN-US");

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

                    var asset = MapIdentifiedObjectAssetRef(substation, xmlObj);
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
                 }
            }
            else if (cimObj.ClassType == CIMClassEnum.PowerTransformer)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new PowerTransformer();

                // create asset info
                var assetInfo = new PowerTransformerInfoExt { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                if (cimObj.ContainsPropertyValue("ext.thermalrateds"))
                    assetInfo.thermalRatedS = new ApparentPower { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.thermalrateds"), _cultureInfo) * 1000000, multiplier = UnitMultiplier.c, unit = UnitSymbol.VA };

                if (cimObj.ContainsPropertyValue("ext.lowerbound"))
                    assetInfo.lowerBound = new PU() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.lowerbound"), _cultureInfo), multiplier = UnitMultiplier.c, unit = UnitSymbol.none };

                if (cimObj.ContainsPropertyValue("ext.upperbound"))
                    assetInfo.upperBound = new PU() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.upperbound"), _cultureInfo), multiplier = UnitMultiplier.c, unit = UnitSymbol.none };

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

                cimObj.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                yield return assetInfo;


                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);

                if (_includeEquipment)
                {
                    yield return xmlObj;

                    // Create terminal and winding for each neighbor
                    bool firstEndFound = false;

                    PowerTransformerEndExt firstEnd = null;

                    var ptTerminals = ((CIMConductingEquipment)cimObj).Terminals.ToList();

                    foreach (var cimTerminal in ptTerminals)
                    {
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
                            mRID = GetMrid(cimObj, $"v{cimTerminal.EndNumber}.mrid").ToString(),
                            PowerTransformer = new PowerTransformerEndPowerTransformer() { @ref = xmlObj.mRID },
                            endNumber = "" + cimTerminal.EndNumber,
                            Terminal = new TransformerEndTerminal() { @ref = cimTerminal.mRID.ToString() },
                            BaseVoltage = Convert.ToSingle(cimObj.GetPropertyValueAsString($"cim.v{cimTerminal.EndNumber}.nominalvoltage"), _cultureInfo)
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
                        if (cimObj.ContainsPropertyValue("cim.v1.rateds"))
                            xmlTe.ratedS = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.v1.rateds"), _cultureInfo) };

                        if (cimObj.ContainsPropertyValue("ext.loss"))
                            xmlTe.loss = new KiloActivePower { unit = UnitSymbol.W, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.loss"), _cultureInfo) };

                        if (cimObj.ContainsPropertyValue("ext.losszero"))
                            xmlTe.lossZero = new KiloActivePower { unit = UnitSymbol.W, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.losszero"), _cultureInfo) };

                        if (cimObj.ContainsPropertyValue("ext.ratingFactor"))
                            xmlTe.ratingFactor = new PerCent() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("ext.ratingFactor"), _cultureInfo), multiplier = UnitMultiplier.c, unit = UnitSymbol.none };


                        // Vinkling 1
                        if (cimTerminal.EndNumber == 1)
                        {
                            if (cimObj.ContainsPropertyValue("cim.v1.nominalvoltage"))
                                xmlTe.nominalVoltage = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.v1.nominalvoltage"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("cim.v1.phaseangleclock"))
                                xmlTe.phaseAngleClock = cimObj.GetPropertyValueAsString("cim.v1.phaseangleclock");

                            if (cimObj.ContainsPropertyValue("cim.v1.connectionkind"))
                                xmlTe.connectionKind = cimObj.GetPropertyValueAsString("cim.v1.connectionkind");

                            if (cimObj.ContainsPropertyValue("cim.v1.grounded"))
                                xmlTe.grounded = ParseBoolString(cimObj.GetPropertyValueAsString("cim.v1.grounded"));

                            if (cimObj.ContainsPropertyValue("cim.v1.ratedu"))
                                xmlTe.ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.v1.ratedu"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("ext.v1.uk"))
                                xmlTe.uk = new PerCent() { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("ext.v1.uk"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("ext.v1.excitingcurrentzero"))
                                xmlTe.excitingCurrentZero = new PerCent { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("ext.v1.excitingcurrentzero"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("cim.v1.r"))
                                xmlTe.r = new Resistance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.r")) };

                            if (cimObj.ContainsPropertyValue("cim.v1.x"))
                                xmlTe.x = new Reactance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.x")) };

                            if (cimObj.ContainsPropertyValue("cim.v1.g"))
                                xmlTe.g = new Conductance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.g")) };

                            if (cimObj.ContainsPropertyValue("cim.v1.b"))
                                xmlTe.b = new Susceptance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.b")) };

                            if (cimObj.ContainsPropertyValue("cim.v1.r0"))
                                xmlTe.r0 = new Resistance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.r0")) };

                            if (cimObj.ContainsPropertyValue("cim.v1.x0"))
                                xmlTe.x0 = new Reactance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.x0")) };

                            if (cimObj.ContainsPropertyValue("cim.v1.g"))
                                xmlTe.g = new Conductance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.g")) };

                            if (cimObj.ContainsPropertyValue("cim.v1.b"))
                                xmlTe.b = new Susceptance { multiplier = UnitMultiplier.micro, Value = ConvertToDouble(cimObj.GetPropertyValueAsString("cim.v1.b")) };

                        }

                        // Vinkling 2
                        if (cimTerminal.EndNumber == 2)
                        {
                            if (cimObj.ContainsPropertyValue("cim.v2.nominalvoltage"))
                                xmlTe.nominalVoltage = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.v2.nominalvoltage"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("cim.v2.phaseangleclock"))
                                xmlTe.phaseAngleClock = cimObj.GetPropertyValueAsString("cim.v2.phaseangleclock");

                            if (cimObj.ContainsPropertyValue("cim.v2.connectionkind"))
                                xmlTe.connectionKind = cimObj.GetPropertyValueAsString("cim.v2.connectionkind");

                            if (cimObj.ContainsPropertyValue("cim.v2.grounded"))
                                xmlTe.grounded = ParseBoolString(cimObj.GetPropertyValueAsString("cim.v2.grounded"));

                            if (cimObj.ContainsPropertyValue("cim.v2.ratedu"))
                                xmlTe.ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.v2.ratedu"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("ext.v2.uk"))
                                xmlTe.uk = new PerCent() { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("ext.v2.uk"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("ext.v2.excitingcurrentzero"))
                                xmlTe.excitingCurrentZero = new PerCent { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("ext.v2.excitingcurrentzero"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("cim.v2.r"))
                                xmlTe.r = new Resistance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.v2.r"), _cultureInfo) };

                            if (cimObj.ContainsPropertyValue("cim.v2.x"))
                                xmlTe.x = new Reactance() { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.v2.x"), _cultureInfo) };
                        }


                        if (_includeEquipment)
                            yield return xmlTe;
                    }

                    if (firstEnd != null)
                    {
                        // Trin kobler
                        RatioTapChanger tap = new RatioTapChanger() { TransformerEnd = new RatioTapChangerTransformerEnd() { @ref = firstEnd.mRID } };

                        tap.mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 100).ToString();
                        CheckIfProcessed(tap.mRID, tap);

                        if (cimObj.ContainsPropertyValue("tap.lowstep"))
                            tap.lowStep = cimObj.GetPropertyValueAsString("tap.lowstep");

                        if (cimObj.ContainsPropertyValue("tap.highstep"))
                            tap.highStep = cimObj.GetPropertyValueAsString("tap.highstep");

                        if (cimObj.ContainsPropertyValue("tap.ltcflag"))
                        {
                            string val = cimObj.GetPropertyValueAsString("tap.ltcflag").ToLower();

                            tap.ltcFlag = false;

                            if (val != null && (val == "1" || val == "true" || val == "yes"))
                                tap.ltcFlag = true;
                        }

                        if (cimObj.ContainsPropertyValue("tap.neutralstep"))
                            tap.neutralStep = cimObj.GetPropertyValueAsString("tap.neutralstep");


                        if (cimObj.ContainsPropertyValue("tap.normalstep"))
                            tap.normalStep = cimObj.GetPropertyValueAsString("tap.normalstep");

                        if (cimObj.ContainsPropertyValue("tap.stepvoltageincrement"))
                        {
                            tap.stepVoltageIncrement = new PerCent() { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("tap.stepvoltageincrement"), _cultureInfo) };
                        }

                        if (cimObj.ContainsPropertyValue("tap.neutralu"))
                            tap.neutralU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("tap.neutralu"), _cultureInfo) };

                        if (_includeEquipment)
                            yield return tap;
                    }
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.VoltageLevel)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var voltageLevel = cimObj as CIMEquipmentContainer;
                    VoltageLevel xmlObj = new VoltageLevel();

                    MapIdentifiedObjectFields(voltageLevel, xmlObj);

                    // Voltage level
                    if (cimObj.VoltageLevel > 0)
                    {
                        xmlObj.BaseVoltage = cimObj.VoltageLevel;
                    }

                    // Equipment container
                    if (cimObj.EquipmentContainerRef != null)
                    {
                        xmlObj.EquipmentContainer1 = new VoltageLevelEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                    }

                    if (_includeEquipment)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.Bay)
            {
                CIMEquipmentContainer bay = cimObj as CIMEquipmentContainer;

                var xmlBay = new BayExt();

                MapIdentifiedObjectFields(bay, xmlBay);

                xmlBay.order = bay.GetPropertyValueAsString("cim.order");

                // Equipment container
                if (cimObj.EquipmentContainerRef != null)
                {
                    xmlBay.VoltageLevel = new BayVoltageLevel() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                }

                if (_includeEquipment)
                    yield return xmlBay;
            }
            else if (cimObj.ClassType == CIMClassEnum.Enclosure)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var enclosure = cimObj as CIMEquipmentContainer;
                    Substation xmlObj = new Substation();

                    var asset = MapIdentifiedObjectAssetRef(enclosure, xmlObj);
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
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.EnergyConsumer)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var consumer = cimObj as CIMConductingEquipment;

                    EnergyConsumer xmlConsumer = new EnergyConsumer();

                    var asset = MapIdentifiedObjectAssetRef(consumer, xmlConsumer);
                    if (asset != null)
                        yield return asset;


                    foreach (var identifiedObject in MapLocation(consumer, xmlConsumer))
                        yield return identifiedObject;

                    xmlConsumer.mRID = cimObj.mRID.ToString();
                    MapIdentifiedObjectFields(cimObj, xmlConsumer);
                    MapVoltageAndEquipmentContainerFields(consumer, xmlConsumer);

                    if (_includeEquipment)
                        yield return xmlConsumer;

                    // Map terminals
                    foreach (var identifiedObject in MapTerminals(consumer, xmlConsumer))
                        yield return identifiedObject;



                }
            }
            else if (cimObj.ClassType == CIMClassEnum.UsagePoint)
            {

                var xmlUsagePoint = new UsagePoint();
                xmlUsagePoint.mRID = cimObj.mRID.ToString();
                xmlUsagePoint.name = cimObj.Name;
                xmlUsagePoint.description = cimObj.Description;

                // Relate usagepoint to energy consumer
                if (cimObj.ContainsPropertyValue("cim.ref.energyconsumer"))
                {
                    var ecRef = cimObj.GetPropertyValueAsString("cim.ref.energyconsumer");
        
                    xmlUsagePoint.Equipments = new UsagePointEquipments() { @ref = ecRef };
                }

                if (_includeEquipment)
                    yield return xmlUsagePoint;

            }
            else if (cimObj.ClassType == CIMClassEnum.ACLineSegment)
            {
                var neighboors = cimObj.GetNeighbours();

                if (!CheckIfProcessed(cimObj, false))
                {
                    var ce = cimObj as CIMConductingEquipment;
                    ACLineSegmentExt xmlObj = new ACLineSegmentExt();

                    var asset = MapIdentifiedObjectAssetRef(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    var assetInfo = MapDummyAssetInfo(ce, (PowerSystemResource)xmlObj);

                    if (assetInfo != null)
                        yield return assetInfo;

                    foreach (var identifiedObject in MapLocation(ce, (PowerSystemResource)xmlObj))
                        yield return identifiedObject;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapVoltageAndEquipmentContainerFields(ce, (ConductingEquipment)xmlObj);
                    MapACLineSegmentFields(ce, (ACLineSegmentExt)xmlObj);

                    if (_includeEquipment)
                        yield return xmlObj;

                    foreach (var identifiedObject in MapTerminals(ce, (ConductingEquipment)xmlObj)) yield return identifiedObject;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.ExternalNetworkInjection)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ce = cimObj as CIMConductingEquipment;
                    ExternalNetworkInjection xmlObj = new ExternalNetworkInjection();

                    var asset = MapIdentifiedObjectAssetRef(ce, (PowerSystemResource)xmlObj);
                    if (asset != null)
                        yield return asset;

                    foreach (var identifiedObject in MapLocation(ce, (PowerSystemResource)xmlObj))
                        yield return identifiedObject;

                    MapIdentifiedObjectFields(ce, (ConductingEquipment)xmlObj);
                    MapVoltageAndEquipmentContainerFields(ce, (ConductingEquipment)xmlObj);
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
                            xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
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
                            xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                        }
                    }

                    // Rated voltage
                    if (ci.ContainsPropertyValue("cim.ratedvoltage"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedvoltage");

                        if (val != null)
                        {
                            xmlObj.ratedVoltage = new Voltage() { multiplier = UnitMultiplier.none, unit = UnitSymbol.V, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                        }
                    }

                    // Breaking Capacity
                    if (ci.ContainsPropertyValue("cim.ratedbreakingcurrent"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedbreakingcurrent");

                        if (val != null)
                        {
                            xmlObj.ratedBreakingCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                        }
                    }

                    // Making Current
                    if (ci.ContainsPropertyValue("cim.ratedmakingcurrent"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedmakingcurrent");

                        if (val != null)
                        {
                            xmlObj.ratedMakingCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                        }
                    }

                    // Withstand current 1 sec
                    if (ci.ContainsPropertyValue("cim.ratedwithstandcurrent1sec"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedwithstandcurrent1sec");

                        if (val != null)
                        {
                            xmlObj.ratedWithstandCurrent1sec = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                        }
                    }

                    // Withstand current 3 sec
                    if (ci.ContainsPropertyValue("cim.ratedwithstandcurrent3sec"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedwithstandcurrent3sec");

                        if (val != null)
                        {
                            xmlObj.ratedWithstandCurrent3sek = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
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
                            xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                        }
                    }

                    // Rated voltage
                    if (ci.ContainsPropertyValue("cim.ratedvoltage"))
                    {
                        var val = ci.GetPropertyValueAsDecimal("cim.ratedvoltage");

                        if (val != null)
                        {
                            xmlObj.ratedVoltage = new Voltage() { multiplier = UnitMultiplier.none, unit = UnitSymbol.V, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                        }
                    }

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.Asset)
            {
                var asset = new AssetExt();

                MapIdentifiedObjectFields(cimObj, asset);

                MapAsset(cimObj, asset);

                if (_includeAsset)
                    yield return asset;
            }
            else if (cimObj.ClassType == CIMClassEnum.AssetOwner)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    AssetOwner xmlObj = new AssetOwner();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);

                    if (_includeAsset)
                        yield return xmlObj;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.Maintainer)
            {
                if (!CheckIfProcessed(cimObj, false))
                {
                    var ci = cimObj as CIMIdentifiedObject;

                    Maintainer xmlObj = new Maintainer();
                    MapIdentifiedObjectFields(ci, (IdentifiedObject)xmlObj);

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
                        protectionEq.ProtectedSwitches = new ProtectionEquipmentProtectedSwitches[] { new ProtectionEquipmentProtectedSwitches() { @ref = protectiveSwitchMrid } };

                        if (currenttransformerMrid != null)
                            protectionEq.CurrentTransformers = new ProtectionEquipmentExtCurrentTransformers[] { new ProtectionEquipmentExtCurrentTransformers() { @ref = currenttransformerMrid } };

                        if (potentialtransformerMrid != null)
                            protectionEq.PotentialTransformers = new ProtectionEquipmentExtPotentialTransformers[] { new ProtectionEquipmentExtPotentialTransformers() { @ref = potentialtransformerMrid } };

                        var asset = MapIdentifiedObjectAssetRef(ci, protectionEq);

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
            else if (cimObj.ClassType == CIMClassEnum.BusbarSection)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new BusbarSectionExt();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                var assetInfo = MapDummyAssetInfo(cimObj, (PowerSystemResource)xmlObj);

                if (assetInfo != null)
                    yield return assetInfo;


                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);
           
                if (cimObj.ContainsPropertyValue("cim.ipmax"))
                    ((BusbarSectionExt)xmlObj).ipMax = new CurrentFlow() { unit = UnitSymbol.A, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.ipmax"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.powerfactormin"))
                {
                    ((BusbarSectionExt)xmlObj).powerFactorMin = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.powerfactormin"), _cultureInfo);
                    ((BusbarSectionExt)xmlObj).powerFactorMinSpecified = true;
                }

                if (cimObj.ContainsPropertyValue("cim.powerfactormax"))
                {
                    ((BusbarSectionExt)xmlObj).powerFactorMax = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.powerfactormax"), _cultureInfo);
                    ((BusbarSectionExt)xmlObj).powerFactorMaxSpecified = true;
                }

                if (cimObj.ContainsPropertyValue("cim.sspmin"))
                    ((BusbarSectionExt)xmlObj).sspMin = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.m, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.sspmin"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.sspmax"))
                    ((BusbarSectionExt)xmlObj).sspMax = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.m, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.sspmax"), _cultureInfo) };

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;

                if (_includeEquipment)
                    yield return xmlObj;
            }
            else if (cimObj.ClassType == CIMClassEnum.PetersenCoil)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new PetersenCoil();

                // create asset info
                var assetInfo = new PetersenCoilInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.minimumcurrent"))
                    assetInfo.minimumCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.minimumcurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.maximumcurrent"))
                    assetInfo.maximumCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.maximumcurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.actualcurrent"))
                    assetInfo.actualCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.actualcurrent")), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                cimObj.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                // create asset
                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj, true);

                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);


                if (cimObj.ContainsPropertyValue("cim.nominalu"))
                    ((PetersenCoil)xmlObj).nominalU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.nominalu"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.offsetcurrent"))
                    ((PetersenCoil)xmlObj).offsetCurrent = new CurrentFlow() { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.offsetcurrent"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.positioncurrent"))
                    ((PetersenCoil)xmlObj).positionCurrent = new CurrentFlow() { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.positioncurrent"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.xgroundmin"))
                    ((PetersenCoil)xmlObj).xGroundMin = new Reactance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.xgroundmin"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.xgroundmax"))
                    ((PetersenCoil)xmlObj).xGroundMax = new Reactance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.xgroundmax"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.xgroundnominal"))
                    ((PetersenCoil)xmlObj).xGroundNominal = new Reactance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.xgroundnominal"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.r"))
                    ((PetersenCoil)xmlObj).r = new Resistance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.r"), _cultureInfo) };

                ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.automaticPositioning;

                if (cimObj.ContainsPropertyValue("cim.mode"))
                {
                    string mode = cimObj.GetPropertyValueAsString("cim.mode").ToLower();
                    if (mode.Contains("manual") || mode.Contains("manuel"))
                        ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.manual;
                    else if (mode.Contains("automatic"))
                        ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.automaticPositioning;
                    else if (mode.Contains("fixed"))
                        ((PetersenCoil)xmlObj).mode = PetersenCoilModeKind.@fixed;
                }

                if (_includeEquipment)
                    yield return xmlObj;

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;

                if (_includeAsset)
                {
                    if (asset != null)
                        yield return asset;

                    if (assetInfo != null)
                        yield return assetInfo;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.SynchronousMachine)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new SynchronousMachine();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);

                if (cimObj.ContainsPropertyValue("cim.ratedu"))
                    ((SynchronousMachine)xmlObj).ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.ratedu"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.rateds"))
                    ((SynchronousMachine)xmlObj).ratedS = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.rateds"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.ratedpowerfactor"))
                {
                    ((SynchronousMachine)xmlObj).ratedPowerFactor = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.ratedpowerfactor"), _cultureInfo);
                    ((SynchronousMachine)xmlObj).ratedPowerFactorSpecified = true;
                }

                if (cimObj.ContainsPropertyValue("cim.maxq"))
                    ((SynchronousMachine)xmlObj).maxQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.maxq"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.minq"))
                    ((SynchronousMachine)xmlObj).minQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.minq"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.qpercent"))
                    ((SynchronousMachine)xmlObj).qPercent = new PerCent { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.qpercent"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.referencepriority"))
                    ((SynchronousMachine)xmlObj).referencePriority = cimObj.GetPropertyValueAsString("cim.referencepriority");

                if (cimObj.ContainsPropertyValue("cim.ikk"))
                    ((SynchronousMachine)xmlObj).ikk = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.ikk"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.mu"))
                {
                    ((SynchronousMachine)xmlObj).mu = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.mu"), _cultureInfo);
                    ((SynchronousMachine)xmlObj).muSpecified = true;
                }

                if (cimObj.ContainsPropertyValue("cim.r"))
                    ((SynchronousMachine)xmlObj).r = new Resistance { unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.r"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.r0"))
                    ((SynchronousMachine)xmlObj).r0 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.r0"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.r2"))
                    ((SynchronousMachine)xmlObj).r2 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.r2"), _cultureInfo) };


                if (cimObj.ContainsPropertyValue("cim.shortcircuitrotortype"))
                {
                    string type = cimObj.GetPropertyValueAsString("cim.shortcircuitrotortype").ToLower();

                    if (type.Contains("salientpole1"))
                        ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.salientPole1;
                    else if (type.Contains("salientpole2"))
                        ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.salientPole2;
                    else if (type.Contains("turboseries1"))
                        ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.turboSeries1;
                    else if (type.Contains("turboseries2"))
                        ((SynchronousMachine)xmlObj).shortCircuitRotorType = ShortCircuitRotorKind.turboSeries2;
                }

                if (cimObj.ContainsPropertyValue("cim.voltageregulationrange"))
                    ((SynchronousMachine)xmlObj).voltageRegulationRange = new PerCent { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.voltageregulationrange"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.x0"))
                    ((SynchronousMachine)xmlObj).x0 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.x0"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.x2"))
                    ((SynchronousMachine)xmlObj).x2 = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.x2"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.satdirectsubtransx"))
                    ((SynchronousMachine)xmlObj).satDirectSubtransX = new PU() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.satdirectsubtransx"), _cultureInfo) };

                if (_includeEquipment)
                    yield return xmlObj;

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;
            }
            else if (cimObj.ClassType == CIMClassEnum.AsynchronousMachine)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new AsynchronousMachine();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;


                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);

                if (cimObj.ContainsPropertyValue("cim.ratedu"))
                    ((AsynchronousMachine)xmlObj).ratedU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.ratedu"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.rateds"))
                    ((AsynchronousMachine)xmlObj).ratedS = new ApparentPower() { unit = UnitSymbol.VA, multiplier = UnitMultiplier.k, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.rateds"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.ratedpowerfactor"))
                {
                    ((AsynchronousMachine)xmlObj).ratedPowerFactor = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.ratedpowerfactor"), _cultureInfo);
                    ((AsynchronousMachine)xmlObj).ratedPowerFactorSpecified = true;
                }

                if (cimObj.ContainsPropertyValue("cim.nominalfrequency"))
                    ((AsynchronousMachine)xmlObj).nominalFrequency = new Frequency() { unit = UnitSymbol.Hz, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.nominalfrequency"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.nominelspeed"))
                    ((AsynchronousMachine)xmlObj).nominalSpeed = new RotationSpeed() { multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.nominelspeed"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.converterfeddrive"))
                {
                    string boolStr = cimObj.GetPropertyValueAsString("cim.converterfeddrive");

                    if (boolStr == "1")
                        ((AsynchronousMachine)xmlObj).converterFedDrive = true;
                    else
                        ((AsynchronousMachine)xmlObj).converterFedDrive = false;
                }

                if (cimObj.ContainsPropertyValue("cim.efficiency"))
                    ((AsynchronousMachine)xmlObj).efficiency = new PerCent { Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.efficiency"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.iairratio"))
                    ((AsynchronousMachine)xmlObj).iaIrRatio = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.iairratio"), _cultureInfo);

                if (cimObj.ContainsPropertyValue("cim.polepairnumber"))
                    ((AsynchronousMachine)xmlObj).polePairNumber = cimObj.GetPropertyValueAsString("cim.polepairnumber");

                if (cimObj.ContainsPropertyValue("cim.reversible"))
                {
                    string boolStr = cimObj.GetPropertyValueAsString("cim.reversible");

                    if (boolStr == "1")
                        ((AsynchronousMachine)xmlObj).reversible = true;
                    else
                        ((AsynchronousMachine)xmlObj).reversible = false;
                }

                if (cimObj.ContainsPropertyValue("cim.rxlockedrotorratio"))
                    ((AsynchronousMachine)xmlObj).rxLockedRotorRatio = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.rxlockedrotorratio"), _cultureInfo);

                if (_includeEquipment)
                    yield return xmlObj;

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;
            }
            else if (cimObj.ClassType == CIMClassEnum.LinearShuntCompensator)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new LinearShuntCompensator();

                // create asset info
                var assetInfo = new LinearShuntCompensatorInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                if (cimObj.ContainsPropertyValue("cim.ratedvoltage"))
                    assetInfo.ratedVoltage = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.ratedvoltage"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.minimumreactivepower"))
                    assetInfo.minimumReactivePower = new ReactivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.minimumreactivepower"), _cultureInfo), multiplier = UnitMultiplier.M, unit = UnitSymbol.VAr };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.maximumreactivepower"))
                    assetInfo.maximumReactivePower = new ReactivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.maximumreactivepower"), _cultureInfo), multiplier = UnitMultiplier.M, unit = UnitSymbol.VAr };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.actualreactivepower"))
                    assetInfo.actualReactivePower = new ReactivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.actualreactivepower"), _cultureInfo), multiplier = UnitMultiplier.M, unit = UnitSymbol.VAr };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.loss"))
                    assetInfo.loss = new ActivePower() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.assetinfo.loss"), _cultureInfo), multiplier = UnitMultiplier.none, unit = UnitSymbol.W };

                if (cimObj.ContainsPropertyValue("cim.assetinfo.qualityfactory"))
                    assetInfo.qualityFactory = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.assetinfo.qualityfactory"), _cultureInfo);

                if (cimObj.ContainsPropertyValue("cim.assetinfo.technology"))
                    assetInfo.technology = cimObj.GetPropertyValueAsString("cim.assetinfo.technology");


                cimObj.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                // create asset
                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);

                if (cimObj.ContainsPropertyValue("cim.nomu"))
                    ((LinearShuntCompensator)xmlObj).nomU = new Voltage() { unit = UnitSymbol.V, multiplier = UnitMultiplier.none, Value = Convert.ToSingle(cimObj.GetPropertyValueAsString("cim.nomu"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.normalsections"))
                    ((LinearShuntCompensator)xmlObj).normalSections = cimObj.GetPropertyValueAsString("cim.normalsections");

                if (cimObj.ContainsPropertyValue("cim.maximumsections"))
                    ((LinearShuntCompensator)xmlObj).maximumSections = cimObj.GetPropertyValueAsString("cim.maximumsections");

                if (cimObj.ContainsPropertyValue("cim.bpersection"))
                    ((LinearShuntCompensator)xmlObj).bPerSection = new Susceptance() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.bpersection"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.gpersection"))
                    ((LinearShuntCompensator)xmlObj).gPerSection = new Conductance() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.gpersection"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.b0persection"))
                    ((LinearShuntCompensator)xmlObj).b0PerSection = new Susceptance() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.b0persection"), _cultureInfo) };

                if (cimObj.ContainsPropertyValue("cim.g0persection"))
                    ((LinearShuntCompensator)xmlObj).g0PerSection = new Conductance() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.g0persection"), _cultureInfo) };

                if (_includeEquipment)
                    yield return xmlObj;

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;

                if (_includeAsset)
                {
                    if (asset != null)
                        yield return asset;

                    if (assetInfo != null)
                        yield return assetInfo;
                }
            }
            else if (cimObj.ClassType == CIMClassEnum.LoadBreakSwitch)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new LoadBreakSwitch();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                var assetInfo = MapDummyAssetInfo(cimObj, (PowerSystemResource)xmlObj);

                if (assetInfo != null)
                    yield return assetInfo;


                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);
                MapSwitchEquipmentFields(cimObj, (Switch)xmlObj);

                if (_includeEquipment)
                    yield return xmlObj;

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;
            }
            else if (cimObj.ClassType == CIMClassEnum.Breaker)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new Breaker();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                var assetInfo = MapDummyAssetInfo(cimObj, (PowerSystemResource)xmlObj);

                if (assetInfo != null)
                    yield return assetInfo;

                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);
                MapSwitchEquipmentFields(cimObj, (Switch)xmlObj);

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

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;
            }
            else if (cimObj.ClassType == CIMClassEnum.Fuse)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new Fuse();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                var assetInfo = MapDummyAssetInfo(cimObj, (PowerSystemResource)xmlObj);

                if (assetInfo != null)
                    yield return assetInfo;



                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);
                MapSwitchEquipmentFields(cimObj, (Switch)xmlObj);

                if (_includeEquipment)
                    yield return xmlObj;

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;
            }
            else if (cimObj.ClassType == CIMClassEnum.Disconnector)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new Disconnector();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                var assetInfo = MapDummyAssetInfo(cimObj, (PowerSystemResource)xmlObj);

                if (assetInfo != null)
                    yield return assetInfo;

                MapIdentifiedObjectFields(cimObj, (ConductingEquipment)xmlObj);
                MapVoltageAndEquipmentContainerFields(cimObj, (ConductingEquipment)xmlObj);
                MapSwitchEquipmentFields(cimObj, (Switch)xmlObj);

                if (_includeEquipment)
                    yield return xmlObj;

                foreach (var identifiedObject in MapTerminals((CIMConductingEquipment)cimObj, (ConductingEquipment)xmlObj)) yield return identifiedObject;

            }
            else if (cimObj.ClassType == CIMClassEnum.FaultIndicatorExt)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new FaultIndicatorExt();

                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj);
                if (asset != null)
                    yield return asset;

                var assetInfo = MapDummyAssetInfo(cimObj, (PowerSystemResource)xmlObj);

                if (assetInfo != null)
                    yield return assetInfo;


                var faultIndicator = (FaultIndicatorExt)xmlObj;
                MapIdentifiedObjectFields(cimObj, (PowerSystemResource)xmlObj);

                // Equipment container
                if (cimObj.EquipmentContainerRef != null)
                {
                    faultIndicator.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                }

                // Connect to termianl
                if (cimObj.ContainsPropertyValue("cim.terminal"))
                {
                    faultIndicator.Terminal = new AuxiliaryEquipmentTerminal() { @ref = cimObj.GetPropertyValueAsString("cim.terminal") };
                }

                // Reset kind
                if (cimObj.ContainsPropertyValue("cim.resetkind"))
                {
                    var val = cimObj.GetPropertyValueAsString("cim.resetkind").ToLower().Trim();

                    FaultIndicatorResetKind resetKind = FaultIndicatorResetKind.manual;

                    if (val == "automatisk" || val == "automatic")
                        resetKind = FaultIndicatorResetKind.automatic;

                    faultIndicator.resetKind = resetKind;
                    faultIndicator.resetKindSpecified = true;
                }

                if (_includeEquipment)
                    yield return xmlObj;
            }
            else if (cimObj.ClassType == CIMClassEnum.CurrentTransformer)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new CurrentTransformerExt();


                // create asset info
                var assetInfo = new CurrentTransformerInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                if (cimObj.ContainsPropertyValue("cim.primarycurrent"))
                    assetInfo.primaryCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.primarycurrent"), _cultureInfo), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                if (cimObj.ContainsPropertyValue("cim.secondarycurrent"))
                    assetInfo.secondaryCurrent = new CurrentFlow() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.secondarycurrent"), _cultureInfo), multiplier = UnitMultiplier.c, unit = UnitSymbol.A };

                cimObj.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                // create asset
                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj, true);



                var currentTransformer = (CurrentTransformerExt)xmlObj;
                MapIdentifiedObjectFields(cimObj, (PowerSystemResource)xmlObj);

                // Equipment container
                if (cimObj.EquipmentContainerRef != null)
                {
                    currentTransformer.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                }

                // Connect to termianl
                if (cimObj.ContainsPropertyValue("cim.terminal"))
                {
                    currentTransformer.Terminal = new AuxiliaryEquipmentTerminal() { @ref = cimObj.GetPropertyValueAsString("cim.terminal") };
                }

                // maximumcurrent
                if (cimObj.ContainsPropertyValue("cim.maximumcurrent"))
                    currentTransformer.maximumCurrent = new CurrentFlow() { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.maximumcurrent"), _cultureInfo) };

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
            else if (cimObj.ClassType == CIMClassEnum.PotentialTransformer)
            {
                CheckIfProcessed(cimObj);

                var xmlObj = new PotentialTransformer();


                // create asset info
                var assetInfo = new PotentialTransformerInfoExt() { mRID = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200).ToString() };

                if (cimObj.ContainsPropertyValue("cim.primaryvoltage"))
                    assetInfo.primaryVoltage = new Voltage() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.primaryvoltage"), _cultureInfo), multiplier = UnitMultiplier.c, unit = UnitSymbol.V };

                if (cimObj.ContainsPropertyValue("cim.secondaryvoltage"))
                    assetInfo.secondaryVoltage = new Voltage() { Value = Convert.ToDouble(cimObj.GetPropertyValueAsString("cim.secondaryvoltage"), _cultureInfo), multiplier = UnitMultiplier.c, unit = UnitSymbol.V };

                cimObj.SetPropertyValue("cim.ref.assetinfo", assetInfo.mRID);

                // create asset
                var asset = MapIdentifiedObjectAssetRef(cimObj, (PowerSystemResource)xmlObj, true);

                var potentialTransformer = (PotentialTransformer)xmlObj;
                MapIdentifiedObjectFields(cimObj, (PotentialTransformer)xmlObj);

                // Equipment container
                if (cimObj.EquipmentContainerRef != null)
                {
                    potentialTransformer.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
                }

                // Connect to termianl
                if (cimObj.ContainsPropertyValue("cim.terminal"))
                {
                    potentialTransformer.Terminal = new AuxiliaryEquipmentTerminal() { @ref = cimObj.GetPropertyValueAsString("cim.terminal") };
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
        }

        private Guid GetMrid(CIMIdentifiedObject cimObj, string attrName)
        {
            if (cimObj.ContainsPropertyValue(attrName))
            {
                var mridStr = cimObj.GetPropertyValueAsString(attrName);

                if (Guid.TryParse(mridStr, out Guid result))
                    return result;

            }

            return Guid.Empty;
        }

        private void MapWireInfoFields(CIMIdentifiedObject ce, WireInfoExt xmlObj)
        {
            if (ce.ContainsPropertyValue("cim.bch"))
                ((WireInfoExt)xmlObj).bch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.bch"), _cultureInfo), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.b0ch"))
                ((WireInfoExt)xmlObj).b0ch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.b0ch"), _cultureInfo), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.gch"))
                ((WireInfoExt)xmlObj).gch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.gch"), _cultureInfo), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.g0ch"))
                ((WireInfoExt)xmlObj).g0ch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.g0ch"), _cultureInfo), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.r"))
                ((WireInfoExt)xmlObj).r = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.r0"))
                ((WireInfoExt)xmlObj).r0 = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r0"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x"))
                ((WireInfoExt)xmlObj).x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x0"))
                ((WireInfoExt)xmlObj).x0 = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x0"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            // Rated current
            if (ce.ContainsPropertyValue("cim.ratedcurrent"))
            {
                var val = ce.GetPropertyValueAsDecimal("cim.ratedcurrent");

                if (val != null)
                {
                    xmlObj.ratedCurrent = new CurrentFlow() { multiplier = UnitMultiplier.none, unit = UnitSymbol.A, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                }
            }

            // Rated voltage
            if (ce.ContainsPropertyValue("cim.ratedvoltage"))
            {
                var val = ce.GetPropertyValueAsDecimal("cim.ratedvoltage");

                if (val != null)
                {
                    xmlObj.ratedVoltage = new Voltage() { multiplier = UnitMultiplier.none, unit = UnitSymbol.V, Value = Convert.ToDouble(val.Value, _cultureInfo) };
                }
            }

            if (ce.ContainsPropertyValue("cim.ratedwithstandcurrent1sec"))
                ((WireInfoExt)xmlObj).ratedWithstandCurrent1sec = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.none, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.ratedwithstandcurrent1sec"), _cultureInfo) };
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

        private AssetExt MapAsset(CIMIdentifiedObject cimObj, AssetExt asset, bool forceAssetRecord = false)
        {
            if (cimObj.ContainsPropertyValue("cim.ref.assetinfo"))
            {
                asset.AssetInfo = new AssetAssetInfo() { @ref = cimObj.GetPropertyValueAsString("cim.ref.assetinfo").ToLower() };
            }
            else
            {
                // If equipment has productassetmodel but no assetinfo, create dummy asset info ref
                if (cimObj.ContainsPropertyValue("cim.ref.productassetmodel"))
                {
                    // Create derived ref from equipment id
                    var derivedAssetInfoId = GUIDHelper.CreateDerivedGuid(cimObj.mRID, 200, true).ToString();

                    asset.AssetInfo = new AssetAssetInfo() { @ref = derivedAssetInfoId };
                }
            }


            if (cimObj.ContainsPropertyValue("cim.asset.name"))
            {
                asset.name = cimObj.GetPropertyValueAsString("cim.asset.name");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.description"))
            {
                asset.description = cimObj.GetPropertyValueAsString("cim.asset.description");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.type"))
            {
                asset.type = cimObj.GetPropertyValueAsString("cim.asset.type");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.lotnumber"))
            {
                asset.lotNumber = cimObj.GetPropertyValueAsString("cim.asset.lotnumber");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.serialnumber"))
            {
                asset.serialNumber = cimObj.GetPropertyValueAsString("cim.asset.serialnumber");
            }

            if (cimObj.ContainsPropertyValue("cim.asset.installationdate"))
            {
                asset.lifecycle = new LifecycleDate();
                asset.lifecycle.installationDateSpecified = true;
                asset.lifecycle.installationDate = Convert.ToDateTime(cimObj.GetPropertyValueAsString("cim.asset.installationdate"));
            }

            asset.OrganisationRoles = MapRelation(cimObj, "cim.ref.organisationroles", (string referenceType, string @ref) =>
            {
                return new AssetOrganisationRole()
                {
                    referenceType = referenceType,
                    @ref = @ref
                };
            });


            if (cimObj.ContainsPropertyValue("cim.asset.productmodel"))
            {
                string name = cimObj.GetPropertyValueAsString("cim.asset.productmodel");

                if (name != null)
                {
                    asset.assetModelName = name;
                }
            }

            return asset;
        }

        private AssetExt MapIdentifiedObjectAssetRef(CIMIdentifiedObject cimObj, PowerSystemResource xmlObj, bool forceAssetRecord = false)
        {
            xmlObj.Assets = MapRelation(cimObj, "cim.ref.assets", (string referenceType, string @ref) =>
            {
                return new PowerSystemResourceAssets()
                {
                    referenceType = null,
                    @ref = @ref
                };
            }).FirstOrDefault();

            return null;
        }

        private AssetInfo MapDummyAssetInfo(CIMIdentifiedObject cimObj, PowerSystemResource xmlObj, bool forceAssetRecord = false)
        {
            // Creates dummy asset info when cim.ref.assetinfo = null and cim.ref.productassetmodel <> null

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

        private void MapVoltageAndEquipmentContainerFields(CIMIdentifiedObject cimObj, ConductingEquipment xmlObj)
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
                xmlObj.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = cimObj.EquipmentContainerRef.mRID.ToString() };
            }
        }

        private void MapACLineSegmentFields(CIMIdentifiedObject ce, ACLineSegmentExt xmlObj)
        {
          
            ////////////
            // Transfer all electric parameters

            if (ce.ContainsPropertyValue("cim.length"))
                ((ACLineSegmentExt)xmlObj).length = new Length() { multiplier = UnitMultiplier.none, unit = UnitSymbol.m, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.length"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.bch"))
                ((ACLineSegmentExt)xmlObj).bch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.bch"), _cultureInfo) ,  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.b0ch"))
                ((ACLineSegmentExt)xmlObj).b0ch = new Susceptance() { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.b0ch"), _cultureInfo),  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.gch"))
                ((ACLineSegmentExt)xmlObj).gch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.gch"), _cultureInfo), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.g0ch"))
                ((ACLineSegmentExt)xmlObj).g0ch = new Conductance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.g0ch"), _cultureInfo), multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.r"))
                ((ACLineSegmentExt)xmlObj).r = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.r0"))
                ((ACLineSegmentExt)xmlObj).r0 = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.r0"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x"))
                ((ACLineSegmentExt)xmlObj).x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.x0"))
                ((ACLineSegment)xmlObj).x0 = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.x0"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.c"))
                ((ACLineSegmentExt)xmlObj).c = new Capacitance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.c"), _cultureInfo),  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.c0"))
                ((ACLineSegmentExt)xmlObj).c0 = new Capacitance { Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.c0"), _cultureInfo),  multiplier = UnitMultiplier.micro };

            if (ce.ContainsPropertyValue("cim.maximumcurrent"))
                ((ACLineSegmentExt)xmlObj).maximumCurrent = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.none,  Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.maximumcurrent"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.neutral_r"))
                ((ACLineSegmentExt)xmlObj).neutral_r = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_r"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.neutral_r0"))
                ((ACLineSegmentExt)xmlObj).neutral_r0 = new Resistance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_r0"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.neutral_x"))
                ((ACLineSegmentExt)xmlObj).neutral_x = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_x"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.neutral_x0"))
                ((ACLineSegmentExt)xmlObj).neutral_x0 = new Reactance { Value = Convert.ToSingle(Convert.ToDouble(ce.GetPropertyValueAsString("cim.neutral_x0"), _cultureInfo)), unit = UnitSymbol.ohm, multiplier = UnitMultiplier.none };

            if (ce.ContainsPropertyValue("cim.ik"))
                ((ACLineSegmentExt)xmlObj).iK = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.k, Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.ik"), _cultureInfo) };
        }

        private void MapExternalNetworkInjectionFields(CIMIdentifiedObject ce, ExternalNetworkInjection xmlObj)
        {
            ////////////
            // Transfer all electric parameters

            double lenKm = Convert.ToDouble(ce.GetPropertyValueAsString("cim.length"), _cultureInfo) / 1000;

            ((ExternalNetworkInjection)xmlObj).ikSecond = false;
            ((ExternalNetworkInjection)xmlObj).ikSecondSpecified = true;

            if (ce.ContainsPropertyValue("cim.iksecond"))
            {
                string val = ce.GetPropertyValueAsString("cim.iksecond");

                if (val != null && (val == "1" || val.ToLower() == "true" || val.ToLower() == "yes"))
                    ((ExternalNetworkInjection)xmlObj).ikSecond = true;
            }

            if (ce.ContainsPropertyValue("cim.maxinitialsymshccurrent"))
                ((ExternalNetworkInjection)xmlObj).maxInitialSymShCCurrent = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.k,  Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.maxinitialsymshccurrent"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.maxr0tox0ratio"))
                ((ExternalNetworkInjection)xmlObj).maxR0ToX0Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxr0tox0ratio"), _cultureInfo);

            if (ce.ContainsPropertyValue("cim.maxr1tox1ratio"))
                ((ExternalNetworkInjection)xmlObj).maxR1ToX1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxr1tox1ratio"), _cultureInfo);

            if (ce.ContainsPropertyValue("cim.maxz0toz1ratio"))
                ((ExternalNetworkInjection)xmlObj).maxZ0ToZ1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxz0toz1ratio"), _cultureInfo);

            if (ce.ContainsPropertyValue("cim.mininitialsymshccurrent"))
                ((ExternalNetworkInjection)xmlObj).minInitialSymShCCurrent = new CurrentFlow { unit = UnitSymbol.A, multiplier = UnitMultiplier.k,  Value = Convert.ToDouble(ce.GetPropertyValueAsString("cim.mininitialsymshccurrent"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.minr0tox0ratio"))
                ((ExternalNetworkInjection)xmlObj).minR0ToX0Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minr0tox0ratio"), _cultureInfo);

            if (ce.ContainsPropertyValue("cim.minr1tox1ratio"))
                ((ExternalNetworkInjection)xmlObj).minR1ToX1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minr1tox1ratio"), _cultureInfo);

            if (ce.ContainsPropertyValue("cim.minz0toz1ratio"))
                ((ExternalNetworkInjection)xmlObj).minZ0ToZ1Ratio = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minz0toz1ratio"), _cultureInfo);

            if (ce.ContainsPropertyValue("cim.voltagefactor"))
                ((ExternalNetworkInjection)xmlObj).voltageFactor = new PU() { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.voltagefactor"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.governorscd"))
                ((ExternalNetworkInjection)xmlObj).governorSCD = new ActivePowerPerFrequency { Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.governorscd"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.maxp"))
                ((ExternalNetworkInjection)xmlObj).maxP = new ActivePower() { unit = UnitSymbol.W, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxp"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.maxq"))
                ((ExternalNetworkInjection)xmlObj).maxQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.maxq"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.minp"))
                ((ExternalNetworkInjection)xmlObj).minP = new ActivePower() { unit = UnitSymbol.W, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minp"), _cultureInfo) };

            if (ce.ContainsPropertyValue("cim.minq"))
                ((ExternalNetworkInjection)xmlObj).minQ = new ReactivePower { unit = UnitSymbol.VAr, multiplier = UnitMultiplier.M, Value = Convert.ToSingle(ce.GetPropertyValueAsString("cim.minq"), _cultureInfo) };


        }

        private void MapSwitchEquipmentFields(CIMIdentifiedObject cimObj, Switch xmlObj)
        {
            if (cimObj.ContainsPropertyValue("cim.normalopen"))
            {
                xmlObj.normalOpen = (bool)cimObj.GetPropertyValue("cim.normalopen");

                var str = cimObj.GetPropertyValue("cim.normalopen");
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

        private T[] MapRelation<T>(CIMIdentifiedObject cimObj, string propertyName, Func<string, string, T> f)
        {
            if (cimObj.ContainsPropertyValue(propertyName))
            {
                string roleRef = cimObj.GetPropertyValueAsString(propertyName);

                if (roleRef != null)
                {
                    string[] roles = roleRef.Split(",");

                    List<T> orgRoles = new();

                    foreach (var role in roles)
                    {
                        string[] refTypeAndRef = role.Split(":");

                        if (refTypeAndRef.Length == 1)
                        {
                            T assetOrganisationRole = f(null, refTypeAndRef[0]);
                            orgRoles.Add(assetOrganisationRole);
                        }
                        else if (refTypeAndRef.Length == 2)
                        {
                            T assetOrganisationRole = f(refTypeAndRef[0], refTypeAndRef[1]);
                            orgRoles.Add(assetOrganisationRole);
                        }
                    }

                    return orgRoles.ToArray();
                }
            }

            return [];
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
                return Convert.ToDouble(value, _cultureInfo);
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
