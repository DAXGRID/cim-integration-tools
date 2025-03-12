using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CIM.Filter.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CIM Filter CLI.");

        var inputFilePathOption = new Option<string>(
            name: "--input-file-path",
            description: "The path to the input file, example: /home/user/my_input_file.jsonl."
        )
        { IsRequired = true };

        var outputFilePathOption = new Option<string>(
            name: "--output-file-path",
            description: "The path to the output file, example: /home/user/my_output_file.jsonl."
        )
        { IsRequired = true };

        var baseVoltageLowerBoundOption = new Option<int?>(
            name: "--base-voltage-lower-bound",
            description: "Example 400."
        )
        { IsRequired = false };

        var baseVoltageUpperBoundOption = new Option<int?>(
            name: "--base-voltage-upper-bound",
            description: "Example 10000."
        )
        { IsRequired = false };

        rootCommand.Add(inputFilePathOption);
        rootCommand.Add(outputFilePathOption);
        rootCommand.Add(baseVoltageLowerBoundOption);
        rootCommand.Add(baseVoltageUpperBoundOption);

        var logger = LoggerFactory.Create(nameof(CIM.Filter.CLI.Program));

        rootCommand.SetHandler(
            async (inputFilePath, outputFilePath, baseVoltageLowerBound, baseVoltageUppperBound) =>
            {
                await ProcessFilterAsync(
                    logger,
                    inputFilePath,
                    outputFilePath,
                    baseVoltageLowerBound ?? 0,
                    baseVoltageUppperBound ?? int.MaxValue
                ).ConfigureAwait(false);
            },
            inputFilePathOption,
            outputFilePathOption,
            baseVoltageLowerBoundOption,
            baseVoltageUpperBoundOption);

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
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
