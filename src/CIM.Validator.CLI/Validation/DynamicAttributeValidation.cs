using System.Reflection;

namespace CIM.Validator.CLI;

internal static class DynamicAttributeValidation
{
    public static IEnumerable<ValidationError> ValidateNotNullOrEmptyString(object x, IReadOnlyCollection<PropertyInfo> properties)
    {
        var validationErrors = new List<ValidationError>();

        var objectType = x.GetType();
        foreach (var propertyInfo in properties)
        {
            var propertyName = propertyInfo.Name;
            var propertyValue = propertyInfo.GetValue(x);

            if (propertyValue is null || propertyValue is string && string.IsNullOrWhiteSpace(((string)propertyValue)))
            {
                var mrid = objectType.GetProperty("mRID").GetValue(x)?.ToString() ??
                    throw new InvalidOperationException("The mRID could not be found on the object, something is wrong, this should never happen.");

                validationErrors.Add(new ValidationError
                {
                    Code = $"{propertyName.ToSnakeCaseUpper()}_IS_NOT_SET",
                    Severity = Severity.Warning,
                    Description = $"{propertyName} value is not set.",
                    IdentifiedObjectId = mrid,
                    IdentifiedObjectClass = objectType.Name
                });
            }
        }

        return validationErrors;
    }

    public static IEnumerable<PropertyInfo> BuiltDynamicPropertySchema(object x, IReadOnlySet<string> excludedPropertyNames)
    {
        return x.GetType().GetProperties()
            .Where(p => !excludedPropertyNames.Contains(p.Name))
            .Where(p => p.PropertyType.IsPrimitive || !p.PropertyType.GetProperties().Any(x => x.Name == "ref"));
    }
}
