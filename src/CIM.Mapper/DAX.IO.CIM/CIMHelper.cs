using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace DAX.IO.CIM
{
    public class CIMHelper {

        public static bool equalWithTolerance(double theOne, double theOther, double tolerance)
        {
            if (theOne < (theOther - tolerance))
                return false;
            return theOne <= (theOther + tolerance);
        }

        public static double FindDist(double x1, double y1, double x2, double y2)
        {
            double r1 = (x1 - x2);
            r1 = r1 * r1;
            double r2 = (y1 - y2);
            r2 = r2 * r2;
            return Math.Sqrt(r1 + r2);
        }

        //
        // Does the point 'coor' lie on the line end1-end2?
        // The tolerance is 'tolerance'
        public static bool intersects(double[] coor, double[] end1, double[] end2, double tolerance)
        {
            double rx = coor[0];
            double ry = coor[1];

            double px = end1[0];
            double py = end1[1];

            double qx = end2[0];
            double qy = end2[1];

            //double d = (qx - px) * (ry - py) - (qy - py) * (rx - px);


            double vx = qx - px;
            double vy = qy - py;

            double vmx = rx - px;
            double vmy = ry - py;

            double d2 = vx * vmy - vy * vmx;

            if (Math.Abs(d2) < tolerance)
            {

                double r1 = vx * vmx + vy * vmy;
                if (r1 < -tolerance)
                    return false;

                //double wx = px - qx; // -vx
                //double wy = py - qy; // -vy

                double wmx = rx - qx;
                double wmy = ry - qy;

                double r2 = -vx * wmx - vy * wmy;
                if (r2 > -tolerance)
                    return true;
            }
            return false;

        }
    }
}
