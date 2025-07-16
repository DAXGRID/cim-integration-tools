using CIM.Cson;
using CIM.PhysicalNetworkModel.FeederInfo;
using CIM.PhysicalNetworkModel.Traversal.Internals;

namespace CIM.TopologyProcessor.Tests
{
    public class PerformanceTests
    {
        [Fact]
        public void SimpleTest1()
        {
            CsonSerializer serializer = new CsonSerializer();

            string bigFileName = "C:/data/tme/tme_output.jsonl";

            if (File.Exists(bigFileName))
            {
                var cimObjects = serializer.DeserializeObjects(File.OpenRead(bigFileName));

                var cimContext = new InMemCimContext(cimObjects);

                var feederInfoContext = new FeederInfoContext(cimContext);
                feederInfoContext.CreateFeederObjects();

                var feederInfoCreator = new FlatFeederInfoCreator();

                var feeders = feederInfoCreator.CreateFeederInfos(cimContext, feederInfoContext);

                var notFeededCount = feeders.Count(f => f.Nofeed == true);
                var feededCount = feeders.Count(f => f.Nofeed == false);
                var multiFeededCount = feeders.Count(f => f.Multifeed == true);

                System.Diagnostics.Debug.WriteLine($"{feeders.Count()} total equipments");
                System.Diagnostics.Debug.WriteLine($"{feededCount} equipments feeded");
                System.Diagnostics.Debug.WriteLine($"{notFeededCount} equipments not feeded");
                System.Diagnostics.Debug.WriteLine($"{multiFeededCount} equipments multi feeded");
            }
        }


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

                var notFeededCount = feeders.Count(f => f.Nofeed == true);
                var feededCount = feeders.Count(f => f.Nofeed == false);
                var multiFeededCount = feeders.Count(f => f.Multifeed == true);

                Console.WriteLine($"{feeders.Count()} total equipments");
                Console.WriteLine($"{feededCount} equipments feeded");
                Console.WriteLine($"{notFeededCount} equipments not feeded");
                Console.WriteLine($"{multiFeededCount} equipments multi feeded");
            }
        }


    }
}
