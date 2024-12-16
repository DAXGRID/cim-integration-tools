using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using System.Threading.Channels;

namespace CIM.PostgresImporter.CLI;

internal static class PostgresImport
{
    public static async Task CreateImportSchemaAsync(string connectionString, Schema schema, string schemaName)
    {
        var createTablesScript = PostgresSchemaBuilder.Build(schema, schemaName);
        await ExecuteScriptAsync(connectionString, createTablesScript).ConfigureAwait(false);
    }

    public static async Task CreateSchemaAsync(string connectionString, string schemaName)
    {
        await ExecuteScriptAsync(connectionString, $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"").ConfigureAwait(false);
    }

    public static async Task ExecuteScriptAsync(string connectionString, string sql)
    {
        using var conn = new NpgsqlConnection(connectionString);

        using var cmd = new NpgsqlCommand(sql, conn);

        await conn.OpenAsync().ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public static async Task<int> ImportAsync(
        int srid,
        int bulkInsertCount,
        string connectionString,
        Dictionary<string, Type> schemaType,
        string typeName,
        string schemaName,
        ChannelReader<Dictionary<string, JsonElement>> readerChannel)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseNetTopologySuite();
        using var dataSource = builder.Build();
        using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        var fields = string.Join(",", schemaType.Keys.Select(key => $"\"{key}\""));

        var copySql = $"COPY \"{schemaName}\".\"{typeName}\" ({fields}) FROM STDIN (FORMAT BINARY)";

        var postgresqlBinaryWriter = await connection.BeginBinaryImportAsync(copySql).ConfigureAwait(false);

        var totalInsertionCount = 0;
        await foreach (var properties in readerChannel.ReadAllAsync())
        {
            totalInsertionCount++;
            await postgresqlBinaryWriter.StartRowAsync().ConfigureAwait(false);

            foreach (var schemaProperty in schemaType)
            {
                var propertyType = schemaProperty.Value;

                JsonElement? propertyValue = null;
                if (properties.TryGetValue(schemaProperty.Key, out var pv))
                {
                    propertyValue = pv;
                }

                dynamic? parameter = null;
                if (propertyValue is null)
                {
                    parameter = DBNull.Value;
                }
                else if (propertyType == typeof(CompositeObject))
                {
                    parameter = propertyValue.Value.GetRawText();
                }
                else if (propertyType == typeof(ICollection<Point2D>))
                {
                    var points = propertyValue.Value.Deserialize<ICollection<Point2D>>()?.ToArray()
                        ?? throw new InvalidOperationException("Could not deserialize point array.");

                    Geometry? geometry;
                    if (points.Length == 1)
                    {
                        geometry = new Point(points[0].X, points[0].Y);
                    }
                    else
                    {
                        geometry = new LineString(points.Select(x => new Coordinate(x.X, x.Y)).ToArray());
                    }

                    geometry.SRID = srid;
                    parameter = geometry;
                }
                else
                {
                    parameter = propertyValue.Value.Deserialize(propertyType);
                }

                await postgresqlBinaryWriter
                    .WriteAsync(
                        parameter,
                        ConvertInternalTypeToPostgresqlType(propertyType))
                    .ConfigureAwait(false);
            }

            if (totalInsertionCount % bulkInsertCount == 0)
            {
                await postgresqlBinaryWriter.CompleteAsync().ConfigureAwait(false);
                await postgresqlBinaryWriter.DisposeAsync().ConfigureAwait(false);
                postgresqlBinaryWriter = await connection.BeginBinaryImportAsync(copySql).ConfigureAwait(false);
            }
        }

        await postgresqlBinaryWriter.CompleteAsync().ConfigureAwait(false);
        await postgresqlBinaryWriter.DisposeAsync().ConfigureAwait(false);

        return totalInsertionCount;
    }

    private static readonly Dictionary<Type, NpgsqlDbType> TypeToPostgresqlTypeMap = new()
    {
        { typeof(int), NpgsqlDbType.Integer },
        { typeof(double), NpgsqlDbType.Double },
        { typeof(Guid), NpgsqlDbType.Uuid },
        { typeof(string), NpgsqlDbType.Text },
        { typeof(bool), NpgsqlDbType.Boolean },
        { typeof(ICollection<Point2D>), NpgsqlDbType.Geometry },
        { typeof(ICollection<Guid>), NpgsqlDbType.Array | NpgsqlDbType.Uuid },
        { typeof(ICollection<string>), NpgsqlDbType.Array | NpgsqlDbType.Text },
        { typeof(CompositeObject), NpgsqlDbType.Jsonb }
    };

    private static NpgsqlDbType ConvertInternalTypeToPostgresqlType(Type type)
    {
        if (TypeToPostgresqlTypeMap.TryGetValue(type, out var npgsqlType))
        {
            return npgsqlType;
        }
        else
        {
            throw new ArgumentException($"Could not convert type: '{type}' to PostgreSQL type.");
        }
    }
}
