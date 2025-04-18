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

        var (conductingEquipments, terminals, powerTransformerEnds, equipmentContainers, currentTransformers) = await LoadCimAsync(inputFile).ConfigureAwait(false);

        var terminalsByConductingEquipment = terminals
            .GroupBy(x => x.ConductingEquipment.@ref)
            .ToFrozenDictionary(x => x.Key, x => x.ToArray());

        var powerTransformerEndsByConductingEquipment = powerTransformerEnds
            .GroupBy(x => x.PowerTransformer.@ref)
            .ToFrozenDictionary(x => x.Key, x => x.ToArray());

        var equipmentContainersByMrid = equipmentContainers.ToFrozenDictionary(x => Guid.Parse(x.mRID), x => x);

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
                () => ConductingEquipmentValidation.ReferencedTerminalConnectivityNode(conductingEquipment, conductingEquipmentTerminals),
                () => ConductingEquipmentValidation.EquipmentContainerRelation(conductingEquipment),
                () => ConductingEquipmentValidation.EquipmentContainerCorrectType(
                    conductingEquipment,
                    equipmentContainersByMrid.TryGetValue(
                        conductingEquipment.EquipmentContainer?.@ref is not null ? Guid.Parse(conductingEquipment.EquipmentContainer.@ref) : Guid.Empty,
                        out var equipmentContainer) ? equipmentContainer : null)
            };

            if (conductingEquipment is PowerTransformer)
            {
                validations.Add(
                    () => PowerTransformerValidation.PowerTransformerEndPerTerminal(
                        (PowerTransformer)conductingEquipment,
                        terminalsByConductingEquipment[conductingEquipment.mRID],
                        powerTransformerEndsByConductingEquipment[conductingEquipment.mRID]));

                validations.Add(
                    () => PowerTransformerValidation.PowerTransformerEndNumberMatchesTerminalNumber(
                        (PowerTransformer)conductingEquipment,
                        terminalsByConductingEquipment[conductingEquipment.mRID],
                        powerTransformerEndsByConductingEquipment[conductingEquipment.mRID]));
            }

            foreach (var validate in validations)
            {
                var validationError = validate();
                if (validationError is not null)
                {
                    validationErrors.Add(validationError);
                }
            }
        });

        var conductingEquipmentLookup = conductingEquipments.Select(x => Guid.Parse(x.mRID)).ToFrozenSet();

        // Validates terminals equipment
        Parallel.ForEach(terminals, (terminal) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => TerminalValidation.ConductingEquipmentReferenceId(terminal),
                () => TerminalValidation.ConductingEquipmentReferenceExist(terminal, conductingEquipmentLookup),
                () => TerminalValidation.SequenceNumberRequired(terminal),
                () => TerminalValidation.SequenceNumberValidValue(terminal),
                () => TerminalValidation.PhaseRequired(terminal),
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

        // Validate current transformers.
        Parallel.ForEach(currentTransformers, (currentTransformer) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => CurrentTransformerValidation.ValidateEquipmentContainerType(
                    currentTransformer,
                    equipmentContainersByMrid.TryGetValue(
                        currentTransformer.EquipmentContainer?.@ref is not null ? Guid.Parse(currentTransformer.EquipmentContainer.@ref) : Guid.Empty,
                        out var equipmentContainer) ? equipmentContainer : null)
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

    private static async Task<(
        FrozenSet<ConductingEquipment>,
        FrozenSet<Terminal>,
        FrozenSet<PowerTransformerEnd>,
        FrozenSet<EquipmentContainer>,
        FrozenSet<CurrentTransformer>)> LoadCimAsync(string inputFile)
    {
        var conductingEquipments = new List<ConductingEquipment>();
        var terminals = new List<Terminal>();
        var powerTransformerEnds = new List<PowerTransformerEnd>();
        var equipmentContainers = new List<EquipmentContainer>();
        var currentTransformers = new List<CurrentTransformer>();

        var serializer = new CsonSerializer();
        await foreach (var line in File.ReadLinesAsync(inputFile).ConfigureAwait(false))
        {
            var identifiedObject = serializer.DeserializeObject(line);

            if (identifiedObject is ConductingEquipment)
            {
                conductingEquipments.Add((ConductingEquipment)identifiedObject);
            }
            else if (identifiedObject is Terminal)
            {
                terminals.Add((Terminal)identifiedObject);
            }
            else if (identifiedObject is PowerTransformerEnd)
            {
                powerTransformerEnds.Add((PowerTransformerEnd)identifiedObject);
            }
            else if (identifiedObject is EquipmentContainer)
            {
                equipmentContainers.Add((EquipmentContainer)identifiedObject);
            }
            else if (identifiedObject is CurrentTransformer)
            {
                currentTransformers.Add((CurrentTransformer)identifiedObject);
            }
        }

        return (
            conductingEquipments.ToFrozenSet(),
            terminals.ToFrozenSet(),
            powerTransformerEnds.ToFrozenSet(),
            equipmentContainers.ToFrozenSet(),
            currentTransformers.ToFrozenSet()
        );
    }
}
