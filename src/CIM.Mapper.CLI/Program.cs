using System.CommandLine;

namespace CIM.Mapper.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CIM Mapper CLI.");

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}
