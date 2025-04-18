using CIM.PhysicalNetworkModel;
using System.Collections.Frozen;

namespace CIM.Validator.CLI;

internal static class CimValidation
{
    public static IEnumerable<ValidationError> Validate(
     FrozenSet<ConductingEquipment> conductingEquipments,
     FrozenSet<Terminal> terminals,
     FrozenSet<PowerTransformerEnd> powerTransformerEnds,
     FrozenSet<EquipmentContainer> equipmentContainers,
     FrozenSet<CurrentTransformer> currentTransformers,
     FrozenSet<FaultIndicator> faultIndicators)
    {
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

        return [
            ..conductionEquipmentErrors,
            ..terminalValidationErrors,
            ..equipmentContainerValidationErrors,
            ..currentTransformerValidationErrors,
            ..faultIndicatorValidationErrors
        ];
    }
}
