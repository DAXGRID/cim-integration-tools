using System.Text.Json;
using System.Threading.Channels;

namespace CIM.PostgresImporter.CLI;

internal static class CimReader
{
    public static async Task ReadAsync(
        string dataFilePath,
        Dictionary<string, Channel<Dictionary<string, JsonElement>>> importChannelsLookup)
    {
        using var jsonReader = new StreamReader(dataFilePath);
        string? line;
        while ((line = await jsonReader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line)
                ?? throw new ArgumentException($"Could not deserialize the line: '{line}'");

            string typeName;
            if (properties.TryGetValue("$type", out JsonElement typeNameProperty))
            {
                typeName = typeNameProperty.GetString()
                    ?? throw new ArgumentException("Could convert $type to a string, something is wrong with the data.");
            }
            else
            {
                throw new ArgumentException("Could not find $type on the read in line, something is wrong with the data.");
            }

            // We do not want the type indexed.
            properties.Remove("$type");

            await importChannelsLookup[typeName].Writer.WriteAsync(properties).ConfigureAwait(false);
        }
    }
}
