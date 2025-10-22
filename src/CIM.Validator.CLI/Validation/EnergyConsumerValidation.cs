using CIM.PhysicalNetworkModel;

namespace CIM.Validator.CLI;

internal static class EnergyConsumerValidation
{
    public static ValidationError? ValidateLocation(EnergyConsumer e, Location? l)
    {
        if (string.IsNullOrWhiteSpace(e.Location?.@ref))
        {
            return new ValidationError
            {
                IdentifiedObjectId = e.mRID,
                IdentifiedObjectClass = e.GetType().Name,
                Code = "ENERGY_CONSUMER_MISSING_LOCATION_REFERENCE",
                Description = "The energy consumer is missing the required location reference.",
                Severity = Severity.Error
            };
        }

        // The referenced location does not exist.
        if (l is null)
        {
            return new ValidationError
            {
                IdentifiedObjectId = e.mRID,
                IdentifiedObjectClass = e.GetType().Name,
                Code = "ENERGY_CONSUMER_REFERENCED_LOCATION_DOES_NOT_EXIST",
                Description = $"Energy consumer has a reference to a location ({e.Location.@ref}) that does not exist.",
                Severity = Severity.Error
            };
        }

        // The referenced location should be a point.
        if (l is LocationExt && ((LocationExt)l).GeometryType != GeometryType.Point)
        {
            return new ValidationError
            {
                IdentifiedObjectId = e.mRID,
                IdentifiedObjectClass = e.GetType().Name,
                Code = "ENERGY_CONSUMER_REFERENCED_LOCATION_IS_NOT_A_POINT",
                Description = $"Energy consumer has a reference to a location ({e.Location.@ref}) that is not a point.",
                Severity = Severity.Error
            };
        }

        return null;
    }
}
