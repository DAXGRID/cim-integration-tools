using System.Xml;

namespace DAX.IO.Readers
{
    public class HexagonLandXMLReader : IDaxReader
    {
        private string _nameAndVersion = "HexagonLandXMLReader ver. 1.0 (19-09-2013)";
        private XmlElement _rootElement;
        private List<DAXFeature> _features = new List<DAXFeature>();
        private string _currentDataSetName = null;
        private List<DAXFeature> _currentDataSetCollection = null;
        private int _currentFeatureIndex = 0;
        private string _dataSourceName = null;
        private string _jobName = "Unknown";
        private List<KeyValuePair<string, string>> _additionalInformation = new List<KeyValuePair<string,string>>();

        public string DataSourceClassName()
        {
            return _nameAndVersion;
        }

        public string DataSourceName()
        {
            return _dataSourceName;
        }

        public string JobName()
        {
            return _jobName;
        }

        public List<KeyValuePair<string, string>> AdditionalInformation()
        {
            return _additionalInformation;
        }

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, List<ConfigParameter> parameters = null)
        {
            _dataSourceName = dataSourceName;
        }

        public void Open(string connectionStringOrUrlOrData)
        {
            XmlDocument doc = new XmlDocument();

            if (connectionStringOrUrlOrData.Contains("<LandXML"))
            {
                doc.LoadXml(connectionStringOrUrlOrData);
            }
            else
            {
                string xmlString = File.ReadAllText(connectionStringOrUrlOrData);
                doc.LoadXml(xmlString);
            }

            _rootElement = doc.DocumentElement;

            ReadJobData();
            ReadPointData();
        }

        public void Open(DAXSelectionSet selectionSet = null)
        {
        }


        public void OpenDataSet(string dataSetName)
        {
            string dataSetNameLower = dataSetName.ToLower();

            _currentDataSetName = dataSetName;
            _currentDataSetCollection = new List<DAXFeature>();

            foreach (DAXFeature feature in _features)
            {
                if (feature.CategoryName != null && feature.CategoryName.ToLower() == dataSetName)
                    _currentDataSetCollection.Add(feature);
            }
        }

        public void CloseDataSet(string dataSetName)
        {
            _currentDataSetName = null;
            _currentDataSetCollection = null;
        }

        public DAXFeature ReadFeature()
        {
            if (_currentDataSetCollection == null)
            {
                if (_currentFeatureIndex >= _features.Count)
                    return null;
                else
                {
                    _currentFeatureIndex++;
                    return _features[_currentFeatureIndex - 1];
                }
            }
            else
            {
                if (_currentFeatureIndex >= _currentDataSetCollection.Count)
                    return null;
                else
                {
                    _currentFeatureIndex++;
                    return _currentDataSetCollection[_currentFeatureIndex - 1];
                }
            }
        }

