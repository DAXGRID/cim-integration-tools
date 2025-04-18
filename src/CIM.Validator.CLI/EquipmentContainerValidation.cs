using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class EquipmentContainerValidation
{
    public static ValidationError? EquipmentContainerCorrectType(VoltageLevel v, EquipmentContainer? equipmentContainer)
    {
        if (equipmentContainer is null)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(v.mRID),
                TypeName = typeof(VoltageLevel).Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"Cannot validate equipment container type because the reference is missing.",
                Severity = Severity.Warning
            };
        }

        if (equipmentContainer is not Substation)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(v.mRID),
                TypeName = typeof(VoltageLevel).Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"The equipment container for the current transformer should be of type: '{typeof(Substation).Name}'. Current type is '{equipmentContainer.GetType().Name}'.",
                Severity = Severity.Warning
            };

        }

        return null;
    }

    public static ValidationError? EquipmentContainerCorrectType(Bay b, EquipmentContainer? equipmentContainer)
    {
        if (equipmentContainer is null)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(b.mRID),
                TypeName = typeof(Bay).Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"Cannot validate equipment container type because the reference is missing.",
                Severity = Severity.Warning
            };
        }

        if (equipmentContainer is not VoltageLevel)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(b.mRID),
                TypeName = typeof(Bay).Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"The equipment container for the current transformer should be of type: '{typeof(VoltageLevel).Name}'. Current type is '{equipmentContainer.GetType().Name}'.",
                Severity = Severity.Warning
            };

        }

        return null;
    }
}
