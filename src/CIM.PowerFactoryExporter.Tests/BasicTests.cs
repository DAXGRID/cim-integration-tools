using CIM.Cson;
using CIM.Filter.CLI;
using CIM.PhysicalNetworkModel;
using DAX.IO;
using DAX.IO.CIM;
using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.IO.CIM.Serialization.CIM100;
using DAX.IO.Writers;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

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

            var testFolder = "c:/data/TME"; 

            var mappingConfigFile = testFolder + "/MapperConfig.xml";

            if (File.Exists(mappingConfigFile))
            {

                var config = new TransformationConfig().LoadFromFile(mappingConfigFile);

                var transformer = config.InitializeDataTransformer("test");

                transformer.TransferData();

                CIMGraphWriter writer = transformer.GetFirstDataWriter() as CIMGraphWriter;
                CIMGraph graph = writer.GetCIMGraph();

                string fileName = testFolder + "/mapper_output.jsonl";

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
        public async Task ExportMediumVoltageNetworkOldWay()
        {
            var testFolder = "c:/data/TME";

            CsonSerializer serializer = new CsonSerializer();

            string jsonInputFileName = testFolder + "/mapper_output.jsonl";

            if (File.Exists(jsonInputFileName))
            {
                var cimObjects = serializer.DeserializeObjects(File.OpenRead(jsonInputFileName)).ToList();

                double minVoltage = 10000;
                double maxVoltage = 10000;


                // Equipment containers that must be included
                HashSet<string> containesToInclude = new HashSet<string>();


                // Find all power transformer with an power tranformer end within voltage range
                HashSet<string> powerTransformersWithinVoltageRange = new HashSet<string>();

                foreach (var cimObject in cimObjects)
                {
                    if (cimObject is PowerTransformerEnd ce)
                    {
                        if (ce.BaseVoltage >= minVoltage && ce.BaseVoltage <= maxVoltage)
                            powerTransformersWithinVoltageRange.Add(ce.PowerTransformer.@ref);
                    }
                }


                // Find all conducting equipments that is not within voltage range
                HashSet<string> conductingEquipmentWithinVoltageRange = new HashSet<string>();

                foreach (var cimObject in cimObjects)
                {
                    if (cimObject is ConductingEquipment ce)
                    {
                        if (ce.BaseVoltage >= minVoltage && ce.BaseVoltage <= maxVoltage)
                            conductingEquipmentWithinVoltageRange.Add(ce.mRID);

                        if (ce.EquipmentContainer != null && ce.EquipmentContainer.@ref != null)
                            containesToInclude.Add(ce.EquipmentContainer.@ref);
                    }
                }


                string folder = testFolder + "/pf";

                var writer = new CimArchiveWriter(cimObjects, folder, "complete", Guid.Parse("b8a2ec4d-8337-4a1c-9aec-32b8335435c0"), "TME");
            }
        }

        [Fact]
        public void Test60kVsplit()
        {

            var testFolder = "c:/data/TME";

            CsonSerializer serializer = new CsonSerializer();

            string jsonInputFileName = testFolder + "/mapper_output.jsonl";

            if (File.Exists(jsonInputFileName))
            {
                var cimObjects = serializer.DeserializeObjects(File.OpenRead(jsonInputFileName)).ToList();
             
                string folder = @"C:\temp\pf";


                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);


                foreach (var st in cimObjects.Where(c => c is Substation && ((Substation)c).PSRType == "PrimarySubstation"))
                {
                    if (st.name != null)
                    {
                        if (!File.Exists(@"C:\temp\pf\" + st.name + ".zip"))
                        {
                            if (Directory.Exists(@"C:\temp\pf\files"))
                                Directory.Delete(@"C:\temp\pf\files", true);

                            new CimArchiveWriter(cimObjects, folder, st.name, Guid.Parse("b8a2ec4d-8337-4a1c-9aec-32b8335435c0"), ExportKind.MediumVoltageSplit, st.name);
                        }
                    }
                }
            }
        }

    }
}
