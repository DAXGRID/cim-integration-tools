using System.Text.Json.Serialization;

namespace CIM.Validator.CLI;

internal sealed record ValidationError
{
    public Guid mRID { get; init; } = Guid.NewGuid();

    [JsonPropertyName("$type")]
    public string Type { get; private init; } = "ValidationError";

    public required string IdentifiedObjectClass { get; init; }

    public required string IdentifiedObjectId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required Severity Severity { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }
}
