using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class PowerTransformerValidation
{
    public static ValidationError? PowerTransformerEndPerTerminal(PowerTransformer powerTransformer, IReadOnlyList<Terminal> terminals, IReadOnlyList<PowerTransformerEnd> powerTransformerEnds)
    {
        if (!powerTransformerEnds.All(pe => terminals.Any(t => pe.Terminal.@ref == t.mRID)))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(powerTransformer.mRID),
                TypeName = powerTransformer.GetType().Name,
                Code = "ONE_POWER_TRANSFORMER_END_PER_TERMINAL",
                Description = "One power transformer end per terminal is required.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
