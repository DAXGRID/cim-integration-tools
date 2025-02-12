using System.Text.Json;

namespace CIM.PostgresImporter.CLI;

internal sealed record Schema
{
    public required IReadOnlyCollection<SchemaType> Types { get; init; }
}

internal sealed record SchemaType
{
    public required string Name { get; init; }
    public required IReadOnlyCollection<SchemaTypeProperty> Properties { get; init; }
}

internal sealed record SchemaTypeProperty
{
    public required string Name { get; init; }

    public required Type Type { get; init; }

    // This is a special case where we are dealing with a composite type.
    public string? ContainingObjectName { get; init; }
    public string? InnerObjectName { get; init; }

    public int SortNumber => GetSortNumber();

    private int GetSortNumber()
    {
        if (Name == "mRID")
        {
            return 2;
        }
        else if (char.IsUpper(Name[0]) && Type == typeof(Guid))
        {
            return 0;
        }
        else
        {
            return 1;
        }
    }
}

#pragma warning disable CA1812 // Type class for utility useage.
internal sealed record Point2D
{
    public double X { get; init; }
    public double Y { get; init; }
}
#pragma warning restore CA1812

#pragma warning disable CA1812 // Type class for utility useage.
internal sealed class CompositeObject { }
#pragma warning restore CA1812

internal sealed class SchemaColumn
{
    public required string Name { get; init; }
    public required Type Type { get; init; }

    // This is a special case where we are dealing with a composite type.
    public string? ContainingObjectName { get; init; }
    public string? InnerObjectName { get; init; }
}

internal static class DynamicSchema
{
    public static async Task<Schema> BuildFromJsonAsync(StreamReader reader)
    {
        var schemas = new Dictionary<string, Dictionary<string, SchemaColumn>>();

        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)
                ?? throw new ArgumentException($"Could not deserialize the line: '{line}'");

            string typeName;
            if (properties.TryGetValue("$type", out JsonElement typeNameProperty))
            {
                typeName = typeNameProperty.ToString();
            }
            else
            {
                throw new ArgumentException("Could not find $type on the read in line, something is wrong with the data.");
            }

            // We do not want the type indexed.
            properties.Remove("$type");

            foreach (var property in properties)
            {
                Dictionary<string, SchemaColumn>? typeSchema;
                if (!schemas.TryGetValue(typeName, out typeSchema))
                {
                    typeSchema = new();
                    schemas.Add(typeName, typeSchema);
                }

                if (!typeSchema.ContainsKey(property.Key))
                {
                    var conversionType = ConvertJsonType((JsonElement)property.Value);
                    if (conversionType != typeof(CompositeObject))
                    {
                        var schemaColumn = new SchemaColumn { Name = property.Key, Type = conversionType };
                        typeSchema.Add(property.Key, schemaColumn);
                    }
                    else
                    {
                        var innerProperties = property.Value.Deserialize<Dictionary<string, JsonElement>>() ??
                            throw new InvalidOperationException($"Could not deserialize inner object: '{property.Value.GetRawText()}'.");

                        innerProperties.Remove("$type");

                        foreach (var innerProperty in innerProperties)
                        {
                            var name = $"{property.Key}_{innerProperty.Key}";
                            var schemaColumn = new SchemaColumn
                            {
                                Name = name,
                                Type = ConvertJsonType((JsonElement)innerProperty.Value),
                                ContainingObjectName = property.Key,
                                InnerObjectName = innerProperty.Key
                            };

                            typeSchema.TryAdd(name, schemaColumn);
                        }
                    }
                }
            }
        }

        return new Schema
        {
            Types = schemas
            .Select(
                x =>
                new SchemaType
                {
                    Name = x.Key,
                    Properties = x.Value
                      .Select(y => new SchemaTypeProperty
                      {
                          Name = y.Key,
                          Type = y.Value.Type,
                          ContainingObjectName = y.Value.ContainingObjectName,
                          InnerObjectName = y.Value.InnerObjectName
                      })
                      .OrderByDescending(x => x.SortNumber)
                      .ThenBy(x => x.Name)
                      .ToList()
                      .AsReadOnly()
                }
            ).ToList().AsReadOnly()
        };
    }

    private static Type ConvertJsonType(JsonElement jsonElement)
    {
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Number:
                if (jsonElement.TryGetInt32(out int intValue))
                {
                    return typeof(int);
                }
                else if (jsonElement.TryGetDouble(out double doubleValue))
                {
                    return typeof(double);
                }
                break;
            case JsonValueKind.Array:
                var jsonElements = jsonElement.Deserialize<List<JsonElement>>()
                    ?? throw new InvalidOperationException($"Could not deserialize JSON elements. {JsonSerializer.Serialize(jsonElement)}");

                return typeof(ICollection<>).MakeGenericType(ConvertJsonType(jsonElements[0]));
            case JsonValueKind.String:
                if (Guid.TryParse(jsonElement.GetString(), out Guid _))
                {
                    return typeof(Guid);
                }
                else
                {
                    return typeof(string);
                }
            case JsonValueKind.True:
            case JsonValueKind.False:
                return typeof(bool);
            default:
                if (jsonElement.TryGetProperty("$type", out var elementType))
                {
                    // Point2D is a special type.
                    // In the future it might be simplified to an array of arrays with doubles in side.
                    // Until then it's handled in this way.
                    if (elementType.GetString() == "Point2D")
                    {
                        return typeof(Point2D);
                    }
                }

                return typeof(CompositeObject);
        }

        return typeof(object);
    }
}
