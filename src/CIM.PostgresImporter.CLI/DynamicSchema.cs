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
    public bool IsPrimaryKey { get; init; }

    public required string Name { get; init; }

    public required Type Type { get; init; }

    // This is a special case where we are dealing with a composite type.
    public string? ContainingObjectName { get; init; }
    public string? InnerObjectName { get; init; }

    public int SortNumber => GetSortNumber();

    // Hack to support many to many relationships.
    // This was hard to support since the collections going in are not in a many to many relationship.
    public bool ManyToManyAttribute => Type.Equals(typeof(ICollection<string>));

    public Dictionary<string, SchemaColumn>? RefTypeSchema { get; init; }

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
    public bool IsPrimaryKey { get; init; }

    public required string Name { get; init; }
    public required Type Type { get; init; }

    // This is a special case where we are dealing with a composite type.
    public string? ContainingObjectName { get; init; }
    public string? InnerObjectName { get; init; }

    // TSpecial case to handle many to many relationship.
    public Dictionary<string, SchemaColumn>? RefTypeSchema { get; set; }
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
                        var schemaColumn = new SchemaColumn
                        {
                            Name = property.Key,
                            Type = conversionType,
                            // If it contains mrid field make it the primary key.
                            IsPrimaryKey = property.Key.Equals("mrid", StringComparison.OrdinalIgnoreCase)
                        };

                        typeSchema.Add(property.Key, schemaColumn);
                    }
                    else
                    {
                        var innerProperties = property.Value.Deserialize<Dictionary<string, JsonElement>>() ??
                            throw new InvalidOperationException($"Could not deserialize inner object: '{property.Value.GetRawText()}'.");

                        innerProperties.Remove("$type");

                        foreach (var innerProperty in innerProperties)
                        {
                            var typeSchemaName = $"{property.Key}_{innerProperty.Key}";
                            if (!typeSchema.ContainsKey(typeSchemaName))
                            {
                                var schemaColumn = new SchemaColumn
                                {
                                    Name = typeSchemaName,
                                    Type = ConvertJsonType((JsonElement)innerProperty.Value),
                                    ContainingObjectName = property.Key,
                                    InnerObjectName = innerProperty.Key,
                                };

                                typeSchema.Add(typeSchemaName, schemaColumn);
                            }
                        }
                    }
                }
            }
        }

        var extraSchemas = new Dictionary<string, Dictionary<string, SchemaColumn>>();

        // Restructure schemas to handle cases with many to many relationships.
        // This is a bit of a hack as it came later and current structure could not support it cleanly.
        // What it does is that it finds all collection types with strings.
        // In the case of a collection with strings, we create a new many to many table.
        foreach (var schema in schemas.ToList())
        {
            // This is done to avoid duplicates.
            var typesContainingRefTypes = schema.Value
                .Select(x => x.Value)
                .Where(x => x.Type.Equals(typeof(ICollection<string>)));

            foreach (var refType in typesContainingRefTypes)
            {
                var refTypeSchema = new Dictionary<string, SchemaColumn>()
                    {
                        { $"{schema.Key}_id", new SchemaColumn { Type = typeof(Guid), Name = $"{schema.Key}_id", IsPrimaryKey = true } },
                        { $"{refType.Name}_id", new SchemaColumn { Type = typeof(Guid), Name = $"{refType.Name}_id", IsPrimaryKey = true } },
                        { $"{refType.Name}_type", new SchemaColumn { Type = typeof(string), Name = $"{refType.Name}_type" } },
                    };

                refType.RefTypeSchema = refTypeSchema;

                var refTypeNameRelationName = $"Relation{schema.Key}{refType.Name}";

                // Avoid duplicate entries.
                if (extraSchemas.ContainsKey(refTypeNameRelationName))
                {
                    continue;
                }


                extraSchemas.Add(refTypeNameRelationName, refTypeSchema);
            }
        }

        // Combine the scheam with the extra ref type schemas.
        schemas = schemas.Concat(extraSchemas).ToDictionary(x => x.Key, x => x.Value);

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
                          InnerObjectName = y.Value.InnerObjectName,
                          RefTypeSchema = y.Value.RefTypeSchema,
                          IsPrimaryKey = y.Value.IsPrimaryKey
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
