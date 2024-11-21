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
    public class QAQueries
    {
        private CIMGraph _g;

        public QAQueries(CIMGraph graph)
        {
            _g = graph;
        }

        public MspStationTraceFeederInfoResult MspStationTrafoFeederInfo()
        {
            var result = new MspStationTraceFeederInfoResult();

            var topologyData = (ITopologyProcessingResult)_g.GetProcessingResult("Topology");

            foreach (var node in topologyData.DAXNodes)
            {
                if (node.ClassType == CIMClassEnum.Substation && node.VoltageLevel < 60000)
                {
                    if (node.Transformers != null && node.Transformers.Length > 0)
                    {
                        foreach (var trafo in node.Transformers)
                        {
                            var info = new MspStationTrafoFeederInfo();

                            info.MspName = node.Name;
                            info.MspDesc = node.Description;
                            info.MspTrafo = trafo.Name;
                            info.HspName = "";
                            info.HspTrafo = "";
                            info.HspDesc = "";
                            info.Info = "";

                            result.Result.Add(info);


                            // Customer information
                            int nCustomers = 0;

                            if (node.Feeders != null)
                            {
                                foreach (var feeder in node.Feeders)
                                {
                                    if (feeder.Transformer == trafo)
                                    {
                                        var traceResult = ((TopologyProcessingResult)topologyData).StationFeederBFSTrace(feeder);

                                        foreach (var ti in traceResult)
                                        {
                                            if (ti.ClassType == CIMClassEnum.EnergyConsumer)
                                                nCustomers++;
                                        }
                                    }
                                }
                            }

                            if (trafo.Sources != null)
                            {
                                info.HspName = trafo.Sources[0].Node.Name;
                                info.HspDesc = trafo.Sources[0].Node.Description;
                                info.HspBay = trafo.Sources[0].Name;

                                if (trafo.Sources[0].Transformer != null)
                                    info.HspTrafo = trafo.Sources[0].Transformer.Name;

                                if (trafo.Sources.Length > 1)
                                {
                                    info.Info = "Trafo fødes også fra følgende kilder: ";

                                    for (int i = 1; i < trafo.Sources.Length; i++)
                                        info.Info += trafo.Sources[i].GetDetailLabel() + " ";
                                }
                            }
                            else
                            {
                                if (nCustomers > 0)
                                    info.Info = "Trafo føder aftagepunkter, men er ikke selv forbundet til nogen strømkilde/feeder.";
                            }

                            info.NumberOfCustomers = nCustomers;
                        }
                    }
                }
            }

            return result;
        }

        public EnergyConsumerTraceFeederInfoResult EnergyConsumerFeederInfo(string name = null)
        {
            var result = new EnergyConsumerTraceFeederInfoResult();

            bool first = true;

            var topologyData = (ITopologyProcessingResult)_g.GetProcessingResult("Topology");

            foreach (var node in topologyData.DAXNodes)
            {
                if (name == null || (name != null && node.Name == name))
                {
                    if (node.ClassType == CIMClassEnum.EnergyConsumer)
                    {
                        var info = GetEnergyConsumerFeederInfo(ref first, node);

                        result.Result.Add(info);
                    }
                }
            }

            return result;
        }

        public Queries.EnergyConsumerFeederInfo GetEnergyConsumerFeederInfo(ref bool first, NetworkModel.CIM.DAXElectricNode node)
        {
            var info = new EnergyConsumerFeederInfo();
            info.EnergyConsumerMRID = node.CIMObject.mRID;

            // Make sure first row has tags for SSRS crap to work
            if (first)
            {
                info.HspBay = "";
                info.HspDesc = "";
                info.HspTrafo = "";
                info.HspName = "";
                info.Info = "";
                info.MspName = "";
                info.MspBay = "";
                info.MspDesc = "";
                info.MspTrafo = "";
                info.Name = "";
                info.CableBox = "";
                info.CableBoxFuseSize = "";
                first = false;
            }

            // Find skab
            var traceInfo = TraceLVCustomer(node.CIMObject);
            if (traceInfo != null && traceInfo.FirstNodeType == "CableBox")
            {
                info.CableBox = traceInfo.FirstNode;
                
                if (traceInfo.enclosureFuseFound != null)
                {
                    if (traceInfo.enclosureFuseFound.ContainsPropertyValue("cim.ratedcurrent"))
                    {
                        var ratedCurrent = traceInfo.enclosureFuseFound.GetPropertyValueAsString("cim.ratedcurrent");

                        info.CableBoxFuseSize = ratedCurrent;

                    }
                }
            }



            info.Name = node.Name;
            info.Info = "";

            // MRIDs
            if (traceInfo.feederCableFound != null)
                info.CustomerFeederCableMRID = traceInfo.feederCableFound.mRID;

            if (traceInfo.enclosureFound != null)
                info.CableBoxMRID = traceInfo.enclosureFound.mRID;

            if (traceInfo.enclosureBusbarFound != null)
                info.CableBoxBusbarMRID = traceInfo.enclosureBusbarFound.mRID;

            if (node.Sources == null || (node.Sources != null && node.Sources.Length == 0))
            {
                info.Info = "Ingen feeder!";
                info.Nofeed = true;
            }
            else
            {
                if (node.Sources.Length > 0)
                {
                    var ecSource = node.Sources[0];
                    info.VoltageLevel = ecSource.Feeder.VoltageLevel;

                    if (ecSource.Feeder.VoltageLevel < 1000)
                    {

                        info.MspBay = ecSource.Feeder.Name;
                        info.MspName = ecSource.Feeder.Node.Name;

                        // MRIDs
                        info.SecondarySubstationMRID = ecSource.Feeder.Node.CIMObject.mRID;
                        info.SecondarySubstationBayMRID = ecSource.Feeder.Bay.mRID;

                        if (ecSource.Feeder.Node.Description != null)
                            info.MspDesc = ecSource.Feeder.Node.Description;

                        if (ecSource.Feeder.Transformer != null)
                        {
                            info.MspTrafo = ecSource.Feeder.Transformer.Name;

                            // MRIDs
                            info.SecondarySubstationTransformerMRID = ecSource.Feeder.Transformer.CIMObject.mRID;

                            if (ecSource.Feeder.Transformer.Sources != null)
                            {
                                if (ecSource.Feeder.Transformer.Sources.Length > 0)
                                {
                                    var hspSource = ecSource.Feeder.Transformer.Sources[0];
                                    info.HspBay = hspSource.Name;
                                    info.HspName = hspSource.Node.Name;
                                    info.HspDesc = hspSource.Node.Description;

                                    // MRIDs
                                    info.PrimarySubstationMRID = hspSource.Node.CIMObject.mRID;
                                    info.PrimarySubstationBayMRID = hspSource.Bay.mRID;


                                    if (hspSource.Transformer != null)
                                    {
                                        info.HspTrafo = hspSource.Transformer.Name;

                                        // MRIDs
                                        info.PrimarySubstationTransformerMRID = hspSource.Transformer.CIMObject.mRID;
                                    }

                                    if (ecSource.Feeder.Transformer.Sources.Length > 1)
                                    {
                                        info.Info += "Msp trafo også forsynet fra: ";
                                        for (int i = 1; i < ecSource.Feeder.Transformer.Sources.Length; i++)
                                        {
                                            info.Info += ecSource.Feeder.Transformer.Sources[i].GetDetailLabel();
                                        }
                                    }
                                }
                            }
                            else
                                info.Info += "Msp tranformer ikke forbundet til nogen højspændingsstation. ";

                        }
                        else
                            info.Info += "Msp udføring ikke forbundet til nogen trafo. ";
                    }
                    // MSP eller HSP kunde
                    else
                    {
                        info.HspBay = ecSource.Feeder.Name;
                        info.HspName = ecSource.Feeder.Node.Name;

                        if (ecSource.Feeder.Node.Description != null)
                            info.HspDesc = ecSource.Feeder.Node.Description;

                        if (ecSource.Feeder.Transformer != null)
                            info.HspTrafo = ecSource.Feeder.Transformer.Name;

                    }
                }


                if (node.Sources.Length > 1)
                {
                    info.Multifeed = true;
                    info.Info += "Kunde også også forsynet fra: ";
                    for (int i = 1; i < node.Sources.Length; i++)
                    {
                        info.Info += node.Sources[i].Feeder.GetDetailLabel();
                    }
                }

            }
            return info;
        }


        private CustomerTraceInfo TraceLVCustomer(CIMIdentifiedObject start)
        {
            var neighbours = start.GetNeighbours();

            Queue<CIMIdentifiedObject> Q = new Queue<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> S = new HashSet<CIMIdentifiedObject>();
            Q.Enqueue(start);
            S.Add(start);

            CIMIdentifiedObject substationFound = null;
            CIMIdentifiedObject enclosureFound = null;
            CIMIdentifiedObject enclosureBayFound = null;
            CIMIdentifiedObject enclosureBusbarFound = null;
            CIMIdentifiedObject enclosureFuseFound = null;
            CIMIdentifiedObject feederCableFound = null;

            while (Q.Count > 0)
            {
                CIMIdentifiedObject p = Q.Dequeue();

                foreach (CIMIdentifiedObject cimObj in p.GetNeighbours())
                {
                    bool stop = false;
                    if (!S.Contains(cimObj))
                    {
                        S.Add(cimObj);

                        if (NormalOpen(cimObj))
                            stop = true;

                        if (cimObj.ClassType == CIMClassEnum.ACLineSegment &&
                            cimObj.VoltageLevel != start.VoltageLevel)
                            stop = true;

                        if (cimObj.ClassType == CIMClassEnum.ACLineSegment && S.Count < 10)
                        {
                            var prop = cimObj.GetPropertyValueAsString("cim.asset.type");
                            if (prop != null && prop == "Stikkabel")
                            {
                                feederCableFound = cimObj;
                            }

                        }

                      

                        var root = cimObj.GetEquipmentContainerRoot();

                        if (root != null && root.ClassType == CIMClassEnum.Substation)
                        {
                            if (substationFound == null)
                                substationFound = root;
                            else
                            {
                                if (substationFound != root)
                                    stop = true;
                            }

                            if (substationFound == root && cimObj.ClassType == CIMClassEnum.BusbarSection)
                            {

                            }

                            // HACK
                            stop = true;
                        }

                        if (root != null && root.ClassType == CIMClassEnum.Enclosure)
                        {
                            if (enclosureFound == null)
                                enclosureFound = root;

                            if (enclosureFound == root && cimObj.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.ClassType == CIMClassEnum.Bay)
                            {
                                if (enclosureBayFound == null)
                                    enclosureBayFound = cimObj.EquipmentContainerRef;
                            }

                            if (enclosureFound == root && cimObj.ClassType == CIMClassEnum.BusbarSection)
                            {
                                if (enclosureBusbarFound == null)
                                    enclosureBusbarFound = cimObj;
                            }

                            if (enclosureFound == root && cimObj.ClassType == CIMClassEnum.Fuse)
                            {
                                if (enclosureFuseFound == null)
                                    enclosureFuseFound = cimObj;
                            }

                        }


                        if (!stop)
                        {

                            Q.Enqueue(cimObj);
                        }
                    }
                }
            }

            var traceInfo = new CustomerTraceInfo();
            traceInfo.CustomerName = start.Name;
            traceInfo.CustomerDescription = start.Description;
            traceInfo.VoltageLevel = start.VoltageLevel;

            if (substationFound != null)
            {
                traceInfo.FeederNode = substationFound.Name;

                if (enclosureFound != null)
                {
                    traceInfo.FirstNode = enclosureFound.Name;
                    traceInfo.FirstNodeType = "CableBox";
                    traceInfo.CustomerType = "B2";
                }
                else
                {
                    traceInfo.FirstNodeType = "SecondarySubstation";
                    traceInfo.FirstNode = substationFound.Name;
                    traceInfo.CustomerType = "B2";
                }

            }
            else
            {
                traceInfo.CustomerType = "B2";
                traceInfo.FeederNode = "No feeder found";
            }

            traceInfo.enclosureFound = enclosureFound;
            traceInfo.enclosureBusbarFound = enclosureBusbarFound;
            traceInfo.enclosureBayFound = enclosureBayFound;
            traceInfo.enclosureFuseFound = enclosureFuseFound;
            traceInfo.feederCableFound = feederCableFound;


            return traceInfo;
        }

        private bool NormalOpen(CIMIdentifiedObject cimObj)
        {
            bool? normalOpen = false;

            if (cimObj.ContainsPropertyValue("cim.normalopen"))
                normalOpen = cimObj.GetPropertyValue("cim.normalopen") as bool?;

            return normalOpen.Value;
        }


    }

    public class CustomerTraceInfo
    {
        public int VoltageLevel { get; set; }
        public string CustomerName { get; set; }
        public string CustomerDescription { get; set; }
        public string CustomerType { get; set; }
        public string FeederNode { get; set; }
        public string FirstNode { get; set; }
        public string FirstNodeType { get; set; }

        public CIMIdentifiedObject enclosureFound  { get; set; }
        public CIMIdentifiedObject enclosureBayFound  { get; set; }
        public CIMIdentifiedObject enclosureBusbarFound  { get; set; }
        public CIMIdentifiedObject enclosureFuseFound { get; set; }
        public CIMIdentifiedObject feederCableFound  { get; set; }
    }

    [DataContract]
    public class EnergyConsumerTraceFeederInfoResult
    {
        [XmlElement("Info")]
        public List<EnergyConsumerFeederInfo> Result = new List<EnergyConsumerFeederInfo>();
    }

    [DataContract]
    public class EnergyConsumerFeederInfo
    {
        [DataMember, XmlAttribute]
        public string HspName { get; set; }

        [DataMember, XmlAttribute]
        public string HspDesc { get; set; }

        [DataMember, XmlAttribute]
        public string HspTrafo { get; set; }

        [DataMember, XmlAttribute]
        public string HspBay { get; set; }

        [DataMember, XmlAttribute]
        public string MspName { get; set; }

        [DataMember, XmlAttribute]
        public string MspBay { get; set; }

        [DataMember, XmlAttribute]
        public string MspDesc { get; set; }

        [DataMember, XmlAttribute]
        public string MspTrafo { get; set; }

        [DataMember, XmlAttribute]
        public string Name { get; set; }

        [DataMember, XmlAttribute]
        public string Info { get; set; }

        [DataMember, XmlAttribute]
        public int VoltageLevel { get; set; }

        [DataMember, XmlAttribute]
        public string CableBox { get; set; }

        [DataMember, XmlAttribute]
        public string CableBoxFuseSize { get; set; }

        public bool Nofeed = false;
        public bool Multifeed = false;
        public Guid EnergyConsumerMRID { get; set; }
        public Guid CustomerFeederCableMRID { get; set; }
        public Guid CableBoxMRID { get; set; }
        public Guid CableBoxBusbarMRID { get; set; }
        public Guid SecondarySubstationMRID { get; set; }
        public Guid SecondarySubstationBayMRID { get; set; }
        public Guid SecondarySubstationTransformerMRID { get; set; }
        public Guid PrimarySubstationMRID { get; set; }
        public Guid PrimarySubstationBayMRID { get; set; }
        public Guid PrimarySubstationTransformerMRID { get; set; }
    }

    [DataContract]
    public class MspStationTraceFeederInfoResult
    {
        [XmlElement("Info")]
        public List<MspStationTrafoFeederInfo> Result = new List<MspStationTrafoFeederInfo>();
    }

    [DataContract]
    public class MspStationTrafoFeederInfo
    {
        [DataMember, XmlAttribute]
        public string HspName{ get; set; }

        [DataMember, XmlAttribute]
        public string HspDesc { get; set; }

        [DataMember, XmlAttribute]
        public string HspTrafo { get; set; }

        [DataMember, XmlAttribute]
        public string HspBay { get; set; }

        [DataMember, XmlAttribute]
        public string MspName { get; set; }

        [DataMember, XmlAttribute]
        public string MspDesc { get; set; }

        [DataMember, XmlAttribute]
        public string MspTrafo { get; set; }

        [DataMember, XmlAttribute]
        public int NumberOfCustomers { get; set; }

        [DataMember, XmlAttribute]
        public string Info { get; set; }
    }

}
