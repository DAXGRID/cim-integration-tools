namespace DAX.IO.Writers
{
    public class MockupWriter : IDaxWriter
    {
        public void Close()
        {
            throw new NotImplementedException();
        }

        public void CloseDataSet(string dataSetName)
        {
            throw new NotImplementedException();
        }

        public void Commit()
        {
            throw new NotImplementedException();
        }

        public string DataSourceName()
        {
            throw new NotImplementedException();
        }

        public string DataSourceTypeName()
        {
            throw new NotImplementedException();
        }

        public DAXMetaData GetMetaData()
        {
            throw new NotImplementedException();
        }

        public ConfigParameter GetParameterByName(string name)
        {
            throw new NotImplementedException();
        }

        public string GetResult()
        {
            throw new NotImplementedException();
        }

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, TransformationConfig transConfig, List<ConfigParameter> parameters = null)
        {
            throw new NotImplementedException();
        }

        public string JobName()
        {
            throw new NotImplementedException();
        }

        public void Open(string connectionStringOrUrl)
        {
            throw new NotImplementedException();
        }

        public void OpenDataSet(string dataSetName)
        {
            throw new NotImplementedException();
        }

        public void WriteFeature(DAXFeature feature, DataSetMappingGuide dsGuide = null)
        {
            throw new NotImplementedException();
        }
    }

}
