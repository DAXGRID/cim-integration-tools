using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class CurrentTransformerValidation
{
    public static ValidationError? ValidateEquipmentContainerType(CurrentTransformer c, EquipmentContainer? equipmentContainer)
    {
        if (equipmentContainer is null)
        {
             return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = typeof(CurrentTransformerExt).Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"Cannot validate equipment container type because the reference is missing.",
                Severity = Severity.Warning
            };
        }

        if (equipmentContainer is not Bay)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = c.GetType().Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"The equipment container for the current transformer should be of type: '{typeof(Bay).Name}'. Current type is '{equipmentContainer.GetType().Name}'.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
