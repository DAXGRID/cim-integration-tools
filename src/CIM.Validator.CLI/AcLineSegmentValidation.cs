using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class AcLineSegmentValidation
{
    public static ValidationError? ValidateLocation(ACLineSegment a, Location? location)
    {
        if (string.IsNullOrWhiteSpace(a.EquipmentContainer?.@ref))
        {
            return null;
        }

        // If AC line segment does not have an equipment container reference
        // it is laying in the ground and not part of a transformer station,
        // it therefore requires a reference to a location object.
        if (string.IsNullOrWhiteSpace(a.Location?.@ref))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(a.mRID),
                TypeName = a.GetType().Name,
                Code = "LOCATION_REQUIRED_AC_LINE_SEGMENT_IN_GROUND",
                Description = "AC Line Segment without equipment container reference requires location.",
                Severity = Severity.Error
            };
        }

        // The referenced location does not exist.
        if (location is null)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(a.mRID),
                TypeName = a.GetType().Name,
                Code = "AC_LINE_SEGMENT_REFERENCED_LOCATION_DOES_NOT_EXIST",
                Description = $"AC Line Segment has a reference to a location ({a.Location.@ref}) that does not exist.",
                Severity = Severity.Error
            };
        }

        // The referenced location should be a line.
        if (location is LocationExt && ((LocationExt)location).coordinates.Length > 1)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(a.mRID),
                TypeName = a.GetType().Name,
                Code = "AC_LINE_SEGMENT_REFERENCED_LOCATION_IS_NOT_A_LINE",
                Description = $"AC Line Segment has a reference to a location ({a.Location.@ref}) is not a line.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
