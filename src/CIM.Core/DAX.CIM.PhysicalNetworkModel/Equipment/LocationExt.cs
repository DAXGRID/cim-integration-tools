namespace CIM.PhysicalNetworkModel
{
    /// <summary>
    /// Location extension that keeps coordinates in an array instead of references to position points
    /// </summary>
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://daxgrid.net/PhysicalNetworkModel_0_1")]
    public partial class LocationExt : Location
    {
        private GeometryType geometryType;

        private string geometry;

        public GeometryType GeometryType
        {
            get
            {
                return this.geometryType;
            }
            set
            {
                this.geometryType = value;
            }
        }

        public string Geometry
        {
            get
            {
                return this.geometry;
            }
            set
            {
                this.geometry = value;
            }
        }
    }
}
