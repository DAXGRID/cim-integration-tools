using CIM.PhysicalNetworkModel;
using DAX.CIM.Serialization.NetSam1_3.Equipment;
using DAX.IO.CIM;
using DAX.Util;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace DAX.IO.Readers
{
    public class NetSamXMLReader : IDaxReader
    {
        private string _equipmentFileName = null;

        private string _encoding = "utf-8";

        private Dictionary<string, int> _validCimObjectCount = new Dictionary<string, int>();
        private Dictionary<string, int> _invalidCimObjectCount = new Dictionary<string,int>();

        private XmlReader _reader = null;

        private bool _firstParseHasBeenRun = false;

        private Dictionary<Guid, int> _baseVoltageByMrid = new();
        private Dictionary<Guid, VoltageLevelInfo> _voltageLevelByMrid = new();
        private Dictionary<Guid, BayInfo> _bayByMrid = new();
        private Dictionary<Guid, string> _psrTypeByMrid = new();
        private Dictionary<Guid, List<(int,DAXCoordinate)>> _coordsByLocationMrid = new();
        private Dictionary<Guid, List<TerminalInfo>> _terminalsByConductingEquipmentMrid = new();
        private Dictionary<Guid, string> _manufactureByMrid = new();


        private HashSet<string> _secondParseElementsToInclude = new()
        {
            "substation",
            "bay",
            "aclinesegmentext",
            "busbarsection",
            "grounddisconnector",
            "fuse",
            "disconnector",
            "loadbreakswitch",
            "breaker",
            "jumper",
            "powertransformer",
            "ratiotapchanger",
            "energyconsumer",
            "usagepoint",
            "petersencoil",
            "faultindicator"
        };

        private HashSet<string> _supportedCimObjectsNames = new()
        {
            "asset"
            ,"assetowner"
            ,"maintainer"
            ,"coordinatesystem"
            ,"location"
            ,"positionpoint"
            ,"generatingunit"
            ,"generatingunitext"
            ,"usagepoint"
            ,"basevoltage"
            ,"bay"
            ,"connectivitynode"
            ,"geographicalregion"
            ,"name"
            ,"nametype"
            ,"psrtype"
            ,"subgeographicalregion"
            ,"substation"
            ,"terminal"
            ,"voltagelevel"
            ,"aclinesegment"
            ,"aclinesegmentext"
            ,"asynchronousmachine"
            ,"bayext"
            ,"breaker"
            ,"busbarsection"
            ,"connectivitynodecontainer"
            ,"currenttransformer"
            ,"currenttransformerext"
            ,"disconnector"
            ,"energyconsumer"
            ,"externalnetworkinjection"
            ,"faultindicator"
            ,"faultindicatorext"
            ,"fuse"
            ,"ground"
            ,"grounddisconnector"
            ,"groundingimpedance"
            ,"linearshuntcompensator"
            ,"loadbreakswitch"
            ,"nonlinearshuntcompensator"
            ,"nonlinearshuntcompensatorpoint"
            ,"petersencoil"
            ,"powertransformer"
            ,"powertransformerend"
            ,"powertransformerendext"
            ,"ratiotapchanger"
            ,"reactivecapabilitycurve"
            ,"sensor"
            ,"seriescompensator"
            ,"switch"
            ,"synchronousmachine"
            ,"tapschedule"
        };

        public void Open(string connectionStringOrUrlOrData)
        {
        }

        public void Open(DAXSelectionSet selectionSet = null)
        {
        }

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, List<ConfigParameter> parameters = null)
        {
            foreach (ConfigParameter configParam in parameters)
            {
                if (configParam.Name.ToLower() == "equipmentfile")
                {
                    _equipmentFileName = configParam.Value;
                }
            }

            if (_equipmentFileName == null)
            {
                throw new DAXReaderException("EquipmentFile parameter is required");
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _reader = XmlReader.Create(new StreamReader(_equipmentFileName, Encoding.GetEncoding(_encoding)));
        }

        public void OpenDataSet(string dataSetName)
        {
            _reader = XmlReader.Create(dataSetName);
        }

        public void CloseDataSet(string dataSetName)
        {
        }

        public void SetFilter(string filter)
        {
        }

        public DAXFeature ReadFeature()
        {
            if (!_firstParseHasBeenRun)
            {
                FirstParse();
            }

            DAXFeature feature = null;

            string classNameLower = null;
            string attributeName = null;
            string attributValue = null;

            Dictionary<string, string> keyValuePairs = new();

            while (_reader.Read())
            {
                int lineNumber = ((IXmlLineInfo)_reader).LineNumber;

                // Start of cim object
                if (_reader.Depth == 1 && _reader.NodeType == XmlNodeType.Element)
                {
                    classNameLower = _reader.Name.ToLower();

                    feature = new DAXFeature()
                    {
                        ClassName = _reader.Name
                    };
                }
                // Start of cim property
                else if (_reader.Depth == 2 && _reader.NodeType == XmlNodeType.Element)
                {
                    attributeName = _reader.Name.ToLower();
                    attributValue = null;

                    if (_reader.HasAttributes)
                    {
                        var reference = _reader.GetAttribute("ref");

                        if (reference != null)
                        {
                            keyValuePairs.Add(attributeName.ToLower(), reference);
                            feature.Add(attributeName, reference);
                        }
                    }
                }
                // Property value
                else if (_reader.Depth == 3 && _reader.NodeType == XmlNodeType.Text)
                {
                    attributValue = _reader.Value;
                }
                // End of cim property
                else if (_reader.Depth == 2 && _reader.NodeType == XmlNodeType.EndElement)
                {
                    keyValuePairs.Add(attributeName.ToLower(), attributValue);
                    feature.Add(attributeName, attributValue);
                }
                // End of cim object
                if (_reader.Depth == 1 && _reader.NodeType == XmlNodeType.EndElement)
                {
                    if (_secondParseElementsToInclude.Contains(classNameLower))
                    {
                        var mrid = TryParseGuidElement(feature.ClassName, "mrid", keyValuePairs, lineNumber);

                        // Give up if no mrid found
                        if (mrid == null)
                            break;
                                             
                        AddEquipmentContainerRef(feature, mrid.Value);
                        AddBaseVoltage(feature, mrid.Value);
                        AddPsrType(feature, mrid.Value);
                        AddGeometry(feature, mrid.Value);
                        AddTerminals(feature, mrid.Value);

                        // Get related data...
                        break;
                    }
                    else
                    {
                        // Continue search for relevant cim object
                        feature = null;
                        attributeName = null;
                        attributValue = null;
                        keyValuePairs.Clear();
                    }
                }

            }

            if (feature == null)
            {
                Reset();
            }

            return feature;
        }

        private void AddGeometry(DAXFeature feature, Guid mrid)
        {
            if (feature.ContainsKey("location"))
            {
                var locRef = Guid.Parse(feature.GetAttributeAsString("location"));

                feature.Remove("location");

                if (_coordsByLocationMrid.ContainsKey(locRef))
                {
                    var coordsSorted = _coordsByLocationMrid[locRef].OrderBy(c => c.Item1).Select(c => c.Item2).ToArray();
                    feature.Coordinates = coordsSorted;

                    if (feature.Coordinates.Length > 1)
                        feature.GeometryType = DAXGeometryType.Line;
                    else
                        feature.GeometryType = DAXGeometryType.Point;
                }
                else
                {
                    throw new DAXReaderException("Invalid location reference: " + locRef + " in " + feature.ClassName + " with mRID: " + mrid);
                }
            }
            else
            {
                feature.GeometryType = DAXGeometryType.NoGemoetry;
            }
        }

        private void AddPsrType(DAXFeature feature, Guid mrid)
        {
            if (feature.ContainsKey("psrtype"))
            {
                var @ref = Guid.Parse(feature.GetAttributeAsString("psrtype"));

                if (_psrTypeByMrid.ContainsKey(@ref))
                {
                    feature.Remove("psrtype");
                    feature.Add("psrtype", _psrTypeByMrid[@ref]);
                }
                else
                {
                    throw new DAXReaderException("Invalid psrType reference: " + @ref + " in " + feature.ClassName + " with mRID: " + mrid);
                }
            }
        }
        private void AddBaseVoltage(DAXFeature feature, Guid mrid)
        {
            if (feature.ContainsKey("basevoltage"))
            {
                var @ref = Guid.Parse(feature.GetAttributeAsString("basevoltage"));

                if (_baseVoltageByMrid.ContainsKey(@ref))
                {
                    feature.Remove("basevoltage");
                    feature.Add("basevoltage", _baseVoltageByMrid[@ref]);
                }
                else
                {
                    throw new DAXReaderException("Invalid baseVoltage reference: " + @ref + " in " + feature.ClassName + " with mRID: " + mrid);
                }
            }
            else if (feature.ContainsKey("voltagelevel"))
            {
                var voltageLevelRef = Guid.Parse(feature.GetAttributeAsString("voltagelevel"));

                if (_voltageLevelByMrid.ContainsKey(voltageLevelRef))
                {
                    var vl = _voltageLevelByMrid[voltageLevelRef];

                    if (_baseVoltageByMrid.ContainsKey(vl.BaseVoltage))
                    {
                        feature.Remove("voltagelevel");
                        feature.Remove("basevoltage");
                        feature.Add("basevoltage", _baseVoltageByMrid[vl.BaseVoltage]);
                    }
                }
                else
                {
                    throw new DAXReaderException("Invalid VoltageLevel reference: " + voltageLevelRef + " in " + feature.ClassName + " with mRID: " + mrid);
                }
            }
            else if (feature.ContainsKey("voltagelevel"))
            {
                var voltageLevelRef = Guid.Parse(feature.GetAttributeAsString("voltagelevel"));

                if (_voltageLevelByMrid.ContainsKey(voltageLevelRef))
                {
                    var vl = _voltageLevelByMrid[voltageLevelRef];

                    if (_baseVoltageByMrid.ContainsKey(vl.BaseVoltage))
                    {
                        feature.Remove("voltagelevel");
                        feature.Remove("basevoltage");
                        feature.Add("basevoltage", _baseVoltageByMrid[vl.BaseVoltage]);
                    }
                }
                else
                {
                    throw new DAXReaderException("Invalid VoltageLevel reference: " + voltageLevelRef + " in " + feature.ClassName + " with mRID: " + mrid);
                }
            }
            else if (feature.ContainsKey("equipmentcontainer"))
            {
                var equipmentContainerRef = Guid.Parse(feature.GetAttributeAsString("equipmentcontainer"));

                if (_bayByMrid.ContainsKey(equipmentContainerRef))
                {
                    var bay = _bayByMrid[equipmentContainerRef];

                    if (_voltageLevelByMrid.ContainsKey(bay.VoltageLevel))
                    {
                        var vl = _voltageLevelByMrid[bay.VoltageLevel];

                        if (_baseVoltageByMrid.ContainsKey(vl.BaseVoltage))
                        {
                            feature.Remove("voltagelevel");
                            feature.Remove("basevoltage");
                            feature.Add("basevoltage", _baseVoltageByMrid[vl.BaseVoltage]);
                        }
                    }
                }

                feature.Remove("equipmentcontainer");
            }
            
        }

        private void AddEquipmentContainerRef(DAXFeature feature, Guid mrid)
        {
            if (feature.ContainsKey("voltagelevel"))
            {
                var voltageLevelRef = Guid.Parse(feature.GetAttributeAsString("voltagelevel"));

                if (_voltageLevelByMrid.ContainsKey(voltageLevelRef))
                {
                    var vl = _voltageLevelByMrid[voltageLevelRef];

                    feature.Add("dax.parent.equipmentcontainermrid", vl.EquipmentContainer);
                    feature.Add("dax.parent.equipmentcontainertype", "Substation");
                }
                else
                {
                    throw new DAXReaderException("Invalid VoltageLevel reference: " + voltageLevelRef + " in " + feature.ClassName + " with mRID: " + mrid);
                }
            }
            else if (feature.ContainsKey("equipmentcontainer"))
            {
                var equipmentContainerRef = Guid.Parse(feature.GetAttributeAsString("equipmentcontainer"));

                if (_bayByMrid.ContainsKey(equipmentContainerRef))
                {
                    var bay = _bayByMrid[equipmentContainerRef];

                    feature.Add("dax.parent.equipmentcontainermrid", equipmentContainerRef);
                    feature.Add("dax.parent.equipmentcontainertype", "Bay");
                }
                else
                {
                    feature.Add("dax.parent.equipmentcontainermrid", equipmentContainerRef);
                    feature.Add("dax.parent.equipmentcontainertype", "Substation");
                }
            }
        }

        private void AddTerminals(DAXFeature feature, Guid mrid)
        {
            if (_terminalsByConductingEquipmentMrid.ContainsKey(mrid))
            {
                var terminals = _terminalsByConductingEquipmentMrid[mrid].OrderBy(t => t.SequenceNumber);

                foreach (TerminalInfo terminal in terminals)
                {
                    feature.Add($"terminal.{terminal.SequenceNumber}.id", terminal.TerminalId.ToString());
                    feature.Add($"terminal.{terminal.SequenceNumber}.cn", terminal.ConnectivityNodeId.ToString());
                }
            }
        }

        public void Reset()
        {
            _firstParseHasBeenRun = false;
            _coordsByLocationMrid = new();
        }

        public void Close()
        {
        }

        public string DataSourceClassName()
        {
            return "NetSamXMLReader v0.1.0";
        }

        public string DataSourceName()
        {
            return "NetSamData";
        }

        public string JobName()
        {
            return "NetSamXMLReader";
        }

        public List<KeyValuePair<string, string>> AdditionalInformation()
        {
            return null;
        }

        /// <summary>
        /// Inserts cim object into dicts used for lookup in the second parse
        /// </summary>
        private void FirstParse()
        {
            _firstParseHasBeenRun = true;
            
            var reader = XmlReader.Create(_equipmentFileName);

            string className = null;
            string classNameLower = null;

            Dictionary<string, string> keyValuePairs = new();

            string attributeName = null;

            string attributeValue = null;

            while (reader.Read())
            {
                int lineNumber = ((IXmlLineInfo)reader).LineNumber;

                // Start of cim object
                if (reader.Depth == 1 && reader.NodeType == XmlNodeType.Element)
                {
                    className = reader.Name;
                    classNameLower = reader.Name.ToLower();
                }
                // Start of cim property
                else if (reader.Depth == 2 && reader.NodeType == XmlNodeType.Element)
                {
                    attributeName = reader.Name;

                    if (reader.HasAttributes)
                    {
                        var reference = reader.GetAttribute("ref");
                        
                        if (reference != null) {
                            keyValuePairs.Add(attributeName.ToLower(), reference);
                        }

                        var multiplier = reader.GetAttribute("multiplier");

                        if (multiplier != null)
                        {
                            keyValuePairs.Add(attributeName.ToLower() + ".multiplier", multiplier);
                        }
                    }
                }
                // Property value
                else if (reader.Depth == 3 && reader.NodeType == XmlNodeType.Text)
                {
                    attributeValue = reader.Value;
                }
                // End of cim property
                else if (reader.Depth == 2 && reader.NodeType == XmlNodeType.EndElement)
                {
                    keyValuePairs.Add(attributeName.ToLower(), attributeValue);
                }
                // End of cim object
                if (reader.Depth == 1 && reader.NodeType == XmlNodeType.EndElement)
                {
                    if (_supportedCimObjectsNames.Contains(classNameLower))
                    {
                        if (_validCimObjectCount.ContainsKey(className))
                            _validCimObjectCount[className]++;
                        else
                            _validCimObjectCount[className] = 1;
                    }
                    else
                    {
                        if (_invalidCimObjectCount.ContainsKey(className))
                            _invalidCimObjectCount[className]++;
                        else
                            _invalidCimObjectCount[className] = 1;
                    }


                    if (classNameLower == "positionpoint")
                    {
                        AddPositionPointToDict(keyValuePairs);
                    }
                    else if (classNameLower == "manufacturer")
                    {
                        AddManufacturerToDict(keyValuePairs);
                    }
                    else if (classNameLower == "terminal")
                    {
                        AddTerminalToDict(keyValuePairs, lineNumber);
                    }
                    else if (classNameLower == "psrtype")
                    {
                        AddPsrTypeToDict(keyValuePairs, lineNumber);
                    }
                    else if (classNameLower == "basevoltage")
                    {
                        AddBaseVoltageToDict(keyValuePairs, lineNumber);
                    }
                    else if (classNameLower == "voltagelevel")
                    {
                        AddVoltageLevelToDict(keyValuePairs, lineNumber);
                    }
                    else if (classNameLower == "bay")
                    {
                        AddBayToDict(keyValuePairs, lineNumber);
                    }

                    keyValuePairs.Clear();
                }

            }

            reader.Close();

            foreach (var validCimObjectCount in _validCimObjectCount.OrderBy(c => c.Key))
            {
                Logger.Log(LogLevel.Info, $"{validCimObjectCount.Key} ({validCimObjectCount.Value} XML elements read)");
            }

            foreach (var invalidCimObjectCount in _invalidCimObjectCount.OrderBy(c => c.Key))
            {
                Logger.Log(LogLevel.Error, $"Invalid XML element: {invalidCimObjectCount.Key} ({invalidCimObjectCount.Value} XML elements skipped)");
            }
        }

        private void AddPositionPointToDict(Dictionary<string, string> keyValuePairs)
        {
            if (keyValuePairs.ContainsKey("location") && keyValuePairs.ContainsKey("xposition") && keyValuePairs.ContainsKey("xposition") && keyValuePairs.ContainsKey("sequencenumber"))
            {
                DAXCoordinate coordinate = new DAXCoordinate();

                coordinate.X = Double.Parse(keyValuePairs["xposition"], CultureInfo.InvariantCulture);
                coordinate.Y = Double.Parse(keyValuePairs["yposition"], CultureInfo.InvariantCulture);

                int sequenceNumber = Int32.Parse(keyValuePairs["sequencenumber"]);

                Guid locationMrid = Guid.Parse(keyValuePairs["location"]);

                if (_coordsByLocationMrid.ContainsKey(locationMrid))
                {
                    _coordsByLocationMrid[locationMrid].Add((sequenceNumber, coordinate));
                }
                else
                {
                    _coordsByLocationMrid[locationMrid] = new List<(int, DAXCoordinate)>()  { (sequenceNumber, coordinate) };
                }
            }
            else
            {
                throw new DAXReaderException("Expected PositionPoint to contain the following child elements: Location, sequenceNumber, xPosition and yPosition");
            }
        }

        private void AddManufacturerToDict(Dictionary<string, string> keyValuePairs)
        {
            if (keyValuePairs.ContainsKey("mrid") && keyValuePairs.ContainsKey("name"))
            {
                Guid mrid = Guid.Parse(keyValuePairs["mrid"]);

                string name = keyValuePairs["name"].Trim();

                _manufactureByMrid[mrid] = name;
            }
            else
            {
                throw new DAXReaderException("Expected Manufacturer to contain the following child elements: mRID and name");
            }
        }

        private void AddBaseVoltageToDict(Dictionary<string, string> keyValuePairs, int lineNumber)
        {
            if (keyValuePairs.ContainsKey("mrid") && keyValuePairs.ContainsKey("nominalvoltage"))
            {
                Guid mrid = Guid.Parse(keyValuePairs["mrid"]);

                int nominalVoltage = 0;

                if (keyValuePairs.ContainsKey("nominalvoltage.multiplier"))
                {
                    var multiplier = keyValuePairs["nominalvoltage.multiplier"];

                    var value = keyValuePairs["nominalvoltage"].Trim();

                    nominalVoltage = ConvertValueWithMultipierToInteger(value, multiplier);
                }
                else
                {
                    nominalVoltage = Int32.Parse(keyValuePairs["nominalvoltage"].Trim());
                }

                if (nominalVoltage == 0)
                {

                }

                _baseVoltageByMrid[mrid] = nominalVoltage;
            }
            else
            {
                throw new DAXReaderException("Expected BaseVoltage to contain the following child elements: mRID and nominalVoltage");
            }
        }

        private static int ConvertValueWithMultipierToInteger(string value, string multiplier)
        {
            var newValue = value.Replace(",", ".");

            if (multiplier == "none")
                return Int32.Parse(newValue);
            else if (multiplier == "k")
                return Convert.ToInt32(Double.Parse(newValue, CultureInfo.InvariantCulture) * 1000);
            else if (multiplier == "M")
                return Convert.ToInt32(Double.Parse(newValue, CultureInfo.InvariantCulture) * 1000000);
            else if (multiplier == "G")
                return Convert.ToInt32(Double.Parse(newValue, CultureInfo.InvariantCulture) * 1000000000);
            else if (multiplier == "T")
                return Convert.ToInt32(Double.Parse(newValue, CultureInfo.InvariantCulture) * 1000000000000);
            else
                return 0;
        }

        private void AddVoltageLevelToDict(Dictionary<string, string> keyValuePairs, int lineNumber)
        {
            if (keyValuePairs.ContainsKey("mrid") && keyValuePairs.ContainsKey("equipmentcontainer") && keyValuePairs.ContainsKey("basevoltage"))
            {
                Guid mrid = Guid.Parse(keyValuePairs["mrid"]);
                Guid equipmentcontainer = Guid.Parse(keyValuePairs["equipmentcontainer"]);
                Guid basevoltage = Guid.Parse(keyValuePairs["basevoltage"]);

                _voltageLevelByMrid[mrid] = new VoltageLevelInfo()
                {
                    EquipmentContainer = equipmentcontainer,
                    BaseVoltage = basevoltage
                };
            }
            else
            {
                throw new DAXReaderException("Expected VoltageLevel to contain the following child elements: mRID, EquipmentContainer and BaseVoltage");
            }
        }

        private void AddBayToDict(Dictionary<string, string> keyValuePairs, int lineNumber)
        {
            if (keyValuePairs.ContainsKey("mrid") && keyValuePairs.ContainsKey("voltagelevel"))
            {
                Guid mrid = Guid.Parse(keyValuePairs["mrid"]);
                Guid voltageLevelRef = Guid.Parse(keyValuePairs["voltagelevel"]);

                _bayByMrid[mrid] = new BayInfo()
                {
                    VoltageLevel = voltageLevelRef
                };
            }
            else
            {
                throw new DAXReaderException("Expected Bay to contain the following child elements: mRID and VoltageLevel");
            }
        }

        private void AddPsrTypeToDict(Dictionary<string, string> keyValuePairs, int lineNumber)
        {
            if (keyValuePairs.ContainsKey("mrid") && keyValuePairs.ContainsKey("name"))
            {
                Guid mrid = Guid.Parse(keyValuePairs["mrid"]);

                string name = keyValuePairs["name"].Trim();

                _psrTypeByMrid[mrid] = name;
            }
            else
            {
                throw new DAXReaderException("Expected PsrType to contain the following child elements: mRID and name");
            }
        }

        private void AddTerminalToDict(Dictionary<string, string> keyValuePairs, int lineNumber)
        {
            Guid? terminalId = TryParseGuidElement("Terminal", "mrid", keyValuePairs, lineNumber);
            Int32? sequenceNumber = TryParseIntElement("Terminal", "sequencenumber", keyValuePairs, lineNumber);
            Guid? conductionEquipmentId = TryParseGuidElement("Terminal", "conductingequipment", keyValuePairs, lineNumber);
            Guid? connectivityNodeId = TryParseGuidElement("Terminal", "connectivitynode", keyValuePairs, lineNumber, false);

            if (terminalId != null && sequenceNumber != null && conductionEquipmentId != null)
            {
                var ti = new TerminalInfo()
                {
                    TerminalId = terminalId.Value,
                    SequenceNumber = sequenceNumber.Value,
                    ConnectivityNodeId = connectivityNodeId != null ? connectivityNodeId.Value : Guid.Empty
                };

                if (_terminalsByConductingEquipmentMrid.ContainsKey(conductionEquipmentId.Value))
                {
                    _terminalsByConductingEquipmentMrid[conductionEquipmentId.Value].Add(ti);
                }
                else
                {
                    _terminalsByConductingEquipmentMrid[conductionEquipmentId.Value] = new List<TerminalInfo> { ti };
                }
            }
        }

        private Guid? TryParseGuidElement(string elementName, string childElementNameToCheck, Dictionary<string, string> keyValuePairs, int lineNumber, bool logError = true)
        {
            if (keyValuePairs.ContainsKey(childElementNameToCheck))
            {
                string value = keyValuePairs[childElementNameToCheck];

                if (Guid.TryParse(value, out Guid guid))
                {
                    return guid;
                }
                else
                {
                    if (logError)
                        CimErrorLogger.Log(Severity.Error, "INVALID_CIM_XML_ELEMENT", $"Invalid UUID. Cannot parse {elementName}.{childElementNameToCheck} value '{value}' at line: {lineNumber}");

                    return null;
                }
            }
            else
            {
                if (logError)
                    CimErrorLogger.Log(Severity.Error, "INVALID_CIM_XML_ELEMENT", $"{elementName} XML element is missing {childElementNameToCheck} child element at line: {lineNumber}");

                return null;
            }
        }

        private Int32? TryParseIntElement(string elementName, string childElementNameToCheck, Dictionary<string, string> keyValuePairs, int lineNumber, bool logError = true)
        {
            if (keyValuePairs.ContainsKey(childElementNameToCheck))
            {
                string value = keyValuePairs[childElementNameToCheck];

                if (Int32.TryParse(value, out int intVal))
                {
                    return intVal;
                }
                else
                {
                    if (logError)
                        CimErrorLogger.Log(Severity.Error, "INVALID_TERMINAL_ELEMENT", $"Invalid interger value. Cannot parse {elementName}.{childElementNameToCheck} value '{value}' at line: {lineNumber}");

                    return null;
                }
            }
            else
            {
                if (logError)
                    CimErrorLogger.Log(Severity.Error, "INVALID_TERMINAL_ELEMENT", $"{elementName} XML element is missing {childElementNameToCheck} child element at line: {lineNumber}");

                return null;
            }
        }

        private class TerminalInfo
        {
            public int SequenceNumber { get; set; }
            public Guid TerminalId { get; set; }
            public Guid ConnectivityNodeId { get; set; }
        }

        private class VoltageLevelInfo
        {
            public Guid EquipmentContainer { get; set; }
            public Guid BaseVoltage { get; set; }
        }

        private class BayInfo
        {
            public Guid VoltageLevel { get; set; }
        }
    }
}

