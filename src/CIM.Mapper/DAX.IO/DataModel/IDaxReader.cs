using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DAX.IO;

namespace DAX.IO
{
    public interface IDaxReader
    {
        void Open(string connectionStringOrUrl);
        void Open(DAXSelectionSet selectionSet);
        void Initialize(string dataSourceName, DataReaderWriterSpecification spec, List<ConfigParameter> parameters = null);
        void OpenDataSet(string dataSetName);
        void CloseDataSet(string dataSetName);
        void SetFilter(string filter);
        DAXFeature ReadFeature();
        void Reset();
        void Close();
        string DataSourceClassName();
        string DataSourceName();
        string JobName();
        List<KeyValuePair<string, string>> AdditionalInformation();
    }

    public class DAXSelectionSet
    {
        public List<DAXLayerSelection> LayerSelections = new List<DAXLayerSelection>();
    }

    public class DAXLayerSelection 
    {
        public string FeatureClassName { get; set; }
        public string Fields { get; set; }
        public List<string> IDs = new List<string>();
    }
}
