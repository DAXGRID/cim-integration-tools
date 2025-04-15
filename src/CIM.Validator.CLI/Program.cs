using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace CIM.Validator.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CIM Validator CLI.");

        var logger = LoggerFactory.Create(nameof(CIM.Validator.CLI));

        logger.LogInformation("Starting CIM Validator.");

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}
