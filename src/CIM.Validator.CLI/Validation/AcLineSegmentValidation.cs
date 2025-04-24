using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

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
                IdentifiedObjectId = a.mRID,
                IdentifiedObjectClass = a.GetType().Name,
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
                IdentifiedObjectId = a.mRID,
                IdentifiedObjectClass = a.GetType().Name,
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
                IdentifiedObjectId = a.mRID,
                IdentifiedObjectClass = a.GetType().Name,
                Code = "AC_LINE_SEGMENT_REFERENCED_LOCATION_IS_NOT_A_LINE",
                Description = $"AC Line Segment has a reference to a location ({a.Location.@ref}) is not a line.",
                Severity = Severity.Error
            };
        }

        return null;
    }

    public static ValidationError? NoEquipmentContainerLengthGreaterThanZero(ACLineSegment a)
    {
        // Length is only available on ACLineSegmentExt
        if (a is not ACLineSegmentExt)
        {
            return null;
        }

        // It does not require to have a length if does not have an equipment container.
        if (a.EquipmentContainer?.@ref is null)
        {
            return null;
        }

        var acLineSegmentExt = (ACLineSegmentExt)a;

        if (acLineSegmentExt.length is null || acLineSegmentExt.length.Value <= 0)
        {
            return new ValidationError
            {
                IdentifiedObjectId = a.mRID,
                IdentifiedObjectClass = a.GetType().Name,
                Code = "AC_LINE_SEGMENT_NO_EQUIPMENT_CONTAINER_SHOULD_HAVE_LENGTH_GREATER_THAN_ZERO",
                Description = $"AC line segment without an equipment container should have a length and the value should be greater than zero.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
