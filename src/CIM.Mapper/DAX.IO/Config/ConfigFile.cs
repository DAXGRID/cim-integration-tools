using DAX.IO.Transformers;
using DAX.IO.Writers;
using System.Runtime.Remoting;
using System.Xml.Serialization;

namespace DAX.IO
{
    public class TransformationConfig
    {
        private Dictionary<string, ValueMappingSpecification> _valueMappingSpecQuickLookup = null;
        
        [XmlElement("TransformationSpecification")]
        public List<TransformationSpecification> TransformationSpecifications = new List<TransformationSpecification>();

        [XmlElement("ValueMappingSpecification")]
        public List<ValueMappingSpecification> ValueMappingSpecs = new List<ValueMappingSpecification>();

        [XmlElement("DataReader")]
        public List<DataReaderWriterSpecification> DataReaders = new List<DataReaderWriterSpecification>();

        [XmlElement("DataWriter")]
        public List<DataReaderWriterSpecification> DataWriters = new List<DataReaderWriterSpecification>();

        [XmlElement("Serializer")]
        public List<SerializerSpecification> Serializers = new List<SerializerSpecification>();

        [XmlElement("GraphProcessor")]
        public List<GraphProcessorSpecification> GraphProcessors = new List<GraphProcessorSpecification>();


        public TransformationConfig LoadFromFile(string fileName)
        {
        
            // Read the specification
            XmlSerializer serializer = new XmlSerializer(typeof(TransformationConfig));
            TransformationConfig config;
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
            {
                config = (TransformationConfig)serializer.Deserialize(fileStream);
            }

            CleanConfig(config);

            return config;
        }

        private void CleanConfig(TransformationConfig config)
        {
            // Clean data readers
            if (config.DataReaders != null)
            {
                foreach (var dr in config.DataReaders)
                {
                    dr.Name = dr.Name.ToLower();

                    if (dr.DataSetSpecifications != null)
                    {
                        foreach (var ds in dr.DataSetSpecifications)
                        {
                            ds.Name = ds.Name.ToLower();
                        }
                    }
                }
            }

            // Clean data writers
            if (config.DataWriters != null)
            {
                foreach (var dw in config.DataWriters)
                {
                    dw.Name = dw.Name.ToLower();

                    if (dw.DataSetSpecifications != null)
                    {
                        foreach (var ds in dw.DataSetSpecifications)
                        {
                            ds.Name = ds.Name.ToLower();
                        }
                    }
                }
            }

            if (config.TransformationSpecifications != null)
            {
                foreach (var ts in config.TransformationSpecifications)
                {
                    if (ts.DataReaderName != null)
                        ts.DataReaderName = ts.DataReaderName.ToLower();

                    if (ts.DataWriterName != null)
                        ts.DataWriterName = ts.DataWriterName.ToLower();

                    foreach (var dsm in ts.DataSetMappings)
                    {
                        if (dsm.InputDataSet != null)
                            dsm.InputDataSet = dsm.InputDataSet.ToLower();

                        if (dsm.OutputDataSet != null)
                            dsm.OutputDataSet = dsm.OutputDataSet.ToLower();

                        if (dsm.AttributeMappings != null)
                        {
                            foreach (var am in dsm.AttributeMappings)
                            {
                                if (am.InputFieldName != null)
                                    am.InputFieldName = am.InputFieldName.ToLower();

                                /*
                                if (am.OutputFieldName != null)
                                    am.OutputFieldName = am.OutputFieldName.ToLower();
                                    */
                            }
                        }

                    }
                }
            }

        }

