using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI.Validation;

internal static class PowerTransformerValidation
{
    public static ValidationError? PowerTransformerEndPerTerminal(PowerTransformer powerTransformer, IReadOnlyList<Terminal> terminals, IReadOnlyList<PowerTransformerEnd> powerTransformerEnds)
    {
        if (!powerTransformerEnds.All(pe => terminals.Any(t => pe.Terminal.@ref == t.mRID)))
        {
            return new ValidationError
            {
                IdentifiedObjectId = powerTransformer.mRID,
                IdentifiedObjectClass = powerTransformer.GetType().Name,
                Code = "ONE_POWER_TRANSFORMER_END_PER_TERMINAL",
                Description = "One power transformer end per terminal is required.",
                Severity = Severity.Error
            };
        }

        return null;
    }

    public static ValidationError? PowerTransformerEndNumberMatchesTerminalNumber(PowerTransformer powerTransformer, IReadOnlyList<Terminal> terminals, IReadOnlyList<PowerTransformerEnd> powerTransformerEnds)
    {
        if (!powerTransformerEnds.All(e => e.endNumber == terminals.FirstOrDefault(t => t.mRID == e.Terminal.@ref)?.sequenceNumber))
        {
            return new ValidationError
            {
                IdentifiedObjectId = powerTransformer.mRID,
                IdentifiedObjectClass = powerTransformer.GetType().Name,
                Code = "POWER_TRANSFORMER_END_NUMBER_MATCHES_TERMINAL_NUMBER",
                Description = "All the powertransformer end numbers need to match the sequence number of the terminal it is pointing to.",
                Severity = Severity.Warning
            };
        }

        return null;
    }
}
