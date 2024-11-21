using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;

namespace DAX.IO.Writers
{
    public class AGSWriter : IDaxWriter
    {
        private string _mapServiceUrl = null;

        private string _dataSourceName = null;

        public void Initialize(string dataSourceName,  DataReaderWriterSpecification spec, TransformationConfig config, List<ConfigParameter> parameters = null)
        {
            _dataSourceName = dataSourceName;
        }

        public void Open(string connectionStringOrUrl)
        {
            _mapServiceUrl = connectionStringOrUrl;
        }

        public ConfigParameter GetParameterByName(string name)
        {
            return null;
        }


        public void OpenDataSet(string dataSetName)
        {
        }

        public void CloseDataSet(string dataSetName)
        {
        }

        public void WriteFeature(DAXFeature daxFeature, DataSetMappingGuide dsGuide = null)
        {
            DAXMetaData metaData = GetMetaData();
            DAXClassDef fcDef = metaData.GetFeatureClassDefinition(daxFeature.ClassName);
            List<KeyValuePair<string, string>> outAttributes = new List<KeyValuePair<string, string>>();

            if (daxFeature.Coordinates == null && daxFeature.Coordinates.Length < 1)
            {
                throw new Exception("Fejl: Featuren indeholder ingen koordinater.\r\n" + daxFeature.GetStringDetailed());
            }

            if (fcDef != null)
            {
                string jsonGeometry = "";

                if (daxFeature.GeometryType == DAXGeometryType.Point)
                    jsonGeometry = "\"geometry\" : {\"x\" : " + daxFeature.Coordinates[0].X +  ", \"y\" : " + daxFeature.Coordinates[0].Y + "}";

                // Add attributes
                foreach (KeyValuePair<string, object> attribute in daxFeature) 
                {
                    DAXAttributeDef attrDef = metaData.GetFeatureAttributeDefinition(daxFeature.ClassName, attribute.Key);

                    if (attrDef != null)
                        outAttributes.Add(new KeyValuePair<string,string>(attrDef.Name, attribute.Value.ToString()));
                }

                AddFeatures(0, new List<DAXFeature>() { daxFeature });
            }
            else
                throw new Exception("Fejl: Kan ikke finde featureklassen: " + daxFeature.ClassName + ".\r\nHusk at inkludere alle featureklasser i MXD'en, som du ønskes at importere data til, og som er specificeret i mapningsfilen.");
        }

        public void Close()
        {
        }

        public string DataSourceTypeName()
        {
            return "AGSWriter";
        }

        public string DataSourceName()
        {
            return _mapServiceUrl;
        }

        public string JobName()
        {
            return "AGSWriter";
        }

        public DAXMetaData GetMetaData()
        {
            DAXMetaData metaData = new DAXMetaData();

            var cli = new WebClient();
            string content = cli.DownloadString(_mapServiceUrl + "/MapServer/layers?f=json");

            JObject result = JsonConvert.DeserializeObject<dynamic>(content) as JObject;

            JArray layers = result.GetValue("layers") as JArray;

            foreach (JObject layer in layers) 
            {
                string classId = layer.GetValue("id").ToString();
                string className = layer.GetValue("name").ToString();
                string classGeometryType = layer.GetValue("geometryType").ToString();

                DAXClassDef fcDef = metaData.AddOrGetFeatureClassDefinition(null, className, null);
                fcDef.Id = Convert.ToInt32(classId);

                fcDef.ClassType = DAXClassType.Unknown;

                if (classGeometryType == "esriGeometryPoint")
                    fcDef.ClassType = DAXClassType.Point;
                else if (classGeometryType == "esriGeometryLine")
                    fcDef.ClassType = DAXClassType.Line;
                else if (classGeometryType == "esriGeometryPolygon")
                    fcDef.ClassType = DAXClassType.Polygon;

                JArray fields = layer.GetValue("fields") as JArray;

                foreach (JObject field in fields) 
                {
                    string fieldName = field.GetValue("name").ToString();
                    string fieldType = field.GetValue("type").ToString();
                    string fieldAlias = field.GetValue("alias").ToString();
                    var fieldLength = field.GetValue("length");

                    DAXAttributeDef attrDef = metaData.AddOrGetAttributeDefinition(className, fieldName, fieldAlias);

                    if (fieldLength != null)
                        attrDef.Length = Convert.ToInt32(fieldLength.ToString());

                    if (fieldType == "esriFieldTypeOID" || fieldType =="esriFieldTypeInteger")
                        attrDef.AttributeType = DAXAttributeType.Int32;
                    else if (fieldType == "esriFieldTypeString")
                        attrDef.AttributeType = DAXAttributeType.String;
                    else if (fieldType == "esriFieldTypeDate")
                        attrDef.AttributeType = DAXAttributeType.DateTime;
                    else if (fieldType == "esriFieldTypeSmallInteger")
                        attrDef.AttributeType = DAXAttributeType.Int16;
                    else if (fieldType == "esriFieldTypeSingle")
                        attrDef.AttributeType = DAXAttributeType.Single;
                    else if (fieldType == "esriFieldTypeDouble")
                        attrDef.AttributeType = DAXAttributeType.Double;
                    else
                        attrDef.AttributeType = DAXAttributeType.Unknown;
                }
            }

            return metaData;
        }

