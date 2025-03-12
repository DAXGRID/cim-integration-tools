using System.Text.Json;

namespace CIM.Filter;

internal static class Program
{
    public static async Task Main()
    {
        const string inputFilePath = "./mapper_output.jsonl";
        const string outputFilePath = "./filter_output.jsonl";
        const int baseVoltageLowerBound = 10000;
        const int baseVoltageUppperBound = int.MaxValue;

        Console.WriteLine($"Filtering base voltage lower bound: '{baseVoltageLowerBound}', upper bound: '{baseVoltageUppperBound}'.");
        var idsToIncludeInOutput = await BaseVoltageFilter
            .FilterAsync(
                File.ReadLinesAsync(inputFilePath),
                baseVoltageLowerBound,
                baseVoltageUppperBound)
            .ConfigureAwait(false);

        Console.WriteLine($"Writing a total of {idsToIncludeInOutput.Count} CIM objects to {outputFilePath}.");
        using var outputFile = new StreamWriter(File.Open(outputFilePath, FileMode.Create));
        await foreach (var line in FilterOutputAsync(File.ReadLinesAsync(inputFilePath), idsToIncludeInOutput).ConfigureAwait(false))
        {
            await outputFile.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<string> FilterOutputAsync(IAsyncEnumerable<string> inputStream, HashSet<Guid> idsToIncludeInOutput)
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
}
