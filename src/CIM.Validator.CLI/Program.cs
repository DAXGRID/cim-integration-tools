using CIM.Cson;
using CIM.PhysicalNetworkModel;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.CommandLine;
using System.Text.Json;

namespace CIM.Validator.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        const string inputFile = "./mapper_output.jsonl";
        const string outputFile = "./warnings.jsonl";

        var rootCommand = new RootCommand("CIM Validator CLI.");

        var logger = LoggerFactory.Create(nameof(CIM.Validator.CLI));

        logger.LogInformation("Starting CIM Validator.");

        var (conductingEquipments, terminals) = await LoadCimAsync(inputFile).ConfigureAwait(false);

        var terminalsByConductingEquipment = terminals
            .GroupBy(x => x.ConductingEquipment.@ref)
            .ToFrozenDictionary(x => x.Key, x => x.ToArray());

        var validationErrors = new ConcurrentBag<ValidationError>();

        // Validates conducting equipment
        Parallel.ForEach(conductingEquipments, (conductingEquipment) =>
        {
            var conductingEquipmentTerminals = terminalsByConductingEquipment.GetValueOrDefault(conductingEquipment.mRID) ?? Array.Empty<Terminal>();

            var validations = new List<Func<ValidationError?>>
            {
                () => ConductingEquipmentValidation.BaseVoltage(conductingEquipment),
                () => ConductingEquipmentValidation.NumberOfTerminals(conductingEquipment, conductingEquipmentTerminals),
                () => ConductingEquipmentValidation.ReferencedTerminalSequenceNumber(conductingEquipment, conductingEquipmentTerminals),
                () => ConductingEquipmentValidation.ReferencedTerminalConnectivityNode(conductingEquipment, conductingEquipmentTerminals)
            };

            foreach (var validate in validations)
            {
                var validationError = validate();
                if (validationError is not null)
                {
                    validationErrors.Add(validationError);
                }
            }
        });

        // Validates terminals equipment
        Parallel.ForEach(terminals, (terminal) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => TerminalValidation.ConductingEquipmentReference(terminal)
            };

            foreach (var validate in validations)
            {
                var validationError = validate();
                if (validationError is not null)
                {
                    validationErrors.Add(validationError);
                }
            }
        });

        await WriteValidationErrors(outputFile, validationErrors).ConfigureAwait(false);

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task WriteValidationErrors(string outputFile, ConcurrentBag<ValidationError> validationErrors)
    {
        using (var sw = new StreamWriter(outputFile))
        {
            foreach (var validationError in validationErrors)
            {
                await sw.WriteLineAsync(JsonSerializer.Serialize(validationError)).ConfigureAwait(false);
            }
        }
    }

    private static async Task<(FrozenSet<ConductingEquipment>, FrozenSet<Terminal>)> LoadCimAsync(string inputFile)
    {
        var conductingEquipments = new List<ConductingEquipment>();
        var terminals = new List<Terminal>();

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

        return (conductingEquipments.ToFrozenSet(), terminals.ToFrozenSet());
    }
}
