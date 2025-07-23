using CIM.Cson;
using DAX.IO;
using DAX.IO.CIM;
using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.IO.CIM.Serialization.CIM100;
using DAX.IO.Writers;
using DAX.Util;
using Serilog;
using System.Diagnostics;
using System.Linq;

namespace CIM.Mapper.Tests.TinyNetwork.NetSam1_3
{
    public class MapNetSamDataTests
    {
        [Fact]
        public async Task MapTinyNetworkToCim100()
        {
            string rootFolder = @"../../../TinyNetwork/NetSam1_3";

            var mapperConfigFile = $"{rootFolder}/mapper_config.xml";

            if (File.Exists(mapperConfigFile))
            {

                Log.Logger = new LoggerConfiguration().WriteTo.Debug().CreateLogger();
                Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

                Logger.WriteToConsole = false;

                var config = new TransformationConfig().LoadFromFile(mapperConfigFile);

                var transformer = config.InitializeDataTransformer("test");

                transformer.TransferData();

                CIMGraphWriter writer = transformer.GetFirstDataWriter() as CIMGraphWriter;
                CIMGraph graph = writer.GetCIMGraph();

                string mapperOutputFileName = $"{rootFolder}/data/mapper_ouput.jsonl";

                // Serialize to CIM 100 (jsonl file)
                var serializer = config.InitializeSerializer("CIM100") as IDAXSerializeable;

                var stopWatch = Stopwatch.StartNew();

                var result = ((CIM100Serializer)serializer).GetIdentifiedObjects(CIMMetaDataManager.Repository, graph.CIMObjects, true, true, true).ToList();

                Assert.True(result.Where(o => o.GetType().Name == "ACLineSegmentExt").Count() == 6);
                Assert.True(result.Where(o => o.GetType().Name == "Substation").Count() == 3);
                Assert.True(result.Where(o => o.GetType().Name == "PowerTransformer").Count() == 3);
                Assert.True(result.Where(o => o.GetType().Name == "PowerTransformerEndExt").Count() == 6);
                Assert.True(result.Where(o => o.GetType().Name == "BayExt").Count() == 18);
                Assert.True(result.Where(o => o.GetType().Name == "BusbarSectionExt").Count() == 6);
                Assert.True(result.Where(o => o.GetType().Name == "Breaker").Count() == 7);
                Assert.True(result.Where(o => o.GetType().Name == "LoadBreakSwitch").Count() == 6);
                Assert.True(result.Where(o => o.GetType().Name == "Disconnector").Count() == 14);
                Assert.True(result.Where(o => o.GetType().Name == "Fuse").Count() == 7);
                Assert.True(result.Where(o => o.GetType().Name == "EnergyConsumer").Count() == 2);
                Assert.True(result.Where(o => o.GetType().Name == "UsagePoint").Count() == 3);

                using (var destination = File.Open(mapperOutputFileName, FileMode.Create))
                {
                    using (var source = new CsonSerializer().SerializeObjects(result))
                    {
                        source.CopyTo(destination);
                    }
                }

                // Run validator
                string validatorOutputFileName = $"{rootFolder}/data/validator_ouput.jsonl";

                await Validator.CLI.Program.Main(new string[] { $"--input-file={mapperOutputFileName}", $"--output-file={validatorOutputFileName}"});

                // This is a hack for now, I'll handle it better in the future where it does not do a contains.
                var validatorLines = File.ReadAllLines(validatorOutputFileName).Where(x => x.Contains("\"Severity\":\"Error\"")).ToList();

                // The input file should have no errors
                Assert.True(validatorLines.Count == 0, "Expected no validation errors, but apparently the validator disagree");
            }
        }
    }
}
