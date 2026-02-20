using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PowerFactoryExporter
{
    /// <summary>
    /// Protection information writer.
    /// Very ugly - just for quick prototyping purpose. Should be refactored to use RDF library.
    /// </summary>
    public class PE_Writer
    {
        string _fileName = null;
        StreamWriter _writer = null;
        CimContext _cimContext = null;

        HashSet<string> _objectAlreadyAdded = new HashSet<string>();

        public static string _protectionModelId = Guid.NewGuid().ToString();
        public static DateTime _timeStamp = DateTime.Now;

        MappingContext _mappingContext;

        private string GetStartContent()
        {
            return (@"<?xml version='1.0' encoding='UTF-8'?>
              <rdf:RDF xmlns:entsoe='http://entsoe.eu/CIM/SchemaExtension/3/1#' xmlns:cim='http://iec.ch/TC57/2013/CIM-schema-cim16#' xmlns:md='http://iec.ch/TC57/61970-552/ModelDescription/1#' xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
              <md:FullModel rdf:about='" + _protectionModelId.ToString() + @"'>
                <md:Model.DependentOn rdf:resource='" + EQ_Writer._eqModelId.ToString() + @"' />
                <md:Model.scenarioTime>" + EQ_Writer._timeStamp.ToString() + @"</md:Model.scenarioTime>
                <md:Model.created>" + EQ_Writer._timeStamp.ToString() + @"</md:Model.created>
                <md:Model.description>" + _mappingContext.OrganisationName + @" Export</md:Model.description>
                <md:Model.version>1</md:Model.version>
                <md:Model.profile>http://" + _mappingContext.OrganisationName + @".dk/CIM/ProtectionExtension/1/1</md:Model.profile>
                <md:Model.modelingAuthoritySet>http://" + _mappingContext.OrganisationName + @".dk/Planning/1</md:Model.modelingAuthoritySet>
              </md:FullModel>
            ");
         }

     
        

        public PE_Writer(string fileName, CimContext cimContext, MappingContext mappingContext)
        {
            _fileName = fileName;
            _cimContext = cimContext;
            _mappingContext = mappingContext;

            Open();
        }

        private void Open()
        {
            _writer = new StreamWriter(_fileName, false, Encoding.UTF8);
            _writer.Write(GetStartContent());
            _writer.Write("\r\n\r\n");
        }

        public void Close()
        {
            string xml = "</rdf:RDF>\r\n";
            _writer.Write(xml);
            _writer.Close();
        }

        public void AddPNMObject(PhysicalNetworkModel.ProtectionEquipmentExt pe)
        {
            string xml = "<cim:ProtectionEquipment rdf:ID='_" + pe.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + pe.name + "</cim:IdentifiedObject.name>\r\n";

            if (pe.ProtectedSwitches != null && pe.ProtectedSwitches.Length > 0)
                xml += "  <cim:ProtectionEquipment.ProtectedSwitches rdf:resource='#_" + pe.ProtectedSwitches[0].@ref + "' />\r\n";

            if (pe.CurrentTransformers != null && pe.CurrentTransformers.Length > 0)
                xml += "  <cim:ProtectionEquipment.CurrentTransformers rdf:resource='#_" + pe.CurrentTransformers[0].@ref + "' />\r\n";

            if (pe.PotentialTransformers != null && pe.PotentialTransformers.Length > 0)
                xml += "  <cim:ProtectionEquipment.PotentialTransformer rdf:resource='#_" + pe.PotentialTransformers[0].@ref + "' />\r\n";

            xml += "</cim:ProtectionEquipment>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.CurrentTransformer pe)
        {
            string xml = "<cim:CurrentTransformer rdf:ID='_" + pe.mRID + "'>\r\n";

            if (pe.name != null)
                xml += "  <cim:IdentifiedObject.name>" + pe.name + "</cim:IdentifiedObject.name>\r\n";

            if (pe.Terminal != null)
                xml += "  <cim:AuxiliaryEquipment.Terminal rdf:resource='#_" + pe.Terminal.@ref + "' />\r\n";

            xml += "</cim:CurrentTransformer>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.PotentialTransformer pe)
        {
            string xml = "<cim:PotentialTransformer rdf:ID='_" + pe.mRID + "'>\r\n";

            if (pe.name != null)
                xml += "  <cim:IdentifiedObject.name>" + pe.name + "</cim:IdentifiedObject.name>\r\n";

            if (pe.Terminal != null)
                xml += "  <cim:AuxiliaryEquipment.Terminal rdf:resource='#_" + pe.Terminal.@ref + "' />\r\n";


            xml += "</cim:PotentialTransformer>\r\n\r\n";
            _writer.Write(xml);
        }


        private string DoubleToString(double value)
        {
            return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
        }

        private string DoubleToString(Decimal value)
        {
            return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
        }

        private double ConvertToEntsoeVoltage(double value)
        {
            return value / 1000;
        }
    }
}
