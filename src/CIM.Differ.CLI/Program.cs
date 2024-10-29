using CIM.Cson;
using CIM.PhysicalNetworkModel;
using System.CommandLine;

namespace CIM.Differ.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var previousStateFileOption = new Option<string?>(
            name: "--previous-state-file",
            description: "The path to the previous state file. Example: './my-previous-state-file.jsonl'."
        ) { IsRequired = false };

        var newStateFileOption = new Option<string>(
            name: "--new-state-file",
            description: "The path to the new state file. Example: './my-new-state-file.jsonl'."
        ) {  IsRequired = true };

        var outputFileOption = new Option<string>(
            name: "--output-file",
            description: "The path and filename of the output file. Example: '/home/notation/my-new-outputfile.jsonl'."
        ) {  IsRequired = true };

        var rootCommand = new RootCommand("CIM Differ CLI.");
        rootCommand.Add(previousStateFileOption);
        rootCommand.Add(newStateFileOption);
        rootCommand.Add(outputFileOption);

        rootCommand.SetHandler(async (previousStateFilePath, newStateFilePath, outputFilePath) =>
        {
            await ProcessDiffAsync(previousStateFilePath, newStateFilePath, outputFilePath).ConfigureAwait(false);
        },
        previousStateFileOption, newStateFileOption, outputFileOption);

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task ProcessDiffAsync(string? previousStateFilePath, string newStateFilePath, string outputFilePath)
    {
        var serializer = new CsonSerializer();
        var differ = new CimDiffer();

        var firstFileIdentifiedObjects = previousStateFilePath is not null
            ? ReadIdentifiedObjectFile(serializer, previousStateFilePath)
            : new List<IdentifiedObject>();

        var secondFileIdentifiedObjects = ReadIdentifiedObjectFile(serializer, newStateFilePath);

        using (var destination = File.Open(outputFilePath, FileMode.CreateNew))
        {
            using (var source = serializer.SerializeObjects(differ.GetDiff(firstFileIdentifiedObjects, secondFileIdentifiedObjects)))
            {
                await source.CopyToAsync(destination).ConfigureAwait(false);
            }
        }
    }

    private static IEnumerable<IdentifiedObject> ReadIdentifiedObjectFile(CsonSerializer serializer, string filePath)
    {
        using (var inputStream = File.OpenRead(filePath))
        {
            return serializer.DeserializeObjects(inputStream);
        }
    }
}
