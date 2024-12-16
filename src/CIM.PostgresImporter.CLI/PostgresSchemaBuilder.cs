namespace CIM.PostgresImporter.CLI;

internal static class PostgresSchemaBuilder
{
    public static string Build(Schema schema, string schemaName)
    {
        return string.Join("\n", schema.Types.Select(x => Build(x, schemaName)));
    }

    private static string Build(SchemaType schemaType, string schemaName)
    {
        var columns = string.Join(",\n  ", schemaType.Properties.Select(x => $"\"{x.Name}\" {ConvertInternalTypeToPostgresqlType(x.Type)}"));
        return @$"
CREATE TABLE ""{schemaName}"".""{schemaType.Name}"" (
  {columns},
  PRIMARY KEY (""mRID"")
);";
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
