using DAX.IO.Geometry;
using DAX.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;

namespace DAX.IO.Writers
{
    public class GeoJsonWriter : IDaxWriter
    {
        private string _versionInfo = "0.6 (03-06-2018)";
        private string _dataSourceName = null;
        private string _fileExt = null;
        private string _outputFolder = null;
        private string _outputFile = null;
        private string _outputResult = null;
        private bool _includeLayerProperty = false;
        private bool _insludeZ = true;

        private string _sourceCS = null;
        private string _targetCS = null;

        private CoordinateConverter _coordConverter = null;

        private Dictionary<string, List<DAXFeature>> _features = new Dictionary<string, List<DAXFeature>>();

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, TransformationConfig config, List<ConfigParameter> parameters = null)
        {
            _dataSourceName = dataSourceName;

            foreach (var param in parameters)
            {
                if (param.Name.ToLower() == "outputfolder")
                {
                    _outputFolder = param.Value;
                    Logger.Log(LogLevel.Info, "GeoJson output folder: " + _outputFolder);
                }

                if (param.Name.ToLower() == "outputfile")
                {
                    _outputFile = param.Value;
                    Logger.Log(LogLevel.Info, "GeoJson output file: " + _outputFile);
                }


                if (param.Name.ToLower() == "ext")
                {
                    _fileExt = param.Value;
                }

                if (param.Name.ToLower() == "sourcecs" && param.InlineValue != null)
                {
                    _sourceCS = param.InlineValue.Trim();
                    Logger.Log(LogLevel.Info, "Source CS: " + _sourceCS);
                }

                if (param.Name.ToLower() == "targetcs" && param.InlineValue != null)
                {
                    _targetCS = param.InlineValue.Trim();
                    Logger.Log(LogLevel.Info, "Target CS: " + _targetCS);
                }

                if (param.Name.ToLower() == "includelayerproperty" && (param.Value.ToLower() == "yes" || param.Value.ToLower() == "true"))
                {
                    _includeLayerProperty = true;
                }
            }
        }

        public void Open(string connectionStringOrUrl)
        {
            System.Diagnostics.Debug.WriteLine("Open called: " + connectionStringOrUrl);
        }

        public ConfigParameter GetParameterByName(string name)
        {
            return null;
        }

        public void OpenDataSet(string dataSetName)
        {
            System.Diagnostics.Debug.WriteLine("OpenDataSet called: " + dataSetName);
        }

        public void CloseDataSet(string dataSetName)
        {
            System.Diagnostics.Debug.WriteLine("CloseDataSet called: " + dataSetName);
        }

        public void WriteFeature(DAXFeature feature, DataSetMappingGuide dsGuide = null)
        {
            if (!_features.ContainsKey(feature.ClassName))
                _features.Add(feature.ClassName, new List<DAXFeature>());

            var featureList = _features[feature.ClassName];

            featureList.Add(feature);
        }

        public void Close()
        {
            System.Diagnostics.Debug.WriteLine("Close called");
        }

        public string DataSourceTypeName()
        {
            return "JsonDataSource " + _versionInfo;
        }

        public string DataSourceName()
        {
            return _dataSourceName;
        }

        public string JobName()
        {
            return "GeoJsonExport";
        }

        public void Commit()
        {

            // If output file set, then write all features in alle datasets to one file
            if (_outputFile != null)
            {
                string fileName = _outputFile;

                string fullFileName = fileName;

                if (_outputFolder != null)
                    fullFileName = _outputFolder + "\\" + fileName;

                if (_fileExt != null)
                    fullFileName += _fileExt;

                StreamWriter geoJsonFile = new StreamWriter(fullFileName, false, Encoding.UTF8);

                // Start geojson feature collection
                geoJsonFile.WriteLine("{ \"type\": \"FeatureCollection\", \"features\": [");

                dynamic geoJsonFeatureCollection = new JObject();

                geoJsonFeatureCollection.type = "FeatureCollection";

                bool first = true;

                foreach (var dataset in _features)
                {

                    foreach (var feature in dataset.Value)
                    {
                        if (!first)
                            geoJsonFile.Write(",");

                        string json = CreateFeatureJsonObject(feature).ToString(Formatting.None) + "\r\n";

                        geoJsonFile.Write(json);

                        if (first)
                            first = false;
                    }

                }

                // End geojson feature collection
                geoJsonFile.WriteLine(" ] }");

                geoJsonFile.Close();

            }
            // If only output folder set, then write one file per dataset
            else if (_outputFolder != null)
            {
                foreach (var dataset in _features)
                {
                    string fileName = dataset.Key;

                    string fullFileName = fileName;

                    if (_outputFolder != null)
                        fullFileName = _outputFolder + "\\" + fileName;

                    if (_fileExt != null)
                        fullFileName += _fileExt;

                    StreamWriter geoJsonFile = new StreamWriter(fullFileName, false, Encoding.UTF8);

                    // Start geojson feature collection
                    geoJsonFile.WriteLine("{ \"type\": \"FeatureCollection\", \"features\": [");

                    dynamic geoJsonFeatureCollection = new JObject();

                    geoJsonFeatureCollection.type = "FeatureCollection";

                    bool first = true;

                    foreach (var feature in dataset.Value)
                    {
                        if (!first)
                            geoJsonFile.Write(",");

                        string json = CreateFeatureJsonObject(feature).ToString(Formatting.None) + "\r\n";

                        geoJsonFile.Write(json);

                        if (first)
                            first = false;
                    }

                    // End geojson feature collection
                    geoJsonFile.WriteLine(" ] }");

                    geoJsonFile.Close();
                }
            }
            // Just write to outputResult in memory string
            else
            {
                StringBuilder stringBuilder = new StringBuilder();
                StringWriter geoJsonText = new StringWriter(stringBuilder);

                // Start geojson feature collection
                geoJsonText.WriteLine("{ \"type\": \"FeatureCollection\", \"features\": [");

                dynamic geoJsonFeatureCollection = new JObject();

                geoJsonFeatureCollection.type = "FeatureCollection";

                bool first = true;

                foreach (var dataset in _features)
                {

                    foreach (var feature in dataset.Value)
                    {
                        if (!first)
                            geoJsonText.Write(",");

                        string json = CreateFeatureJsonObject(feature).ToString(Formatting.None) + "\r\n";

                        geoJsonText.Write(json);

                        if (first)
                            first = false;
                    }

                }

                // End geojson feature collection
                geoJsonText.WriteLine(" ] }");

                geoJsonText.Close();

                _outputResult = stringBuilder.ToString();
            }
        }


