using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using DAX.IO;
using DAX.TransformerUtil;
using DAX.Util;

namespace DAX.IO.Transformers
{
    public class DataTransformer
    {
        public TransformationConfig TransformationConfig;
        private Dictionary<string, IDaxReader> _dataReaders = new Dictionary<string, IDaxReader>();
        private Dictionary<string, IDaxWriter> _dataWriters = new Dictionary<string, IDaxWriter>();
        private TransformationSpecification _transSpec = null;
        private MappingGuide _mappingGuide = null;
        private bool _includeSurveyOutput = true;
        private bool _autoDatasetMapping = false;
        private bool _trimAllStringValues = false;
        private bool _suppressEmptyStringValues = false;
        private DataOrganizer _organizer = null;

        public DataOrganizer GetDataOrganizer
        {
            get { return _organizer; } 
        }

        public bool IncludeSurveyOutput {
            get { return _includeSurveyOutput; } 
            set {_includeSurveyOutput = value; }
        }

        public bool AutoDatasetMapping
        {
            get { return _autoDatasetMapping; }
            set { _autoDatasetMapping = value; }
        }

        public bool TrimAllStringValues
        {
            get { return _trimAllStringValues; }
            set { _trimAllStringValues = value; }
        }

        public bool SupressEmptyStringValues
        {
            get { return _suppressEmptyStringValues; }
            set { _suppressEmptyStringValues = value; }
        }

        public MappingGuide GetMappingGuide()
        {
            return _mappingGuide;
        }

        public TransformationSpecification GetSpecification()
        {
            return _transSpec;
        }

        public DataTransformer(TransformationSpecification transSpec)
        {
            _transSpec = transSpec;
        }

        public void AddDataReader(IDaxReader reader)
        {
            _dataReaders.Add(reader.DataSourceName().ToLower(), reader);
        }

        public void AddDataWriter(IDaxWriter writer)
        {
            _dataWriters.Add(writer.DataSourceName().ToLower(), writer);
        }

        public MappingGuide Simulate()
        {
            _organizer = new DataOrganizer(GetFirstDataReader());

            CreateMappingGuide(GetFirstDataReader(), GetFirstDataWriter());
            return _mappingGuide;
        }

        public MappingGuide TransferData(string projectName = null, string jobName = null, string userName = null)
        {
            if (_organizer == null)
            {
                _organizer = new DataOrganizer(GetFirstDataReader());
            }

            CreateMappingGuide(GetFirstDataReader(), GetFirstDataWriter());

            TransferData(GetFirstDataReader(), GetFirstDataWriter(), projectName, jobName, userName);

            return _mappingGuide;
        }


