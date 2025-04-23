using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class UsagePointValidation
{
    public static ValidationError? EquipmentReference(UsagePoint u)
    {
        if (string.IsNullOrWhiteSpace(u.Equipments?.@ref))
        {
            return new ValidationError
            {
                TypeReferenceMrid = u.mRID,
                TypeName = u.GetType().Name,
                Code = "USAGE_POINT_EQUIPMENT_REFERENCE_REQUIRED",
                Description = "Usage point always requires an equipment reference.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
