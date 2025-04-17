using System.Collections.Frozen;
using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class TerminalValidation
{
    public static ValidationError? ConductingEquipmentReferenceId(Terminal t)
    {
        if (t.ConductingEquipment?.@ref is null)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(t.mRID),
                TypeName = t.GetType().Name,
                Code = "TERMINAL_IS_MISSING_MANDATORY_CONDUCTING_EQUIPMENT_REFERENCE",
                Description = "The terminal is missing a reference to a conducting equipment.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? ConductingEquipmentReferenceExist(Terminal t, FrozenSet<Guid> conductingEquipmentMrIds)
    {
        if (t.ConductingEquipment?.@ref is not null && !conductingEquipmentMrIds.Contains(Guid.Parse(t.ConductingEquipment.@ref)))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(t.mRID),
                TypeName = t.GetType().Name,
                Code = "TERMINAL_CONDUCTING_EQUIPMENT_REFERENCE_DOES_NOT_EXIST",
                Description = "The terminal reference a conducting equipment that does not exist.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? TerminalSequenceNumberRequired(Terminal t)
    {
        if (string.IsNullOrWhiteSpace(t.sequenceNumber))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(t.mRID),
                TypeName = t.GetType().Name,
                Code = "TERMINAL_NUMBER_REQUIRED",
                Description = "All terminals require a sequence number.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
