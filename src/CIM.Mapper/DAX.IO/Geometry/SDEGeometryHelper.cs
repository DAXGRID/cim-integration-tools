namespace DAX.IO.Geometry
{
    public static class SDEGeometryHelper
    {
        public static double RoundTreeDecimals(double coord)
        {
            return Math.Floor(coord * 1000) / 1000;
        }

        public static string ConvertESRICoordsToXYString(IList<ESRICoordinate> coordinates) 
        {
            string coordStr = "";

            foreach (ESRICoordinate coord in coordinates)
            {
                if (coordStr != "")
                    coordStr += " ";

                string x = RoundTreeDecimals(coord.X).ToString();
                string y = RoundTreeDecimals(coord.Y).ToString();

                x = x.Replace(',', '.');
                y = y.Replace(',', '.');

                coordStr += x + "," + y;
            }

            return coordStr;
        }
    }
}
