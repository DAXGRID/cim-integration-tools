using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.CommandLine;
using System.Text.Json;
using System.Threading.Channels;

namespace CIM.PostgresImporter.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        const int BULK_INSERT_COUNT = 50_000;

        var logger = LoggerFactory.Create(nameof(CIM.PostgresImporter.CLI));

        var rootCommand = new RootCommand("CIM Postgres Importer CLI.");

        var inputFilePathOption = new Option<string>(
            name: "--input-file-path",
            description: "The path to the input file, example: /home/user/my_file.jsonl."
        )
        { IsRequired = true };

        var postgresConnectionStringOption = new Option<string>(
            name: "--connection-string",
            description: "The connectionstring to the postgresql database."
        )
        { IsRequired = true };

        var sridOption = new Option<int?>(
            name: "--srid",
            description: "The SRID for the geometries. Default is 25812."
        )
        { IsRequired = false };

        var preImportSqlScriptPathOption = new Option<string?>(
            name: "--pre-import-sql-script-path",
            description: "The path to a SQL script that should be run before the import."
        )
        { IsRequired = false };

        var postImportSqlScriptPathOption = new Option<string?>(
            name: "--post-import-sql-script-path",
            description: "The path to a SQL script that should be run after the import."
        )
        { IsRequired = false };

        var schemaNameOption = new Option<string?>(
            name: "--schema-name",
            description: "The name of the schema that should be used to create and import data into. Default is 'public'."
        )
        { IsRequired = false };

        var createSchemaIfNotExistsOption = new Option<bool?>(
            name: "--create-schema-if-not-exists",
            description: "Enables automatic schema creation if it does not exist."
        )
        { IsRequired = false };

        rootCommand.Add(inputFilePathOption);
        rootCommand.Add(postgresConnectionStringOption);
        rootCommand.Add(sridOption);
        rootCommand.Add(preImportSqlScriptPathOption);
        rootCommand.Add(postImportSqlScriptPathOption);
        rootCommand.Add(schemaNameOption);
        rootCommand.Add(createSchemaIfNotExistsOption);

        rootCommand.SetHandler(
            async (inputFilePath, postgresConnectionString, srid, postImportScriptPath, schemaName, createSchemaIfNotExists, preImportScriptPath) =>
            {
                schemaName = schemaName ?? "public";

                if (preImportScriptPath is not null)
                {
                    logger.LogInformation("Starting executing the pre import script in path {PostImportSqlScriptPath}", preImportScriptPath);

                    var sqlScriptContent = await File.ReadAllTextAsync(preImportScriptPath).ConfigureAwait(false);
                    await PostgresImport
                        .ExecuteScriptAsync(postgresConnectionString, sqlScriptContent)
                        .ConfigureAwait(false);

                    logger.LogInformation("Finished executing the pre import script: {PostImportSqlScriptPath}", preImportScriptPath);
                }

                if (createSchemaIfNotExists.HasValue && createSchemaIfNotExists.Value)
                {
                    logger.LogInformation("Creating schema: {SchemaName}", schemaName);
                    await PostgresImport.CreateSchemaAsync(postgresConnectionString, schemaName).ConfigureAwait(false);
                    logger.LogInformation("Finished creating schema: {SchemaName}", schemaName);
                }

                await ImportFileAsync(
                    srid ?? 25812,
                    BULK_INSERT_COUNT,
                    inputFilePath,
                    postgresConnectionString,
                    schemaName ?? "public",
                    logger).ConfigureAwait(false);

                if (postImportScriptPath is not null)
                {
                    logger.LogInformation("Starting executing the post import script in path: {PostImportSqlScriptPath}", postImportScriptPath);

                    var sqlScriptContent = await File.ReadAllTextAsync(postImportScriptPath).ConfigureAwait(false);
                    await PostgresImport
                        .ExecuteScriptAsync(postgresConnectionString, sqlScriptContent)
                        .ConfigureAwait(false);

                    logger.LogInformation("Finished executing the post import script in path: {PostImportSqlScriptPath}", postImportScriptPath);
                }
            },
            inputFilePathOption,
            postgresConnectionStringOption,
            sridOption,
            postImportSqlScriptPathOption,
            schemaNameOption,
            createSchemaIfNotExistsOption,
            preImportSqlScriptPathOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task ImportFileAsync(int srid, int bulkInsertCount, string dataFilePath, string connectionString, string schemaName, ILogger logger)
    {
        logger.LogInformation("Starting detection of schema.");
        var schemaReader = new StreamReader(dataFilePath);
        var schema = await DynamicSchema
            .BuildFromJsonAsync(schemaReader)
            .ConfigureAwait(false);

        schemaReader.Dispose();

        logger.LogInformation("Creating schema.");
        await PostgresImport
            .CreateImportSchemaAsync(connectionString, schema, schemaName)
            .ConfigureAwait(false);

        var schemaTypeLookup = schema.Types.ToDictionary(x => x.Name, x => x.Properties.ToDictionary(y => y.Name, y => y.Type));

        var importChannelsLookup = new Dictionary<string, Channel<Dictionary<string, JsonElement>>>();

        foreach (var schemaType in schema.Types)
        {
            var ch = Channel.CreateUnbounded<Dictionary<string, JsonElement>>(
                new UnboundedChannelOptions
                {
                    SingleWriter = true,
                    SingleReader = true
                }
            );

            importChannelsLookup.Add(schemaType.Name, ch);
        }

        logger.LogInformation("Starting inserting CIM objects.");

        var cimLineReaderTask = Task.Run(async () =>
        {
            try
            {
                await CimReader
                    .ReadAsync(dataFilePath, importChannelsLookup)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogCritical("{Exception}", ex);
                throw;
            } finally
            {
                importChannelsLookup.Select(x => x.Value).ToList().ForEach(x => x.Writer.Complete());
            }
        });

        var totalInsertCountByType = new ConcurrentDictionary<string, int>();
        var postgresInsertTasks = new List<Task>();

        foreach (var importChannelLookup in importChannelsLookup)
        {
            var postgresInsertTask = Task.Run(async () =>
            {
                try
                {
                    var schemaType = schemaTypeLookup[importChannelLookup.Key];
                    var totalInsertedCount = await PostgresImport.ImportAsync(
                        srid,
                        bulkInsertCount,
                        connectionString,
                        schemaType,
                        importChannelLookup.Key,
                        schemaName,
                        importChannelLookup.Value.Reader
                    ).ConfigureAwait(false);

                    totalInsertCountByType.TryAdd(importChannelLookup.Key, totalInsertedCount);
                }
                catch (Exception ex)
                {
                    logger.LogCritical("{Exception}", ex);
                    // Stop all channels in case something goes wrong.
                    importChannelsLookup.Select(x => x.Value).ToList().ForEach(x => x.Writer.Complete());
                    throw;
                }
            });

            postgresInsertTasks.Add(postgresInsertTask);
        }

        postgresInsertTasks.Add(cimLineReaderTask);
        await Task.WhenAll(postgresInsertTasks).ConfigureAwait(false);

        logger.LogInformation("------- Finished import. --------");
        logger.LogInformation($"Total CIM objects imported: '{totalInsertCountByType.Sum(x => x.Value)}'.");
        foreach (var insertionCountPair in totalInsertCountByType)
        {
            logger.LogInformation($"Inserted a total of '{insertionCountPair.Value}' of type '{insertionCountPair.Key}'.");
        }
    }
}
