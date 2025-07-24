using CIM.PhysicalNetworkModel;
using CIM.Validator.CLI.Validation;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;

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
        var dynamicAttributeSchemaLookup = new ConcurrentDictionary<string, IReadOnlyCollection<PropertyInfo>>();

        var ignoredPropertiesInDynamicPropertyValidation = new HashSet<string>
        {
            "aliasName",
            "description",
            "PSRType",
            "length",
            "Names",
            "phone1",
            "phone2",
            "order",
            "mainAddress"
        };

        // This is done to handle cases where some conducting equipment
        // does not have any terminals.
        var terminalsByConductingEquipment = conductingEquipments
            .ToDictionary(x => x.mRID, x => Array.Empty<Terminal>());

        foreach (var x in terminals.GroupBy(x => x.ConductingEquipment.@ref))
        {
            terminalsByConductingEquipment[x.Key] = x.ToArray();
        }

        // This is done to handle cases where some conducting equipment
        // does not have any power transformer ends.
        var powerTransformerEndsByConductingEquipment = conductingEquipments
            .ToDictionary(x => x.mRID, x => Array.Empty<PowerTransformerEnd>());

        foreach (var x in powerTransformerEnds.GroupBy(x => x.PowerTransformer.@ref))
        {
            powerTransformerEndsByConductingEquipment[x.Key] = x.ToArray();
        }

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
                validations.Add(() => AcLineSegmentValidation.NoEquipmentContainerLengthGreaterThanZero(acLineSegment));
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

            var propertyInfoSchema = NewMethod(conductingEquipment, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(conductingEquipment, propertyInfoSchema));
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

            var propertyInfoSchema = NewMethod(terminal, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(terminal, propertyInfoSchema));
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

            var propertyInfoSchema = NewMethod(equipmentContainer, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(equipmentContainer, propertyInfoSchema));
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

            var propertyInfoSchema = NewMethod(currentTransformer, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(currentTransformer, propertyInfoSchema));
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

            var propertyInfoSchema = NewMethod(faultIndicator, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(faultIndicator, propertyInfoSchema));
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

            var propertyInfoSchema = NewMethod(auxiliaryEquipment, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(auxiliaryEquipment, propertyInfoSchema));
        });

        // Validate usage points
        var usagePointMridByName = usagePoints.Where(x => x.name != null).GroupBy(x => x.name, x => x.mRID).ToDictionary(x => x.Key, x => x.ToList());
        var usagePointValidations = usagePoints.AsParallel().SelectMany((usagePoint) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => UsagePointValidation.EquipmentReference(usagePoint),
                () => UsagePointValidation.VerifyName(usagePoint, usagePointMridByName)
            };

            var propertyInfoSchema = NewMethod(usagePoint, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(usagePoint, propertyInfoSchema));
        });

        // Validate location
        var locationValidations = locations.AsParallel().SelectMany((location) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => LocationValidation.CoordinateSystem(location)
            };

            var propertyInfoSchema = NewMethod(location, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(location, propertyInfoSchema));
        });

        // Validate power transformer end
        var powerTransformerEndValidations = powerTransformerEnds.AsParallel().SelectMany((powerTransformerEnd) =>
        {
            var validations = new List<Func<ValidationError?>>
            {
                () => PowerTransformerEndValidation.BaseVoltageRequired(powerTransformerEnd),
                () => PowerTransformerEndValidation.PowerTransformerRequired(powerTransformerEnd),
                () => PowerTransformerEndValidation.TerminalRequired(powerTransformerEnd)
            };

            var propertyInfoSchema = NewMethod(powerTransformerEnd, dynamicAttributeSchemaLookup, ignoredPropertiesInDynamicPropertyValidation);

            return validations
                .Select(validate => validate()).Where(x => x is not null)
                .Concat(DynamicAttributeValidation.ValidateNotNullOrEmptyString(powerTransformerEnd, propertyInfoSchema));
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

    private static IReadOnlyCollection<PropertyInfo> NewMethod(
        IdentifiedObject identifiedObject,
        ConcurrentDictionary<string, IReadOnlyCollection<PropertyInfo>> dynamicAttributeSchemaLookup,
        HashSet<string> ignoredPropertiesInDynamicPropertyValidation)
    {
        IReadOnlyCollection<PropertyInfo> propertyInfoSchema = null;
        var typeName = identifiedObject.GetType().Name;

        if (!dynamicAttributeSchemaLookup.TryGetValue(identifiedObject.GetType().Name, out propertyInfoSchema))
        {
            propertyInfoSchema = DynamicAttributeValidation.BuiltDynamicPropertySchema(identifiedObject, ignoredPropertiesInDynamicPropertyValidation).ToArray().AsReadOnly();
            dynamicAttributeSchemaLookup.TryAdd(typeName, propertyInfoSchema);
        }

        return propertyInfoSchema;
    }
}
