using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;

namespace CIM.PreValidator.CLI;

internal static class LoggerFactory
{
    public static Microsoft.Extensions.Logging.ILogger Create(string categoryName)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter())
            .CreateLogger();

        using var factory = new SerilogLoggerFactory();
        return factory.CreateLogger(categoryName);
    }
}
