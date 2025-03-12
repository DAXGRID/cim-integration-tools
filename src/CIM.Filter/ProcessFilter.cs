using CIM.Cson;
using CIM.PhysicalNetworkModel;
using System.Text.Json;

namespace CIM.Filter;

internal static class ProcessFilter
{
    public static async Task ProcessAsync(string inputFilePath, string outputFilePath, int baseVoltageLowerBound, int baseVoltageUpperBound)
    {
        // Used to lookup each types with their relational structure.
        var typeIdIndex = new Dictionary<string, List<CimRelationStructure>>();

        // Collection for storing all the mrids that has been filtered.
        var idsToIncludeInOutput = new HashSet<Guid>();

        // Collection used to store the related ids.
        var relatedIds = new HashSet<Guid>();

        var serializer = new CsonSerializer();

        Console.WriteLine($"Starting processing {inputFilePath}.");

        await foreach (var line in File.ReadLinesAsync(inputFilePath).ConfigureAwait(false))
        {
            var source = serializer.DeserializeObject(line);
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)
                ?? throw new InvalidOperationException($"Could not deserialize the line {line}.");

            // In case it is ConductingEquipment we only want the ones in the filter.
            // We handle both cases in this single passthrough to speed up the process.
            // It might make the code a bit uglier, but it is much faster on larger datasets.
            if (source is ConductingEquipment)
            {
                var c = (ConductingEquipment)source;

                if (c.BaseVoltage >= baseVoltageLowerBound && c.BaseVoltage <= baseVoltageUpperBound)
                {
                    var mrid = Guid.Parse(c.mRID);
                    idsToIncludeInOutput.Add(mrid);
                    relatedIds.Add(mrid);

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
            }
            else
            {
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

                if (!typeIdIndex.TryGetValue(type, out var idIndex))
                {
                    idIndex = new List<CimRelationStructure>();
                    typeIdIndex.Add(type, idIndex);
                }

                idIndex.Add(new CimRelationStructure { Mrid = mrid, Guids = guids });
            }
        }

        var typesToProcess = new HashSet<string>(typeIdIndex.Select(x => x.Key));
        var iteration = 0;
        var previousCount = 0;

        // We stop if nothing is added to the output.
        // It means that all relations has been found.
        while (previousCount != idsToIncludeInOutput.Count)
        {
            previousCount = idsToIncludeInOutput.Count;
            iteration++;

            Console.WriteLine($"Total count of types {typesToProcess.Count}. Total count of included '{idsToIncludeInOutput.Count}'.");

            foreach (var kvp in typeIdIndex.Where(x => typesToProcess.Contains(x.Key)))
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

                        typesToProcess.Remove(kvp.Key);
                    }
                }
            }
        }

        Console.WriteLine($"Writing a total of {idsToIncludeInOutput.Count} CIM objects.");
        using var outputFile = new StreamWriter(File.Open(outputFilePath, FileMode.Create));
        await foreach (var line in File.ReadLinesAsync(inputFilePath).ConfigureAwait(false))
        {
            var mrid = Guid.Parse(
                JsonDocument.Parse(line).RootElement.GetProperty("mRID")!.GetString()
                ?? throw new InvalidOperationException("Could not get the mRID from the line."));

            if (idsToIncludeInOutput.Contains(mrid))
            {
                await outputFile.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
    }

    // This is done because the default implementation throws an exception if it
    // is not able to parse the Guid. To improve performance we make our own implementation
    // so we avoid the performance penalty of throwing exceptions.
    private static bool TryGetGuidImpl(this JsonElement element, out Guid guid)
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
}

internal sealed record CimRelationStructure
{
    public required Guid Mrid { get; init; }
    public required HashSet<Guid> Guids { get; init; }
}
