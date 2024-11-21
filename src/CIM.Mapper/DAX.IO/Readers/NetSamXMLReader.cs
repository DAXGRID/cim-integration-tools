using Newtonsoft.Json.Linq;
using System.Xml;

namespace DAX.IO.Readers
{
    public class NetSamXMLReader : IDaxReader
    {
        private string _equipmentFileName = null;

        private XmlReader _reader = null;

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
                throw new DAXReaderException("EquipmentFile parameter is mandatory");
            }

            _reader = XmlReader.Create(_equipmentFileName);
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
            DAXFeature feature = null;
            string attributName = null;
            string attributValue = null;

            while (_reader.Read())
            {
              

                // Xml element at depth 1 is cim objects
                if (_reader.Depth == 1 && _reader.NodeType == XmlNodeType.Element)
                {
                    feature = new DAXFeature()
                    {
                        ClassName = _reader.Name
                    };
                }
                if (_reader.Depth == 1 && _reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                // Xml element at depth 2 is cim object properties
                else if (_reader.Depth == 2 && _reader.NodeType == XmlNodeType.Element)
                {
                    attributName = _reader.Name;
                }
                else if (_reader.Depth == 3 && _reader.NodeType == XmlNodeType.Text)
                {
                    attributValue = _reader.Value;
                }
                else if (_reader.Depth == 2 && _reader.NodeType == XmlNodeType.EndElement)
                {
                    feature.Add(attributName, attributValue);
                }

            }

            return feature;
        }

        public void Reset()
        {
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
    }
}

