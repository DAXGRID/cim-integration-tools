using System.CommandLine;

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

        var includedTypes = new Option<string[]>(
            name: "--included-types",
            description: "The types that should be included in the output."
        )
        { IsRequired = true };

        rootCommand.Add(inputFilePathOption);
        rootCommand.Add(outputFilePathOption);
        rootCommand.Add(includedTypes);

        rootCommand.SetHandler(
            async (inputFilePath, outputFilePath, includedTypes) =>
            {
                await Task.Delay(1000).ConfigureAwait(false);
            },
            inputFilePathOption,
            outputFilePathOption,
            includedTypes
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}
