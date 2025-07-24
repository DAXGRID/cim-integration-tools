namespace CIM.Validator.CLI;

internal static class DynamicAttributeValidation
{
    public static IEnumerable<ValidationError> ValidateNotNull(object x, HashSet<string> excludedPropertyNames)
    {
        var validationErrors = new List<ValidationError>();

        var properties = x.GetType().GetProperties();
        var objectType = x.GetType();

        foreach (var propertyInfo in properties.Where(x => !excludedPropertyNames.Contains(x.Name)))
        {
            var propertyName = propertyInfo.Name;
            var propertyValue = propertyInfo.GetValue(x);

            if (propertyInfo.PropertyType.IsPrimitive && propertyValue is null || (propertyValue is string && string.IsNullOrWhiteSpace(((string)propertyValue))) ||
                !propertyInfo.PropertyType.GetProperties().Any(x => x.Name == "ref") && propertyValue is null)
            {
                validationErrors.Add(new ValidationError
                {
                    Code = $"{propertyName.ToSnakeCaseUpper()}_IS_NULL",
                    Severity = Severity.Warning,
                    Description = $"{propertyName} value is not set.",
                    IdentifiedObjectId = objectType.GetProperty("mRID").GetValue(x)?.ToString() ?? throw new InvalidOperationException("The mRID could not be found on the object, something is wrong, this should never happen."),
                    IdentifiedObjectClass = objectType.Name.ToUpperInvariant()
                });
            }
        }

        return validationErrors;
    }
}
