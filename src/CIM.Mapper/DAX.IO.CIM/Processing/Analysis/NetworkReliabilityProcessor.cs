using DAX.IO.CIM.Processing;
using DAX.NetworkModel.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public class NetworkReliabilityProcessor : IGraphProcessor
    {
        CIMGraph _g = null;
        CimErrorLogger _tableLogger = null;
        NetworkReliabilityProcessingResult _result = new NetworkReliabilityProcessingResult();
        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

        public void Run(CIMGraph g, CimErrorLogger tableLogger)
        {
            _g = g;
            _tableLogger = tableLogger;

            var topologyData = (ITopologyProcessingResult)_g.GetProcessingResult("Topology");

            foreach (var feeder in topologyData.DAXFeeders)
            {
                // Trace medium voltage feeders
                if (!feeder.IsTransformerFeeder && feeder.VoltageLevel > 5000 && feeder.VoltageLevel < 20000)
                {
                    var traceResult = StationFeederDFSTrace(feeder);

                    CIMConnectivityNode lastCn = null;
                    foreach (var ti in traceResult)
                    {
                        if (ti.ClassType == CIMClassEnum.ACLineSegment && ti.EquipmentContainerRef == null)
                        {
                            ACLineSegmentFlowInfo info = null;
                            if (!_result.AcLineSegmentFlowInfo.ContainsKey(ti))
                            {
                                info = new ACLineSegmentFlowInfo();
                                _result.AcLineSegmentFlowInfo.Add(ti, info);
                            }
                            else
                                info = _result.AcLineSegmentFlowInfo[ti];

                            if (info != null)
                            {
                                if (info.cnFeeders.ContainsKey(lastCn))
                                {
                                    var cnFeeders = info.cnFeeders[lastCn];

                                    if (!cnFeeders.Contains(feeder))
                                        cnFeeders.Add(feeder);
                                }
                                else
                                {
                                    info.cnFeeders.Add(lastCn, new List<DAXElectricFeeder>() { feeder });
                                }

                            }
                        }

                        if (ti.ClassType == CIMClassEnum.ConnectivityNode)
                            lastCn = (CIMConnectivityNode)ti;
                    }
                }
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\temp\ring.csv"))
            {
                file.WriteLine("mRID, Name, Type");

                foreach (var tr in _result.AcLineSegmentFlowInfo)
                {
                    string type = "radial";
                    if (tr.Value.cnFeeders.Count > 1)
                        type = "ring";

                    string line = "\"" + tr.Key.mRID + "\",\"" + tr.Key.Name + "\",\"" + type + "\"";
                    file.WriteLine(line);
                }
            }

        }

        // Station feeder DFS trace
        private Queue<CIMIdentifiedObject> StationFeederDFSTrace(DAXElectricFeeder feeder)
        {
            CIMIdentifiedObject root = _g.ObjectManager.GetCIMObjectById(feeder.DownstreamCIMObjectId);

            // Trace variables
            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Stack<CIMIdentifiedObject> stack = new Stack<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> visited = new HashSet<CIMIdentifiedObject>();
            stack.Push(root);
            visited.Add(root);

            while (stack.Count > 0)
            {
                CIMIdentifiedObject p = stack.Pop();

                if (p.ClassType == CIMClassEnum.ACLineSegment || p.ClassType == CIMClassEnum.ConnectivityNode)
                    traverseOrder.Enqueue(p);

                var neighbours = p.Neighbours;

                foreach (CIMIdentifiedObject n in neighbours)
                {
                    if (!visited.Contains(n))
                    {
                        visited.Add(n);

                        if (StationFeederTraceShouldWeEnqueueObject(feeder, n))
                        {
                            stack.Push(n);
                        }
                        else
                        {
                            if (n.ClassType == CIMClassEnum.ACLineSegment || n.ClassType == CIMClassEnum.ConnectivityNode)
                                traverseOrder.Enqueue(n);
                        }
                    }
                }
            }

            return traverseOrder;
        }

        private bool StationFeederTraceShouldWeEnqueueObject(DAXElectricFeeder feeder, CIMIdentifiedObject cimObj)
        {
            bool enqueue = true;

            // If normal feeder and we hit something insite feeder substation (the we're going in the wrong direction) don't enqueue
            if (!feeder.IsTransformerFeeder && cimObj.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.EquipmentContainerRef.InternalId == feeder.Node.CIMObjectId)
                enqueue = false;


            if (!feeder.IsTransformerFeeder
                && cimObj.VoltageLevel > 0
                && cimObj.VoltageLevel != feeder.VoltageLevel
                && cimObj.VoltageLevel != (feeder.VoltageLevel - 5000) // NRGi hack - skal fjernes så snart de får styr på forb kabler subtype
                && cimObj.VoltageLevel != (feeder.VoltageLevel + 5000))// NRGi hack - skal fjernes så snart de får styr på forb kabler subtype
                enqueue = false;

            // If feeder i 10000 volt and we reach substation don't enqueue.
            if (feeder.VoltageLevel == 400 &&
                cimObj.EquipmentContainerRef != null && // Bay
                cimObj.EquipmentContainerRef.EquipmentContainerRef != null && // Substation
                cimObj.EquipmentContainerRef.EquipmentContainerRef.ClassType == CIMClassEnum.Substation)
            {
                enqueue = false;
            }

            return enqueue;
        }
    }
}
