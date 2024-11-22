using System.CommandLine;

namespace CIM.Mapper.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var transformationConfigurationFileOption = new Option<string?>(
            name: "--transformation-configuration-file",
            description: "The file path to the file that contains the transformation settings.."
        ) { IsRequired = true };

        var tranformationSpecificationNameOption = new Option<string?>(
            name: "--transformation-specification-name",
            description: "The file path to the file that contains the transformation settings.."
        ) { IsRequired = true };

        var rootCommand = new RootCommand("CIM Mapper CLI.");

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}
