using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class LocationValidation
{
    public static ValidationError? CoordinateSystem(Location l)
    {
        if (string.IsNullOrWhiteSpace(l.CoordinateSystem.@ref))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(l.mRID),
                TypeName = l.GetType().Name,
                Code = "LOCATION_MISSING_COORDINATE_SYSTEM_REFERENCE",
                Description = "Location is missing coordinate system reference.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
