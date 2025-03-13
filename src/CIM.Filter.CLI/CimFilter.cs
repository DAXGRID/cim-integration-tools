using CIM.PhysicalNetworkModel;
using System.Diagnostics;
using System.Text.Json;

namespace CIM.Filter.CLI;

internal static class CimFilter
{
    public static async IAsyncEnumerable<string> CimJsonLineFilter(IAsyncEnumerable<string> inputStream, HashSet<Guid> idsToIncludeInOutput)
    {
        await foreach (var line in inputStream.ConfigureAwait(false))
        {
            var mrid = Guid.Parse(
                JsonDocument.Parse(line).RootElement.GetProperty("mRID").GetString()
                  ?? throw new InvalidOperationException("Could not get the mRID from the line.")
            );

            if (idsToIncludeInOutput.Contains(mrid))
            {
                yield return line;
            }
        }
    }

    public static bool BaseVoltageFilter(int baseVoltageLowerBound, int baseVoltageUpperBound, double baseVoltage)
    {
        return baseVoltage >= baseVoltageLowerBound && baseVoltage <= baseVoltageUpperBound;
    }

    public static async Task<HashSet<Guid>> ConductingEquipmentFilterAsync(IAsyncEnumerable<string> jsonLines, Func<Dictionary<string, JsonElement>, bool> filter)
    {
        // Used to lookup each types with their relational structure.
        var typeIdIndex = new Dictionary<string, Dictionary<Guid, List<Guid>>>();

        // Collection for storing all the mrids that has been filtered.
        var idsToIncludeInOutput = new HashSet<Guid>();

        // Collection used to store the related ids.
        var relatedIds = new HashSet<Guid>();

        // To find everything that inherits from ConductingEquipment so we do not need to use CSON deserialize.
        var conductingTypes = FindSubClassesOf<ConductingEquipment>().Select(x => x.Name).ToHashSet();

        await foreach (var line in jsonLines.ConfigureAwait(false))
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)
                ?? throw new InvalidOperationException($"Could not deserialize the line {line}.");

            var mrid = properties["mRID"].GetGuid();
            var type = properties["$type"].GetString() ?? throw new UnreachableException($"Could not get mrid from line: {line}.");

            // In case it is ConductingEquipment we only want the ones in the filter.
            // We handle both cases in this single passthrough to speed up the process.
            // It might make the code a bit uglier, but it is much faster on larger datasets.
            if (conductingTypes.Contains(type))
            {
                if (filter(properties))
                {
                    idsToIncludeInOutput.Add(mrid);
                    relatedIds.Add(mrid);

                    foreach (var x in properties.Values)
                    {
                        if (x.TryGetGuidImpl(out var guid))
                        {
                            relatedIds.Add(guid);
                        }
                    }
                }
            }
            else
            {
                var guids = new List<Guid>();
                foreach (var jsonElement in properties.Values)
                {
                    if (jsonElement.TryGetGuidImpl(out var guid))
                    {
                        guids.Add(guid);
                    }
                }

                if (!typeIdIndex.TryGetValue(type, out var idIndex))
                {
                    idIndex = new Dictionary<Guid, List<Guid>>();
                    typeIdIndex.Add(type, idIndex);
                }

                idIndex.Add(mrid, guids);
            }
        }

        // HashSet to keep track of which types we want to collect.
        var typesToProcess = new HashSet<string>(typeIdIndex.Select(x => x.Key));

        var previousCount = 0;

        // We stop if nothing is added to the output.
        // It means that all relations has been found.
        while (previousCount != idsToIncludeInOutput.Count)
        {
            previousCount = idsToIncludeInOutput.Count;

            foreach (var kvp in typeIdIndex.Where(x => typesToProcess.Contains(x.Key)))
            {
                foreach (var v in kvp.Value)
                {
                    if (relatedIds.Overlaps(v.Value))
                    {
                        idsToIncludeInOutput.Add(v.Key);
                        relatedIds.Add(v.Key);
                        foreach (var relationMrid in v.Value)
                        {
                            relatedIds.Add(relationMrid);
                        }

                        // We do not allow certain types to run multiple times.
                        // Because they will reference Conducting equipment and it will give
                        // us the full network.
                        if (kvp.Key == "Terminal")
                        {
                            typesToProcess.Remove(kvp.Key);
                        }
                    }
                }
            }
        }

        return idsToIncludeInOutput;
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

    private static IEnumerable<Type> FindSubClassesOf<TBaseType>()
    {
        var baseType = typeof(TBaseType);
        var assembly = baseType.Assembly;

        return assembly.GetTypes().Where(t => t.IsSubclassOf(baseType));
    }
}