        private JObject CreateFeatureJsonObject(DAXFeature feature)
        {
            dynamic jsonFeature = new JObject();
            jsonFeature.type = "Feature";
            
            if (feature.GeometryType != DAXGeometryType.Unknown && feature.GeometryType != DAXGeometryType.NoGemoetry)
            {
                jsonFeature.geometry = CreateGeometryJsonObject(feature);
            }

            if (_includeLayerProperty)
                feature.Add("layer", feature.ClassName);

            if (feature.Count > 0)
            {
                jsonFeature.properties = CreatePropertiesJsonObject(feature);
            }

            return jsonFeature;
        }
        
        private JObject CreateGeometryJsonObject(DAXFeature feature)
        {
            dynamic geometry = new JObject();
            
            if (feature.GeometryType == DAXGeometryType.Point)
                geometry.type = "Point";
            else if (feature.GeometryType == DAXGeometryType.Line)
                geometry.type = "LineString";
            else if (feature.GeometryType == DAXGeometryType.Polygon)
                geometry.type = "Polygon";

            if (feature.GeometryType == DAXGeometryType.Point || feature.GeometryType == DAXGeometryType.Line || feature.GeometryType == DAXGeometryType.Polygon)
            {
                JArray pointsArray = new JArray();

                if (feature.GeometryType == DAXGeometryType.Line)
                {
                    foreach (var coord in feature.Coordinates)
                    {
                        JArray coordsArray = new JArray();

                        var newCoord = TransformCoordinate(coord);

                        coordsArray.Add(new JValue(newCoord.X));
                        coordsArray.Add(new JValue(newCoord.Y));

                        if (_insludeZ)
                            coordsArray.Add(new JValue(newCoord.Z));

                        pointsArray.Add(coordsArray);
                    }
                }
                if (feature.GeometryType == DAXGeometryType.Polygon)
                {
                    JArray polyArray = new JArray();

                    foreach (var coord in feature.Coordinates)
                    {
                        JArray coordsArray = new JArray();

                        var newCoord = TransformCoordinate(coord);

                        coordsArray.Add(new JValue(newCoord.X));
                        coordsArray.Add(new JValue(newCoord.Y));

                        if (_insludeZ)
                            coordsArray.Add(new JValue(newCoord.Z));

                        polyArray.Add(coordsArray);
                    }

                    pointsArray.Add(polyArray);
                }
                else if (feature.Coordinates.Length == 1)
                {
                    var newCoord = TransformCoordinate(feature.Coordinates[0]);
                    pointsArray.Add(new JValue(newCoord.X));
                    pointsArray.Add(new JValue(newCoord.Y));

                    if (_insludeZ)
                        pointsArray.Add(new JValue(newCoord.Z));
                }

                geometry.coordinates = pointsArray;
            }
            return geometry;
        }

        private JObject CreatePropertiesJsonObject(DAXFeature feature)
        {
            JObject jsonProperties = new JObject();

            jsonProperties.Add(new JProperty("DatasetName", feature.ClassName));

            foreach (var attr in feature)
            {
                if (!attr.Key.StartsWith("{"))
                    jsonProperties.Add(new JProperty(attr.Key, attr.Value));
            }

            return jsonProperties;
        }

        private string ConvertCoordToText(double coord)
        {
            return coord.ToString(CultureInfo.InvariantCulture);
        }

        private DAXCoordinate TransformCoordinate(DAXCoordinate coord)
        {
            if (_sourceCS != null && _targetCS != null)
            {
                if (_coordConverter == null)
                    _coordConverter = new CoordinateConverter(_sourceCS, _targetCS);

                var convCoord = _coordConverter.Convert(coord.X, coord.Y);

                return new DAXCoordinate() { ID = coord.ID, X = convCoord[0], Y = convCoord[1] };
            }

            return coord;
        }

        public DAXMetaData GetMetaData()
        {
            System.Diagnostics.Debug.WriteLine("GetMetaData called");

            DAXMetaData metaData = new DAXMetaData();
            metaData.CanHandleAllOutputTypes = true;
            metaData.CanHandleAllAttributes = true;
        
            return metaData;
        }

        public string GetResult() { return _outputResult; }
    }
}
