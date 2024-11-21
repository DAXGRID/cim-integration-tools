namespace DAX.IO.Readers
{
    public class MockupReader : IDaxReader
    {
        private int _numberOfFeatures = 1;
        private string _dataSourceName = null;

        public void Open(string connectionStringOrUrl)
        {
        }

        public void Open(DAXSelectionSet selectionSet = null)
        {
        }

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, List<ConfigParameter> parameters = null)
        {
            _dataSourceName = dataSourceName;
        }

        public void OpenDataSet(string dataSetName)
        {
        }

        public void CloseDataSet(string dataSetName)
        {
        }

        public void SetFilter(string filter)
        {
        }

        public DAXFeature ReadFeature()
        {
            if (_numberOfFeatures == 0)
                return null;

            DAXFeature feature = new DAXFeature() { GeometryType = DAXGeometryType.Line };
            feature.ClassName = "FiberMultiroer";
            feature.Coordinates = new DAXCoordinate[] { new DAXCoordinate("544209 6176783"), new DAXCoordinate("544219 6176793") };

            _numberOfFeatures = _numberOfFeatures - 1;
            return feature;
        }

        public void Reset()
        {
            _numberOfFeatures = 1;
        }

        public void Close()
        {
        }

        public string DataSourceClassName()
        {
            return "MockupReader";
        }

        public string DataSourceName()
        {
            return _dataSourceName;
        }

        public string JobName()
        {
            return "MockupReader";
        }

        public List<KeyValuePair<string, string>> AdditionalInformation()
        {
            return null;
        }
    }
}
