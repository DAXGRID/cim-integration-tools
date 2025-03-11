using CIM;
using CIM.Cson;
using CIM.PhysicalNetworkModel;
using System.Text.Json;

namespace CIM.Filter;

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

        var idsToIncludeInOutput = new HashSet<string>();
        var connectivityNodes = new HashSet<string>();
        var foundTypes = new HashSet<string>();

        var serializer = new CsonSerializer();

        await foreach (var line in File.ReadLinesAsync(inputFilePath))
        {
            var source = serializer.DeserializeObject(line);
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);

            if (source is ConductingEquipment)
            {
                var c = (ConductingEquipment)source;

                if (c.BaseVoltage >= 10000)
                {
                    idsToIncludeInOutput.Add(c.mRID);
                    var filteredProperties = properties.Where(x => x.Key != "mRID").ToList();
                    foreach (var fp in filteredProperties)
                    {
                        if (fp.Value.TryGetGuidImpl(out var idReferenceInner))
                        {
                            idsToIncludeInOutput.Add(idReferenceInner.ToString());
                        }
                    }
                }
            }
        }

        var newInserted = true;
        var counter = 0;
        while (newInserted)
        {
            counter++;
            newInserted = false;
            Console.WriteLine($"Iteration: {counter}. Total found: {idsToIncludeInOutput.Count}");

            var foundTypesInternal = new HashSet<string>();

            await foreach (var line in File.ReadLinesAsync(inputFilePath))
            {
                var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
                var internalType = properties["$type"].ToString();

                if (idsToIncludeInOutput.Contains(properties["mRID"].ToString()))
                {
                    foreach (var property in properties.Where(x => x.Key != "mRID"))
                    {
                        if (property.Value.TryGetGuidImpl(out var idReference))
                        {
                            foundTypesInternal.Add(internalType);
                            if (idsToIncludeInOutput.Add(idReference.ToString()))
                            {
                                newInserted = true;
                            }
                        }
                    }
                }

            }

            foundTypes.UnionWith(foundTypesInternal);
        }

        Console.WriteLine($"The following types was found.");

        foreach (var foundType in foundTypes)
        {
            Console.WriteLine(foundType);
        }

        Console.WriteLine($"Inserting a total of {idsToIncludeInOutput.Count}");

        using var outputFile = new StreamWriter(File.Open(outputFilePath, FileMode.Create));
        await foreach (var line in File.ReadLinesAsync(inputFilePath))
        {
            var source = serializer.DeserializeObject(line);

            if (!idsToIncludeInOutput.Contains(source.mRID))
            {
                continue;
            }

            await outputFile.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
