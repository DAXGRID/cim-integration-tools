using System.Text.Json;

namespace CIM.PostgressImporter;

//{"$type":"Point2D","X":540131.199,"Y":6177886.151}
internal sealed record Point2D
{
    public double X { get; init; }
    public double Y { get; init; }
}

internal sealed record GeoJsonField
{
    public required string Type { get; init; }
    public required double[] Coordinates { get; init; }
}

internal sealed record CimType
{
    public required string Name { get; init; }
    public required Dictionary<string, dynamic> Properties { get; init; }
}

internal static class Program
{
    public static void Main()
    {
        using var reader = new StreamReader("/home/notation/Downloads/engum.jsonl");

        var schemas = new Dictionary<string, Dictionary<string, dynamic>>();

        var count = 0;
        string line;
        while ((line = reader.ReadLine()) is not null)
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);

            var typeName = properties["$type"].GetString();

            // We do not want the type indexed.
            properties.Remove("$type");

            foreach (var property in properties)
            {
                if (!schemas.ContainsKey(typeName))
                {
                    schemas.Add(typeName, new());
                }

                var typeSchema = schemas[typeName];
                if (!typeSchema.ContainsKey(property.Key))
                {
                    typeSchema.Add(property.Key, ConvertJsonType((JsonElement)property.Value));
                }
            }

            count++;
        }

        foreach (var schema in schemas)
        {
            Console.WriteLine($">> Schema type: '{schema.Key}' <<");

            foreach (var property in schema.Value)
            {
                Console.WriteLine($"{property.Key}: {property.Value}");
            }

            Console.WriteLine("--------------------------------");
        }

        Console.WriteLine($"Total lines: {count}");
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
                var arrayElementType = ConvertJsonType(jsonElement.Deserialize<List<JsonElement>>()[0]);
                return typeof(ICollection<>).MakeGenericType(arrayElementType);
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
                return typeof(object);
                break;
        }

        return typeof(object);
    }
}

