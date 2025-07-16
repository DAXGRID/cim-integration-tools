using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Xml;
using System.Text.Json;

namespace CIM.PreValidator.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var logger = LoggerFactory.Create(nameof(CIM.PreValidator.CLI));

        var rootCommand = new RootCommand("CIM Pre-Validator CLI.");

        var inputFilePathOption = new Option<string>(
            name: "--input-file",
            description: "The path to the input file, example: /home/user/my_file.xml."
        )
        { IsRequired = true };

        var outputFilePathOption = new Option<string>(
            name: "--output-file",
            description: "The path to the output file, example: /home/user/my_file.jsonl."
        )
        { IsRequired = true };

        rootCommand.Add(inputFilePathOption);
        rootCommand.Add(outputFilePathOption);

        rootCommand.SetHandler(
            async (inputFilePath, outputFilePath) =>
            {
                logger.LogInformation("Starting CIM Pre-Validator.");
                await ExecuteAsync(inputFilePath, outputFilePath).ConfigureAwait(false);
                logger.LogInformation("Finished CIM Pre-Validator.");
            }, inputFilePathOption, outputFilePathOption);

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(string inputFilePath, string outputFilePath)
    {
        var mrids = new HashSet<Guid>();
        var duplicateIds = new HashSet<Guid>();

        using (var reader = XmlReader.Create(inputFilePath, new XmlReaderSettings { Async = true }))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "mRID")
                {
                    var mrid = Guid.Parse(await reader.ReadElementContentAsStringAsync().ConfigureAwait(false));
                    if (mrids.Contains(mrid))
                    {
                        duplicateIds.Add(mrid);
                    }

                    mrids.Add(mrid);
                }
            }
        }

        var validationErrors = duplicateIds.Select(x =>
            new ValidationError
            {
                IdentifiedObjectId = x.ToString(),
                Code = "DUPLICATE_MRID",
                Description = "Duplicate mRIDs are not allowed.",
                Severity = Severity.Error,
                IdentifiedObjectClass = "IdentifiedObject",
            });

        using (var sw = new StreamWriter(outputFilePath))
        {
            foreach (var validationError in validationErrors)
            {
                await sw.WriteLineAsync(JsonSerializer.Serialize(validationError)).ConfigureAwait(false);
            }
        }
    }
}
