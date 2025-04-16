using CIM.PhysicalNetworkModel;
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

    public static ValidationError? WrongNumberOfTerminals(ConductingEquipment c)
    {
        Func<int, bool> x = c switch
        {
            // Missing Generator, ask Jesper??
            EnergyConsumer or BusbarSection or Connector or PetersenCoil or LinearShuntCompensator => (int x) => (x == 1),
            PowerTransformer => (int x) => (x > 2),
            _ => (int x) => (x == 2),
        };

        // Lookup terminals here for conducting equipment and verify that it matches.

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

    public required Severity Severity { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }
}
