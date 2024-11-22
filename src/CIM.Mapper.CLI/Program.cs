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

        var rootCommand = new RootCommand("CIM Mapper CLI.");
        rootCommand.Add(transformationConfigurationFileOption);
        rootCommand.Add(tranformationSpecificationNameOption);
        rootCommand.Add(forceOption);

        rootCommand.SetHandler(
            (transformationConfigurationFile, tranformationSpecificationName, force) =>
            {
                Console.WriteLine(transformationConfigurationFile);
                Console.WriteLine(tranformationSpecificationName);
                Console.WriteLine(force);
            },
            transformationConfigurationFileOption,
            tranformationSpecificationNameOption,
            forceOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}
