using System.Globalization;
using System.Text.RegularExpressions;

namespace CIM.PostgresImporter.CLI;

internal static class PostgresSqlBuilder
{
    public static string Build(Schema schema, string schemaName)
    {
        return string.Join("\n", schema.Types.Select(x => Build(x, schemaName)));
    }

    private static string Build(SchemaType schemaType, string schemaName)
    {
        var columns = string.Join(",\n  ", schemaType.Properties.Select(x => $"\"{CustomTableAndColumnNameConverter(x.Name)}\" {ConvertInternalTypeToPostgresqlType(x.Type)}"));
        return @$"
CREATE TABLE ""{schemaName}"".""{CustomTableAndColumnNameConverter(schemaType.Name)}"" (
  {columns},
  PRIMARY KEY (""mrid"")
);";
    }

    public static string BuildCopyBulkInsert(Dictionary<string, Type> schemaType, string typeName, string schemaName)
    {
        var fields = string.Join(",", schemaType.Keys.Select(key => $"\"{CustomTableAndColumnNameConverter(key)}\""));
        return $"COPY \"{schemaName}\".\"{CustomTableAndColumnNameConverter(typeName)}\" ({fields}) FROM STDIN (FORMAT BINARY)";
    }

    private static string CustomTableAndColumnNameConverter(string x)
    {
        #pragma warning disable CA1308 // We want lower case.
        // This is a hack since it's hard to test for this one.
        x = x.Replace("mRID", "mrid", StringComparison.InvariantCulture);
        return Regex.Replace(x, "(?<=[a-z0-9])[A-Z]|(?<=[A-Z])[A-Z][a-z]", "_$0").ToLower(CultureInfo.InvariantCulture);
        #pragma warning restore CA1308
    }

    private static string ConvertInternalTypeToPostgresqlType(Type type)
    {
        if (type == typeof(int))
        {
            return "INTEGER";
        }
        else if(type == typeof(double))
        {
            return "DOUBLE PRECISION";
        }
        else if (type == typeof(Guid))
        {
            return "UUID";
        }
        else if (type == typeof(string))
        {
            return "TEXT";
        }
        else if (type == typeof(bool))
        {
            return "BOOLEAN";
        }
        else if (type == typeof(ICollection<Point2D>))
        {
            return $"GEOMETRY";
        }
        else if (type == typeof(ICollection<Guid>))
        {
            return $"UUID[]";
        }
        else if (type == typeof(ICollection<string>))
        {
            return $"TEXT[]";
        }
        else if (type == typeof(CompositeObject))
        {
           return "JSONB";
        }
        else
        {
            throw new ArgumentException($"Could not convert type: '{type}' to PosgreSQL type.");
        }
    }
}
