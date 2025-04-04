using CIM.PhysicalNetworkModel;
using CIM.PhysicalNetworkModel.LineInfo;
using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Internals;
using CIM.PowerFactoryExporter.PreProcessors;
using DAX.IO.CIM;
using System.IO.Compression;

namespace CIM.PowerFactoryExporter
{
    /// <summary>
    /// Create a PF CIM archive (zip file with CIM RDF XML files).
    /// </summary>
    public class CimArchiveWriter
    {
        public CimArchiveWriter(IEnumerable<PhysicalNetworkModel.IdentifiedObject> cimObjects, string outputFolder, string archiveName, Guid modelRdfId)
        {
            var mappingContext = new MappingContext();

            var converter = new PNM2PowerFactoryCimConverter(cimObjects,
               new List<IPreProcessor> {
                    new ACLSMerger(mappingContext),
                    new TransformerCableMerger(mappingContext),
                    new DanishDSODataPrepare(mappingContext)
               });

            var _ = CreateCimArchive(outputFolder, archiveName, modelRdfId, mappingContext, converter.GetCimObjects());
        }

        private static CimContext CreateCimArchive(string outputFolder, string archiveName, Guid modelRdfId, MappingContext mappingContext, IEnumerable<IdentifiedObject> outputCimObjects)
        {
            // We need to reinitialize context, because converter has modified objects
            CimContext _context = new InMemCimContext(outputCimObjects);
            Dictionary<string, string> assetToEqRefs = new Dictionary<string, string>();

            System.IO.Directory.CreateDirectory(outputFolder);
            System.IO.Directory.CreateDirectory(outputFolder + "\\files");

            string eqTempFileName = outputFolder + @"\files\" + archiveName + "_eq.xml";
            string glTempFileName = outputFolder + @"\files\" + archiveName + "_gl.xml";
            string aiTempFileName = outputFolder + @"\files\" + archiveName + "_ai.xml";
            string peTempFileName = outputFolder + @"\files\" + archiveName + "_pe.xml";

            var eqWriter = new EQ_Writer(eqTempFileName, _context, mappingContext, modelRdfId, archiveName);
            eqWriter.ForceThreePhases = true;

            var glWriter = new GL_Writer(glTempFileName);
            var aiWriter = new AI_Writer(aiTempFileName, _context, mappingContext);
            var peWriter = new PE_Writer(peTempFileName, _context, mappingContext);


            //////////////////////
            // do the lines
            var lineContext = new LineInfoContext(_context);
            //lineContext.CreateLineInfo();
            Dictionary<SimpleLine, string> lineToGuid = new Dictionary<SimpleLine, string>();

            foreach (var line in lineContext.GetLines())
            {
                var lineGuid = GUIDHelper.CreateDerivedGuid(Guid.Parse(line.Children[0].Equipment.mRID), 678, true).ToString();
                lineToGuid.Add(line, lineGuid);

                //eqWriter.AddLine(lineGuid, line.Name);
            }

            //////////////////////
            // do the general cim objects
            foreach (var cimObject in _context.GetAllObjects())
            {
                if (!(cimObject is Location) && !(cimObject is VoltageLevel && ((VoltageLevel)cimObject).BaseVoltage < 400))
                {
                    if (cimObject is ACLineSegment)
                    {
                        var acls = cimObject as ACLineSegment;

                        if (acls.length == null)
                            acls.length = new Length() { Value = 0, multiplier = UnitMultiplier.none, unit = UnitSymbol.m };

                        var lines = lineContext.GetLines().Where(l => l.Children.Exists(c => c.Equipment == acls)).ToList();

                        if (lines.Count == 1)
                        {
                            var line = lines[0];
                            eqWriter.AddPNMObject(acls, lineToGuid[line]);
                        }
                        else
                            eqWriter.AddPNMObject((dynamic)cimObject);

                    }
                    else
                    {
                        // Don't add things that goes into asset and protectionn file
                        if (!(
                            (cimObject is PhysicalNetworkModel.Asset) ||
                            (cimObject is PhysicalNetworkModel.AssetInfo) ||
                            (cimObject is PhysicalNetworkModel.ProductAssetModel) ||
                            (cimObject is PhysicalNetworkModel.Manufacturer) ||
                            (cimObject is CurrentTransformerExt) ||
                            (cimObject is PotentialTransformer) ||
                            (cimObject is ProtectionEquipmentExt)
                            ))
                            eqWriter.AddPNMObject((dynamic)cimObject);
                    }
                }

                if (cimObject is PowerSystemResource)
                {
                    var psrObj = cimObject as PowerSystemResource;

                    if (psrObj.Location != null && psrObj.Location.@ref != null && psrObj.PSRType != "InternalCable")
                    {
                        var loc = _context.GetObject<PhysicalNetworkModel.LocationExt>(psrObj.Location.@ref);
                        glWriter.AddLocation(Guid.Parse(psrObj.mRID), loc);
                    }
                    if (psrObj.Assets != null && psrObj.Assets.@ref != null)
                        assetToEqRefs.Add(psrObj.Assets.@ref, psrObj.mRID);
                }
            }

            //////////////////////
            // do the asset object
            foreach (var cimObject in _context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.Asset)
                {
                    if (assetToEqRefs.ContainsKey(cimObject.mRID))
                    {
                        var eqMrid = assetToEqRefs[cimObject.mRID];
                        aiWriter.AddPNMObject((dynamic)cimObject, eqMrid);
                    }
                }

                if (cimObject is PhysicalNetworkModel.AssetInfo)
                    aiWriter.AddPNMObject((dynamic)cimObject);

                if (cimObject is PhysicalNetworkModel.ProductAssetModel)
                    aiWriter.AddPNMObject((dynamic)cimObject);

                if (cimObject is PhysicalNetworkModel.Manufacturer)
                    aiWriter.AddPNMObject((dynamic)cimObject);

            }

            //////////////////////
            // do the projection object
            foreach (var cimObject in _context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.ProtectionEquipment)
                {
                    peWriter.AddPNMObject((dynamic)cimObject);
                }
                if (cimObject is PhysicalNetworkModel.PotentialTransformer)
                {
                    peWriter.AddPNMObject((dynamic)cimObject);
                }
                if (cimObject is PhysicalNetworkModel.CurrentTransformer)
                {
                    peWriter.AddPNMObject((dynamic)cimObject);
                }
            }

            eqWriter.Close();
            glWriter.Close();
            aiWriter.Close();
            peWriter.Close();

            string startPath = outputFolder + "\\files";
            string zipPath = outputFolder + "\\" + archiveName + ".zip";

            File.Delete(zipPath);

            ZipFile.CreateFromDirectory(startPath, zipPath);
            return _context;
        }
    }
}
