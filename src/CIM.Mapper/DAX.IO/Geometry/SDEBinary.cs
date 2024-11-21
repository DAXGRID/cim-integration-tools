// Copyright 2011, Jesper Ladegaard (GIS Bandit). All rights reserved. 
//
// DISCLAIMER OF WARRANTY 
// This source code is provided "as is" and without warranties as to performance or merchantability. 
//
namespace DAX.IO.Geometry
{
    /// <summary>
    /// Used to hold coordinates (x,y,z and m) returned from SDE binary conversion.
    /// </summary>
    public class ESRICoordinate 
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double M { get; set; }

        public ESRICoordinate(double x, double y, double z, double m)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.M = m;
        }
    }

    public static class SDEBinary
    {
        /// <summary>
        /// <para>Converts SDE Compressed binary to a list of coordinates.</para>
        /// <para>You'll need to fetch falseX, falseY, falseZ and xyUnits values from SDE_spatial_references to do the conversion.</para>
        /// <para>Also you need to know the number of coordinates in the binary, which can be found in the f(feature) table along with the binary data.</para>
        /// <para>To find the srid use the SDE_layers table.</para>
        /// <para>To find the f table use the SDE_geometry_columns table.</para>
        /// <para>Good luck :)</para>
        /// </summary>
        /// <param name="buffer">Byte array containing SDE compressed binary data</param>
        /// <param name="nPoints">Value to be found in the SDE_geometry_columns table</param>
        /// <param name="falseX">Value to be found in the SDE_geometry_columns table</param>
        /// <param name="falseY">Value to be found in the SDE_geometry_columns table</param>
        /// <param name="falseZ">Value to be found in the SDE_geometry_columns table</param>
        /// <param name="xyUnits">Value to be found in the SDE_geometry_columns table</param>
        /// <returns>List of coordiates</returns>
        public static IList<ESRICoordinate> SDEBinary2Coords(byte[] buffer, int nPoints, double falseX, double falseY, double falseZ, double xyUnits)
        {
            // TODO: z og m support

            IList<long> integers = ReadPackedIntegers(buffer, buffer.Length);

            IList<ESRICoordinate> koords = new List<ESRICoordinate>();

            double lastX = 0;
            double lastY = 0;

            for (int i = 0; i < (nPoints * 2); i += 2)
            {
                double x = 0;
                double y = 0;

                if (i == 0)
                {
                    x = (integers[i] / xyUnits) + falseX;
                    lastX = x;
                    y = (integers[i + 1] / xyUnits) + falseY;
                    lastY = y;
                }
                else
                {
                    x = (integers[i] / xyUnits) + lastX;
                    lastX = x;
                    y = (integers[i + 1] / xyUnits) + lastY;
                    lastY = y;
                }
                
                koords.Add(new ESRICoordinate(x, y, 0, 0));
            }

            return koords;
        }

        private static IList<long> ReadPackedIntegers(byte[] buffer, long len)
        {
            IList<long> integers = new List<long>();

            int byteCount = 8;
            bool hasZ = (buffer[5] & 0x01) > 0 ? true : false;
            bool hasM = (buffer[5] & 0x02) > 0 ? true : false;

            while (byteCount < len)
            {
                long val = (long)buffer[byteCount] & 0x3f; // Første data uden sign og last
                long sign = (buffer[byteCount] & 0x40) > 0 ? (long)-1 : (long)1;
                long shift = 64;
                bool last = (buffer[byteCount] & 0x80) > 0 ? false : true;

                while (!last && byteCount < len)
                {
                    byteCount++;
                    val += ((long)buffer[byteCount] & 0x7f) * shift;
                    shift *= 128;
                    last = (buffer[byteCount] & 0x80) > 0 ? false : true;
                }

                integers.Add(val * sign);

                byteCount++;
            }

            return integers;
        }
    }
}
