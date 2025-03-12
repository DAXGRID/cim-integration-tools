using CIM.Cson;
using CIM.PhysicalNetworkModel;
using System.Text.Json;

namespace CIM.Filter;

internal sealed record TestType
{
    public required Guid Mrid { get; init; }
    public required HashSet<Guid> Guids { get; init; }
}

internal static class Program
{
    public static bool TryGetGuidImpl(this JsonElement element, out Guid guid)
    {
        guid = Guid.Empty;

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var stringValue = element.GetString();

        if (string.IsNullOrEmpty(stringValue))
        {
            return false;
        }

        return Guid.TryParse(stringValue, out guid);
    }

    public static async Task Main()
    {
        const string inputFilePath = "./mapper_output.jsonl";
        const string outputFilePath = "./filter_output.jsonl";

        var typeIdIndex = new Dictionary<string, List<TestType>>();
        var idsToIncludeInOutput = new HashSet<Guid>();
        var relatedIds = new HashSet<Guid>();

        var serializer = new CsonSerializer();

        // Build collection.
        await foreach (var line in File.ReadLinesAsync(inputFilePath).ConfigureAwait(false))
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)
                ?? throw new InvalidOperationException($"Could not deserialize the line {line}.");

            var type = properties["$type"].ToString();
            var mrid = Guid.Parse(properties["mRID"].ToString());

            var guids = new HashSet<Guid>();
            foreach (var x in properties.Values)
            {
                if (x.TryGetGuidImpl(out var guid))
                {
                    guids.Add(guid);
                }
            }

            if (!typeIdIndex.ContainsKey(type))
            {
                typeIdIndex.Add(type, new());
            }

            typeIdIndex[type].Add(new TestType { Mrid = mrid, Guids = guids });
        }

        var conductingTypes = new HashSet<string>();

        await foreach (var line in File.ReadLinesAsync(inputFilePath).ConfigureAwait(false))
        {
            var source = serializer.DeserializeObject(line);
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)
                ?? throw new InvalidOperationException($"Could not deserialize the line {line}.");

            var type = properties["$type"].ToString();

            if (source is ConductingEquipment)
            {
                var c = (ConductingEquipment)source;

                if (c.BaseVoltage < 10000)
                {
                    idsToIncludeInOutput.Add(Guid.Parse(c.mRID));
                    relatedIds.Add(Guid.Parse(c.mRID));

                    var guids = new HashSet<Guid>();
                    foreach (var x in properties.Values)
                    {
                        if (x.TryGetGuidImpl(out var guid))
                        {
                            guids.Add(guid);
                        }
                    }

                    foreach (var x in guids)
                    {
                        relatedIds.Add(x);
                    }
                }

                conductingTypes.Add(type);
            }
        }

        foreach (var conductingType in conductingTypes)
        {
            typeIdIndex.Remove(conductingType);
        }

        var levelOne = new HashSet<string>(typeIdIndex.Select(x => x.Key));

        var iteration = 0;
        var previousCount = 0;

        while (previousCount != idsToIncludeInOutput.Count)
        {
            previousCount = idsToIncludeInOutput.Count;
            iteration++;

            Console.WriteLine($"Total count of types {levelOne.Count}. Total count of included {idsToIncludeInOutput.Count}");

            foreach (var kvp in typeIdIndex.Where(x => levelOne.Contains(x.Key)))
            {
                foreach (var v in kvp.Value)
                {
                    if (relatedIds.Overlaps(v.Guids))
                    {
                        idsToIncludeInOutput.Add(v.Mrid);
                        relatedIds.Add(v.Mrid);
                        foreach (var x in v.Guids)
                        {
                            relatedIds.Add(x);
                        }

                        levelOne.Remove(kvp.Key);
                    }
                }
            }
        }

        using var outputFile = new StreamWriter(File.Open(outputFilePath, FileMode.Create));
        await foreach (var line in File.ReadLinesAsync(inputFilePath).ConfigureAwait(false))
        {
            // This is done for improved performance.
            var mrid = Guid.Parse(JsonDocument.Parse(line).RootElement.GetProperty("mRID").GetString());

            if (idsToIncludeInOutput.Contains(mrid))
            {
                await outputFile.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
    }
}
