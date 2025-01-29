using CIM.Cson;
using CIM.PhysicalNetworkModel.FeederInfo;
using CIM.PhysicalNetworkModel.Traversal.Internals;
using System.CommandLine;

namespace CIM.TopologyProcessor.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var inputFileOption = new Option<string>(
            name: "--input-file",
            description: "The file path to the input file."
        )
        { IsRequired = true };

        var outputFileOption = new Option<string>(
            name: "--output-file",
            description: "The file path to the output file."
        )
        { IsRequired = true };

        var rootCommand = new RootCommand("CIM Topology Processor CLI.");
        rootCommand.AddOption(inputFileOption);
        rootCommand.AddOption(outputFileOption);

        rootCommand.SetHandler(
            async (inputFilePath, outputFilePath) =>
            {
                await ProcessFileAsync(inputFilePath, outputFilePath).ConfigureAwait(false);
            },
            inputFileOption,
            outputFileOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task ProcessFileAsync(string inputFilePath, string outputFilePath)
    {
        if (!File.Exists(inputFilePath))
        {
            throw new InvalidOperationException($"The file does not exist '{inputFilePath}'.");
        }

        var serializer = new CsonSerializer();

        using var fileStream = File.OpenRead(inputFilePath);
        var cimObjects = serializer.DeserializeObjects(fileStream);
        var cimContext = new InMemCimContext(cimObjects);

        var feederInfoContext = new FeederInfoContext(cimContext);
        feederInfoContext.CreateFeederObjects();
        var feederInfoCreator = new FlatFeederInfoCreator();
        var flatFeederObjects = feederInfoCreator
            .CreateFeederInfos(cimContext, feederInfoContext);

        using var destination = File.Open(outputFilePath, FileMode.Create);
        using var source = serializer.SerializeObjects(flatFeederObjects);
        await source.CopyToAsync(destination).ConfigureAwait(false);
    }
}
