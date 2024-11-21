using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DAX.IO
{
    public enum DAXGeometryType
    {
        Unknown = 0,
        Point = 1,
        Line = 2,
        Polygon = 3,
        NoGemoetry = 99,
    }

    //var comparer = StringComparer.OrdinalIgnoreCase;
    //var caseInsensitiveDictionary = new Dictionary<string, int>(comparer);

    public class DAXRow 
    {
        protected Dictionary<string, object> _attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public object this[string key]
        {
            get
            {
                return _attributes[key];
            }

            set
            {
                _attributes[key] = value;
            }

        }

        public void Add(string key, object value)
        {
            _attributes.Add(key, value);
        }

        public void Remove(string key)
        {
            _attributes.Remove(key);
        }

        public bool ContainsKey(string key)
        {
            return _attributes.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _attributes.GetEnumerator();
        }

        public List<object> Values
        {
            get {
                return _attributes.Values.ToList();
            }
        }

        public int Count
        {
            get
            {
                return _attributes.Count;
            }
        }
    }

    public class DAXFeature : DAXRow
    {
        public string CategoryName { get; set; }
        public string ClassName { get; set; }
        public DAXCoordinate[] Coordinates { get; set; }
        public DAXGeometryType GeometryType { get; set; }

        public string GetStringDetailed()
        {
            string result = "DAX feature (" + this.ClassName + "):\r\n";

            result += "  GeometryType=" + this.GeometryType.ToString() + "\r\n";

            string coordString = "";

            if (Coordinates != null)
            {
                foreach (DAXCoordinate coord in Coordinates)
                {
                    if (coordString != "")
                        coordString += ", ";

                    coordString += coord.X + " " + coord.Y + " " + coord.Z;
                }
            }
            else
                coordString = "NULL (Invalid)";

            result += "  Coords=" + coordString + "\r\n";

            foreach (KeyValuePair<string, object> attr in this)
                result += "  Attribute: " + attr.Key + "=" + (attr.Value != null ? attr.Value.ToString() : "null") + "\r\n";

            return result;
        }

        public override string ToString()
        {
            if (GeometryType == DAXGeometryType.Point && Coordinates != null && Coordinates.Length > 0) 
                return Coordinates[0].ToString();
            else if (GeometryType == DAXGeometryType.Line && Coordinates != null)
            {
                string returnString = "";
                foreach (var coord in Coordinates)
                {
                    if (returnString != "")
                        returnString += ",";
                    returnString += coord.ToString();
                }

                return returnString;
            }

            return null;
        }

        public string ToStringWithoutZ()
        {
            if (GeometryType == DAXGeometryType.Point && Coordinates != null && Coordinates.Length > 0)
                return Coordinates[0].ToStringWithoutZ();
            else if (GeometryType == DAXGeometryType.Line && Coordinates != null)
            {
                string returnString = "";
                foreach (var coord in Coordinates)
                {
                    if (returnString != "")
                        returnString += " ";
                    returnString += coord.ToStringWithoutZ();
                }

                return returnString;
            }

            return null;
        }

        public string GetAttributeAsString(string name)
        {
            if (!_attributes.ContainsKey(name))
                return null;

            object val = _attributes[name];
            if (val != null)
                return val.ToString();
            else
                return null;
        }
    }
     
    public class DAXCoordinate
    {
        public string ID;
        public double X;
        public double Y;
        public double Z;
        public double XYPrecision = 0;
        public double ZPrecision = 0;
        public DateTime TimeStamp;

        public DAXCoordinate()
        {
        }

        public DAXCoordinate(string coordinateString)
        {
            string[] coordSplit = coordinateString.Split(' ');
            if (coordSplit.Length < 2)
            {
                throw new Exception("DAXCoordinate: Error parsing '" + coordinateString + "'");
            }

            NumberFormatInfo provider = new NumberFormatInfo();
            provider.NumberDecimalSeparator = ".";

            X = Convert.ToDouble(coordSplit[0], provider);
            Y = Convert.ToDouble(coordSplit[1], provider);

            // X skal være mindre end Y, når der er tale om UTM ETRS 89 
            if (X > Y)
            {
                double tempX = X;
                double tempY = Y;
                X = tempY;
                Y = tempX;
            }

            if (coordSplit.Length > 2)
                Z = Convert.ToDouble(coordSplit[2], provider);

        }

        public string ToString()
        {
            string zValue = Convert.ToString(Z).Replace(',', '.');
            return Convert.ToString(Y).Replace(',', '.') + " " + Convert.ToString(X).Replace(',', '.') + " " + zValue;
        }

        public string ToStringWithoutZ()
        {
            return Convert.ToString(X).Replace(',', '.') + "," + Convert.ToString(Y).Replace(',', '.');
        }

    }
}
