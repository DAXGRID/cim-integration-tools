using DAX.IO.Cache;
using DAX.Util;
using Newtonsoft.Json.Linq;

namespace DAX.IO.Readers
{
    public class GeoJsonReader : IDaxReader
    {
        private string _folderName;
        private List<JsonDataset> _jsonDatasets = null;

        int _datasetReadIndex = 0;
        int _lineReadIndex = 0;
        

        public void Open(string data)
        {
            _jsonDatasets = new List<JsonDataset> {
                new JsonDataset()
                {
                    DatasetName = "data",
                    Features = JObject.Parse(data)["features"] as JArray
                }
            };
        }

        public void Open(DAXSelectionSet selectionSet = null)
        {
        }

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, List<ConfigParameter> parameters = null)
        {
            foreach (ConfigParameter configParam in parameters)
            {
                if (configParam.Name.ToLower() == "foldername")
                {
                    _folderName = configParam.Value;
                }
            }

            if (_folderName == null)
            {
                throw new DAXReaderException("FolderName parameter is mandatory");
            }

            _jsonDatasets = new List<JsonDataset> { };

            // Read all data from all folders
            var jsonFileNames = Directory.GetFiles(_folderName);

            foreach (var jsonFileName in jsonFileNames)
            {
                if (jsonFileName.ToLower().Contains(".geojson"))
                {
                    var fileText = File.ReadAllText(jsonFileName);

                    _jsonDatasets.Add(
                        new JsonDataset()
                        {
                            DatasetName = GetDataSetName(Path.GetFileName(jsonFileName)),
                            Features = JObject.Parse(fileText)["features"] as JArray
                        }
                    );
                }
                else
                {
                    Logger.Log(LogLevel.Info, $"GeoJsonReader reader: Skipping file: '{jsonFileName}' because it has no .geojson extension");
                }
            }
        }

        private string GetDataSetName(string fileName)
        {
            if (fileName.Contains("."))
            {
                return fileName.Split('.')[0];
            }
            else
            {
                return fileName;
            }
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
            if (_jsonDatasets == null)
                return null;


            if (_datasetReadIndex < (_jsonDatasets.Count))
            {
                var jsonDataset = _jsonDatasets[_datasetReadIndex];

                

                // Change dataset if end of features
                if (_lineReadIndex >= jsonDataset.Features.Count)
                {
                    _datasetReadIndex++;

                    // If more datasets to process 
                    if (_datasetReadIndex < (_jsonDatasets.Count - 1))
                    {
                        jsonDataset = _jsonDatasets[_datasetReadIndex];
                        _lineReadIndex = 0;
                    }
                    // No more datasets to processs
                    else
                    {
                        return null;
                    }
                }

                if (_lineReadIndex == 0)
                {
                    Logger.Log(LogLevel.Info, "GeoJsonReader processing: '" + jsonDataset.DatasetName + "' in folder: '" + _folderName + "'");

                }

                JObject jFeat = jsonDataset.Features[_lineReadIndex] as JObject;

                _lineReadIndex++;

                var jProps = jFeat["properties"] as JObject;


                DAXFeature feature = new DAXFeature() { GeometryType = DAXGeometryType.NoGemoetry, ClassName = jsonDataset.DatasetName };


                foreach (var prop in jProps)
                {
                    string name = prop.Key.ToLower();
                    JToken value = prop.Value;
                    feature.Add(name, value);
                }

                var jGeo = jFeat["geometry"];
                if (jGeo != null)
                {
                    List<DAXCoordinate> coords = new List<DAXCoordinate>();

                    var geoType = jGeo["type"].ToString();

                    if (geoType == "Point")
                    {
                        feature.GeometryType = DAXGeometryType.Point;

                        JArray coordinates = jGeo["coordinates"] as JArray;

                        JToken jx = ((JArray)coordinates)[0] as JToken;
                        JToken jy = ((JArray)coordinates)[1] as JToken;


                        double x = Convert.ToDouble(jx);
                        double y = Convert.ToDouble(jy);

                        var coord = new DAXCoordinate() { X = x, Y = y };

                        if (((JArray)coordinates).Count == 3)
                        {
                            JToken jz = ((JArray)coordinates)[2] as JToken;
                            coord.Z = Convert.ToDouble(jz);
                        }

                        coords.Add(coord);
                    }

                    else if (geoType == "LineString")
                    {
                        feature.GeometryType = DAXGeometryType.Line;

                        JArray coordinates = jGeo["coordinates"] as JArray;

                        foreach (var coordPairArray in coordinates)
                        {
                            JToken jx = ((JArray)coordPairArray)[0] as JToken;
                            JToken jy = ((JArray)coordPairArray)[1] as JToken;

                            double x = Convert.ToDouble(jx);
                            double y = Convert.ToDouble(jy);

                            var coord = new DAXCoordinate() { X = x, Y = y };

                            if (((JArray)coordPairArray).Count == 3)
                            {
                                JToken jz = ((JArray)coordPairArray)[2] as JToken;
                                coord.Z = Convert.ToDouble(jz);
                            }

                            coords.Add(coord);
                        }
                    }
                    else if (geoType == "Polygon")
                    {
                        feature.GeometryType = DAXGeometryType.Polygon;

                        JArray coordinates = jGeo["coordinates"] as JArray;

                        coordinates = coordinates[0] as JArray;

                        foreach (var coordPairArray in coordinates)
                        {
                            JToken jx = ((JArray)coordPairArray)[0] as JToken;
                            JToken jy = ((JArray)coordPairArray)[1] as JToken;

                            double x = Convert.ToDouble(jx);
                            double y = Convert.ToDouble(jy);

                            var coord = new DAXCoordinate() { X = x, Y = y };

                            if (((JArray)coordPairArray).Count == 3)
                            {
                                JToken jz = ((JArray)coordPairArray)[2] as JToken;
                                coord.Z = Convert.ToDouble(jz);
                            }

                            coords.Add(coord);
                        }
                    }

                    feature.Coordinates = coords.ToArray();
                }

                return feature;
            }
            else
                return null;
        }

        public void Reset()
        {
            _datasetReadIndex = 0;
            _lineReadIndex = 0;
        }

        public void Close()
        {
            _jsonDatasets.Clear();
        }

        public string DataSourceClassName()
        {
            return "JsonReader";
        }

        public string DataSourceName()
        {
            return "JsonReader";
        }

        public string JobName()
        {
            return "JsonReader";
        }

        public List<KeyValuePair<string, string>> AdditionalInformation()
        {
            return null;
        }

        private class JsonDataset
        {
            public string DatasetName { get; set; }
            public JArray Features { get; set; }
        }

    }

}

