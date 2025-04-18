using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class FaultIndicatorValidation
{
    public static ValidationError? ValidateEquipmentContainerType(FaultIndicator f, EquipmentContainer? equipmentContainer)
    {
        if (equipmentContainer is null)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(f.mRID),
                TypeName = typeof(FaultIndicator).Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"Cannot validate equipment container type because the reference is missing.",
                Severity = Severity.Warning
            };
        }

        if (equipmentContainer is not Bay)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(f.mRID),
                TypeName = typeof(FaultIndicator).Name,
                Code = "INVALID_EQUIPMENT_CONTAINER_TYPE",
                Description = $"The equipment container for the current transformer should be of type: '{typeof(Bay).Name}'. Current type is '{equipmentContainer.GetType().Name}'.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
