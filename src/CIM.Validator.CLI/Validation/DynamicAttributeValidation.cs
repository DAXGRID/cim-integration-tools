namespace CIM.Validator.CLI;

internal static class DynamicAttributeValidation
{
    public static IEnumerable<ValidationError> ValidateNotNull(object x)
    {
        var validationErrors = new List<ValidationError>();
        var properties = x.GetType().GetProperties();
        var objectType = x.GetType();

        var ignorePropertiesLookup = new HashSet<string>
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

        foreach (var propertyInfo in objectType.GetProperties().Where(x => !ignorePropertiesLookup.Contains(x.Name)))
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
