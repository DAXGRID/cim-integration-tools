using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class EquipmentContainerValidation
{
    public static ValidationError? EquipmentContainerCorrectType(VoltageLevel v, EquipmentContainer? equipmentContainer)
    {
        if (equipmentContainer is null)
        {
            return new ValidationError
            {
                TypeReferenceMrid = v.mRID,
                TypeName = v.GetType().Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"Cannot validate equipment container type because the reference is missing.",
                Severity = Severity.Warning
            };
        }

        if (equipmentContainer is not Substation)
        {
            return new ValidationError
            {
                TypeReferenceMrid = v.mRID,
                TypeName = v.GetType().Name,
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
                TypeReferenceMrid = b.mRID,
                TypeName = b.GetType().Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"Cannot validate equipment container type because the reference is missing.",
                Severity = Severity.Warning
            };
        }

        if (equipmentContainer is not VoltageLevel)
        {
            return new ValidationError
            {
                TypeReferenceMrid = b.mRID,
                TypeName = b.GetType().Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"The equipment container for the current transformer should be of type: '{typeof(VoltageLevel).Name}'. Current type is '{equipmentContainer.GetType().Name}'.",
                Severity = Severity.Warning
            };

        }

        return null;
    }
}
