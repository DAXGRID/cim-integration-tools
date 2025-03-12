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

        var idsToIncludeInOutput = await ProcessFilter
            .ProcessAsync(
                File.ReadLinesAsync(inputFilePath),
                baseVoltageLowerBound,
                baseVoltageUppperBound)
            .ConfigureAwait(false);

        await WriteOutputAsync(inputFilePath, outputFilePath, idsToIncludeInOutput).ConfigureAwait(false);
    }

    private static async Task WriteOutputAsync(string inputFilePath, string outputFilePath, HashSet<Guid> idsToIncludeInOutput)
    {
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
}
