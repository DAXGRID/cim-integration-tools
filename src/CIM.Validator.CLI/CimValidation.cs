using CIM.PhysicalNetworkModel;
using CIM.Validator.CLI.Validation;
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
     FrozenSet<FaultIndicator> faultIndicators,
     FrozenSet<AuxiliaryEquipment> auxiliaryEquipments,
     FrozenSet<Location> locations,
     FrozenSet<UsagePoint> usagePoints)
    {
        var terminalsByConductingEquipment = terminals
            .GroupBy(x => x.ConductingEquipment.@ref)
            .ToFrozenDictionary(x => x.Key, x => x.ToArray());

        var powerTransformerEndsByConductingEquipment = powerTransformerEnds
            .GroupBy(x => x.PowerTransformer.@ref)
            .ToFrozenDictionary(x => x.Key, x => x.ToArray());

        var equipmentContainersByMrid = equipmentContainers.ToFrozenDictionary(x => Guid.Parse(x.mRID), x => x);
        var terminalsByMrid = terminals.ToFrozenDictionary(x => Guid.Parse(x.mRID), x => x);
        var locationsByMrid = locations.ToFrozenDictionary(x => Guid.Parse(x.mRID), x => x);

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
                var terminal = terminalsByConductingEquipment[conductingEquipment.mRID];
                var powerTransformerEnd = powerTransformerEndsByConductingEquipment[conductingEquipment.mRID];

                validations.Add(
                    () => PowerTransformerValidation.PowerTransformerEndPerTerminal(
                        (PowerTransformer)conductingEquipment,
                        terminal,
                        powerTransformerEnd));

                validations.Add(
                    () => PowerTransformerValidation.PowerTransformerEndNumberMatchesTerminalNumber(
                        (PowerTransformer)conductingEquipment,
                        terminal,
                        powerTransformerEnd));
            }
            else if (conductingEquipment is ACLineSegment)
            {
                var acLineSegment = (ACLineSegment)conductingEquipment;
                Location? location = null;
                if (acLineSegment.Location?.@ref is not null)
                {
                    if (locationsByMrid.TryGetValue(Guid.Parse(acLineSegment.Location.@ref), out var outLocation))
                    {
                        location = outLocation;
                    }
                }

                validations.Add(() => AcLineSegmentValidation.ValidateLocation(acLineSegment, location));
            }
            else if (conductingEquipment is EnergyConsumer)
            {
                var energyConsumer = (EnergyConsumer)conductingEquipment;
                Location? location = null;
                if (energyConsumer.Location?.@ref is not null)
                {
                    if (locationsByMrid.TryGetValue(Guid.Parse(energyConsumer.Location.@ref), out var outLocation))
                    {
                        location = outLocation;
                    }
                }

                validations.Add(() => EnergyConsumerValidation.ValidateLocation(energyConsumer, location));
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
            else if (equipmentContainer is Substation)
            {
                var substation = (Substation)equipmentContainer;

                locationsByMrid.TryGetValue(
                    substation.Location?.@ref is not null ? Guid.Parse(substation.Location.@ref) : Guid.Empty, out var substationLocation);

                validations.Add(() => SubstationValidation.PsrType(substation));
                validations.Add(() => SubstationValidation.LocationRequired(substation, substationLocation));
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
                        out var equipmentContainer) ? equipmentContainer : null),
                () => CurrentTransformerValidation.MaximumCurrentRequired(currentTransformer)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate fault indicators.
        var faultIndicatorValidationErrors = faultIndicators.AsParallel().SelectMany((faultIndicator) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => FaultIndicatorValidation.ResetKindRequired(faultIndicator),
                () => FaultIndicatorValidation.ValidateEquipmentContainerType(
                    faultIndicator,
                    equipmentContainersByMrid.TryGetValue(
                        faultIndicator.EquipmentContainer?.@ref is not null ? Guid.Parse(faultIndicator.EquipmentContainer.@ref) : Guid.Empty,
                        out var equipmentContainer) ? equipmentContainer : null)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate auxiliary equipment indicators.
        var auxiliaryEquipmentValidationErrors = auxiliaryEquipments.AsParallel().SelectMany((auxiliaryEquipment) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => AuxiliaryEquipmentValidation.HasTerminal(auxiliaryEquipment),
                () => AuxiliaryEquipmentValidation.TerminalReferenceExist(
                    auxiliaryEquipment,
                    terminalsByMrid.TryGetValue(
                        auxiliaryEquipment.Terminal?.@ref is not null ? Guid.Parse(auxiliaryEquipment.Terminal.@ref) : Guid.Empty,
                        out var terminal) ? terminal : null),
                () => AuxiliaryEquipmentValidation.ReferencesBay(
                    auxiliaryEquipment,
                    equipmentContainersByMrid.TryGetValue(
                        auxiliaryEquipment.EquipmentContainer?.@ref is not null ? Guid.Parse(auxiliaryEquipment.EquipmentContainer.@ref) : Guid.Empty,
                        out var equipmentContainer) ? equipmentContainer : null)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate usage points
        var usagePointValidations = usagePoints.AsParallel().SelectMany((usagePoint) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => UsagePointValidation.EquipmentReference(usagePoint)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate location
        var locationValidations = locations.AsParallel().SelectMany((location) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => LocationValidation.CoordinateSystem(location)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        // Validate location
        var powerTransformerEndValidations = powerTransformerEnds.AsParallel().SelectMany((powerTransformerEnd) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => PowerTransformerEndValidation.BaseVoltageRequired(powerTransformerEnd),
                () => PowerTransformerEndValidation.PowerTransformerRequired(powerTransformerEnd),
                () => PowerTransformerEndValidation.TerminalRequired(powerTransformerEnd)
            };

            return validations.Select(validate => validate()).Where(x => x is not null);
        });

        return [
            ..conductionEquipmentErrors,
            ..terminalValidationErrors,
            ..equipmentContainerValidationErrors,
            ..currentTransformerValidationErrors,
            ..faultIndicatorValidationErrors,
            ..auxiliaryEquipmentValidationErrors,
            ..usagePointValidations,
            ..locationValidations,
            ..powerTransformerEndValidations
        ];
    }
}
