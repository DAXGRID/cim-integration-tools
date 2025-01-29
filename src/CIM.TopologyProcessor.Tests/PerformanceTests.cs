using CIM.Cson;
using CIM.PhysicalNetworkModel.FeederInfo;
using CIM.PhysicalNetworkModel.Traversal.Internals;

namespace CIM.TopologyProcessor.Tests
{
    public class PerformanceTests
    {
        [Fact]
        public void PerformanceTest1()
        {
            CsonSerializer serializer = new CsonSerializer();

            string bigFileName = "C:/data/big.jsonl";

            if (File.Exists(bigFileName))
            {
                var cimObjects = serializer.DeserializeObjects(File.OpenRead(bigFileName));

                var cimContext = new InMemCimContext(cimObjects);

                var feederInfoContext = new FeederInfoContext(cimContext);
                feederInfoContext.CreateFeederObjects();

                var feederInfoCreator = new FlatFeederInfoCreator();

                var feeders = feederInfoCreator.CreateFeederInfos(cimContext, feederInfoContext);

                int notFeededCount = feeders.Count(f => f.Nofeed == true);
                int feededCount = feeders.Count(f => f.Nofeed == false);
                int multiFeededCount = feeders.Count(f => f.Multifeed == true);

                System.Console.WriteLine(feeders.Count() + " total equipments");
                System.Console.WriteLine(feededCount + " equipments feeded");
                System.Console.WriteLine(notFeededCount + " equipments not feeded");
                System.Console.WriteLine(multiFeededCount + " equipments multi feeded");
            }
        }
    }
}
