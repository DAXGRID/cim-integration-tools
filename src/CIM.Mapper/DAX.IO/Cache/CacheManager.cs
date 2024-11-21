using DAX.IO.Writers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.Cache
{
    /// <summary>
    /// Util to write datasets in geojson format to cache folder
    /// </summary>
    public class CacheManager
    {
        string _cacheFolder = null;
        GeoJsonWriter _geojsonWriter = null;

        string _dataSetName = null;
        string[] _lines = null;
        long _lineReadIndex = 0;

        public CacheManager(string cacheFolder)
        {
            _cacheFolder = cacheFolder;
        }

        public string Folder { get { return _cacheFolder; } }

        public void BeginWriteDataSet(string name)
        {
            _geojsonWriter = new DAX.IO.Writers.GeoJsonWriter();

            List<ConfigParameter> paramerters = new List<ConfigParameter>();
            paramerters.Add(new ConfigParameter() { Name = "OutputFolder", Value = _cacheFolder });
            paramerters.Add(new ConfigParameter() { Name = "Ext", Value = ".geojson" });

            _geojsonWriter.Initialize(null, null, null, paramerters);
        }

        public void WriteFeature(DAXFeature feature)
        {
            _geojsonWriter.WriteFeature(feature);
        }

        public void EndWriteDataSet()
        {
            _geojsonWriter.Commit();
        }

        public void BeginReadDataSet(string name)
        {
            string fileName = _cacheFolder + "/" + name + ".geojson";

            _dataSetName = name;

            if (File.Exists(fileName))
            {
                _lines = File.ReadAllLines(fileName);
                _lineReadIndex = 1; // jump over collection header
            }
            else
            {
                _lines = null;
            }
        }

        public DAXFeature ReadFeature()
        {
            if (_lines == null)
                return null;

            if (_lineReadIndex < (_lines.Length - 1))
            {
                DAXFeature feature = new DAXFeature() { GeometryType = DAXGeometryType.NoGemoetry, ClassName = _dataSetName };

                var line = _lines[_lineReadIndex];

                if (line.StartsWith(","))
                    line = line.Substring(1, line.Length -1);

                _lineReadIndex++;
            

                JObject jFeat = JObject.Parse(line);

                var jProps = jFeat["properties"] as JObject;

                foreach (var prop in jProps)
                {
                    string name = prop.Key;
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

                        coords.Add(new DAXCoordinate() { X = x, Y = y });
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

                            coords.Add(new DAXCoordinate() { X = x, Y = y });
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

                            coords.Add(new DAXCoordinate() { X = x, Y = y });
                        }
                    }


                    feature.Coordinates = coords.ToArray();
                    

               }



                // Put the attributes in

                return feature;

            }
            else
                return null;
        }

        public void EndReadDataSet()
        {
        }
    }
}
