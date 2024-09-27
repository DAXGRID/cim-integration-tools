using System.Text.Json.Serialization;

namespace CIM.Change;

public abstract class IdentifiedObject
{
    [JsonPropertyName("mRID")]
    public required string mRID { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
