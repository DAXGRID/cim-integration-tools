namespace DAX.IO.Writers
{
    public class HexagonLandXMLWriter : IDaxWriter
    {
        private string _nameAndVersion = "HexagonLandXMLWriter ver. 1.0 (19-09-2013)";
        private List<ConfigParameter> _parameters = null;

        private List<DAXFeature> _features = new List<DAXFeature>();
        private string _dataSourceName = null;

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, TransformationConfig config, List<ConfigParameter> parameters = null) 
        {
            _dataSourceName = dataSourceName;
            _parameters = parameters;
        }

        public void Open(string connectionStringOrUrl)
        {
        }

        public ConfigParameter GetParameterByName(string name)
        {
            if (_parameters != null)
            {
                foreach (var param in _parameters)
                {
                    if (name.ToLower() == param.Name.ToLower())
                        return param;
                }
            }

            return null;
        }

        public void OpenDataSet(string dataSetName)
        {
        }

        public void CloseDataSet(string dataSetName)
        {
        }

        public void WriteFeature(DAXFeature feature, DataSetMappingGuide dsGuide = null)
        {
            _features.Add(feature);
        }

        public void Close()
        {
            _features.Clear();
        }

        public string DataSourceTypeName()
        {
            return _nameAndVersion;
        }

        public string DataSourceName()
        {
            return _dataSourceName;
        }

        public string JobName()
        {
            return "HexagonWriter";
        }

        public DAXMetaData GetMetaData()
        {
            DAXMetaData metaData = new DAXMetaData();
            metaData.CanHandleAllOutputTypes = true;
            return metaData;
        }

        public void Commit()
        {
        }

        public string GetResult()
        {
            string xml = "";
            xml += "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n";
            xml += "<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.landxml.org/schema/LandXML-1.2 http://www.landxml.org/schema/LandXML-1.2/LandXML-1.2.xsd\" version=\"1.2\" date=\"2012-09-18\" time=\"19:56:37\" readOnly=\"false\" language=\"English\">\r\n";
            xml += "  <Units>\r\n";
            xml += "        <Metric linearUnit=\"meter\" areaUnit=\"squareMeter\" volumeUnit=\"cubicMeter\" angularUnit=\"decimal dd.mm.ss\" latLongAngularUnit=\"decimal degrees\" temperatureUnit=\"celsius\" pressureUnit=\"milliBars\"/>\r\n";
            xml += "  </Units>\r\n";
            xml += "  <CoordinateSystem name=\"UTM32JY\" horizontalDatum=\"Local\" verticalDatum=\"Local\" ellipsoidName=\"WGS 1984\" projectedCoordinateSystemName=\"UTM32\" fileLocation=\"\"> </CoordinateSystem>\r\n";
            xml += "  <Application name=\"DAX LandXML Export\" manufacturer=\"DAX\" version=\"0.1\" manufacturerURL=\"www.dax.dk\">\r\n";
            xml += "    <Author company=\"DAX\" companyURL=\"www.dax.dk\" timeStamp=\"" + DateTime.Now.ToString() + "\"/>";
            xml += "  </Application>\r\n";

            // CgPoints
            xml += "  <CgPoints>\r\n";

            int pointCounter = 1;

            // Knudepunkter
            foreach (DAXFeature feature in _features)
            {
                if (feature.Coordinates != null && feature.Coordinates.Length == 1 && feature.GeometryType == DAXGeometryType.Point)
                {
                    // code
                    string codeName = feature.ClassName;
                    string[] classNameSplit = feature.ClassName.Split('.');
                    if (classNameSplit.Length > 1)
                        codeName = classNameSplit[classNameSplit.Length - 1];

                    // name
                    string fcName = feature.ClassName != null ? feature.ClassName : "Ukendt";

                    if (fcName.Length > 28)
                        fcName = fcName.Substring(0, 28);

                    fcName += "" + pointCounter;


                    xml += "    <CgPoint name=\"" + fcName + "\" timeStamp=\"" + DateTime.Now.ToString() + "\" oID=\"PNT" + pointCounter + "\" code=\""  + codeName + "\">" + feature.Coordinates[0].ToString() + "</CgPoint>\r\n";
                    pointCounter++;
                }
            }

            // Linje punkter
            int lineCounter = 1;
            int linePointCounter = 1;
            foreach (DAXFeature feature in _features)
            {
                linePointCounter = 1;
                if (feature.GeometryType == DAXGeometryType.Line && feature.Coordinates != null && feature.Coordinates.Length > 1)
                {
                    foreach (DAXCoordinate point in feature.Coordinates)
                    {
                        string lineId = "L" + lineCounter + "P" + linePointCounter;

                        xml += "    <CgPoint name=\"" + lineId + "\" timeStamp=\"" + DateTime.Now.ToString() + "\" oID=\"" + lineId + "\">" + point.ToString() + "</CgPoint>\r\n";
                        linePointCounter++;
                    }

                    lineCounter++;
                }
                
            }
            xml += "  </CgPoints>\r\n";

            // PlanFeatures

            lineCounter = 1;

            xml += "  <PlanFeatures>\r\n";
            foreach (DAXFeature feature in _features)
            {
                string codeName = feature.ClassName;
                string[] classNameSplit = feature.ClassName.Split('.');
                if (classNameSplit.Length > 1)
                    codeName = classNameSplit[classNameSplit.Length - 1];

                if (feature.GeometryType == DAXGeometryType.Line && feature.Coordinates != null && feature.Coordinates.Length > 1 && feature.ClassName != null)
                {
                    string fcName = feature.ClassName != null ? feature.ClassName : "Ukendt";

                    if (fcName.Length > 28)
                        fcName = fcName.Substring(0, 28);

                    fcName += "" + lineCounter;

                    xml += "    <PlanFeature name=\"" + fcName + "\" code=\""  + codeName +  "\">\r\n";
                    xml += "      <CoordGeom>\r\n";

                    linePointCounter = 1;

                    DAXCoordinate lastPoint = null;
                    foreach (DAXCoordinate point in feature.Coordinates)
                    {
                        if (lastPoint != null)
                        {
                            string lineId1 = "L" + lineCounter + "P" + (linePointCounter - 1);
                            string lineId2 = "L" + lineCounter + "P" + linePointCounter;

                            xml += "        <Line>\r\n";
                            xml += "          <Start pntRef=\"" + lineId1 + "\">" + lastPoint.ToString() + "</Start>\r\n";
                            xml += "          <End pntRef=\"" + lineId2 + "\">" + point.ToString() + "</End>\r\n";
                            xml += "        </Line>\r\n";
                        }
                        lastPoint = point;
                        linePointCounter++;
                    }
                    xml += "      </CoordGeom>\r\n";
                    xml += "    </PlanFeature>\r\n";

                    lineCounter++;
                }
            }

            xml += "  </PlanFeatures>\r\n";

            xml += "</LandXML>\r\n";

            return xml;
        }
    }
}

