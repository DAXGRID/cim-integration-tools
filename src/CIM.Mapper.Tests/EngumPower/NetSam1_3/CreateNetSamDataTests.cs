using DAX.IO;
using DAX.IO.CIM;
using DAX.IO.CIM.DataModel;
using DAX.IO.Serialization.NetSam_1_3;
using DAX.IO.Writers;
using DAX.Util;
using Serilog;
using System.Diagnostics;
using System.Xml.Serialization;

namespace CIM.Mapper.Tests.EngumPower.NetSam1_3
{
    // This has been commented out for now until we have fixed issue with pipeline.

    // public class CreateNetSamDataTests
    // {
    //     [Fact]
    //     public void CreateNetSam_1_3_TestData()
    //     {
    //         Log.Logger = new LoggerConfiguration().WriteTo.Debug().CreateLogger();
    //         Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

    //         Logger.WriteToConsole = false;

    //         var config = new TransformationConfig().LoadFromFile("../../../EngumPower/NetSam1_3/CreateNetSamDataConfig.xml");

    //         var transformer = config.InitializeDataTransformer("test");

    //         transformer.TransferData();

    //         CIMGraphWriter writer = transformer.GetFirstDataWriter() as CIMGraphWriter;
    //         CIMGraph graph = writer.GetCIMGraph();

    //         string fileName = @"../../../EngumPower/NetSam1_3/data/Input.xml";

    //         var serializer = config.InitializeSerializer("NetSam");

    //         var env = ((NetSamSerializer)serializer).Serialize(CIMMetaDataManager.Repository, graph.CIMObjects);
    //         XmlSerializer xmlSerializer = new XmlSerializer(env.GetType());
    //         StreamWriter file = new StreamWriter(fileName);
    //         xmlSerializer.Serialize(file, env);
    //         file.Close();
    //     }
    // }
}
