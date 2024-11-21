using DAX.IO.CIM.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DAX.IO.CIM.Queries
{
    public class SubstationQueries
    {
        private CIMGraph _g;

        public SubstationQueries(CIMGraph graph)
        {
            _g = graph;
        }

        public List<StationTraceInfo> SubstationTrace(string stationName, bool includeLV = false)
        {
            var topologyData = (ITopologyProcessingResult)_g.GetProcessingResult("Topology");

            List<StationTraceInfo> result = new List<StationTraceInfo>();

            // Try with case
            var daxNode = topologyData.GetDAXNodeByName(stationName);

            // Then try with upper case
            if (daxNode == null)
                daxNode = topologyData.GetDAXNodeByName(stationName.ToUpper());

            if (daxNode != null)
            {
                InternalTraceStation(topologyData, result, daxNode, includeLV);
            }

            return result;

        }

        private void InternalTraceStation(ITopologyProcessingResult topologyData, List<StationTraceInfo> result, NetworkModel.CIM.DAXElectricNode daxNode, bool includeLV = false, string stationName = null, string trafoName = null, string feederName = null)
        {
            var st = daxNode;

            bool firstRow = true;

            if (st.Feeders != null)
            {
                foreach (var feeder in st.Feeders)
                {
                    if (feeder.Trace != null)
                    {
                        foreach (var trace in feeder.Trace)
                        {
                            bool dontAdd = false;

                            if (Math.Round(trace.Length, 2) != 0.1)
                            {
                                var ti = new StationTraceInfo();

                                if (stationName == null)
                                    ti.StName = st.Name;
                                else
                                    ti.StName = stationName;

                                if (feederName == null)
                                    ti.StFeeder = feeder.Name;
                                else
                                    ti.StFeeder = feederName;

                                ti.Name = trace.Name;
                                ti.Type = trace.Type;
                                ti.VoltageLevel = trace.VoltageLevel;
                                ti.CIMType = trace.ClassType.ToString();

                                if (trace.Details != null)
                                    ti.Details = trace.Details;

                                ti.Length = Math.Round(trace.Length, 2);
                                ti.BranchInfo = trace.BranchInfo;

                                if (trafoName == null)
                                {
                                    if (feeder.Transformer != null)
                                        ti.StTrafo = feeder.Transformer.Name;
                                }
                                else
                                    ti.StTrafo = trafoName;

                                // Hvis installation tilføj aftagenummer
                                if (trace.ClassType == CIMClassEnum.EnergyConsumer)
                                {
                                    var cimObj = _g.ObjectManager.GetCIMObjectById(trace.CIMObjectId);
                                    if (cimObj != null)
                                    {
                                        ti.Aftagenummer = cimObj.Name;
                                    }

                                }

                                // Tilføj ikke kabler som ligger inde i stationer (forbindelse kabler)
                                if (trace.ClassType == CIMClassEnum.ACLineSegment)
                                {
                                    var cimObj = _g.ObjectManager.GetCIMObjectById(trace.CIMObjectId);

                                    if (cimObj != null && cimObj.EquipmentContainerRef != null)
                                        dontAdd = true;
                                }


                                // Include low voltage, if parameter is set to true
                                if (includeLV && st.VoltageLevel > 20000 && trace.ClassType == CIMClassEnum.Substation && trace.VoltageLevel < 20000)
                                {
                                    bool traceSecondaryStation = false;

                                    var subNode = topologyData.GetDAXNodeByName(trace.Name);
                                    if (subNode != null && subNode.Transformers != null)
                                    {
                                        foreach (var trafo in subNode.Transformers)
                                        {
                                            if (trafo.Sources != null)
                                            {
                                                foreach (var source in trafo.Sources)
                                                {
                                                    if (source == feeder)
                                                        traceSecondaryStation = true;
                                                }
                                            }
                                        }

                                        if (traceSecondaryStation)
                                            InternalTraceStation(topologyData, result, subNode, false, ti.StName, ti.StFeeder);
                                        else
                                            dontAdd = true;
                                    }
                                }

                                if (!dontAdd)
                                {
                                    result.Add(ti);

                                    if (firstRow)
                                    {
                                        firstRow = false;
                                        if (ti.StName == null)
                                            ti.StName = "";

                                        if (ti.StFeeder == null)
                                            ti.StFeeder = "";

                                        if (ti.Name == null)
                                            ti.Name = "";

                                        if (ti.Type == null)
                                            ti.Type = "";

                                        if (ti.VoltageLevel == null)
                                            ti.VoltageLevel = 0;

                                        if (ti.CIMType == null)
                                            ti.CIMType = "";

                                        if (ti.Details == null)
                                            ti.Details = "";

                                        if (ti.Length == null)
                                            ti.Length = 0;

                                        if (ti.BranchInfo == null)
                                            ti.BranchInfo = "";

                                        if (ti.Aftagenummer == null)
                                            ti.Aftagenummer = "";
                                    }
                                }
                            }
                            else
                            {

                            }
                        }
                    }
                }
            }
        }
    }

    [DataContract]
    public class StationTraceInfo
    {
        [DataMember, XmlAttribute]
        public string StName { get; set; }

        [DataMember, XmlAttribute]
        public string StTrafo { get; set; }

        [DataMember, XmlAttribute]
        public string StFeeder { get; set; }

        [DataMember, XmlAttribute]
        public string Name { get; set; }

        [DataMember, XmlAttribute]
        public string Type { get; set; }

        [DataMember, XmlAttribute]
        public string Details { get; set; }

        [DataMember, XmlAttribute]
        public string CIMType { get; set; }

        [DataMember, XmlAttribute]
        public double Length { get; set; }

        [DataMember, XmlAttribute]
        public string BranchInfo { get; set; }

        [DataMember, XmlAttribute]
        public int VoltageLevel { get; set; }

        [DataMember, XmlAttribute]
        public string Aftagenummer { get; set; }

    }


}
