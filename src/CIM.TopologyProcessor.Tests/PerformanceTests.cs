using CIM.Cson;
using CIM.PhysicalNetworkModel;
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

            string bigFileName = "C:/data/tme/mapper_output_test.jsonl";

            if (File.Exists(bigFileName))
            {
                var cimObjects = serializer.DeserializeObjects(File.OpenRead(bigFileName));

                var cimContext = new InMemCimContext(cimObjects);

                var feederInfoContext = new FeederInfoContext(cimContext);
                feederInfoContext.CreateFeederObjects();

                var feederInfoCreator = new FlatFeederInfoCreator();

                var feederInfos = feederInfoCreator.CreateFeederInfos(cimContext, feederInfoContext, '.');

                var notFeededCount = feederInfos.Count(f => f.Nofeed == true);
                var feededCount = feederInfos.Count(f => f.Nofeed == false);
                var multiFeededCount = feederInfos.Count(f => f.Multifeed == true);
                var allowedMultiFeededCount = feederInfos.Count(f => f.MultifeedAllowed == true);

                var allCimObjects = cimContext.GetAllObjects();

                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is ACLineSegment)} ac line segments");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is BusbarSectionExt)} busbars");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is PowerTransformer)} power transformers");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is PowerTransformerEnd)} power transformer ends");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is Breaker)} breakers");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is Disconnector)} disconnectors");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is LoadBreakSwitch)} load break switchs");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is Fuse)} fuses");
                System.Diagnostics.Debug.WriteLine($"{allCimObjects.Count(c => c is EnergyConsumer)} energy consumers");

                System.Diagnostics.Debug.WriteLine($"----");


                System.Diagnostics.Debug.WriteLine($"{feederInfos.Count()} total equipments");
                System.Diagnostics.Debug.WriteLine($"{feededCount} equipments feeded");
                System.Diagnostics.Debug.WriteLine($"{notFeededCount} equipments not feeded");
                System.Diagnostics.Debug.WriteLine($"{multiFeededCount} equipments multi feeded");
                System.Diagnostics.Debug.WriteLine($"{allowedMultiFeededCount} equipments multi feeded allowed");

                System.Diagnostics.Debug.WriteLine($"----");

                System.Diagnostics.Debug.WriteLine($"{feederInfos.Count(c => c.EquipmentClass == "EnergyConsumer" && c.Multifeed)} energy consumers multi feeded");
                System.Diagnostics.Debug.WriteLine($"{feederInfos.Count(c => c.EquipmentClass == "EnergyConsumer" && c.MultifeedAllowed)} energy consumers multi feeded allowed");
                System.Diagnostics.Debug.WriteLine($"{feederInfos.Count(c => c.EquipmentClass == "ACLineSegmentExt" && c.Multifeed)} ac line segments multi feeded");
                System.Diagnostics.Debug.WriteLine($"{feederInfos.Count(c => c.EquipmentClass == "ACLineSegmentExt" && c.MultifeedAllowed)} ac line segments multi feeded allowed");
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
