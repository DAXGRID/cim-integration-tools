using Microsoft.Extensions.Logging;

namespace CIM.Filter;

internal static class Program
{
    public static async Task Main()
    {
        const string inputFilePath = "./mapper_output.jsonl";
        const string outputFilePath = "./filter_output.jsonl";
        const int baseVoltageLowerBound = 10000;
        const int baseVoltageUppperBound = int.MaxValue;

        var logger = LoggerFactory.Create(nameof(CIM.Filter.Program));

        await ProcessFilterAsync(
            logger,
            inputFilePath,
            outputFilePath,
            baseVoltageLowerBound,
            baseVoltageUppperBound
        ).ConfigureAwait(false);
    }

    private static async Task ProcessFilterAsync(
        ILogger logger,
        string inputFilePath,
        string outputFilePath,
        int baseVoltageLowerBound,
        int baseVoltageUppperBound)
    {
        logger.LogInformation($"Filtering base voltage lower bound: '{baseVoltageLowerBound}', upper bound: '{baseVoltageUppperBound}'.");
        var idsToIncludeInOutput = await CimFilter
            .BaseVoltageFilterAsync(
                File.ReadLinesAsync(inputFilePath),
                baseVoltageLowerBound,
                baseVoltageUppperBound)
            .ConfigureAwait(false);

        logger.LogInformation($"Writing a total of {idsToIncludeInOutput.Count} CIM objects to {outputFilePath}.");
        using var outputFile = new StreamWriter(File.Open(outputFilePath, FileMode.Create));
        await foreach (var line in CimFilter.CimJsonLineFilter(File.ReadLinesAsync(inputFilePath), idsToIncludeInOutput).ConfigureAwait(false))
        {
            await outputFile.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
