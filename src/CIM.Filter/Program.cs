namespace CIM.Filter;

internal static class Program
{
    public static async Task Main()
    {
        const string inputFilePath = "./mapper_output.jsonl";
        const string outputFilePath = "./filter_output.jsonl";

        await ProcessFilter.ProcessAsync(inputFilePath, outputFilePath).ConfigureAwait(false);
    }
}
