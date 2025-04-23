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

    public static ValidationError? NameRequired(UsagePoint u)
    {
        if (string.IsNullOrWhiteSpace(u.name))
        {
            return new ValidationError
            {
                TypeReferenceMrid = u.mRID,
                TypeName = u.GetType().Name,
                Code = "USAGE_POINT_NAME_IS_REQUIRED",
                Description = "Usage point always requires the 'name' attribute to be filled.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
