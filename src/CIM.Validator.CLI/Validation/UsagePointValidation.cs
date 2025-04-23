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

    public static ValidationError? VerifyName(UsagePoint u, Dictionary<string, List<string>> usagePointNameToMrid)
    {
        // Validate that the usage point has a name.
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

        // Validate that usage point names are unique.
        if (usagePointNameToMrid[u.name].Count > 1)
        {
            var usagePointSharedIds = string.Join(", ", usagePointNameToMrid[u.name].Where(x => x != u.mRID));
            return new ValidationError
            {
                TypeReferenceMrid = u.mRID,
                TypeName = u.GetType().Name,
                Code = "USAGE_POINT_NAME_IS_REQUIRED",
                Description = $"Usage point name should be unique, it is shared with the following usage points: '{usagePointSharedIds}'.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
