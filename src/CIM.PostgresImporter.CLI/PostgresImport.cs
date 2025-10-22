using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace CIM.PostgresImporter.CLI;

internal sealed class BinaryCopyImportConnection : IDisposable
{
    private NpgsqlDataSource _npgsqlDataSource;

    private NpgsqlConnection _npgsqlConnection;

    public NpgsqlBinaryImporter NpgsqlBinaryImporter { get; private set; }

    public BinaryCopyImportConnection(NpgsqlDataSourceBuilder dataSourceBuilder, string binaryImportSql)
    {
        _npgsqlDataSource = dataSourceBuilder.Build();
        _npgsqlConnection = _npgsqlDataSource.OpenConnection();
        NpgsqlBinaryImporter = _npgsqlConnection.BeginBinaryImport(binaryImportSql);
    }

    public void Dispose()
    {
        NpgsqlBinaryImporter?.Complete();
        NpgsqlBinaryImporter?.Dispose();
        _npgsqlConnection?.Dispose();
        _npgsqlDataSource?.Dispose();
    }
}

internal static class PostgresImport
{
    public static async Task CreateImportSchemaAsync(string connectionString, Schema schema, string schemaName, bool addIfNotExists)
    {
        var createTablesScript = PostgresSqlBuilder.Build(schema, schemaName, addIfNotExists);
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
        string connectionString,
        Dictionary<string, SchemaTypeProperty> schemaType,
        string typeName,
        string schemaName,
        ChannelReader<Dictionary<string, JsonElement>> readerChannel)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseNetTopologySuite();
        using var dataSource = builder.Build();
        using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);

        var copySql = PostgresSqlBuilder.BuildCopyBulkInsert(schemaType.Where(x => !x.Value.ManyToManyAttribute).ToDictionary(x => x.Key, x => x.Value.Type), typeName, schemaName);

        var manyToManyRelationCopyInsertSqls = schemaType
            .Where(x => x.Value.ManyToManyAttribute && x.Value.RefTypeSchema is not null)
            .ToDictionary(x => x.Key, x => x.Value)
            .ToDictionary(x => x.Key, x => PostgresSqlBuilder.BuildCopyBulkInsert(x.Value.RefTypeSchema?.ToDictionary(x => x.Key, x => x.Value.Type) ?? throw new UnreachableException("Warning, even tho it has been checked earlier, so it should not happen that it is null"), $"Relation{typeName}{x.Key}", schemaName))
            ?? new();

        var postgresqlBinaryWriter = await connection.BeginBinaryImportAsync(copySql).ConfigureAwait(false);

        var manyToManyRelationshipImporter = new Dictionary<string, BinaryCopyImportConnection>();

        foreach (var manyToManyRelationCopyInsertSql in manyToManyRelationCopyInsertSqls)
        {
            var binaryCopyImportConnection = new BinaryCopyImportConnection(builder, manyToManyRelationCopyInsertSql.Value);
            manyToManyRelationshipImporter.Add(manyToManyRelationCopyInsertSql.Key, binaryCopyImportConnection);
        }

        var totalInsertionCount = 0;
        await foreach (var properties in readerChannel.ReadAllAsync().ConfigureAwait(false))
        {
            totalInsertionCount++;
            await postgresqlBinaryWriter.StartRowAsync().ConfigureAwait(false);

            foreach (var schemaProperty in schemaType)
            {
                if (schemaProperty.Value.ManyToManyAttribute)
                {
                    if (properties.TryGetValue(schemaProperty.Key, out var pv))
                    {
                        var manyToManyBinaryPostgresWriter = manyToManyRelationshipImporter[schemaProperty.Value.Name].NpgsqlBinaryImporter;

                        var references = pv.Deserialize<List<string>>();
                        if (references is null)
                        {
                            throw new InvalidOperationException("References are null, this should not be possible.");
                        }

                        foreach (var reference in references)
                        {
                            await manyToManyBinaryPostgresWriter.StartRowAsync().ConfigureAwait(false);

                            // This is a convention that it looks like the following: Owner/2f2164c3-c683-4257-ba8e-7213997c545b
                            var splittedReference = reference.Split("/");

                            // parent_ref_id
                            await manyToManyBinaryPostgresWriter
                                .WriteAsync(
                                    properties["mRID"].GetGuid(),
                                    ConvertInternalTypeToPostgresqlType(typeof(Guid)))
                                .ConfigureAwait(false);

                            // child_ref_id
                            await manyToManyBinaryPostgresWriter
                                .WriteAsync(
                                    Guid.Parse(splittedReference[1]),
                                    ConvertInternalTypeToPostgresqlType(typeof(Guid)))
                                .ConfigureAwait(false);

                            // child_ref_type
                            await manyToManyBinaryPostgresWriter
                                .WriteAsync(
                                    // Want lowercase name.
                                    PostgresSqlBuilder.CustomTableAndColumnNameConverter(splittedReference[0]),
                                    ConvertInternalTypeToPostgresqlType(typeof(string)))
                                .ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    var propertyType = schemaProperty.Value.Type;

                    JsonElement? propertyValue = null;

                    // If it is a composite object we have to lookup the value in a different way.
                    if (schemaProperty.Value.ContainingObjectName is not null && schemaProperty.Value.InnerObjectName is not null)
                    {
                        if (properties.TryGetValue(schemaProperty.Value.ContainingObjectName, out var pv))
                        {
                            if (pv.TryGetProperty(schemaProperty.Value.InnerObjectName, out var innerPv))
                            {
                                propertyValue = innerPv;
                            }
                        }
                    }
                    else
                    {
                        if (properties.TryGetValue(schemaProperty.Key, out var pv))
                        {
                            propertyValue = pv;
                        }
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
                    else if (schemaProperty.Key.Equals("Geometry", StringComparison.OrdinalIgnoreCase))
                    {
                        var geometryType = properties.First(x => x.Key == "GeometryType").Value.GetString()!;

                        Geometry? geometry;

                        var coordinateJson = propertyValue.Value.GetString()!;

                        if (geometryType.Equals("Point", StringComparison.OrdinalIgnoreCase))
                        {
                            var coordinates = JsonSerializer.Deserialize<double[]>(coordinateJson)!;
                            geometry = new Point(coordinates[0], coordinates[1]);
                        }
                        else if (geometryType.Equals("LineString", StringComparison.OrdinalIgnoreCase))
                        {
                            var coordinates = JsonSerializer.Deserialize<double[][]>(coordinateJson)!;
                            geometry = new LineString(coordinates.Select(x => new Coordinate(x[0], x[1])).ToArray());
                        }
                        else
                        {
                            throw new InvalidOperationException($"Could not handle: '{geometryType}'.");
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
                            schemaProperty.Key.Equals("Geometry", StringComparison.OrdinalIgnoreCase)
                            ? NpgsqlDbType.Geometry : ConvertInternalTypeToPostgresqlType(propertyType))
                        .ConfigureAwait(false);
                }
            }
        }

        await postgresqlBinaryWriter.CompleteAsync().ConfigureAwait(false);
        await postgresqlBinaryWriter.DisposeAsync().ConfigureAwait(false);

        foreach (var manyToManyRelationshipImport in manyToManyRelationshipImporter)
        {
            manyToManyRelationshipImport.Value.Dispose();
        }

        return totalInsertionCount;
    }

    private static readonly Dictionary<Type, NpgsqlDbType> TypeToPostgresqlTypeMap = new()
    {
        { typeof(int), NpgsqlDbType.Integer },
        { typeof(double), NpgsqlDbType.Double },
        { typeof(Guid), NpgsqlDbType.Uuid },
        { typeof(string), NpgsqlDbType.Text },
        { typeof(bool), NpgsqlDbType.Boolean },
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
