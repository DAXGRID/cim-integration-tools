﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Extensions;


namespace CIM.PhysicalNetworkModel.LineInfo
{
    public class LineInfoContext
    {
        private CimContext _cimContext;
        private List<SimpleLine> _lines = new List<SimpleLine>();

        public LineInfoContext(CimContext cimContext)
        {
            _cimContext = cimContext;
        }

        public List<SimpleLine> GetLines()
        {
            return _lines;
        }

        public void CreateLineInfo()
        {
            HashSet<IdentifiedObject> alreadyProcesseed = new HashSet<IdentifiedObject>();

            // Iterate over all AC line segments that exists outside substations
            foreach (var obj in _cimContext.GetAllObjects().Where(o => o is ACLineSegment && !o.IsInsideSubstation(_cimContext)).OfType<ACLineSegment>())
            {
                if (!alreadyProcesseed.Contains(obj))
                {
                    // Trace objects until we reach a substation container unless it's a t-junction
                    var firstFoundACLSTraceResult = obj.Traverse(
                        _cimContext,
                        ci => !ci.IsInsideSubstation(_cimContext) || ci.GetSubstation(_cimContext).PSRType == "T-Junction",
                        cn => !cn.IsInsideSubstation(_cimContext) || cn.GetSubstation(_cimContext).PSRType == "T-Junction",
                        true
                    ).ToList();

                    List<EndInfo> ends = new List<EndInfo>();
                      
                    // Find reached substations
                    foreach (var traceObj in firstFoundACLSTraceResult)
                    {
                        if (!alreadyProcesseed.Contains(traceObj))
                            alreadyProcesseed.Add(traceObj);

                        if (traceObj.IsInsideSubstation(_cimContext) && traceObj.GetSubstation(_cimContext).PSRType != "T-Junction")
                            ends.Add(new EndInfo() { StartObj = traceObj, Substation = traceObj.GetSubstation(_cimContext), Bay = traceObj.GetBay(_cimContext) });
                    }

                    // If two ends found, and no energy consumers found, we're dealing with a real "line" according to NRGi DSO
                    if (ends.Count == 2)
                    {
                        var endsOrdered = SortEnds(ends);

                        // Do trace starting at the ac line segment first node

                        var cnNeighbors = ((ConnectivityNode)endsOrdered[0].StartObj).GetNeighborConductingEquipments(_cimContext);

                        IdentifiedObject start = null;

                        foreach (var cnNeighbor in cnNeighbors)
                            if (firstFoundACLSTraceResult.Contains(cnNeighbor))
                                start = cnNeighbor;

                        // Trace until we reach a substation container unless it's a t-junction
                        var endTraceResult = start.Traverse(
                            _cimContext,
                            ci => !ci.IsInsideSubstation(_cimContext) || ci.GetSubstation(_cimContext).PSRType == "T-Junction",
                            cn => !cn.IsInsideSubstation(_cimContext) || cn.GetSubstation(_cimContext).PSRType == "T-Junction",
                            false
                        ).ToList();

                        // Create line object
                        var line = new SimpleLine();
                        line.FromBay = endsOrdered[0].Bay;
                        line.ToBay = endsOrdered[1].Bay;
                        line.FromSubstation = endsOrdered[0].Substation;
                        line.ToSubstation = endsOrdered[1].Substation;
                        line.Name = endsOrdered[0].Substation.name + "-" + endsOrdered[1].Substation.name;

                        // Add ACLS's (childrens of the line)
                        int order = 1;
                        bool isFirst = true;

                        foreach (var acls in endTraceResult.Where(o => o is ACLineSegment).OfType<ACLineSegment>())
                        {
                            var parLineRel = new LineRelation() { Equipment = acls, Line = line, Order = order, IsFirst = isFirst };
                            line.Children.Add(parLineRel);

                            isFirst = false;
                            order++;
                        }

                        _lines.Add(line);
                    }


                    alreadyProcesseed.Add(obj);
                }
            }
        }

        private List<EndInfo> SortEnds(List<EndInfo> ends)
        {
            EndInfo from;
            EndInfo to;

            // Find the from object
            if (ends[0].Substation.PSRType.Contains("Substation") && 
                            (
                            ends[1].Substation.PSRType == "Tower" ||
                            ends[1].Substation.PSRType == "Enclosure"
                            )
                          )
            { 
                from = ends[0];
                to = ends[1];
            }
            else if (ends[1].Substation.PSRType.Contains("Substation") &&
                            (
                            ends[0].Substation.PSRType == "Tower" ||
                            ends[0].Substation.PSRType == "Enclosure"
                            )
                          )
            {
                from = ends[0];
                to = ends[1];
            }
            else
            {
                // First check if the ends are numeric or not. In DSO if the primary substation is tre letters. The secondary substations has a number.
                string sEnd0 = ends[0].Substation.name;
                int nEnd0;
                bool isEnd0Numeric = int.TryParse(sEnd0, out nEnd0);

                string sEnd1 = ends[1].Substation.name;
                int nEnd1;
                bool isEnd1Numeric = int.TryParse(sEnd1, out nEnd1);

                if (!isEnd0Numeric && isEnd1Numeric)
                {
                    from = ends[0];
                    to = ends[1];
                }
                else if (isEnd0Numeric && !isEnd1Numeric)
                {
                    from = ends[1];
                    to = ends[0];
                }
                else if (isEnd0Numeric && isEnd1Numeric)
                {
                    if (nEnd0 < nEnd1)
                    {
                        from = ends[0];
                        to = ends[1];
                    }
                    else
                    {
                        from = ends[1];
                        to = ends[0];
                    }
                }
                // If too primary stations
                else if (!isEnd0Numeric && !isEnd1Numeric && sEnd0.Length > 0 && sEnd1.Length > 0)
                {
                    if (sEnd0[0] < sEnd1[0])
                    {
                        from = ends[0];
                        to = ends[1];
                    }
                    else
                    {
                        from = ends[1];
                        to = ends[0];
                    }
                }
                else
                {
                    from = ends[0];
                    to = ends[1];
                }
            }

            return new List<EndInfo>() {from, to};
        }
    }

}
