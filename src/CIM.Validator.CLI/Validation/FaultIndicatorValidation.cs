using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

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

    public static ValidationError? ResetKindRequired(FaultIndicator f)
    {
        if (f is not FaultIndicatorExt)
        {
            return null;
        }

        if (((FaultIndicatorExt)f).resetKindSpecified)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(f.mRID),
                TypeName = typeof(FaultIndicator).Name,
                Code = "FAULT_INDICATOR_RESET_KIND_REQUIRED",
                Description = "The reset kind has not been specified on the fault indicator.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
