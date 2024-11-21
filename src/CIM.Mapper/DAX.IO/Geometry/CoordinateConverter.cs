using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace DAX.IO.Geometry
{
    public class CoordinateConverter
    {
        string _sourceCsWkt = null;
        string _targetCsWkt = null;
        CoordinateSystem _fromCS;
        CoordinateSystem _toCS;
        CoordinateTransformationFactory _ctfac;
        ICoordinateTransformation _trans;

        public CoordinateConverter(string sourceCsWkt, string targetCsWkt)
        {
            _sourceCsWkt = sourceCsWkt;
            _targetCsWkt = targetCsWkt;
        }

        public double[] Convert(double x, double y)
        {
            Initialize();

            // Transform point to WGS84 latitude longitude 
            double[] fromPoint = new double[] { x, y };
            double[] toPoint = _trans.MathTransform.Transform(fromPoint);

            return toPoint;
        }


        private void Initialize()
        {
            if (_fromCS == null)
            {
                // Initialize objects needed for coordinate transformation
                var cf = new ProjNet.CoordinateSystems.CoordinateSystemFactory();

                _fromCS = cf.CreateFromWkt(_sourceCsWkt);
                _toCS = cf.CreateFromWkt(_targetCsWkt);
                
                _trans = _ctfac.CreateFromCoordinateSystems(_fromCS, _toCS);
            }
        }

    }
}
