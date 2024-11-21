using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DAX.IO;

namespace DAX.IO
{
    public interface IDaxWriter
    {
        void Initialize(string dataSourceName, DataReaderWriterSpecification spec, TransformationConfig transConfig, List<ConfigParameter> parameters = null);
        void Open(string connectionStringOrUrl);
        void OpenDataSet(string dataSetName);
        void CloseDataSet(string dataSetName);
        void WriteFeature(DAXFeature feature, DataSetMappingGuide dsGuide = null);
        void Close();
        string DataSourceTypeName();
        string DataSourceName();
        string JobName();
        DAXMetaData GetMetaData();
        void Commit();
        string GetResult();
        ConfigParameter GetParameterByName(string name);
    }
        
}
