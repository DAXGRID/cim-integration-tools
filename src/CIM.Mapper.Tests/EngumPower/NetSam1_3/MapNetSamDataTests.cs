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

namespace CIM.Mapper.Tests.EngumPower.NetSam1_3
{
    public class MapNetSamDataTests
    {
        [Fact]
        public void MapNetSamDataToCim100()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Debug().CreateLogger();
            Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

            Logger.WriteToConsole = false;

            var config = new TransformationConfig().LoadFromFile("../../../EngumPower/NetSam1_3/MapNetSamDataConfig.xml");

            var transformer = config.InitializeDataTransformer("test");

            transformer.TransferData();

            CIMGraphWriter writer = transformer.GetFirstDataWriter() as CIMGraphWriter;
            CIMGraph graph = writer.GetCIMGraph();

            string fileName = @"../../../EngumPower/NetSam1_3/data/Ouput.jsonl";

            // Serialize to CIM 100 (jsonl file)
            var serializer = config.InitializeSerializer("CIM100") as IDAXSerializeable;

            var stopWatch = Stopwatch.StartNew();

            var result = ((CIM100Serializer)serializer).GetIdentifiedObjects(CIMMetaDataManager.Repository, graph.CIMObjects, false, true, false).ToList();

            var cson = new CsonSerializer();

            using (var destination = File.Open(fileName, FileMode.Create))
            {
                using (var source = cson.SerializeObjects(result))
                {
                    source.CopyTo(destination);
                }
            }
        }

        [Fact]
        public void MapBigDataFileToCim100()
        {
            var mapperConfigFile = "c:/data/big/mapper_config.xml";

            if (File.Exists(mapperConfigFile))
            {

                Log.Logger = new LoggerConfiguration().WriteTo.Debug().CreateLogger();
                Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

                Logger.WriteToConsole = false;

                var config = new TransformationConfig().LoadFromFile("c:/data/big/mapper_config.xml");

                var transformer = config.InitializeDataTransformer("test");

                transformer.TransferData();

                CIMGraphWriter writer = transformer.GetFirstDataWriter() as CIMGraphWriter;
                CIMGraph graph = writer.GetCIMGraph();

                string fileName = @"c:/data/big/ouput.jsonl";

                // Serialize to CIM 100 (jsonl file)
                var serializer = config.InitializeSerializer("CIM100") as IDAXSerializeable;

                var stopWatch = Stopwatch.StartNew();

                var result = ((CIM100Serializer)serializer).GetIdentifiedObjects(CIMMetaDataManager.Repository, graph.CIMObjects, true, true, true).ToList();

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
    }
}