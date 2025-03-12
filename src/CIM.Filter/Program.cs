namespace CIM.Filter;

internal static class Program
{
    public static async Task Main()
    {
        const string inputFilePath = "./mapper_output.jsonl";
        const string outputFilePath = "./filter_output.jsonl";
        const int baseVoltageLowerBound = 10000;
        const int baseVoltageUppperBound = int.MaxValue;


        await ProcessFilter
            .ProcessAsync(
                inputFilePath,
                outputFilePath,
                baseVoltageLowerBound,
                baseVoltageUppperBound)
            .ConfigureAwait(false);
    }
}
