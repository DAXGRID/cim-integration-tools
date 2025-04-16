using System.Text.Json.Serialization;
using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class Validation
{
    public static ValidationError? BaseVoltageValidation(IdentifiedObject identifiedObject)
    {
        if (identifiedObject is ConductingEquipment && ((ConductingEquipment)identifiedObject).BaseVoltage > 0)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(identifiedObject.mRID),
                TypeName = identifiedObject.GetType().Name,
                Code = "BASE_VOLTAGE_LESS_OR_EQUAL_TO_ZERO",
                Description = "Conducting equipment should have base voltage greater than 0.",
                Severity = Severity.Warning
            };
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

    public required Severity Severity { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }
}
