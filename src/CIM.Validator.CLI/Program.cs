using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CIM.Validator.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CIM Validator CLI.");

        var logger = LoggerFactory.Create(nameof(CIM.Validator.CLI));

        logger.LogInformation("Starting CIM Validator.");

        var validationErrors = new List<ValidationError>
        {
            new ValidationError
            {
                Mrid = Guid.NewGuid(),
                TypeName = "PowerTransformerEnd",
                Code = "NO_RATED_S",
                Description = "ratedS value not set",
                Severity = Severity.Warning
            },
            new ValidationError
            {
                Mrid = Guid.NewGuid(),
                TypeName = "PowerTransformerEnd",
                Code = "NO_RATED_S",
                Description = "ratedS value not set",
                Severity = Severity.Warning
            },
        };

        using (StreamWriter sw = new StreamWriter("./warnings.jsonl"))
        {
            foreach (var validationError in validationErrors)
            {
                await sw.WriteLineAsync(JsonSerializer.Serialize(validationError)).ConfigureAwait(false);
            }
        }

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}

internal enum Severity
{
    Warning,
    Error
}

internal sealed record ValidationError
{
    [JsonPropertyName("$type")]
    public string TypeName { get; private init; } = "ValidationError";

    public required Guid Mrid { get; init; }

    public required Severity Severity { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }
}
