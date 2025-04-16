using CIM.PhysicalNetworkModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace CIM.Validator.CLI;

internal static class Validation
{
    public static ValidationError? BaseVoltage(ConductingEquipment c)
    {
        if (c.BaseVoltage > 0)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = c.GetType().Name,
                Code = "BASE_VOLTAGE_LESS_OR_EQUAL_TO_ZERO",
                Description = "Conducting equipment should have base voltage greater than 0.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? WrongNumberOfTerminals(ConductingEquipment c, IEnumerable<Terminal> terminals)
    {
        Func<int, bool> validate = c switch
        {
            // Missing Generator, ask Jesper??
            EnergyConsumer or BusbarSection or Connector or PetersenCoil or LinearShuntCompensator => (int x) => (x == 1),
            PowerTransformer => (int x) => (x > 2),
            _ => (int x) => (x == 2),
        };

        if (!validate(terminals.Count()))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = c.GetType().Name,
                Code = "WRONG_NUMBER_OF_TERMINALS",
                Description = "Wrong number of terminals on conducting equipment.",
                Severity = Severity.Warning
            };
        }

        // Lookup terminals here for conducting equipment and verify that it matches.
        return null;
    }

    public static ValidationError? TerminalNumbering(ConductingEquipment c, IEnumerable<Terminal> terminals)
    {
        var expectedSequenceNumber = 0;
        foreach (var sequenceNumber in terminals.OrderBy(x => x.sequenceNumber).Select(x => x.sequenceNumber))
        {
            expectedSequenceNumber++;
            if (expectedSequenceNumber != int.Parse(sequenceNumber, CultureInfo.InvariantCulture))
            {
                return new ValidationError
                {
                    Mrid = Guid.Parse(c.mRID),
                    TypeName = c.GetType().Name,
                    Code = "TERMINAL_NUMBERING_IS_INVALID",
                    Description = "Terminal numbering is invalid, should always be a valid sequence.",
                    Severity = Severity.Warning
                };
            }
        }
        return null;
    }
}

internal enum Severity
{
    Warning,
    Error
}

internal sealed record ValidationError
{
    [JsonPropertyName("$type")]
    public string Type { get; private init; } = "ValidationError";

    public required string TypeName { get; init; }

    public required Guid Mrid { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required Severity Severity { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }
}
