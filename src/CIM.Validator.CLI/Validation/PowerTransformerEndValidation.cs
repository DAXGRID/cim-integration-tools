using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class PowerTransformerEndValidation
{
    public static ValidationError? BaseVoltageRequired(PowerTransformerEnd p)
    {
        if (p.BaseVoltage <= 0)
        {
            return new ValidationError
            {
                IdentifiedObjectId = p.mRID,
                IdentifiedObjectClass = p.GetType().Name,
                Code = "POWER_TRANSFORMER_END_REQUIRES_BASEVOLTAGE_GREATER_THAN_ZERO",
                Description = "Power transformer end requires a base voltage greater than zero.",
                Severity = Severity.Error
            };
        }

        return null;
    }

    public static ValidationError? PowerTransformerRequired(PowerTransformerEnd p)
    {
        if (p.PowerTransformer?.@ref is null)
        {
            return new ValidationError
            {
                IdentifiedObjectId = p.mRID,
                IdentifiedObjectClass = p.GetType().Name,
                Code = "POWER_TRANSFORMER_END_REQUIRES_POWER_TRANSFORMER",
                Description = "Power transformer end requires a reference to a power transformer.",
                Severity = Severity.Error
            };
        }

        return null;
    }

    public static ValidationError? TerminalRequired(PowerTransformerEnd p)
    {
        if (p.Terminal?.@ref is null)
        {
            return new ValidationError
            {
                IdentifiedObjectId = p.mRID,
                IdentifiedObjectClass = p.GetType().Name,
                Code = "POWER_TRANSFORMER_END_REQUIRES_TERMINAL",
                Description = "Power transformer end requires a reference to a terminal.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
