using DAX.IO.CIM.Processing;
using DAX.NetworkModel.CIM;
using DAX.Util;

namespace DAX.IO.CIM
{
    public class TopologyProcessor : IGraphProcessor
    {
        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

        public void Run(CIMGraph g, CimErrorLogger tableLogger)
        {
            Logger.Log(LogLevel.Debug, "TopologyProcesser: Tracing all feeders...");

            var topologyData = (ITopologyProcessingResult)g.GetProcessingResult("Topology");

            ((TopologyProcessingResult)topologyData).InitialTraceAllFeeders();

            LogFeederProblems(topologyData, tableLogger);
        }

        private void LogFeederProblems(ITopologyProcessingResult topology, CimErrorLogger tableLogger)
        {
            int nCustomerTotal = 0;
            int nCustomerNoFeed = 0;
            int nCustomerMutliFeed = 0;
            int nCustomerMutliFromSameNode = 0;

            foreach (var node in topology.DAXNodes)
            {
                if (node.ClassType == CIMClassEnum.EnergyConsumer)
                {
                    nCustomerTotal++;
                    if (node.Sources == null || node.Sources.Length == 0)
                    {
                        // JL: HACK
                        //if (node.CIMObject.GetNeighboursNeighbors(CIMClassEnum.

                        tableLogger.Log(Severity.Warning, (int)TopologyProcessingErrors.CustomerNoFeed, TopologyProcessingErrorToString.getString(TopologyProcessingErrors.CustomerNoFeed), node.CIMObject);
                        nCustomerNoFeed++;

                    }

                    if (node.Sources != null && node.Sources.Length > 1)
                    {
                        bool feededFromSameNode = true;

                        DAXElectricNode lastNode = node.Sources[0].Feeder.Node;

                        string feededFrom = "";

                        foreach (var source in node.Sources)
                        {
                            if (source.Feeder.Node != lastNode)
                                feededFromSameNode = false;

                            lastNode = source.Feeder.Node;

                            if (source.Feeder.Node.Name != null)
                                feededFrom += source.Feeder.Node.Name + ":";
                            if (source.Feeder.Name != null)
                                feededFrom += source.Feeder.Name + " ";
                        }

                        bool multiFeedAllowed = false;

                        // Check if bay allows multifeed
                        if (node.Sources[0].Feeder.Bay != null && node.Sources[0].Feeder.Bay.AllowMultiFeed == true)
                            multiFeedAllowed = true;


                        var multiFeedAllowedAttr = node.CIMObject.GetPropertyValueAsString("dax.multifeedallowed");

                        if (multiFeedAllowedAttr != null)
                        {
                            if (multiFeedAllowedAttr.ToLower() == "yes" || multiFeedAllowedAttr.ToLower() == "true" || multiFeedAllowedAttr.ToLower() == "1")
                                multiFeedAllowed = true;
                        }

                        if (!multiFeedAllowed)
                        {
                            if (!feededFromSameNode)
                            {
                                tableLogger.Log(Severity.Warning, (int)TopologyProcessingErrors.CustomerMultiFeed, TopologyProcessingErrorToString.getString(TopologyProcessingErrors.CustomerMultiFeed) + " " + feededFrom, node.CIMObject);
                                nCustomerMutliFeed++;
                            }
                            else
                            {
                                tableLogger.Log(Severity.Warning, (int)TopologyProcessingErrors.CustomerMultiFeedFromSameNode, TopologyProcessingErrorToString.getString(TopologyProcessingErrors.CustomerMultiFeedFromSameNode) + " " + feededFrom, node.CIMObject);
                                nCustomerMutliFromSameNode++;
                            }
                        }

                    }
                }


                if (node.ClassType == CIMClassEnum.Substation)
                {
                    if (node.VoltageLevel > 5000 && node.VoltageLevel < 20000)
                    {
                        string line = node.CIMObject.mRID + ";" + node.Name + ";";

                        if (node.Transformers == null || node.Transformers.Length == 0)
                            tableLogger.Log(Severity.Warning, (int)TopologyProcessingErrors.TransformerNotFound, TopologyProcessingErrorToString.getString(TopologyProcessingErrors.TransformerNotFound), node.CIMObject);
                        else if (node.Transformers[0].Sources == null || node.Transformers[0].Sources.Length == 0)
                            tableLogger.Log(Severity.Warning, (int)TopologyProcessingErrors.TransformerNoFeed, TopologyProcessingErrorToString.getString(TopologyProcessingErrors.TransformerNoFeed), node.CIMObject);
                        else if (node.Transformers[0].Sources.Length > 1)
                        {
                            tableLogger.Log(Severity.Warning, (int)TopologyProcessingErrors.TransformerMultiFeed, TopologyProcessingErrorToString.getString(TopologyProcessingErrors.TransformerMultiFeed), node.CIMObject);
                        }
                        else
                            line += node.Transformers[0].Sources[0].Node.Name + ";";
                    }
                }
            }

            // print out trace information
            Logger.Log(LogLevel.Info, "TopologyProcesser: " + nCustomerTotal + " total Energy Comsumers (EC) traced.");
            Logger.Log(LogLevel.Info, "TopologyProcesser: " + nCustomerNoFeed + " EC's not feeded.");
            Logger.Log(LogLevel.Info, "TopologyProcesser: " + nCustomerMutliFeed + " EC's multi feeded.");
            Logger.Log(LogLevel.Info, "TopologyProcesser: " + nCustomerMutliFromSameNode + " EC's multi feeded from same node.");
        }


    }
}
