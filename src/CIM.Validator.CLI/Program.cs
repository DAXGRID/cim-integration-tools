using CIM.Cson;
using CIM.PhysicalNetworkModel;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Text.Json;

namespace CIM.Validator.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        const string inputFile = "";
        const string outputFile = "";

        var rootCommand = new RootCommand("CIM Validator CLI.");

        var logger = LoggerFactory.Create(nameof(CIM.Validator.CLI));

        logger.LogInformation("Starting CIM Validator.");


        var validations = new List<Func<IdentifiedObject, ValidationError?>>();
        validations.Add(Validation.BaseVoltageValidation);

        var validationErrors = new List<ValidationError>();

        var serializer = new CsonSerializer();
        await foreach (var line in File.ReadLinesAsync(inputFile).ConfigureAwait(false))
        {
            var x = serializer.DeserializeObject(line);
            foreach (var validate in validations)
            {
                var validationError = validate(x);
                if (validationError is not null)
                {
                    validationErrors.Add(validationError);
                }
            }
        }

        using (var sw = new StreamWriter(outputFile))
        {
            foreach (var validationError in validationErrors)
            {
                await sw.WriteLineAsync(JsonSerializer.Serialize(validationError)).ConfigureAwait(false);
            }
        }

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}
