using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class SubstationValidation
{
    public static ValidationError? LocationRequired(Substation s, Location? l)
    {
        if (string.IsNullOrWhiteSpace(s.Location?.@ref))
        {
            return new ValidationError
            {
                IdentifiedObjectId = s.mRID,
                IdentifiedObjectClass = s.GetType().Name,
                Code = "SUBSTATION_MISSING_LOCATION_REFERENCE",
                Description = "The substation is missing the required location reference.",
                Severity = Severity.Error
            };
        }

        if (l is null)
        {
            return new ValidationError
            {
                IdentifiedObjectId = s.mRID,
                IdentifiedObjectClass = s.GetType().Name,
                Code = "SUBSTATION_LOCATION_REFERENCED_DO_NOT_EXIST",
                Description = "The location that the substation has reference to does not exist: '{s.Location.@ref}'.",
                Severity = Severity.Error
            };
        }

        if (l is LocationExt && ((LocationExt)l).GeometryType != GeometryType.Point)
        {
            return new ValidationError
            {
                IdentifiedObjectId = s.mRID,
                IdentifiedObjectClass = s.GetType().Name,
                Code = "SUBSTATION_LOCATION_SHOULD_BE_POINT",
                Description = "The location of the substation should always be a point.",
                Severity = Severity.Error
            };
        }

        return null;
    }

    public static ValidationError? PsrType(Substation s)
    {
        return s.PSRType switch
        {
            "PrimarySubstation" or "SecondarySubstation" or "Tower" or "CableBox" or "T-Junction" => null,
            _ => new ValidationError
            {
                IdentifiedObjectId = s.mRID,
                IdentifiedObjectClass = s.GetType().Name,
                Code = "INCORRECT_PSR_TYPE_FOR_SUBSTATION",
                Description = $"Incorrect PSR type for substation, should be PrimarySubstation, SecondarySubstation, Tower, T-Junction or CableBox. The supplied PSR type is: '{s.PSRType}'.",
                Severity = Severity.Error
            }
        };
    }
}
