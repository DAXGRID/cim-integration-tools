namespace DAX.IO.Writers
{
    public class DSFLWriter : IDaxWriter
    {
        private string _dsflFileName = null;
        private List<ConfigParameter> _parameters = null;
        private string _dsflText = null;
        private string _dataSourceName = null;

        public void Open(string connectionStringOrUrl)
        {
            if (connectionStringOrUrl == null)
                throw new DAXWriterException("Fejl: DSFLWriter.Open skal kaldes med url parameter");

            _dsflFileName = connectionStringOrUrl;
        }

        public void Initialize(string dataSourceName, DataReaderWriterSpecification spec, TransformationConfig config, List<ConfigParameter> parameters = null)
        {
            _dataSourceName = dataSourceName;

            if (parameters == null || parameters.Count < 1)
                throw new DAXWriterException("Fejl: DSFLWriter.Open skal kaldes med et eller flere parameter");

            _parameters = parameters;
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

        public string GetResult() { return _dsflText; }

        public void OpenDataSet(string dataSetName)
        {
        }

        public void CloseDataSet(string dataSetName)
        {
        }

        public void WriteFeature(DAXFeature feature, DataSetMappingGuide dsGuide = null)
        {
            if (_dsflText == null)
            {
                _dsflText = "%H0 æøåÆØÅ\r\n";

                string horKoordSys = GetParameterValue("HorizontalCoordSys");
                if (horKoordSys != null)
                    _dsflText += "%H1 " + horKoordSys + "\r\n";

                string verKoordSys = GetParameterValue("VerticalCoordSys");
                if (verKoordSys != null)
                    _dsflText += "%H2 " + verKoordSys + "\r\n";

                _dsflText += "%H3 YXZ\r\n";

                foreach (ConfigParameter param in _parameters)
                {
                    if (param.Name != null && param.Name.StartsWith("%"))
                    {
                        string headerValue = param.Name + "        ";
                        headerValue = headerValue.Substring(0, 8);
                        _dsflText += headerValue + "" + param.Value + "\r\n";
                    }
                }
            }

            _dsflText += feature.ClassName + "\r\n";

            if (feature.GeometryType == DAXGeometryType.Line)
            {
                _dsflText += "%L1KR\r\n";

                if (feature.Coordinates != null)
                {
                    foreach (var coord in feature.Coordinates)
                    {
                        _dsflText += "        " + coord.Y.ToString("0.000").Replace(',', '.') + " " + coord.X.ToString("0.000").Replace(',', '.') + " " + coord.Z.ToString("0.000").Replace(',', '.') + "\r\n";
                    }
                }
            }
            else if (feature.GeometryType == DAXGeometryType.Point)
            {
                if (feature.Coordinates != null && feature.Coordinates.Length == 1)
                {
                    _dsflText += "%P1K\r\n";
                    _dsflText += "        " + feature.Coordinates[0].Y.ToString("0.000").Replace(',', '.') + " " + feature.Coordinates[0].X.ToString("0.000").Replace(',', '.') + " " + feature.Coordinates[0].Z.ToString("0.000").Replace(',', '.') + "\r\n";
                }
            }

            _dsflText += "%D\r\n";
        }

        public void Close()
        {
        }

        public string DataSourceTypeName()
        {
            return "DSFLWriter";
        }

        public string DataSourceName()
        {
            return _dataSourceName;
        }

        public string JobName()
        {
            return _dsflFileName;
        }

        public DAXMetaData GetMetaData()
        {
            DAXMetaData metaData = new DAXMetaData();
            metaData.CanHandleAllOutputTypes = true;
            return metaData;
        }

        public void Commit()
        {
            _dsflText += "%S\r\n";
        }

        public string GetDSFLText()
        {
            return _dsflText;
        }

        private string GetParameterValue(string parameterName)
        {
            foreach (ConfigParameter configParam in _parameters)
            {
                if (configParam.Name != null && configParam.Name.ToLower() == parameterName.ToLower())
                    return configParam.Value;
            }

            return null;
        }        
    }
}
