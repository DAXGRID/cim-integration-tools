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
        const string inputFile = "/home/notation/Downloads/mapper_output.jsonl";
        const string outputFile = "./warnings.jsonl";

        var rootCommand = new RootCommand("CIM Validator CLI.");

        var logger = LoggerFactory.Create(nameof(CIM.Validator.CLI));

        logger.LogInformation("Starting CIM Validator.");

        var validationsConductingEquipment = new List<Func<ConductingEquipment, ValidationError?>>
        {
            Validation.BaseVoltage,
            Validation.WrongNumberOfTerminals
        };

        var conductingEquipments = new List<ConductingEquipment>();
        var terminals = new List<Terminal>();

        var validationErrors = new List<ValidationError>();

        var serializer = new CsonSerializer();
        await foreach (var line in File.ReadLinesAsync(inputFile).ConfigureAwait(false))
        {
            var identifiedObject = serializer.DeserializeObject(line);

            if (identifiedObject is ConductingEquipment)
            {
                conductingEquipments.Add((ConductingEquipment)identifiedObject);
            }

            if (identifiedObject is Terminal)
            {
                terminals.Add((Terminal)identifiedObject);
            }
        }

        foreach (var conductingEquipment in conductingEquipments)
        {
            foreach (var validate in validationsConductingEquipment)
            {
                var validationError = validate(conductingEquipment);
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
