﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CIM.PowerFactoryExporter
{
    /// <summary>
    /// CGMES geographical layout (GL) profil RDF/XML builder
    /// Ugly - just MVP / for quick prototyping purpose. Should be refactored to use RDF library.
    /// </summary>
    public class GL_Writer
    {
        string _fileName = null;
        StreamWriter _writer = null;

        string _startContent = @"<?xml version='1.0' encoding='UTF-8'?>
 <rdf:RDF xmlns:entsoe='http://entsoe.eu/CIM/SchemaExtension/3/1#' xmlns:cim='http://iec.ch/TC57/2013/CIM-schema-cim16#' xmlns:md='http://iec.ch/TC57/61970-552/ModelDescription/1#' xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
  <md:FullModel rdf:about='urn:uuid:f096a1ff-281c-004f-3a89-9483fc7ab964'>
    <md:Model.DependentOn rdf:resource='" + EQ_Writer._eqModelId.ToString() + @"' />
    <md:Model.scenarioTime>" + EQ_Writer._timeStamp.ToString() + @"</md:Model.scenarioTime>
    <md:Model.created>" + EQ_Writer._timeStamp.ToString() + @"</md:Model.created>
    <md:Model.modelingAuthoritySet>http://TME.dk/Planning/1</md:Model.modelingAuthoritySet>
    <md:Model.description>DAX Konstant PowerFactory Export</md:Model.description>
    <md:Model.version>4</md:Model.version>
    <md:Model.profile>http://entsoe.eu/CIM/GeographicalLocation/2/1</md:Model.profile>
  </md:FullModel>

  <cim:CoordinateSystem rdf:ID= '_e4c22cb0-c8fc-11e1-bd6b-b8f6b1180b6d' >
    <cim:IdentifiedObject.name>WGS84</cim:IdentifiedObject.name>
    <cim:CoordinateSystem.crsUrn>urn:ogc:def:crs:EPSG::4326</cim:CoordinateSystem.crsUrn>
  </cim:CoordinateSystem>";

        int _locationNameCounter = 1;

        UTM32WGS84Converter converter = new();

        public GL_Writer(string fileName)
        {
            _fileName = fileName;
            Open();
        }

        private void Open()
        {
            _writer = new StreamWriter(_fileName, false, Encoding.UTF8);
            _writer.Write(_startContent);
            _writer.Write("\r\n\r\n");
        }

        public void Close()
        {
            string xml = "</rdf:RDF>\r\n";
            _writer.Write(xml);
            _writer.Close();
        }

        public void AddLocation(Guid psrId, PhysicalNetworkModel.LocationExt loc)
        {
            Guid locationId = AddLocation(psrId, Guid.Parse(loc.mRID));

            if (loc.GeometryType == PhysicalNetworkModel.GeometryType.Point)
            {
                var point = JsonConvert.DeserializeObject<double[]>(loc.Geometry);
                AddPositionPoint(locationId, 0, point[0], point[1]);
            }
            else
            {
                int seqNo = 1;
                var points = JsonConvert.DeserializeObject<double[][]>(loc.Geometry);
                foreach (var point in points)
                {
                    AddPositionPoint(locationId, seqNo, point[0], point[1]);
                    seqNo++;
                }
            }
        }

        /*
        public void AddLocationWithSingleCoordinate(Guid psrId, double x, double y)
        {
            Guid locationId = AddLocation(psrId);
            AddPositionPoint(locationId, 0, x, y);
        }

        public void AddLocationWithMultipleCoordinates(Guid psrId, double[] coords)
        {
            Guid locationId = AddLocation(psrId);

            int seqNo = 1;
            for (int i = 0; i < (coords.Length - 1); i += 2)
            {
                AddPositionPoint(locationId, seqNo, coords[i], coords[i + 1]);
                seqNo++;
            }
        }
        */

        private Guid AddLocation(Guid psrId, Guid locationId)
        {
            // Create location element
            string xml = "<cim:Location rdf:ID = '_" + locationId + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + _locationNameCounter++ + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Location.CoordinateSystem rdf:resource = '#_e4c22cb0-c8fc-11e1-bd6b-b8f6b1180b6d'/>\r\n";
            xml += "  <cim:Location.PowerSystemResources rdf:resource = '#_" + psrId + "'/>\r\n";
            xml += "</cim:Location>\r\n\r\n";
            _writer.Write(xml);

            return locationId;
        }

        private void AddPositionPoint(Guid locationId, int seqNr, double x, double y)
        {
            string positionPointId = Guid.NewGuid().ToString();

            string xml = "<cim:PositionPoint rdf:ID='" + positionPointId + "'>\r\n";

            if (seqNr > 0)
                xml += "  <cim:PositionPoint.sequenceNumber>" + seqNr + "</cim:PositionPoint.sequenceNumber>\r\n";

            var wgs84 = converter.ConvertFromUTM32NToWGS84(x, y);

            xml += "  <cim:PositionPoint.xPosition>" + DoubleToString(wgs84[0]) + "</cim:PositionPoint.xPosition>\r\n";
            xml += "  <cim:PositionPoint.yPosition>" + DoubleToString(wgs84[1]) + "</cim:PositionPoint.yPosition>\r\n";
            xml += "  <cim:PositionPoint.Location rdf:resource = '#_" + locationId + "'/>\r\n";
            xml += "</cim:PositionPoint>\r\n\r\n";
            _writer.Write(xml);
        }

        private string DoubleToString(double value)
        {
            return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
        }
    }
}

