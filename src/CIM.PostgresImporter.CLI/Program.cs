using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using System.CommandLine;

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

        rootCommand.Add(inputFilePathOption);
        rootCommand.Add(postgresConnectionStringOption);
        rootCommand.Add(sridOption);

        rootCommand.SetHandler(
            async (inputFilePath, postgresConnectionString, srid) =>
            {
                await ImportFileAsync(
                    srid ?? 25812,
                    BULK_INSERT_COUNT,
                    inputFilePath,
                    postgresConnectionString,
                    logger).ConfigureAwait(false);
            },
            inputFilePathOption,
            postgresConnectionStringOption,
            sridOption
        );

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task ImportFileAsync(int SRID, int BULK_INSERT_COUNT, string dataFilePath, string connectionString, ILogger logger)
    {
        logger.LogInformation("Starting detection of schema.");
        var schemaReader = new StreamReader(dataFilePath);
        var schema = await DynamicSchema
            .BuildFromJsonAsync(schemaReader)
            .ConfigureAwait(false);

        schemaReader.Dispose();

        logger.LogInformation("Creating schema.");
        await PostgresImport
            .CreateImportSchemaAsync(connectionString, schema)
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
                    var totalInsertedCount = await PostgresImport.Import(
                        SRID,
                        BULK_INSERT_COUNT,
                        connectionString,
                        schemaType,
                        importChannelLookup.Key,
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
