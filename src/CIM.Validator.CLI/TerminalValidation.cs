using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class TerminalValidation
{
    public static ValidationError? ConductingEquipmentReference(Terminal t)
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
}
