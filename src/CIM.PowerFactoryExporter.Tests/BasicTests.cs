using CIM.Cson;
using DAX.IO;
using DAX.IO.CIM;
using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.IO.CIM.Serialization.CIM100;
using DAX.IO.Writers;
using System.Diagnostics;
using Serilog;

namespace CIM.PowerFactoryExporter.Tests
{
    public class BasicTests
    {
        [Fact]
        public void MapToCim100()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Debug().CreateLogger();
            Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));

            Logger.WriteToConsole = false;

            var testFolder = "c:/data/test"; 

            var mappingConfigFile = testFolder + "/MapperConfig.xml";

            if (File.Exists(mappingConfigFile))
            {

                var config = new TransformationConfig().LoadFromFile(mappingConfigFile);

                var transformer = config.InitializeDataTransformer("test");

                transformer.TransferData();

                CIMGraphWriter writer = transformer.GetFirstDataWriter() as CIMGraphWriter;
                CIMGraph graph = writer.GetCIMGraph();

                string fileName = testFolder + "/output.jsonl";

                // Serialize to CIM 100 (jsonl file)
                var serializer = config.InitializeSerializer("CIM100") as IDAXSerializeable;

                var stopWatch = Stopwatch.StartNew();

                var result = ((CIM100Serializer)serializer).GetIdentifiedObjects(CIMMetaDataManager.Repository, graph.CIMObjects, true, true, true);

                var cson = new CsonSerializer();

                using (var destination = File.Open(fileName, FileMode.Create))
                {
                    using (var source = cson.SerializeObjects(result))
                    {
                        source.CopyTo(destination);
                    }
                }
            }
        }

        [Fact]
        public void ExportMediumVoltageNetwork()
        {
            var testFolder = "c:/data/test";

            CsonSerializer serializer = new CsonSerializer();

            string jsonInputFileName = testFolder + "output.jsonl";

            if (File.Exists(jsonInputFileName))
            {
                var cimObjects = serializer.DeserializeObjects(File.OpenRead(jsonInputFileName));

                string folder = testFolder + "/pf";

                var writer = new CimArchiveWriter(cimObjects, folder, "10kv", Guid.Parse("b8a2ec4d-8337-4a1c-9aec-32b8335435c0"), ExportKind.MediumVoltage);
            }
        }
    }
}