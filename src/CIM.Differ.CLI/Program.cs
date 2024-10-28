using System.CommandLine;
using CIM.Cson;
using CIM.PhysicalNetworkModel;

namespace CIM.Differ.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CIM Differ CLI.");

        var firstFileAbsolutePath = "";
        var secondFileAbsolutePath = "";
        var outputFileAbsolutePath = "";

        var serializer = new CsonSerializer();

        IEnumerable<IdentifiedObject> firstFileIdentifiedObjects = ReadIdentifiedObjectFile(serializer, firstFileAbsolutePath);
        IEnumerable<IdentifiedObject> secondFileIdentifiedObjects = ReadIdentifiedObjectFile(serializer, secondFileAbsolutePath);

        var differ = new CimDiffer();
        using (var destination = File.Open(outputFileAbsolutePath, FileMode.CreateNew))
        {
            using (var source = serializer.SerializeObjects(differ.GetDiff(firstFileIdentifiedObjects, secondFileIdentifiedObjects)))
            {
                await source.CopyToAsync(destination).ConfigureAwait(false);
            }
        }

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static IEnumerable<IdentifiedObject> ReadIdentifiedObjectFile(CsonSerializer serializer, string filePath)
    {
        using (var inputStream = File.OpenRead(filePath))
        {
            return serializer.DeserializeObjects(inputStream);
        }
    }
}
