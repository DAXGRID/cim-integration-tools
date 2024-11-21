using DAX.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace DAX.IO
{
    public class TransformationSpecification
    {
        private bool _lookupPrepared = false;
        private Dictionary<string, List<AttributeMapping>> _attributMappingQuickLookup = new Dictionary<string, List<AttributeMapping>>();
        private Dictionary<string, List<DataSetMapping>> _dataSetMappingQuickLookup = new Dictionary<string, List<DataSetMapping>>();

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("logFileName")]
        public string LogFileName { get; set; }
        
        [XmlAttribute("description")]
        public string Description { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("dataReader")]
        public string DataReaderName { get; set; }

        [XmlAttribute("dataWriter")]
        public string DataWriterName { get; set; }

        [XmlAttribute("autoDataSetMapping")]
        public string AutoDatasetMapping { get; set; }

        [XmlAttribute("trimAllStringValues")]
        public string TrimAllStringValues { get; set; }

        [XmlAttribute("suppressEmptyStringValues")]
        public string SuppressEmptyStringValues { get; set; }

        public SurveySettings SurveySettings;

        [XmlElement("DataReader")]
        public List<DataReaderWriterSpecification> DataReaders = new List<DataReaderWriterSpecification>();

        [XmlElement("DataWriter")]
        public List<DataReaderWriterSpecification> DataWriters = new List<DataReaderWriterSpecification>();

        [XmlElement("AttributeMapping")]
        public List<AttributeMapping> GlobalAttributeMappings = new List<AttributeMapping>();

        [XmlElement("DataSetMapping")]
        public List<DataSetMapping> DataSetMappings = new List<DataSetMapping>();

        public AttributeMapping[] FindAttributeMappings(string className, string attributeName)
        {
            List<AttributeMapping> result = new List<AttributeMapping>();

            PrepareLookup();

            string attrKey = attributeName.ToLower();
            string classKey = className.ToLower();

            //string attrKey = attributeName;
            //string classKey = className;

            // First check global attribut mappings
            if (_attributMappingQuickLookup.ContainsKey(attrKey))
                result.AddRange(_attributMappingQuickLookup[attrKey]);

            // Check attribut mappings part of row mapping
            if (_attributMappingQuickLookup.ContainsKey(classKey + "." + attrKey))
                result.AddRange(_attributMappingQuickLookup[classKey + "." + attrKey]);

            return result.ToArray();
        }

        public DataSetMapping[] FindDataSetMappings(string className)
        {
            PrepareLookup();

            string classKey = className.ToLower();
            //string classKey = className;


            if (_dataSetMappingQuickLookup.ContainsKey(classKey))
                return _dataSetMappingQuickLookup[classKey].ToArray();
            else if (_dataSetMappingQuickLookup.ContainsKey(classKey.ToLower()))
                return _dataSetMappingQuickLookup[classKey.ToLower()].ToArray();
            else
            {
                // Try take the name after last dot, if the className contains dot
                string[] dotSplit = classKey.Split('.');
                if (dotSplit.Length > 1)
                {
                    classKey = dotSplit[dotSplit.Length - 1];

                    if (_dataSetMappingQuickLookup.ContainsKey(classKey))
                        return _dataSetMappingQuickLookup[classKey].ToArray();
                }
            }

            return new DataSetMapping[0];
        }

        private void PrepareLookup()
        {
            if (!_lookupPrepared)
            {
                // Dataset mappings
                foreach (DataSetMapping dsMapping in DataSetMappings)
                {
                    string key = dsMapping.InputDataSet.ToLower().Trim();
                    //string key = dsMapping.InputDataSet;

                    if (_dataSetMappingQuickLookup.ContainsKey(key))
                        _dataSetMappingQuickLookup[key].Add(dsMapping);
                    else
                    {
                        _dataSetMappingQuickLookup[key] = new List<DataSetMapping>();
                        _dataSetMappingQuickLookup[key].Add(dsMapping);
                    }
                }

                // Global attribut rules
                foreach (AttributeMapping attrMapping in GlobalAttributeMappings)
                {
                    string key = attrMapping.InputFieldName.ToLower().Trim();
                    //string key = attrMapping.InputFieldName;

                    if (_attributMappingQuickLookup.ContainsKey(key))
                        _attributMappingQuickLookup[key].Add(attrMapping);
                    else
                    {
                        _attributMappingQuickLookup[key] = new List<AttributeMapping>();
                        _attributMappingQuickLookup[key].Add(attrMapping);
                    }
                }

                // Attribute rules part of dataset rules
                foreach (DataSetMapping dsMapping in DataSetMappings)
                {
                    foreach (AttributeMapping attrMapping in dsMapping.AttributeMappings)
                    {
                        if (attrMapping.InputFieldName != null)
                        {
                            attrMapping.DataSetMapping = dsMapping;

                            string key = dsMapping.InputDataSet.ToLower().Trim() + "." + attrMapping.InputFieldName.ToLower().Trim();
                            //string key = dsMapping.InputDataSet + "." + attrMapping.InputFieldName;

                            if (_attributMappingQuickLookup.ContainsKey(key))
                                _attributMappingQuickLookup[key].Add(attrMapping);
                            else
                            {
                                _attributMappingQuickLookup[key] = new List<AttributeMapping>();
                                _attributMappingQuickLookup[key].Add(attrMapping);
                            }
                        }
                    }
                }

                _lookupPrepared = true;
            }
        }
    }

    public class SurveySettings
    {
        public string JobFieldName { get; set; }
        public string ProjectFieldName { get; set; }
        public string UserFieldName { get; set; }
        public string MeasuredDateFieldName { get; set; }
        public string ImportedDateFieldName { get; set; }
        public string PointFcName { get; set; }
        public string PointFcXFieldName { get; set; }
        public string PointFcYFieldName { get; set; }
        public string PointFcZFieldName { get; set; }
        public string PointFcPointIdFieldName { get; set; }
        public string PointFcJobFieldName { get; set; }
        public string PointFcProjectFieldName { get; set; }
        public string PointFcUserFieldName { get; set; }
        public string PointFcMeasuredDateFieldName { get; set; }
        public string PointFcImportedDateFieldName { get; set; }
        public string PointFcXYPrecisionFieldName { get; set; }
        public string PointFcZPrecisionFieldName { get; set; }
        public string ObjectFcXYPrecisionFieldName { get; set; }
        public string ObjectFcZPrecisionFieldName { get; set; }
    }

    public class ConfigParameter
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlAttribute("value")]
        public string Value { get; set; }

        [XmlText]
        public string InlineValue { get; set; }
    }

    public class DataReaderWriterSpecification
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("class")]
        public string ClassName { get; set; }

        [XmlAttribute("assembly")]
        public string AssemblyName { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }

        [XmlElement("PreSql")]
        public string PreSql { get; set; }

        [XmlElement("Parameter")]
        public List<ConfigParameter> ConfigParameters = new List<ConfigParameter>();

        [XmlElement("DataSetSpecification")]
        public List<DataSetSpecification> DataSetSpecifications = new List<DataSetSpecification>();

        public string GetParameterValue(string parameterName)
        {
            foreach (ConfigParameter configParam in ConfigParameters)
            {
                if (configParam.Name != null && configParam.Name.ToLower() == parameterName.ToLower())
                    return configParam.Value;
            }

            return null;
        }

        public bool CheckIfParameterExistAndIsTrue(string parameterName)
        {
            foreach (ConfigParameter configParam in ConfigParameters)
            {
                if (configParam.Name != null && configParam.Name.ToLower() == parameterName.ToLower())
                {
                    if (configParam.Value != null && (configParam.Value.ToLower() == "yes" || configParam.Value.ToLower() == "true"))
                        return true;
                }
            }

            return false;
        }
    }

    public class DataWriterSpecification
    {
        [XmlAttribute("class")]
        public string ClassName { get; set; }

        [XmlElement("Parameter")]
        public List<ConfigParameter> ConfigParameters = new List<ConfigParameter>();
    }

    public class DataSetSpecification
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("class")]
        public string ClassName { get; set; }

        [XmlAttribute("geometryTable")]
        public string GeometryTable { get; set; }

        public string SQL { get; set; }

        /// <summary>
        /// Used for linq queries
        /// </summary>
        public string OfType { get; set; }

        /// <summary>
        /// Used for linq queries
        /// </summary>
        public string Select { get; set; }

        /// <summary>
        /// Used for linq queries
        /// </summary>
        public string SelectMany { get; set; }

        /// <summary>
        /// Used for linq queries
        /// </summary>
        public string Where { get; set; }

        /// <summary>
        /// Used for linq queries
        /// </summary>
        public string OrderBy { get; set; }

    }

    public enum InputActionType
    {
        Trim = 0,
        Lower = 1,
        Upper = 2,
        BlankToNull = 3
    }

    public enum OutputActionType
    {
        AppendInput = 1,
        AppendText = 2
    }

    public class OutputAction
    {
        public OutputActionType Type { get; set; }
        public string Param1 { get; set; }
        public string Param2 { get; set; }
        public string Param3 { get; set; }
    }
    
    public class AttributeMapping
    {
        [XmlAttribute("key")]
        public bool Key { get; set; }

        [XmlAttribute("inputField")]
        public string InputFieldName { get; set; }

        [XmlAttribute("outputField")]
        public string OutputFieldName { get; set; }

        [XmlAttribute("constantValue")]
        public string ContantValue { get; set; }

        [XmlAttribute("valueMapping")]
        public string ValueMappingRef { get; set; }

        [XmlAttribute("valueMappingDirection")]
        public string ValueMappingDirection { get; set; }

        [XmlAttribute("inputActions")]
        public string InputActions { get; set; }

        [XmlAttribute("outputActions")]
        public string OutputActions { get; set; }

        [XmlAttribute("conditions")]
        public string Conditions { get; set; }

        [XmlIgnore]
        public DataSetMapping DataSetMapping = null;

        [XmlIgnore]
        public int Order = 0;

        private List<InputActionType> _inputActions = null;
        private List<OutputAction> _outputActions = null;

        public object ApplyInputActions(object value) 
        {
            if (InputActions == null)
                return null;

            if (_inputActions == null)
                _inputActions = ParseInputActionString(InputActions);

            return ApplyInputActions(_inputActions, value);

            return value;
        }

        private List<InputActionType> ParseInputActionString(string actions)
        {
            List<InputActionType> result = new List<InputActionType>();

            string[] actionSplit = actions.Split(';');

            foreach (var actionSTr in actionSplit)
            {
                InputActionType action;
                if (InputActionType.TryParse<InputActionType>(actionSTr.Trim(), true, out action))
                    result.Add(action);
                else
                    Logger.Log(LogLevel.Warning, "Input action: '" + actionSTr.Trim() + "' not recognized. Check documentation.");
            }

            return result;
        }

        private object ApplyInputActions(List<InputActionType> actionList, object inputValue)
        {
            if (actionList.Count == 0 || inputValue == null)
                return inputValue;
            else
            {
                string result = inputValue.ToString();

                foreach (var action in actionList)
                {
                    if (action == InputActionType.Lower)
                        result = result.ToLower();
                    else if (action == InputActionType.Upper)
                        result = result.ToUpper();
                    else if (action == InputActionType.Trim)
                        result = result.Trim();
                    else if (action == InputActionType.BlankToNull)
                    {
                        if (result == "")
                            result = null;
                    }
                }

                return result;
            }
        }
        
        public object ApplyOutputActions(object inputValue, object outputValue)
        {
            if (OutputActions == null)
                return outputValue;

            if (_outputActions == null)
                _outputActions = ParseOutputActionString(OutputActions);

            return ApplyOutputActions(_outputActions, inputValue, outputValue);
        }

        private List<OutputAction> ParseOutputActionString(string actions)
        {
            List<OutputAction> result = new List<OutputAction>();

            string[] actionSplit = actions.Split(';');

            foreach (var actionSTr in actionSplit)
            {
                OutputActionType action;
                string actionStrTrimmed = actionSTr.Trim();
                if (OutputActionType.TryParse<OutputActionType>(actionStrTrimmed, true, out action))
                    result.Add(new OutputAction() { Type = action });
                else
                {
                    // Try to check if param action
                    if (actionStrTrimmed.ToLower().StartsWith("appendtext"))
                    {
                        string[] paramSplit = actionStrTrimmed.Split('\'');

                        if (paramSplit.Length == 3)
                            result.Add(new OutputAction() { Type = OutputActionType.AppendText, Param1 = paramSplit[1] });
                    }
                    else if (actionStrTrimmed.ToLower().StartsWith("appendinput"))
                    {
                        string[] paramSplit = actionStrTrimmed.Split('\'');

                        if (paramSplit.Length == 3)
                            result.Add(new OutputAction() { Type = OutputActionType.AppendInput, Param1 = paramSplit[1] });
                    }
                    else
                        Logger.Log(LogLevel.Warning, "Output action: '" + actionSTr.Trim() + "' not recognized. Check documentation.");
                }
                   
            }

            return result;
        }
        
        private object ApplyOutputActions(List<OutputAction> actionList, object inputValue, object outputValue)
        {
            if (actionList.Count == 0)
                return outputValue;
            else
            {
                object result = outputValue;

                foreach (var action in actionList)
                {
                    if (action.Type == OutputActionType.AppendInput)
                    {
                        if (result == null)
                            result = inputValue;
                        else if (inputValue != null)
                        {
                            result += action.Param1 + inputValue;
                        }
                    }
                    if (action.Type == OutputActionType.AppendText)
                    {
                        if (result == null)
                            result = action.Param1;
                        else if (inputValue != null)
                        {
                            result += action.Param1;
                        }
                    }
                    
                }

                return result;
            }
        }
        
    }

    public class ConstantValue
    {
        [XmlAttribute("fieldName")]
        public string FieldName { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }
    }

    public class DataSetMapping
    {
        private List<KeyValuePair<string, string>> _constValues = null;
        private Dictionary<string, AttributeMapping> _attrMappingQuickLookup = null;

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("inputDataSet")]
        public string InputDataSet { get; set; }

        [XmlAttribute("outputDataSet")]
        public string OutputDataSet { get; set; }

        [XmlElement("AttributeMapping")]
        public List<AttributeMapping> AttributeMappings = new List<AttributeMapping>();

        [XmlElement("Constant")]
        public List<ConstantValue> Constants = new List<ConstantValue>();

        [XmlAttribute("autoMapping")]
        public string AutomaticFieldMapping { get; set; }

        public bool HasAutomaticMappingEnabled()
        {
            if (AutomaticFieldMapping != null)
            {
                if (AutomaticFieldMapping.ToLower() == "true" || AutomaticFieldMapping.ToLower() == "yes")
                    return true;
            }

            return false;
        }

        public List<KeyValuePair<string, string>> GetConstValues()
        {
            if (_constValues == null)
            {
                _constValues = new List<KeyValuePair<string, string>>();

                foreach (var attr in AttributeMappings)
                {
                    if (attr.OutputFieldName != null && attr.ContantValue != null)
                        _constValues.Add(new KeyValuePair<string, string>(attr.OutputFieldName, attr.ContantValue));
                }
            }

            return _constValues;
        }

        public AttributeMapping GetAttributeMapping(string name)
        {
            if (_attrMappingQuickLookup == null)
            {
                _attrMappingQuickLookup = new Dictionary<string, AttributeMapping>();

                if (AttributeMappings != null)
                {
                    foreach (var attrMapping in AttributeMappings)
                    {
                        if (attrMapping.InputFieldName != null)
                        {

                            //string key = attrMapping.InputFieldName.ToLower();
                            string key = attrMapping.InputFieldName;

                            if (!_attrMappingQuickLookup.ContainsKey(key))
                                _attrMappingQuickLookup.Add(key, attrMapping);
                        }
                    }
                }
            }

            //if (_attrMappingQuickLookup.ContainsKey(name.ToLower()))
            //    return _attrMappingQuickLookup[name.ToLower()];

            if (_attrMappingQuickLookup.ContainsKey(name))
                return _attrMappingQuickLookup[name];

            return null;
        }
    }

}
