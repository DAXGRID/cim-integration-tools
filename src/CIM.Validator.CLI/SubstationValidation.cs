using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class SubstationValidation
{
    public static ValidationError? PsrType(Substation s)
    {
        return s.PSRType switch
        {
            "PrimarySubstation" or "SecondarySubstation" or "Tower" or "CableBox" => null,
            _ => new ValidationError
            {
                Mrid = Guid.Parse(s.mRID),
                TypeName = s.GetType().Name,
                Code = "INCORRECT_PSR_TYPE_FOR_SUBSTATION",
                Description = $"Incorrect PSR type for substation, should be PrimarySubstation, SecondarySubstation, Tower or CableBox. The supplied PSR type is: '{s.PSRType}'.",
                Severity = Severity.Error
            }
        };
    }
}
