using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class CurrentTransformerValidation
{
    public static ValidationError? ValidateEquipmentContainerType(CurrentTransformer c, EquipmentContainer? equipmentContainer)
    {
        if (equipmentContainer is null)
        {
            return new ValidationError
            {
                IdentifiedObjectId = c.mRID,
                IdentifiedObjectClass = c.GetType().Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"Cannot validate equipment container type because the reference is missing.",
                Severity = Severity.Warning
            };
        }

        if (equipmentContainer is not Bay)
        {
            return new ValidationError
            {
                IdentifiedObjectId = c.mRID,
                IdentifiedObjectClass = c.GetType().Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"The equipment container for the current transformer should be of type: '{typeof(Bay).Name}'. Current type is '{equipmentContainer.GetType().Name}'.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? MaximumCurrentRequired(CurrentTransformer c)
    {
        // We can only check maximum current on the current transformer extension type.
        if (c is not CurrentTransformerExt)
        {
            return null;
        }

        if (((CurrentTransformerExt)c).maximumCurrent is null)
        {
            return new ValidationError
            {
                IdentifiedObjectId = c.mRID,
                IdentifiedObjectClass = c.GetType().Name,
                Code = "CURRENT_TRANSFORMER_EXT_REQUIRES_MAXIMUM_CURRENT",
                Description = $"Maximum current is required on current transformer extension.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