        public DataReaderWriterSpecification[] GetDataSourceByClass(string className)
        {
            List<DataReaderWriterSpecification> result = new List<DataReaderWriterSpecification>();

            foreach (var dw in DataWriters)
            {
                if (dw.ClassName is not null && dw.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
                    result.Add(dw);
            }
            foreach (var dr in DataReaders)
            {
                if (dr.ClassName is not null && dr.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
                    result.Add(dr);
            }

            return result.ToArray();
        }

        public DataReaderWriterSpecification GetDataSourceByName(string name)
        {
            foreach (var dw in DataWriters)
            {
                if (dw.Name is not null && dw.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return dw;
            }
            foreach (var dr in DataReaders)
            {
                if (dr.Name is not null && dr.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return dr;
            }

            return null;
        }

        public DataTransformer InitializeDataTransformer(string name, bool useMockupWriter = false)
        {
            TransformationSpecification transSpec = GetTransformationSpecification(name);

            if (transSpec == null)
                throw new Exception("Cannot find transformation specification: '" + name + "' in config file.");
            else
            {
                DataTransformer dataTransformer = new DataTransformer(transSpec);
                dataTransformer.TransformationConfig = this;

                // Initialize reader
                if (transSpec.DataReaderName != null)
                {
                    DataReaderWriterSpecification readerSpec = GetDataSourceByName(transSpec.DataReaderName);
                    
                    if (readerSpec == null)
                        throw new Exception("Cannot find reader specification with name '" + transSpec.DataReaderName + "' in config file");

                    if (readerSpec.ClassName == null)
                        throw new Exception("No class attribute specified on reader specification '" + transSpec.DataReaderName + "'. Cannot instantiate class.");

                    string classNameToLoad = "DAX.IO.Readers." + readerSpec.ClassName;

                    try
                    {
                        ObjectHandle objHandle = null;

                        if (readerSpec.AssemblyName != null)
                            objHandle = Activator.CreateInstance(readerSpec.AssemblyName, classNameToLoad);
                        else
                            objHandle = Activator.CreateInstance(null, classNameToLoad);

                        var dataReader = (IDaxReader)objHandle.Unwrap();
                        dataReader.Initialize(readerSpec.Name, readerSpec, readerSpec.ConfigParameters);
                        dataTransformer.AddDataReader(dataReader);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error occured trying to instantiate class: '" + classNameToLoad + "' - " + ex.Message + "\r\n\r\n" + ex.ToString());
                    }
                }

                // Initialize writer
                if (transSpec.DataWriterName != null)
                {
                    DataReaderWriterSpecification writerSpec = GetDataSourceByName(transSpec.DataWriterName);

                    if (writerSpec == null)
                        throw new Exception("Cannot find writer specification with name '" + transSpec.DataWriterName + "' in configuration");

                    if (writerSpec.ClassName == null)
                        throw new Exception("No class attribute specified on writer specification '" + transSpec.DataWriterName + "'. Cannot instantiate class.");

                    if (useMockupWriter)
                    {
                        MockupWriter mockupWriter = new MockupWriter();
                        mockupWriter.Initialize(writerSpec.Name, writerSpec, this, writerSpec.ConfigParameters);
                        dataTransformer.AddDataWriter(mockupWriter);
                    }
                    else
                    {
                        string classNameToLoad = "DAX.IO.Writers." + writerSpec.ClassName;

                        try
                        {
                            ObjectHandle objHandle = Activator.CreateInstance(writerSpec.AssemblyName, classNameToLoad);

                            var dataWriter = (IDaxWriter)objHandle.Unwrap();
                            dataWriter.Initialize(writerSpec.Name, writerSpec, this, writerSpec.ConfigParameters);
                            dataTransformer.AddDataWriter(dataWriter);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Error occured trying to instantiate class: '" + classNameToLoad + "' - " + ex.Message + "\r\n\r\n" + ex.ToString());
                        }
                    }
                }

                // If no survey element found, then don't output survey information to writer
                if (transSpec.SurveySettings == null)
                    dataTransformer.IncludeSurveyOutput = false;

                // Check if auto dataset mapping is set to yes or true
                if (transSpec.AutoDatasetMapping != null && (transSpec.AutoDatasetMapping.ToLower() == "yes" || transSpec.AutoDatasetMapping.ToLower() == "true"))
                    dataTransformer.AutoDatasetMapping = true;

                // Check if trim all string values is set to yes or true
                if (transSpec.TrimAllStringValues != null && (transSpec.TrimAllStringValues.ToLower() == "yes" || transSpec.TrimAllStringValues.ToLower() == "true"))
                    dataTransformer.TrimAllStringValues = true;

                // Check if suppress empty string values is set to yes or true
                if (transSpec.SuppressEmptyStringValues != null && (transSpec.SuppressEmptyStringValues.ToLower() == "yes" || transSpec.SuppressEmptyStringValues.ToLower() == "true"))
                    dataTransformer.SupressEmptyStringValues = true;


                return dataTransformer;
            }
        }

        public IDAXInitializeable InitializeSerializer(string name)
        {
            SerializerSpecification serializerSpec = GetSerializerSpecification(name);

            if (serializerSpec == null)
                throw new Exception("Cannot find serializer: '" + name + "' in config file.");
            else
            {
                try
                {

                    if (serializerSpec.AssemblyName != null)
                    {

                        string classNameToLoad = serializerSpec.AssemblyName + "." + serializerSpec.ClassName;
                        ObjectHandle objHandle = Activator.CreateInstance(serializerSpec.AssemblyName, classNameToLoad);

                        var serializer = (IDAXInitializeable)objHandle.Unwrap();
                        serializer.Initialize(serializerSpec.Name, serializerSpec.ConfigParameters);

                        return serializer;
                    }
                    else
                    {
                        ObjectHandle objHandle = Activator.CreateInstance(null, serializerSpec.ClassName);

                        var serializer = (IDAXInitializeable)objHandle.Unwrap();
                        serializer.Initialize(serializerSpec.Name, serializerSpec.ConfigParameters);

                        return serializer;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Error occured trying to instantiate class: '" + serializerSpec.ClassName + "' - " + ex.Message + "\r\n\r\n" + ex.ToString());
                }
            }
        }

        public IDAXInitializeable InitializeGraphProcessor(GraphProcessorSpecification graphProcessorSpec)
        {
            string classNameToLoad = "Unknown";

            try
            {
                if (graphProcessorSpec.AssemblyName != null)
                {
                    classNameToLoad = graphProcessorSpec.AssemblyName + "." + graphProcessorSpec.ClassName;

                    ObjectHandle objHandle = Activator.CreateInstance(graphProcessorSpec.AssemblyName, classNameToLoad);

                    var graphProcessor = (IDAXInitializeable)objHandle.Unwrap();
                    graphProcessor.Initialize(graphProcessorSpec.Name, graphProcessorSpec.ConfigParameters);

                    return graphProcessor;
                }
                else
                {
                    ObjectHandle objHandle = Activator.CreateInstance(null, graphProcessorSpec.ClassName);

                    var graphProcessor = (IDAXInitializeable)objHandle.Unwrap();
                    graphProcessor.Initialize(graphProcessorSpec.Name, graphProcessorSpec.ConfigParameters);

                    return graphProcessor;
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Error occured trying to instantiate class: '" + classNameToLoad + "' - " + ex.Message + "\r\n\r\n" + ex.ToString());
            }
        }


        public TransformationSpecification GetTransformationSpecification(string name) 
        {
            foreach (TransformationSpecification transSpec in TransformationSpecifications) 
            {
                if (transSpec.Name is not null && transSpec.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return transSpec;
            }

            return null;
        }

        public SerializerSpecification GetSerializerSpecification(string name)
        {
            foreach (SerializerSpecification serializer in Serializers)
            {
                if (serializer.Name is not null && serializer.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return serializer;
            }

            return null;
        }

        public GraphProcessorSpecification GetGraphProcessorSpecification(string name)
        {
            foreach (GraphProcessorSpecification graphProcessor in GraphProcessors)
            {
                if (graphProcessor.Name is not null && graphProcessor.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return graphProcessor;
            }

            return null;
        }


        public ValueMappingSpecification GetValueMappingSpecification(string name)
        {
            if (_valueMappingSpecQuickLookup == null)
            {
                _valueMappingSpecQuickLookup = new Dictionary<string, ValueMappingSpecification>();

                if (ValueMappingSpecs != null)
                {
                    foreach (var valueMappingSpec in ValueMappingSpecs)
                    {
                        _valueMappingSpecQuickLookup.Add(valueMappingSpec.Name.ToLower(), valueMappingSpec);
                    }
                }
            }


            string nameLower = name.ToLower();

            if (_valueMappingSpecQuickLookup.ContainsKey(nameLower))
                return _valueMappingSpecQuickLookup[nameLower];
            else
                return null;

        }
    }

    public class ValueMappingSpecification
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("caseSensitive")]
        public bool CaseSensitive { get; set; }

        [XmlElement("ValueMapping")]
        public List<ValueMapping> ValueMappings = new List<ValueMapping>();

        private Dictionary<string, string> _quickForwardLockup = null;
        private Dictionary<string, string> _quickReverseLockup = null;

        public string GetValue(string inputValue, bool reverse = false)
        {
            if (_quickForwardLockup == null)
            {
                _quickForwardLockup = new Dictionary<string, string>();
                _quickReverseLockup = new Dictionary<string, string>();

                if (ValueMappings != null)
                {
                    foreach (var valueMapping in ValueMappings)
                    {
                        string fromValueKey = valueMapping.FromValue;
                        string toValueKey = valueMapping.ToValue;

                        if (!CaseSensitive)
                        {
                            fromValueKey = valueMapping.FromValue.ToLower();
                            toValueKey = valueMapping.ToValue.ToLower();
                        }

                        if (!_quickForwardLockup.ContainsKey(fromValueKey))
                            _quickForwardLockup.Add(fromValueKey, valueMapping.ToValue);

                        if (!_quickReverseLockup.ContainsKey(toValueKey))
                            _quickReverseLockup.Add(toValueKey, valueMapping.FromValue);
                    }
                }
            }

            string inputValueKey = inputValue;

            if (!CaseSensitive)
                inputValueKey = inputValue.ToLower();


            if (reverse)
            {
                if (_quickReverseLockup.ContainsKey(inputValueKey))
                    return _quickReverseLockup[inputValueKey];
            }
            else
            {
                if (_quickForwardLockup.ContainsKey(inputValueKey))
                    return _quickForwardLockup[inputValueKey];
            }

            return null;

        }
    }

    public class ValueMapping
    {
        [XmlAttribute("from")]
        public string FromValue { get; set; }

        [XmlAttribute("to")]
        public string ToValue { get; set; }
    }

}
