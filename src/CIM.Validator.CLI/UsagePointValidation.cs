using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class UsagePointValidation
{
    public static ValidationError? EquipmentReference(UsagePoint u)
    {
        if (string.IsNullOrWhiteSpace(u.Equipments?.@ref))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(u.mRID),
                TypeName = u.GetType().Name,
                Code = "USAGE_POINT_EQUIPMENT_REFERENCE_REQUIRED",
                Description = "Usage point always requires an equipment reference.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
