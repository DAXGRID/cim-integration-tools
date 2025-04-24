using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class LocationValidation
{
    public static ValidationError? CoordinateSystem(Location l)
    {
        if (string.IsNullOrWhiteSpace(l.CoordinateSystem.@ref))
        {
            return new ValidationError
            {
                IdentifiedObjectId = l.mRID,
                IdentifiedObjectClass = l.GetType().Name,
                Code = "LOCATION_MISSING_COORDINATE_SYSTEM_REFERENCE",
                Description = "Location is missing coordinate system reference.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
