using System.Text.Json.Serialization;

namespace CIM.Validator.CLI;

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
