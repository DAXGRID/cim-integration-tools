using CIM.Cson;
using System.CommandLine;

namespace CIM.PowerFactoryExporter.CLI;

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

        var modelIdOption = new Option<Guid>(
            name: "--model-id",
            description: "The model ID is a guid to uniquely identify the model."
        )
        { IsRequired = true };

        var organizationNameOption = new Option<string>(
            name: "--organization-name",
            description: "The organization name."
        )
        { IsRequired = true };

        var rootCommand = new RootCommand("CIM Powerfactory exporter CLI.");
        rootCommand.AddOption(inputFileOption);
        rootCommand.AddOption(outputFileOption);
        rootCommand.AddOption(modelIdOption);
        rootCommand.AddOption(organizationNameOption);

        rootCommand.SetHandler(
            (inputFilePath, outputFilePath, modelId, organizationName) =>
            {
                Execute(inputFilePath, outputFilePath, modelId, organizationName);
            },
            inputFileOption,
            outputFileOption,
            modelIdOption,
            organizationNameOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static void Execute(string inputFilePath, string outputFilePath, Guid modelId, string organizationName)
    {
        var serializer = new CsonSerializer();

        var outputPathDirectoryName = Path.GetDirectoryName(inputFilePath)
            ?? throw new ArgumentException($"Could not extract directory name from: '{inputFilePath}'");
        var outputPathFileName = Path.GetFileName(inputFilePath)
            ?? throw new ArgumentException($"Could not extract file name from: '{inputFilePath}'");

        var cimObjects = serializer.DeserializeObjects(File.OpenRead(inputFilePath));
        var _ = new CimArchiveWriter(cimObjects, outputPathDirectoryName, outputPathFileName, modelId, organizationName);
     }
}
