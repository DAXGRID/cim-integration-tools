using System;

namespace DAX.CIM.PhysicalNetworkModel.Traversal.Extensions
{
    public static class DoubleExtensions
    {
        public static bool IsEqualTo(this double value, double other, CimContext context, double? tolerance = null)
        {
            var currentTolerance = tolerance ?? context.Tolerance;

            return Math.Abs(value - other) < currentTolerance;
        }
    }
}
