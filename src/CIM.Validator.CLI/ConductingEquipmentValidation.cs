using CIM.PhysicalNetworkModel;
using System.Globalization;

namespace CIM.Validator.CLI;

internal static class ConductingEquipmentValidation
{
    public static ValidationError? EquipmentContainerRelation(ConductingEquipment c)
    {
        // All conducting equipment, but ACLineSegment needs to reference an equipment container.
        if (string.IsNullOrWhiteSpace(c.EquipmentContainer?.@ref) && c is not ACLineSegment)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = c.GetType().Name,
                Code = "REQUIRED_EQUIPMENT_CONTAINER_REFERENCE",
                Description = "Conducting equipment should have a reference to an equipment container.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? BaseVoltage(ConductingEquipment c)
    {
        if (c.BaseVoltage <= 0)
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = c.GetType().Name,
                Code = "BASE_VOLTAGE_LESS_OR_EQUAL_TO_ZERO",
                Description = "Conducting equipment should have base voltage greater than 0.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? NumberOfTerminals(ConductingEquipment c, IReadOnlyCollection<Terminal> terminals)
    {
        Func<int, bool> validate = c switch
        {
            BusbarSection or LinearShuntCompensator or NonlinearShuntCompensator or SynchronousMachine or AsynchronousMachine or PetersenCoil or EnergyConsumer => (int x) => (x == 1),
            Breaker or LoadBreakSwitch or Disconnector or Fuse or GroundDisconnector or ACLineSegmentExt => (int x) => (x == 2),
            PowerTransformer => (int x) => (x > 1),
            _ => (int x) => throw new ArgumentException($"Could not handle type {c.GetType().Name}")
        };

        if (!validate(terminals.Count))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = c.GetType().Name,
                Code = "WRONG_NUMBER_OF_TERMINALS",
                Description = "Wrong number of terminals on conducting equipment.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? ReferencedTerminalConnectivityNode(ConductingEquipment c, IEnumerable<Terminal> terminals)
    {
        if (!terminals.Any(x => x.ConnectivityNode is not null))
        {
            return new ValidationError
            {
                Mrid = Guid.Parse(c.mRID),
                TypeName = c.GetType().Name,
                Code = "MANDATORY_SINGLE_CONNECTIVY_NODE",
                Description = "Mandatory so have at least one terminal pointing to a conducting equipment point to a connectivity node.",
                Severity = Severity.Warning
            };
        }

        return null;
    }

    public static ValidationError? ReferencedTerminalSequenceNumber(ConductingEquipment c, IEnumerable<Terminal> terminals)
    {
        var expectedSequenceNumber = 0;
        foreach (var sequenceNumber in terminals.OrderBy(x => x.sequenceNumber).Select(x => x.sequenceNumber))
        {
            expectedSequenceNumber++;
            if (expectedSequenceNumber != int.Parse(sequenceNumber, CultureInfo.InvariantCulture))
            {
                return new ValidationError
                {
                    Mrid = Guid.Parse(c.mRID),
                    TypeName = c.GetType().Name,
                    Code = "TERMINAL_NUMBERING_IS_INVALID",
                    Description = "Terminal numbering is invalid, should always be a valid sequence.",
                    Severity = Severity.Warning
                };
            }
        }
        return null;
    }
}
