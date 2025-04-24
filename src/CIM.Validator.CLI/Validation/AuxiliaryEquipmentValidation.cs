using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class AuxiliaryEquipmentValidation
{
    public static ValidationError? HasTerminal(AuxiliaryEquipment a)
    {
        if (string.IsNullOrWhiteSpace(a.Terminal?.@ref))
        {
            return new ValidationError
            {
                IdentifiedObjectId = a.mRID,
                IdentifiedObjectClass = a.GetType().Name,
                Code = "AUXILIARY_EQUIPMENT_REQUIRE_TERMINAL_REFERENCE",
                Description = "Auxiliary equipment requires terminal reference.",
                Severity = Severity.Error
            };
        }

        return null;
    }

    public static ValidationError? ReferencesBay(AuxiliaryEquipment a, EquipmentContainer? e)
    {
        if (e is not Bay)
        {
            return new ValidationError
            {
                IdentifiedObjectId = a.mRID,
                IdentifiedObjectClass = a.GetType().Name,
                Code = "AUXILIARY_EQUIPMENT_CONTAINER_SHOULD_BE_BAY",
                Description = "Auxiliary equipments should reference an equipment container of type Bay.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? TerminalReferenceExist(AuxiliaryEquipment a, Terminal? t)
    {
        if (!string.IsNullOrWhiteSpace(a.Terminal?.@ref) && t is null)
        {
            return new ValidationError
            {
                IdentifiedObjectId = a.mRID,
                IdentifiedObjectClass = a.GetType().Name,
                Code = "AUXILIARY_EQUIPMENT_TERMINAL_REFERENCE_DOES_NOT_EXIST",
                Description = "Auxiliary equipment reference to terminal '{a.Terminal?.@ref}' do not exist.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
