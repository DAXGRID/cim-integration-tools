using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class PowerTransformerEndValidation
{
    public static ValidationError? BaseVoltageRequired(PowerTransformerEnd p)
    {
        if (p.BaseVoltage <= 0)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(p.mRID),
                TypeName = p.GetType().Name,
                Code = "POWER_TRANSFORMER_END_REQUIRES_BASEVOLTAGE_GREATER_THAN_ZERO",
                Description = "",
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
                Mrid = Guid.Parse(p.mRID),
                TypeName = p.GetType().Name,
                Code = "POWER_TRANSFORMER_END_REQUIRES_POWER_TRANSFORMER",
                Description = "",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
