using DAX.IO;
using DAX.IO.CIM;
using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.IO.Transformers;
using DAX.IO.Writers;
using System.CommandLine;

namespace CIM.Mapper.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var transformationConfigurationFileOption = new Option<string>(
            name: "--transformation-configuration-file",
            description: "The file path to the file that contains the transformation settings.."
        )
        { IsRequired = true };

        var tranformationSpecificationNameOption = new Option<string>(
            name: "--transformation-specification-name",
            description: "The file path to the file that contains the transformation settings.."
        )
        { IsRequired = true };

        var forceOption = new Option<bool>(
            name: "--force",
            description: "Ignore errors."
        )
        { IsRequired = false };

        var debugOption = new Option<bool>(
            name: "--debug",
            description: "Run the program in debug mode."
        )
        { IsRequired = false };

        var serializerOption = new Option<string?>(
            name: "--serializer",
            description: "The name of the serializer."
        )
        { IsRequired = false };

        var outputFilePathOption = new Option<string?>(
            name: "--output",
            description: "The output file path."
        )
        { IsRequired = false };

        var rootCommand = new RootCommand("CIM Mapper CLI.");
        rootCommand.Add(transformationConfigurationFileOption);
        rootCommand.Add(tranformationSpecificationNameOption);
        rootCommand.Add(forceOption);
        rootCommand.Add(debugOption);
        rootCommand.Add(serializerOption);
        rootCommand.Add(outputFilePathOption);

        rootCommand.SetHandler(
            (transformationConfigurationFile, tranformationSpecificationName, force, debugMode, serializerName, outputFilePath) =>
            {
                // Check if transformation configuration file exists
                if (!File.Exists(transformationConfigurationFile))
                {
                    throw new ArgumentException(
                        $"Cannot find transformation configuration file: '{transformationConfigurationFile}'.");
                }

                var config = new TransformationConfig().LoadFromFile(transformationConfigurationFile);
                var transSpecNameSplit = tranformationSpecificationName.Split(',');

                foreach (string transSpecName in transSpecNameSplit)
                {
                    if (config.GetTransformationSpecification(transSpecName.Trim()) == null)
                    {
                        throw new ArgumentException(
                            $"Cannot find transformation specification name: '{transSpecName.Trim()}' in transformation specification configuration file: '{transformationConfigurationFile}'.");
                    }
                }

                foreach (string transSpecName in transSpecNameSplit)
                {
                    var transSpec = config.GetTransformationSpecification(transSpecName.Trim());
                    var transformer = config.InitializeDataTransformer(transSpecName);
                    var guide = transformer.Simulate();

                    if (debugMode)
                    {
                        Console.WriteLine(guide.TextReport());
                    }

                    if (guide.ProblemsFound > 0)
                    {
                        Console.Error.WriteLine(guide.TextReport());

                        if (!force)
                            Console.Error.WriteLine("Error found in mapping (se log). Transformation {transSpecName} is terminated! Please correct mapping and try again.");
                        else
                        {
                            Console.Error.WriteLine("Error found in mapping (se log). But will attempt to transfer data because of force parameter.");
                            transformer.TransferData();
                            CheckForCIMProcessing(serializerName, outputFilePath, config, transformer);
                        }
                    }
                    else
                    {
                        transformer.TransferData();
                        CheckForCIMProcessing(serializerName, outputFilePath, config, transformer);
                    }
                }
            },
            transformationConfigurationFileOption,
            tranformationSpecificationNameOption,
            forceOption,
            debugOption,
            serializerOption,
            outputFilePathOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static void CheckForCIMProcessing(
        string? serializerName,
        string? outputFileName,
        TransformationConfig config,
        DataTransformer transformer)
    {
        if (config.TransformationSpecifications != null && transformer != null)
        {
            var writer = transformer.GetFirstDataWriter() as CIMGraphWriter;

            if (writer != null)
            {
                var graph = writer.GetCIMGraph();

                if (serializerName is not null && outputFileName is not null)
                {
                    Console.WriteLine($"Running '{serializerName}' serializer - writing to: '{outputFileName}'.");

                    var serializer = config.InitializeSerializer(serializerName);

                    var compressedData = ((IDAXSerializeable)serializer).Serialize(CIMMetaDataManager.Repository, graph.CIMObjects, graph);

                    var fileStream = new System.IO.FileStream(outputFileName, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    fileStream.Write(compressedData, 0, compressedData.Length);
                    fileStream.Close();
                }
            }
        }
    }
}
