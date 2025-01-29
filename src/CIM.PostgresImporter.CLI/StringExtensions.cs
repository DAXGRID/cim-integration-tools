using System.Globalization;
using System.Text.RegularExpressions;

namespace CIM.PostgresImporter.CLI;

internal static class StringExtensions
{
    public static string ToSnakeCase(this string text)
    {
        #pragma warning disable CA1308 // We want lower case.
        return Regex.Replace(text, "(?<=[a-z0-9])[A-Z]|(?<=[A-Z])[A-Z][a-z]", "_$0").ToLower(CultureInfo.InvariantCulture);
        #pragma warning restore CA1308
    }
}