        public void SetFilter(string filter)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            _currentFeatureIndex = 0;
        }

        public void Close()
        {
        }

        private void ReadJobData()
        {
            _additionalInformation.Clear();

            var hexagonXMLElements = _rootElement.GetElementsByTagName("HexagonLandXML");

            if (hexagonXMLElements != null && hexagonXMLElements.Count > 0) 
            {
                XmlElement hexagonElement = hexagonXMLElements[0] as XmlElement;

                var surveyElements = hexagonElement.GetElementsByTagName("Survey");

                if (surveyElements != null && surveyElements.Count > 0)
                {
                    var surveyElement = surveyElements[0];

                    // Jobname
                    var attribute = surveyElement.Attributes["name"];
                    if (attribute != null) {
                        _jobName = attribute.Value;

                        // Fjern underscore til sidst i jobnavnet
                        if (_jobName.LastIndexOf('_') > 0)
                        {
                            int underscoreIndex = _jobName.LastIndexOf('_');
                            _jobName = _jobName.Substring(0, underscoreIndex);
                        }

                        _additionalInformation.Add(new KeyValuePair<string, string>("JobName", _jobName));
                    }

                    // Description 1
                    attribute = surveyElement.Attributes["Description1"];
                    if (attribute != null)
                    {
                        _additionalInformation.Add(new KeyValuePair<string, string>("Description1", attribute.Value));
                    }

                    // Description 2
                    attribute = surveyElement.Attributes["Description2"];
                    if (attribute != null)
                    {
                        _additionalInformation.Add(new KeyValuePair<string, string>("Description2", attribute.Value));
                    }

                    // Creator
                    attribute = surveyElement.Attributes["Creator"];
                    if (attribute != null)
                    {
                        _additionalInformation.Add(new KeyValuePair<string, string>("Creator", attribute.Value));
                    }
                }
            }
        }

        private void ReadPointData()
        {
            // Gather all LandXML points
            Dictionary<string, DAXCoordinate> pointCoordinates = new Dictionary<string, DAXCoordinate>();

            var cgPoints = _rootElement.GetElementsByTagName("CgPoint");

            foreach (XmlElement cgPoint in cgPoints)
            {
                DAXCoordinate coord = new DAXCoordinate(cgPoint.InnerText);
                coord.ID = cgPoint.Attributes["name"].Value;

                coord.TimeStamp = Convert.ToDateTime(cgPoint.Attributes["timeStamp"].Value);

                pointCoordinates.Add(coord.ID, coord);
            }

            // Find all hexagon points that represents features - e.g. have a PointCode
            var points = _rootElement.GetElementsByTagName("Point");

            foreach (XmlElement point in points)
            {
                string pointId = point.Attributes["uniqueID"].Value;

                var pointCode = point["PointCode"];
                if (pointCode != null)
                {
                    string code = pointCode.Attributes["code"].Value;
                    string codeGroup = pointCode.Attributes["codeGroup"].Value;

                    if (pointCoordinates.ContainsKey(pointId))
                    {
                        DAXFeature feature = new DAXFeature() { CategoryName = codeGroup, ClassName = code.ToLower() };
                        feature.Coordinates = new DAXCoordinate[] { pointCoordinates[pointId] };
                        feature.GeometryType = DAXGeometryType.Point;

                        // Find attributter
                        var attributes = point.GetElementsByTagName("Attribute");
                        foreach (XmlElement attribute in attributes)
                        {
                            string name = attribute.Attributes["name"].Value.ToLower();
                            string value = attribute.Attributes["value"].Value;

                            if (name != null && value != null)
                                feature[name] = value;
                        }

                        // GEV new leica instrument hack
                        // The GPS is returning points on all line codes.
                        // To ignore, we don't add point if there's no attributes
                        if (attributes.Count != 0)
                            _features.Add(feature);
                    }
                }

                // Get PointQuality if exists
                var pointQualitys = point.GetElementsByTagName("PointQuality");
                if (pointQualitys.Count == 1)
                {
                    var pointQuality = pointQualitys[0];

                    var CQPos = pointQuality.Attributes["CQPos"];

                    if (CQPos != null)
                    {
                        double cqPos = Convert.ToDouble(CQPos.Value.Replace('.', ','));
                        pointCoordinates[pointId].XYPrecision = cqPos;
                    }

                    var CQHeight = pointQuality.Attributes["CQHeight"];

                    if (CQHeight != null)
                    {
                        double cqHeight = Convert.ToDouble(CQHeight.Value.Replace('.', ','));
                        pointCoordinates[pointId].ZPrecision = cqHeight;
                    }

                }
            }

            // Find alle LandXML PlanFeatures that contains the coordinates
            Dictionary<string, List<DAXCoordinate>> lineCoordinates = new Dictionary<string, List<DAXCoordinate>>();

            XmlElement planFeaturesElement = _rootElement["PlanFeatures"];

            if (planFeaturesElement != null)
            {
                var planFeatures = planFeaturesElement.GetElementsByTagName("PlanFeature");

                foreach (XmlElement planFeature in planFeatures)
                {
                    string planFeatureName = planFeature.Attributes["name"].Value;

                    XmlElement coordGeom = planFeature["CoordGeom"];
                    if (coordGeom != null)
                    {
                        List<DAXCoordinate> coordinates = new List<DAXCoordinate>();

                        var lines = coordGeom.GetElementsByTagName("Line");
                        bool firstLine = true;
                        string lastCoordText = "";

                        foreach (XmlElement line in lines)
                        {
                            XmlElement start = line["Start"];
                            XmlElement end = line["End"];

                            if (start != null && end != null)
                            {
                                if (firstLine)
                                {
                                    
                                    coordinates.Add(pointCoordinates[start.GetAttribute("pntRef")]);
                                    coordinates.Add(pointCoordinates[end.GetAttribute("pntRef")]);
                                }
                                else
                                {
                                    if (end.InnerText != lastCoordText)
                                        coordinates.Add(pointCoordinates[end.GetAttribute("pntRef")]);
                                }

                                lastCoordText = end.InnerText;

                            }

                            firstLine = false;
                        }

                        if (coordinates.Count > 1)
                            lineCoordinates.Add(planFeatureName, coordinates);
                    }
                }
            }


            var allPlanFeatures = _rootElement.GetElementsByTagName("PlanFeature");

            foreach (XmlElement planFeature in allPlanFeatures)
            {
                // If lineCode child element exists, then it's a HeXML PlanFeature
                XmlElement lineCode = planFeature["LineCode"];
                if (lineCode != null)
                {
                    var planFeatureNameAttr = planFeature.Attributes["name"];
                    var lineCodeAttr = lineCode.Attributes["code"];
                    var codeGroupAttr = lineCode.Attributes["codeGroup"];

                    string planFeatureName = planFeatureNameAttr != null ? planFeatureNameAttr.Value : "No name";
                    string code = lineCodeAttr != null ? lineCodeAttr.Value : "No code";
                    
                    if (code == "")
                        code = "No code";

                    string codeGroup = codeGroupAttr != null ? codeGroupAttr.Value : "No code group";

                    DAXFeature feature = new DAXFeature() { CategoryName = codeGroup, ClassName = code.ToLower() };

                    if (lineCoordinates.ContainsKey(planFeatureName))
                    {
                        feature.Coordinates = lineCoordinates[planFeatureName].ToArray();
                        feature.GeometryType = DAXGeometryType.Line;

                        // Find attributter
                        var attributes = planFeature.GetElementsByTagName("Attribute");
                        foreach (XmlElement attribute in attributes)
                        {
                            string name = attribute.Attributes["name"].Value.ToLower();
                            string value = attribute.Attributes["value"].Value;

                            if (name != null && value != null)
                                feature[name] = value;
                        }

                        _features.Add(feature);
                    }
                    
                }
            }



        }
    }
}