        private void TransferData(IDaxReader reader, IDaxWriter writer, string projectName = null, string jobName = null, string userName = null)
        {
            // Username
            string userInitials = userName;
            if (userInitials == null)
                userInitials = Environment.UserName;

            DateTime importedDate = DateTime.Now;

            DAXDataSet[] dataSets = _organizer.GetDataSetsOrderedByCategoryAndClassName();

            DAXMetaData writerMetaData = writer.GetMetaData();

            // Run through all datasets
            foreach (DAXDataSet dataSet in dataSets)
            {
                DataSetMappingGuide dsGuide = _mappingGuide.AddOrGetDataSetMappingGuide(dataSet.Name);

                if (dsGuide.DataSetMapping == null)
                {
                    Logger.Log(LogLevel.Warning, "No mapping specification found that uses dataset: " + dataSet.Name);
                }

                if (dsGuide.DataSetMapping != null)
                {
                    var attrMappings = dsGuide.DataSetMapping.AttributeMappings;

                    // Add attribut mapping to dataset from mapping guide, if it's mapped okay, to support transformation specification mapping
                    foreach (var attrMappingGuid in dsGuide.AttributeMappingGuides.Values)
                    {
                        if (attrMappingGuid.AttributeMapping != null && attrMappingGuid.AttributeMapping.InputFieldName != null)
                        {
                            if (!attrMappings.Exists(a => a.InputFieldName != null && (a.InputFieldName.ToLower() == attrMappingGuid.AttributeMapping.InputFieldName.ToLower())))
                            {
                                attrMappings.Add(attrMappingGuid.AttributeMapping);
                            }
                        }
                    }

                    Logger.Log(LogLevel.Info, "Transformer processing dataset mapping " + dsGuide.DataSetMapping.InputDataSet + "->" + dsGuide.DataSetMapping.OutputDataSet);

                    // Run through all features in the dataset
                    foreach (DAXFeature inputFeature in dataSet.Features)
                    {
                        if (inputFeature.GeometryType == DAXGeometryType.NoGemoetry || (inputFeature.Coordinates != null && inputFeature.Coordinates.Length > 0))
                        {
                            // Transform input feature to output feature
                            DAXFeature outputFeature = new DAXFeature() { Coordinates = inputFeature.Coordinates, GeometryType = inputFeature.GeometryType };
                            outputFeature.ClassName = dsGuide.DataSetMapping.OutputDataSet;

                            // Transfer const values
                            foreach (var constValue in dsGuide.DataSetMapping.GetConstValues())
                                outputFeature.Add(constValue.Key, constValue.Value);

                            // Transfer attribute values
                            foreach (var attrMapping in attrMappings)
                            {
                                if (attrMapping.InputFieldName != null)
                                {
                                    string inputFieldKey = attrMapping.InputFieldName.ToLower();

                                    if (inputFeature.ContainsKey(inputFieldKey) && dsGuide.AttributeMappingGuides.ContainsKey(inputFieldKey))
                                    {
                                        var attrGuide = dsGuide.AttributeMappingGuides[inputFieldKey];
                                        var inputAttr = inputFeature[inputFieldKey];

                                        if (attrGuide.AttributeMapping != null)
                                        {
                                            if (attrGuide.AttributeMapping.OutputFieldName == "{datasetname}")
                                            {
                                                var tempValue = TransferValue(attrGuide.AttributeMapping, inputAttr);
                                                if (tempValue != null)
                                                    outputFeature.ClassName = tempValue.ToString();
                                            }

                                            DAXAttributeDef attrDef = writerMetaData.GetFeatureAttributeDefinition(dsGuide.DataSetMapping.OutputDataSet, attrGuide.AttributeMapping.OutputFieldName);
                                            if (attrDef != null)
                                            {
                                                string outputFieldKey = attrDef.Name;

                                                if (!outputFeature.ContainsKey(outputFieldKey))
                                                {
                                                    var value = TransferValue(attrGuide.AttributeMapping, inputAttr);
                                                    if (value != null)
                                                        outputFeature.Add(outputFieldKey, value);
                                                }
                                                else
                                                {
                                                    var outputValue = outputFeature[outputFieldKey];
                                                    var value = TransferValue(attrGuide.AttributeMapping, inputAttr, outputValue);

                                                    if (value != null)
                                                        outputFeature[outputFieldKey] = value;
                                                }
                                            }
                                            else
                                            {
                                                if (writerMetaData.CanHandleAllAttributes == true)
                                                {
                                                    if (attrGuide.AttributeMapping != null && attrGuide.AttributeMapping.OutputFieldName != null)
                                                    {
                                                        if (!outputFeature.ContainsKey(attrGuide.AttributeMapping.OutputFieldName))
                                                            outputFeature.Add(attrGuide.AttributeMapping.OutputFieldName, TransferValue(attrGuide.AttributeMapping, inputAttr));
                                                        else
                                                        {
                                                            var outputValue = outputFeature[attrGuide.AttributeMapping.OutputFieldName];
                                                            var value = TransferValue(attrGuide.AttributeMapping, inputAttr, outputValue);

                                                            if (value != null)
                                                                outputFeature[attrGuide.AttributeMapping.OutputFieldName] = value;
                                                        }
                                                    }
                                                    else
                                                        outputFeature.Add(inputFieldKey, TransferValue(attrGuide.AttributeMapping, inputAttr));
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // Add all values if writer can handle it
                            if (writerMetaData.CanHandleAllOutputTypes == true && outputFeature.Values.Count == 0)
                            {
                                foreach (var inVal in inputFeature)
                                {
                                    if (inVal.Key != "numofpts" &&
                                        inVal.Key != "eminx" &&
                                        inVal.Key != "eminy" &&
                                        inVal.Key != "emaxx" &&
                                        inVal.Key != "emaxy" &&
                                        inVal.Key != "len" &&
                                        inVal.Key != "shape")
                                    {
                                        var value = TransferValue(inVal.Value);

                                        if (value != null)
                                            outputFeature.Add(inVal.Key, TransferValue(inVal.Value));
                                    }
                                }
                            }
                        
                            
                            //////////////////////////////////////////////////
                            // Handle survey information
                            //////////////////////////////////////////////////
                            string pointFcName = null;

                            if (_transSpec.SurveySettings != null && _transSpec.SurveySettings.PointFcName != null)
                                pointFcName = _transSpec.SurveySettings.PointFcName;

                            if (IncludeSurveyOutput && pointFcName != null)
                            {
                                string userFieldName = null;
                                string jobFieldName = null;
                                string projectFieldName = null;
                                string pointXFieldName = null;
                                string pointYFieldName = null;
                                string pointZFieldName = null;
                                string pointPointIdFieldName = null;
                                string messuredDateFieldName = null;
                                string importedDateFieldName = null;
                                string pointFcJobFieldName = null;
                                string pointFcProjectFieldName = null;
                                string pointFcUserFieldName = null;
                                string pointFcMeasuredDateFieldName = null;
                                string pointFcImportedDateFieldName = null;
                                string pointFcXYPrecisionFieldName = null;
                                string pointFcZPrecisionFieldName = null;
                                string objectFcXYPrecisionFieldName = null;
                                string objectFcZPrecisionFieldName = null;

                                if (_transSpec.SurveySettings is not null)
                                {
                                    if (_transSpec.SurveySettings.JobFieldName is not null)
                                        jobFieldName = _transSpec.SurveySettings.JobFieldName;
                                    if (_transSpec.SurveySettings.UserFieldName is not null)
                                        userFieldName = _transSpec.SurveySettings.UserFieldName;
                                    if (_transSpec.SurveySettings.ProjectFieldName is not null)
                                        projectFieldName = _transSpec.SurveySettings.ProjectFieldName;
                                    if (_transSpec.SurveySettings.PointFcXFieldName is not null)
                                        pointXFieldName = _transSpec.SurveySettings.PointFcXFieldName;
                                    if (_transSpec.SurveySettings.PointFcYFieldName is not null)
                                        pointYFieldName = _transSpec.SurveySettings.PointFcYFieldName;
                                    if (_transSpec.SurveySettings.PointFcZFieldName is not null)
                                        pointZFieldName = _transSpec.SurveySettings.PointFcZFieldName;
                                    if (_transSpec.SurveySettings.PointFcPointIdFieldName is not null)
                                        pointPointIdFieldName = _transSpec.SurveySettings.PointFcPointIdFieldName;
                                    if (_transSpec.SurveySettings.MeasuredDateFieldName is not null)
                                        messuredDateFieldName = _transSpec.SurveySettings.MeasuredDateFieldName;
                                    if (_transSpec.SurveySettings.ImportedDateFieldName is not null)
                                        importedDateFieldName = _transSpec.SurveySettings.ImportedDateFieldName;
                                    if (_transSpec.SurveySettings.PointFcJobFieldName is not null)
                                        pointFcJobFieldName = _transSpec.SurveySettings.PointFcJobFieldName;
                                    if (_transSpec.SurveySettings.PointFcProjectFieldName is not null)
                                        pointFcProjectFieldName = _transSpec.SurveySettings.PointFcProjectFieldName;
                                    if (_transSpec.SurveySettings.PointFcUserFieldName is not null)
                                        pointFcUserFieldName = _transSpec.SurveySettings.PointFcUserFieldName;
                                    if (_transSpec.SurveySettings.PointFcMeasuredDateFieldName is not null)
                                        pointFcMeasuredDateFieldName = _transSpec.SurveySettings.PointFcMeasuredDateFieldName;
                                    if (_transSpec.SurveySettings.PointFcImportedDateFieldName is not null)
                                        pointFcImportedDateFieldName = _transSpec.SurveySettings.PointFcImportedDateFieldName;
                                    if (_transSpec.SurveySettings.PointFcXYPrecisionFieldName is not null)
                                        pointFcXYPrecisionFieldName = _transSpec.SurveySettings.PointFcXYPrecisionFieldName;
                                    if (_transSpec.SurveySettings.PointFcZPrecisionFieldName is not null)
                                        pointFcZPrecisionFieldName = _transSpec.SurveySettings.PointFcZPrecisionFieldName;
                                    if (_transSpec.SurveySettings.ObjectFcXYPrecisionFieldName is not null)
                                        objectFcXYPrecisionFieldName = _transSpec.SurveySettings.ObjectFcXYPrecisionFieldName;
                                    if (_transSpec.SurveySettings.ObjectFcZPrecisionFieldName is not null)
                                        objectFcZPrecisionFieldName = _transSpec.SurveySettings.ObjectFcZPrecisionFieldName;
                                }

                                AddJobNameAndProjectNumber(ref outputFeature, jobFieldName, jobName, projectFieldName, projectName);
                                AddFieldValue(ref outputFeature, messuredDateFieldName, inputFeature.Coordinates[0].TimeStamp.ToString());
                                AddFieldValue(ref outputFeature, importedDateFieldName, importedDate.ToString());
                                AddFieldValue(ref outputFeature, userFieldName, userInitials);

                                // Calculate precision for objects (point og line)
                                if (inputFeature.GeometryType == DAXGeometryType.Point)
                                {
                                    // Ved punktobjekter er det nemt - tag blot precision fra første koordinat

                                    // XY Precision
                                    if (objectFcXYPrecisionFieldName != null && inputFeature.Coordinates[0].XYPrecision != 0)
                                        outputFeature.Add(objectFcXYPrecisionFieldName, inputFeature.Coordinates[0].XYPrecision);

                                    // Z Precision
                                    if (objectFcZPrecisionFieldName != null && inputFeature.Coordinates[0].ZPrecision != 0)
                                        outputFeature.Add(objectFcZPrecisionFieldName, inputFeature.Coordinates[0].ZPrecision);
                                }
                                if (inputFeature.GeometryType == DAXGeometryType.Line)
                                {
                                    List<double> planPrecisions = new List<double>();
                                    List<double> heightPrecisions = new List<double>();
                                    
                                    foreach (DAXCoordinate coord in inputFeature.Coordinates)
                                    {
                                        if (coord.XYPrecision != 0)
                                            planPrecisions.Add(coord.XYPrecision);

                                        if (coord.ZPrecision != 0)
                                            heightPrecisions.Add(coord.ZPrecision);
                                    }

                                    // Plan precision
                                    if (objectFcXYPrecisionFieldName != null && planPrecisions.Count > 0)
                                    {
                                        // Beregn middelværdi
                                        double avgValue = 0;
                                        
                                        foreach (double val in planPrecisions)
                                            avgValue += val;
                                        
                                        avgValue = avgValue / planPrecisions.Count;

                                        outputFeature.Add(objectFcXYPrecisionFieldName, avgValue);
                                    }

                                    // Height precision
                                    if (objectFcZPrecisionFieldName != null && heightPrecisions.Count > 0)
                                    {
                                        // Beregn middelværdi
                                        double avgValue = 0;

                                        foreach (double val in heightPrecisions)
                                            avgValue += val;

                                        avgValue = avgValue / heightPrecisions.Count;

                                        outputFeature.Add(objectFcZPrecisionFieldName, avgValue);
                                    }
                                }

                                // Create point features
                                foreach (DAXCoordinate coord in inputFeature.Coordinates)
                                {
                                    DAXFeature pointFeature = new DAXFeature() { Coordinates = new DAXCoordinate[] { coord }, GeometryType = DAXGeometryType.Point, ClassName = pointFcName };
                                    
                                    // Job name
                                    if (pointFcJobFieldName != null)
                                        pointFeature.Add(pointFcJobFieldName, jobName);
                                    else if (jobFieldName != null)
                                        pointFeature.Add(jobFieldName, jobName);

                                    // Project name
                                    if (pointFcProjectFieldName != null)
                                        pointFeature.Add(pointFcProjectFieldName, projectName);
                                    else if (projectFieldName != null)
                                        pointFeature.Add(projectFieldName, projectName);

                                    // User name
                                    if (pointFcUserFieldName != null)
                                        pointFeature.Add(pointFcUserFieldName, userInitials);
                                    else if (userFieldName != null)
                                        pointFeature.Add(userFieldName, userInitials);

                                    // Measured date
                                    if (pointFcMeasuredDateFieldName != null)
                                        pointFeature.Add(pointFcMeasuredDateFieldName, coord.TimeStamp.ToString());
                                    else if (messuredDateFieldName != null)
                                        pointFeature.Add(messuredDateFieldName, coord.TimeStamp.ToString());

                                    // Imported date
                                    if (pointFcImportedDateFieldName != null)
                                        pointFeature.Add(pointFcImportedDateFieldName, importedDate.ToString());
                                    else if (importedDateFieldName != null)
                                        pointFeature.Add(importedDateFieldName, importedDate.ToString());

                                    // X
                                    if (pointXFieldName != null)
                                        pointFeature.Add(pointXFieldName, coord.X);
                                    else
                                        pointFeature.Add("X", coord.X);

                                    // Y
                                    if (pointYFieldName != null)
                                        pointFeature.Add(pointYFieldName, coord.Y);
                                    else
                                        pointFeature.Add("Y", coord.Y);

                                    // Z
                                    if (pointZFieldName != null)
                                        pointFeature.Add(pointZFieldName, coord.Z);
                                    else
                                        pointFeature.Add("Z", coord.Z);

                                    // Point ID
                                    if (coord.ID != null)
                                    {
                                        if (pointPointIdFieldName != null)
                                            pointFeature.Add(pointPointIdFieldName, coord.ID);
                                        else
                                            pointFeature.Add("ID", coord.ID);
                                    }

                                    // XY Precision
                                    if (pointFcXYPrecisionFieldName != null && coord.XYPrecision != 0)
                                        pointFeature.Add(pointFcXYPrecisionFieldName, coord.XYPrecision);

                                    // Z Precision
                                    if (pointFcZPrecisionFieldName != null && coord.ZPrecision != 0)
                                        pointFeature.Add(pointFcZPrecisionFieldName, coord.ZPrecision);

                                    writer.WriteFeature(pointFeature);
                                }
                            }

                            writer.WriteFeature(outputFeature, dsGuide);
                        }
                    }
                }

                dataSet.Clear();
            }

            writer.Commit();
        }

        private void AddJobNameAndProjectNumber(ref DAXFeature feature, string jobNameField, string jobName, string projectNameField, string projectName) 
        {
            if (jobName != null)
                feature.Add(jobNameField, jobName);
            if (projectName != null)
                feature.Add(projectNameField, projectName);
        }

        private void AddFieldValue(ref DAXFeature feature, string fieldName, string value)
        {
            if (fieldName != null && value != null)
                feature.Add(fieldName, value);
        }


        private void CreateMappingGuide(IDaxReader reader, IDaxWriter writer)
        {
            HashSet<string> dontCheckTheseAttributes = new HashSet<string>() { "points", "numofpts", "eminx", "eminy", "emaxx", "emaxy", "len", "objectid", "shape" };

            DAXMetaData writerMetaData = writer.GetMetaData();

            _mappingGuide = new MappingGuide();

            _mappingGuide.DataReaderName = reader.DataSourceName();
            _mappingGuide.DataReaderClassName = reader.DataSourceClassName();
            _mappingGuide.DataWriterName = writer.DataSourceName();
            _mappingGuide.DataWriterClassName = writer.DataSourceTypeName();
            _mappingGuide.SpecificationName = _transSpec.Name;

            if (reader.AdditionalInformation() != null)
                _mappingGuide.DataReaderAdditionalInfo = reader.AdditionalInformation();

            DAXDataSet[] dataSets = _organizer.GetDataSetsOrderedByCategoryAndClassName();

            // Run through all datasets
            foreach (DAXDataSet dataSet in dataSets)
            {
                // Run through all features in the dataset
                foreach (DAXFeature feature in dataSet.Features)
                {
                    //string classKey = feature.ClassName.ToLower().Trim();
                    string classKey = feature.ClassName;

                    // Add feature to preview
                    _mappingGuide.UpdateMinMaxValues(feature);

                    // Analyse and create mapping
                    DataSetMapping[] dsMappings = _transSpec.FindDataSetMappings(classKey);

                    _mappingGuide.AddOrGetDataSetMappingGuide(feature.ClassName, dsMappings);
                    DataSetMappingGuide dsGuide = _mappingGuide.AddOrGetDataSetMappingGuide(feature.ClassName);
                    dsGuide.Antal++;

                    if (AutoDatasetMapping)
                    {
                        string outputDataSet = feature.ClassName;
                        string[] outputDataSetSplit = outputDataSet.Split('.');
                        if (outputDataSetSplit.Length > 1)
                            outputDataSet = outputDataSetSplit[outputDataSetSplit.Length - 1];

                        dsGuide.DataSetMapping = new DataSetMapping() { InputDataSet = feature.ClassName, OutputDataSet = outputDataSet };
                    }


                    // Check output metadata, hvis der er fundet en mapping
                    if (dsMappings.Length == 0)
                    {
                        if (!AutoDatasetMapping)
                            dsGuide.AddMessage(_mappingGuide, MessageLevel.Error, "Kan ikke finde mapning for input datasættet");
                    }
                    else
                    {
                        DataSetMapping dsMapping = dsMappings[0];

                        string outputClassKey = dsMapping.OutputDataSet;

                        // Find output dataset in writer metadata
                        DAXClassDef classDef = writerMetaData.GetFeatureClassDefinition(outputClassKey);

                        if (classDef == null)
                        {
                            if (!writerMetaData.CanHandleAllOutputTypes)
                                dsGuide.AddMessage(_mappingGuide, MessageLevel.Error, "Kan ikke finde output datasættet");
                            else
                            {
                                classDef = new DAXClassDef() { Name = dsMapping.OutputDataSet };
                            }
                        }

                        // Run through the features attributes
                        foreach (KeyValuePair<string, object> attribute in feature)
                        {
                            string attrKey = attribute.Key;

                            AttributeMapping[] attrMappings = _transSpec.FindAttributeMappings(classKey, attrKey);

                            _mappingGuide.AddOrGetAttributeMappingGuide(feature.ClassName, attrKey, attrMappings);
                            AttributeMappingGuide attrGuid = _mappingGuide.AddOrGetAttributeMappingGuide(feature.ClassName, attrKey);

                            // Check output metadata, hvis der er fundet en mapping
                            if (attrMappings.Length == 0 && !dsMapping.HasAutomaticMappingEnabled())
                            {
                                if (!dontCheckTheseAttributes.Contains(attrKey))
                                    attrGuid.AddMessage(_mappingGuide, MessageLevel.Error, "Kan ikke finde mapning for attribut");
                            }

                            // Check metadata
                            DAXAttributeDef attrDef = null;

                            if (attrMappings.Length > 0)
                            {
                                AttributeMapping attrMapping = attrMappings[0];
                                attrDef = writerMetaData.GetFeatureAttributeDefinition(outputClassKey, attrMapping.OutputFieldName);
                            }


                            if (attrDef == null)
                            {
                                bool isKey = false;

                                if (attrGuid != null && attrGuid.AttributeMapping != null)
                                    isKey = attrGuid.AttributeMapping.Key;

                                // If no mapping found, check if automatic mapping is set (and create one automatically)
                                if (attrDef == null && dsMapping.HasAutomaticMappingEnabled())
                                {
                                    attrDef = writerMetaData.GetFeatureAttributeDefinition(outputClassKey, attribute.Key);
                                    if (attrDef != null)
                                    {
                                        // Create attribute mapping, as part of automatic mapping
                                        if (attrGuid.AttributeMapping == null)
                                           attrGuid.AttributeMapping = new AttributeMapping() { DataSetMapping = dsMapping, InputFieldName = attribute.Key, OutputFieldName = attrDef.Name, Key = isKey };
                                    }
                                }

                                // If attrDef still is null, check if writer wants attributes anyway
                                if (attrDef == null && writerMetaData.CanHandleAllAttributes == true)
                                {
                                    // Create attribute mapping in=out
                                    if (attrGuid.AttributeMapping == null)
                                        attrGuid.AttributeMapping = new AttributeMapping() { DataSetMapping = dsMapping, InputFieldName = attribute.Key, OutputFieldName = attribute.Key, Key = isKey };
                                }
                                else
                                {
                                    if (!dontCheckTheseAttributes.Contains(attrKey))
                                        attrGuid.AddMessage(_mappingGuide, MessageLevel.Error, "Kan ikke finde feltet i output datasættet");
                                }
                            }
                        }
                    }
                }
            }
        }

        private object TransferValue(AttributeMapping attrMapping, object inputValue, object outputValue = null)
        {
            if (inputValue != null)
            {
                object value = inputValue;

                if (attrMapping != null)
                {
                    // Check if a constant value is specified
                    if (attrMapping.ContantValue != null)
                        return attrMapping.ContantValue;

                    if (attrMapping.InputActions != null)
                        value = attrMapping.ApplyInputActions(value);

                    if (attrMapping.ValueMappingRef != null)
                    {

                        // Check if a value mapping is specificed
                        var valueMappingSpec = TransformationConfig.GetValueMappingSpecification(attrMapping.ValueMappingRef);

                        if (valueMappingSpec != null)
                        {
                            bool reverse = false;
                            if (attrMapping.ValueMappingDirection != null && attrMapping.ValueMappingDirection == "reverse")
                                reverse = true;

                            var mapResult = valueMappingSpec.GetValue(value.ToString(), reverse);

                            return mapResult;
                        }
                    }

                    if (attrMapping.OutputActions != null)
                    {
                        value = attrMapping.ApplyOutputActions(value, outputValue);
                    }
                    else
                    {
                        // If no outputactions and output is not null, preserve output
                        if (outputValue != null)
                            return outputValue;
                    }
                }

                if (value is string)
                {
                    string stringValue = value as string;

                    // Check if we must trim all string values
                    if (_trimAllStringValues && stringValue != null && stringValue.Length > 0)
                        return String.Intern(stringValue.Trim());

                    // Check if we should suppress blank string values
                    if (_suppressEmptyStringValues && stringValue != null && (stringValue.Length == 0 || stringValue.Trim().Length == 0))
                        return null;

                    return String.Intern((string)value);
                }

                return value;
            }

            return inputValue;
        }

        private object TransferValue(object inputValue)
        {
            return TransferValue(null, inputValue, null);
        }

        public IDaxReader GetFirstDataReader()
        {
            if (_dataReaders.Count < 1)
                throw new Exception("Data transformation error: No datareaders found. Expecting at least one!");

            return _dataReaders.ToArray()[0].Value;
        }

        public IDaxWriter GetFirstDataWriter()
        {
            if (_dataWriters.Count < 1)
                throw new Exception("Data transformation error: No datarwriters found. Expecting at least one!");

            return _dataWriters.ToArray()[0].Value;
        }

    }

}

