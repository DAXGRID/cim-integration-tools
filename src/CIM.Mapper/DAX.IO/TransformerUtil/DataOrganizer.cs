using DAX.IO;
using DAX.Util;

namespace DAX.TransformerUtil
{
    public class DataOrganizer
    {
        private IDaxReader _data;
        private Dictionary<string, DAXDataSet> _dataSets = new Dictionary<string, DAXDataSet>();


        public DataOrganizer(IDaxReader dataReader)
        {
            _data = dataReader;

             OrganizeData();
        }
            
        private void OrganizeData(string dataSetName = null)
        {
            _data.Reset();

            if (dataSetName != null)
                _data.OpenDataSet(dataSetName);

            DAXFeature feature = _data.ReadFeature();

            while (feature != null)
            {
                string classNameLower = feature.ClassName.ToLower();
                if (_dataSets.ContainsKey(classNameLower))
                {
                    _dataSets[classNameLower].Features.Add(feature);
                }
                else
                {
                    _dataSets[classNameLower] = new DAXDataSet() { Name = feature.ClassName, CategoryName = feature.CategoryName };
                    _dataSets[classNameLower].Features.Add(feature);
                }


                feature = _data.ReadFeature();
            }
        }

        public DAXDataSet[] GetDataSetsOrderedByCategoryAndClassName()
        {
            return _dataSets.Values.OrderBy(x => (x.CategoryName == null ? "" : x.CategoryName + ".") + x.Name).ToArray();
        }

        



        private Thread StartWorkerThread(ReaderWorker wi)
        {
            var newThread = new Thread(wi.DoWork);
            newThread.Name = "DAXReader dataset: " + wi._dataSetName;
            newThread.Start("hej");

            wi._thread = newThread;

            return newThread;
        }

    }

    public class ReaderWorker
    {
        public Dictionary<string, DAXDataSet> _dataSets = new Dictionary<string, DAXDataSet>();
        public IDaxReader _dataReader;
        public string _dataSetName;
        public Thread _thread;

        public void DoWork(object data)
        {
            Logger.Log(LogLevel.Info, "Starting parallel reader worker for dataset: " + _dataSetName + "...");
            _dataReader.Reset();

            if (_dataSetName != null)
                _dataReader.OpenDataSet(_dataSetName);

            DAXFeature feature = _dataReader.ReadFeature();

            while (feature != null)
            {
                string classNameLower = feature.ClassName.ToLower();
                if (_dataSets.ContainsKey(classNameLower))
                {
                    _dataSets[classNameLower].Features.Add(feature);
                }
                else
                {
                    _dataSets[classNameLower] = new DAXDataSet() { Name = feature.ClassName, CategoryName = feature.CategoryName };
                    _dataSets[classNameLower].Features.Add(feature);
                }


                feature = _dataReader.ReadFeature();
            }
        }

    }
}
