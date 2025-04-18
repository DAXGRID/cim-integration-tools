using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class AuxiliaryEquipmentValidation
{
    public static ValidationError? HasTerminal(AuxiliaryEquipment a)
    {
        if (string.IsNullOrWhiteSpace(a.Terminal?.@ref))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(a.mRID),
                TypeName = a.GetType().Name,
                Code = "AUXILIARY_EQUIPMENT_REQUIRE_TERMINAL_REFERENCE",
                Description = "Auxiliary equipment requires terminal reference.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
