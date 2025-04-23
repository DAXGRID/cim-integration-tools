using CIM.PhysicalNetworkModel;
using System.Collections.Frozen;

namespace CIM.Validator.CLI.Validation;

internal static class TerminalValidation
{
    public static ValidationError? ConductingEquipmentReferenceId(Terminal t)
    {
        if (t.ConductingEquipment?.@ref is null)
        {
            return new ValidationError
            {
                Mrid = t.mRID,
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
                Mrid = t.mRID,
                TypeName = t.GetType().Name,
                Code = "TERMINAL_CONDUCTING_EQUIPMENT_REFERENCE_DOES_NOT_EXIST",
                Description = "The terminal reference a conducting equipment that does not exist.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? SequenceNumberRequired(Terminal t)
    {
        if (string.IsNullOrWhiteSpace(t.sequenceNumber))
        {
            return new ValidationError
            {
                Mrid = t.mRID,
                TypeName = t.GetType().Name,
                Code = "TERMINAL_IS_MISSING_MANDATORY_NUMBER_ATTRIBUTE",
                Description = "All terminals require a sequence number.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? SequenceNumberValidValue(Terminal t)
    {
        if (!int.TryParse(t.sequenceNumber, out var sequenceNumber) && sequenceNumber <= 0)
        {
            return new ValidationError
            {
                Mrid = t.mRID,
                TypeName = t.GetType().Name,
                Code = "TERMINAL_NUMBER_ATTRIBUTE_HAS_INVALID_VALUE ",
                Description = "Sequence number should always be a whole number and be greater or equal to 1.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? PhaseRequired(Terminal t)
    {
        if (!t.phasesSpecified)
        {
            return new ValidationError
            {
                Mrid = t.mRID,
                TypeName = t.GetType().Name,
                Code = "TERMINAL_IS_MISSING_MANDATORY_FASE_ATTRIBUTE",
                Description = "Terminal 'phases' is required.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
