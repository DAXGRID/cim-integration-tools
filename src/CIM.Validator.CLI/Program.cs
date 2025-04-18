using CIM.Cson;
using CIM.PhysicalNetworkModel;
using Microsoft.Extensions.Logging;
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

        var (conductingEquipments,
             terminals,
             powerTransformerEnds,
             equipmentContainers,
             currentTransformers,
             faultIndicators) = await LoadCimAsync(inputFile).ConfigureAwait(false);

        var terminalsByConductingEquipment = terminals
            .GroupBy(x => x.ConductingEquipment.@ref)
            .ToFrozenDictionary(x => x.Key, x => x.ToArray());

        var powerTransformerEndsByConductingEquipment = powerTransformerEnds
            .GroupBy(x => x.PowerTransformer.@ref)
            .ToFrozenDictionary(x => x.Key, x => x.ToArray());

        var equipmentContainersByMrid = equipmentContainers.ToFrozenDictionary(x => Guid.Parse(x.mRID), x => x);

        var conductionEquipmentErrors = conductingEquipments.AsParallel().SelectMany(conductingEquipment =>
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

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        var conductingEquipmentLookup = conductingEquipments.Select(x => Guid.Parse(x.mRID)).ToFrozenSet();

        // Validates terminals equipment
        var terminalValidationErrors = terminals.AsParallel().SelectMany((terminal) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => TerminalValidation.ConductingEquipmentReferenceId(terminal),
                () => TerminalValidation.ConductingEquipmentReferenceExist(terminal, conductingEquipmentLookup),
                () => TerminalValidation.SequenceNumberRequired(terminal),
                () => TerminalValidation.SequenceNumberValidValue(terminal),
                () => TerminalValidation.PhaseRequired(terminal),
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate equipment containers.
        var equipmentContainerValidationErrors = equipmentContainers.AsParallel().SelectMany((equipmentContainer) =>
        {
            var validations = new List<Func<ValidationError?>>();

            if (equipmentContainer is Bay)
            {
                var bay = (Bay)equipmentContainer;
                validations.Add(() => EquipmentContainerValidation.EquipmentContainerCorrectType(
                    bay,
                    equipmentContainersByMrid.TryGetValue(bay.VoltageLevel?.@ref is not null ? Guid.Parse(bay.VoltageLevel.@ref) : Guid.Empty, out var parentEquipmentContainer)
                    ? parentEquipmentContainer : null));
            }
            else if (equipmentContainer is VoltageLevel)
            {
                var voltageLevel = (VoltageLevel)equipmentContainer;
                validations.Add(() => EquipmentContainerValidation.EquipmentContainerCorrectType(
                    voltageLevel,
                    equipmentContainersByMrid.TryGetValue(voltageLevel.EquipmentContainer1?.@ref is not null ? Guid.Parse(voltageLevel.EquipmentContainer1.@ref) : Guid.Empty, out var parentEquipmentContainer)
                    ? parentEquipmentContainer : null));
            }

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate current transformers.
        var currentTransformerValidationErrors = currentTransformers.AsParallel().SelectMany((currentTransformer) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => CurrentTransformerValidation.ValidateEquipmentContainerType(
                    currentTransformer,
                    equipmentContainersByMrid.TryGetValue(
                        currentTransformer.EquipmentContainer?.@ref is not null ? Guid.Parse(currentTransformer.EquipmentContainer.@ref) : Guid.Empty,
                        out var equipmentContainer) ? equipmentContainer : null)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate fault indicators.
        var faultIndicatorValidationErrors = faultIndicators.AsParallel().SelectMany((faultIndicator) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => FaultIndicatorValidation.ValidateEquipmentContainerType(
                    faultIndicator,
                    equipmentContainersByMrid.TryGetValue(
                        faultIndicator.EquipmentContainer?.@ref is not null ? Guid.Parse(faultIndicator.EquipmentContainer.@ref) : Guid.Empty,
                        out var equipmentContainer) ? equipmentContainer : null)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        var validationErrors = new List<ValidationError>()
            .Concat(conductionEquipmentErrors)
            .Concat(terminalValidationErrors)
            .Concat(equipmentContainerValidationErrors)
            .Concat(currentTransformerValidationErrors)
            .Concat(faultIndicatorValidationErrors)
            .ToList()
            .AsReadOnly();

        await WriteValidationErrors(outputFile, validationErrors).ConfigureAwait(false);

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task WriteValidationErrors(string outputFile, IReadOnlyList<ValidationError?> validationErrors)
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
        FrozenSet<CurrentTransformer>,
        FrozenSet<FaultIndicator>)> LoadCimAsync(string inputFile)
    {
        var conductingEquipments = new List<ConductingEquipment>();
        var terminals = new List<Terminal>();
        var powerTransformerEnds = new List<PowerTransformerEnd>();
        var equipmentContainers = new List<EquipmentContainer>();
        var currentTransformers = new List<CurrentTransformer>();
        var faultIndicators = new List<FaultIndicator>();

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
            else if (identifiedObject is FaultIndicator)
            {
                faultIndicators.Add((FaultIndicator)identifiedObject);
            }
        }

        return (
            conductingEquipments.ToFrozenSet(),
            terminals.ToFrozenSet(),
            powerTransformerEnds.ToFrozenSet(),
            equipmentContainers.ToFrozenSet(),
            currentTransformers.ToFrozenSet(),
            faultIndicators.ToFrozenSet()
        );
    }
}
