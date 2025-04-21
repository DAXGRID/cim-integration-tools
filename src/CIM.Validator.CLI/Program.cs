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
             faultIndicators,
             auxiliaryEquipments,
             locations,
             usagePoints
        ) = await LoadCimFromFile(inputFile).ConfigureAwait(false);

        var validationErrors = CimValidation
            .Validate(
                conductingEquipments,
                terminals,
                powerTransformerEnds,
                equipmentContainers,
                currentTransformers,
                faultIndicators,
                auxiliaryEquipments,
                locations,
                usagePoints
            )
            .ToList()
            .AsReadOnly();

        await WriteValidationErrorsToFile(outputFile, validationErrors).ConfigureAwait(false);

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task WriteValidationErrorsToFile(string outputFile, IReadOnlyList<ValidationError?> validationErrors)
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
        FrozenSet<FaultIndicator>,
        FrozenSet<AuxiliaryEquipment>,
        FrozenSet<Location>,
        FrozenSet<UsagePoint>
    )> LoadCimFromFile(string inputFile)
    {
        var conductingEquipments = new List<ConductingEquipment>();
        var terminals = new List<Terminal>();
        var powerTransformerEnds = new List<PowerTransformerEnd>();
        var equipmentContainers = new List<EquipmentContainer>();
        var currentTransformers = new List<CurrentTransformer>();
        var faultIndicators = new List<FaultIndicator>();
        var auxiliaryEquipments = new List<AuxiliaryEquipment>();
        var locations = new List<Location>();
        var usagePoints = new List<UsagePoint>();

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
            else if (identifiedObject is AuxiliaryEquipment)
            {
                auxiliaryEquipments.Add((AuxiliaryEquipment)identifiedObject);
            }
            else if (identifiedObject is Location)
            {
                locations.Add((Location)identifiedObject);
            }
            else if (identifiedObject is UsagePoint)
            {
                usagePoints.Add((UsagePoint)identifiedObject);
            }
        }

        return (
            conductingEquipments.ToFrozenSet(),
            terminals.ToFrozenSet(),
            powerTransformerEnds.ToFrozenSet(),
            equipmentContainers.ToFrozenSet(),
            currentTransformers.ToFrozenSet(),
            faultIndicators.ToFrozenSet(),
            auxiliaryEquipments.ToFrozenSet(),
            locations.ToFrozenSet(),
            usagePoints.ToFrozenSet()
        );
    }
}
