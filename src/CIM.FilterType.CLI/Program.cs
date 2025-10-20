using System.CommandLine;
using System.Text.Json;

namespace CIM.FilterType.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CIM Filter Type CLI.");

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

        var includedTypesOption = new Option<List<string>>(
            name: "--included-types",
            description: "The types that should be included in the output."
        )
        { IsRequired = true };

        rootCommand.Add(inputFilePathOption);
        rootCommand.Add(outputFilePathOption);
        rootCommand.Add(includedTypesOption);

        rootCommand.SetHandler(
            async (inputFilePath, outputFilePath, includedTypes) =>
            {
                await ExecuteFilterAsync(inputFilePath, outputFilePath, includedTypes.ToHashSet()).ConfigureAwait(false);
            },
            inputFilePathOption,
            outputFilePathOption,
            includedTypesOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task ExecuteFilterAsync(
        string inputFilePath,
        string outputFilePath,
        HashSet<string> includedTypes)
    {
        var linesAsync = File.ReadLinesAsync(inputFilePath).ConfigureAwait(false);
        using var outputFileStream = new StreamWriter(outputFilePath);

        await foreach (var line in linesAsync.ConfigureAwait(false))
        {
            var objectType =
                JsonDocument.Parse(line).RootElement.GetProperty("$type").GetString()
                ?? throw new InvalidOperationException("Could not get the $type from the line.");

            if (includedTypes.Contains(objectType))
            {
                await outputFileStream.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
    }
}