        public void Commit()
        {
        }

        public void AddFeatures(int layerId, List<DAXFeature> daxFeatures)
        {
            string param = "features=[";

            bool firstFeature = true;
            foreach (DAXFeature daxFeature in daxFeatures)
            {
                if (!firstFeature)
                    param += ",";

                param += CreateFeatureJsonString(daxFeature);

                firstFeature = false;
            }

            param += "]&f=json";

            string url = _mapServiceUrl + "/FeatureServer/" + layerId + "/addFeatures";
            string result = POST(url, param);

            if (result == null)
            {
                throw new Exception("Error calling: '" + url + "' - got null result from ArcGIS Server!");
            }

            if (result.Contains("error"))
            {
                throw new Exception(GetErrorText(result));
            }
        }

        private string CreateFeatureJsonString(DAXFeature daxFeature) 
        {
            string jsonGeometry = "";

            if (daxFeature.GeometryType == DAXGeometryType.Point)
                jsonGeometry = "\"geometry\" : {\"x\" : " + daxFeature.Coordinates[0].X + ", \"y\" : " + daxFeature.Coordinates[0].Y + "}";

            string jsonString = "{" + jsonGeometry + ",\"attributes\":{";

            bool first = true;

                foreach (KeyValuePair<string, object> fieldValue in daxFeature)
                {
                    if (!first)
                        jsonString += ",";
                    else
                        first = false;

                    jsonString += "\"" + System.Web.HttpUtility.UrlEncode(fieldValue.Key) + "\":\"" + System.Web.HttpUtility.UrlEncode(fieldValue.Value.ToString()) + "\"";
                }

            jsonString += "}}";

            return jsonString;
        }


        private string GetErrorText(string result)
        {
            string errorText = "";
            JObject json = JsonConvert.DeserializeObject<dynamic>(result) as JObject;

            JObject error = json.GetValue("error") as JObject;

            // If no error in root, try addresult
            if (error == null)
            {
                JObject addResult = json.GetValue("addResult") as JObject;
                if (addResult != null)
                    error = addResult.GetValue("error") as JObject;
            }

            if (error == null)
            {
                return result;
            }

            string code = error.GetValue("code").ToString();
            var message = error.GetValue("message");
            var details = error.GetValue("details");

            errorText += code;

            if (message != null)
                errorText += " " + message.ToString();
            if (details != null)
                errorText += " " + details.ToString();

            return errorText;
        }

        public string POST(string url, string postData)
        {
            // Create request
            WebRequest request = WebRequest.Create(url);
            request.Method = "POST";
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            // Get the response.
            WebResponse response = request.GetResponse();
            //Console.WriteLine(((HttpWebResponse)response).StatusDescription);
            dataStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();

            // Clean up the streams.
            reader.Close();
            dataStream.Close();
            response.Close();

            return responseFromServer;
        }

        public string GetResult() { return null; }
    }
}
