using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.IO.CIM.Processing.Line;
using DAX.Util;
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DAX.IO.CIM
{
    public class LineProcessor : IGraphProcessor
    {
        CIMGraph _g = null;
        CimErrorLogger _tableLogger = null;
        LineProcessingResult _result = new LineProcessingResult();
        string _connectionString = null;
        string _bayInfoSpName = null;
        bool _removeFirstChar = false;

        public void Run(CIMGraph g, CimErrorLogger tableLogger)
        {
            
            _g = g;
            _tableLogger = tableLogger;

            BuildLineInfo();

            UpdateNodeBayName();

            UpdateLVCableName();
        }

        private void BuildLineInfo()
        {
            Logger.Log(LogLevel.Info, "LineProcessor: Tracing all ACLineSegments to build line information...");

            HashSet<CIMIdentifiedObject> alreadyProcesseed = new HashSet<CIMIdentifiedObject>();

            // Trace all AC Line segments
            foreach (var obj in _g.CIMObjects)
            {
         
                if (obj.ClassType == CIMClassEnum.ACLineSegment && obj.EquipmentContainerRef == null
                    && !_g.ObjectManager.IsDeleted(obj)
                    && !alreadyProcesseed.Contains(obj))
                {

                    if (obj.mRID.ToString() == "b0faa60f-4def-442c-a3c8-b1187e6d530d")
                    {
                    }

                    // stik kabel på skab 301727
                    if (obj.mRID.ToString() == "39e64bf9-51b0-44af-b0e3-2b06c1f76bc5")
                    {
                    }

                    alreadyProcesseed.Add(obj);
     
                    // Find ends
                    var traceResult = Trace(obj);

                    List<CIMIdentifiedObject> ends = new List<CIMIdentifiedObject>();

                    foreach (var ti in traceResult)
                    {

                        if (ti.ExternalId == "2915822")
                        {
                        }

                        if (!alreadyProcesseed.Contains(ti))
                            alreadyProcesseed.Add(ti);

                        if (ti.EquipmentContainerRef != null && ti.EquipmentContainerRef.EquipmentContainerRef != null)
                            ends.Add(ti);
                    }

                    
                    if (ends.Count == 1)
                    {
                        AddBayInfo(traceResult, null, ends[0].EquipmentContainerRef, null);
                    }
                    else if (ends.Count == 2)
                    {
                        CIMIdentifiedObject from = null;
                        CIMIdentifiedObject to = null;

                        // Swap ends if nessesary
                        SwapEndsIfNessesary(ends, out from, out to);

                        // Traverse from the from node
                        if (from != null && to != null)
                        {
                            var line = new DAXSimpleLine();
                            line.FromBay = from.EquipmentContainerRef;

                            line.ToBay = to.EquipmentContainerRef;
                            line.Name = from.GetEquipmentContainerRoot().Name + "-" + to.GetEquipmentContainerRoot().Name;

                            // Create Bay infos
                            AddBayInfo(traceResult, line, line.FromBay, line.ToBay);
                            AddBayInfo(traceResult, line, line.ToBay, line.FromBay);

                            if (!_result._lineByBay.ContainsKey(line.FromBay))
                                _result._lineByBay.Add(line.FromBay, line);

                            if (!_result._lineByBay.ContainsKey(line.ToBay))
                                _result._lineByBay.Add(line.ToBay, line);

                            // Add children / DAX line relations
                            HashSet<CIMIdentifiedObject> childAlreadyProcessed = new HashSet<CIMIdentifiedObject>();
                            CIMIdentifiedObject lastObj = null;
                            CIMIdentifiedObject currentObj = from;
                            DAXLineRelation lineRel = null;
                            int order = 1;
                            bool isFirst = true;

                            while (currentObj != null)
                            {
                                if (currentObj.ClassType == CIMClassEnum.ACLineSegment)
                                {
                                    lineRel = new DAXLineRelation() { Line = line, Order = order, IsFirst = isFirst };
                                    line.Children.Add(lineRel);

                                    if (!_result._lineRelByCIMObj.ContainsKey(currentObj))
                                        _result._lineRelByCIMObj.Add(currentObj, lineRel);

                                    order++;

                                    if (isFirst)
                                        isFirst = false;
                                }

                                if (!childAlreadyProcessed.Contains(currentObj))
                                    childAlreadyProcessed.Add(currentObj);

                                // Find next object in the chain
                                lastObj = currentObj;
                                currentObj = null;

                                // If we're dealing with the first object
                                if (lastObj == from)
                                {
                                    // Find the neighbor outside the container
                                    // FIX: Handle parallel cables
                                    foreach (var n in lastObj.GetNeighboursNeighbors2())
                                    {
                                        if (n.EquipmentContainerRef == null && traceResult.Contains(n) && !childAlreadyProcessed.Contains(n))
                                            currentObj = n;
                                    }
                                }
                                // The last object
                                else if (lastObj == to)
                                {
                                    // Set isLast
                                    if (lineRel != null)
                                        lineRel.IsLast = true;
                                }
                                // We're outside the container
                                else
                                {
                                    var neighbors = lastObj.GetNeighboursNeighbors2();

                                        // Find the next neighbor outside the container
                                        foreach (var n in neighbors)
                                        {
                                            //if (n != lastObj && traceResult.Contains(n) && !childAlreadyProcessed.Contains(n))
                                            if (n != lastObj && !childAlreadyProcessed.Contains(n))
                                                currentObj = n;
                                        }
                                }
                            }
                        }

                    }
                    else
                    {
                    }
                }
            }

            _g.AddProcessingResult("Line", _result);
        }
        

        private void SwapEndsIfNessesary(List<CIMIdentifiedObject> ends, out CIMIdentifiedObject from, out CIMIdentifiedObject to)
        {
            // Find the from object
            if (ends[0].GetEquipmentContainerRoot().ClassType == CIMClassEnum.Substation && ends[1].GetEquipmentContainerRoot().ClassType == CIMClassEnum.Enclosure)
            {
                from = ends[0];
                to = ends[1];
            }
            else if (ends[0].GetEquipmentContainerRoot().ClassType == CIMClassEnum.Enclosure && ends[1].GetEquipmentContainerRoot().ClassType == CIMClassEnum.Substation)
            {
                from = ends[1];
                to = ends[0];
            }
            else
            {
                // First check if the ends are numeric or not. In DSO if the primary substation is tre letters. The secondary substations has a number.
                string sEnd0 = ends[0].EquipmentContainerRef.EquipmentContainerRef.Name;
                int nEnd0;
                bool isEnd0Numeric = int.TryParse(ends[0].EquipmentContainerRef.EquipmentContainerRef.Name, out nEnd0);

                if (sEnd0 == null)
                    sEnd0 = "NULL";
                else if (_removeFirstChar && ends[0].EquipmentContainerRef.EquipmentContainerRef.VoltageLevel < 20000 && sEnd0.Length > 1)
                {
                    sEnd0 = VerdoNodeNameHack(sEnd0);
                    isEnd0Numeric = int.TryParse(sEnd0, out nEnd0);
                }

                string sEnd1 = ends[1].EquipmentContainerRef.EquipmentContainerRef.Name;
                int nEnd1;
                bool isEnd1Numeric = int.TryParse(ends[1].EquipmentContainerRef.EquipmentContainerRef.Name, out nEnd1);

                if (sEnd1 == null)
                    sEnd1 = "NULL";
                else if (_removeFirstChar && ends[1].EquipmentContainerRef.EquipmentContainerRef.VoltageLevel < 20000 && sEnd1.Length > 1)
                {
                    sEnd1 = VerdoNodeNameHack(sEnd1);
                    isEnd1Numeric = int.TryParse(sEnd1, out nEnd1);
                }


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
        }

        private string VerdoNodeNameHack(string origName)
        {
            // Verdo hack
            if (origName.StartsWith("EH"))
                return origName.Substring(2, origName.Length - 2);
            else if (origName.StartsWith("R"))
                return origName.Substring(1, origName.Length - 1);
            else if (origName.StartsWith("S"))
                return origName.Substring(1, origName.Length - 1);
            else if (origName.StartsWith("H"))
                return origName.Substring(1, origName.Length - 1);
            else
                return origName;
        }

        private void UpdateNodeBayName()
        {
            Logger.Log(LogLevel.Info, "LineProcessor: Update node bay names...");

            // Find all cable boxes
            foreach (var obj in _g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.Enclosure || obj.ClassType == CIMClassEnum.Substation)
                {
                    CIMEquipmentContainer node = (CIMEquipmentContainer)obj;

                    foreach (var child in node.Children)
                    {
                        if (child.ClassType == CIMClassEnum.Bay && child.VoltageLevel == 400)
                        {
                            CIMEquipmentContainer bay = (CIMEquipmentContainer)child;

                            

                            var bayInfo = _result.GetDAXBayInfo(bay);

                            if (bayInfo != null)
                            {
                                if (bayInfo.Customers != null)
                                {
                                    // If one customer write installation number
                                    if (bayInfo.Customers.Count == 1)
                                    {
                                        var ecUsagePointList = bayInfo.Customers[0].GetPropertyValue("usagepoints") as List<CIMIdentifiedObject>;

                                        if (ecUsagePointList != null)
                                        {
                                            if (ecUsagePointList.Count == 1)
                                            {
                                                var custEAN = ecUsagePointList[0].Name;
                                                if (custEAN != null && custEAN.Length == 18)
                                                {
                                                    string instNr = Convert.ToInt64(custEAN.Substring(10, 7)).ToString();
                                                    bay.Name = instNr;
                                                    bayInfo.OtherEndGeneratedName = bay.Name;
                                                }
                                                else if (custEAN != null)
                                                {
                                                    bay.Name = custEAN;
                                                    bayInfo.OtherEndGeneratedName = bay.Name;
                                                }
                                            }
                                            else
                                            {
                                                bay.Name = "Flere inst";
                                                bayInfo.OtherEndGeneratedName = bay.Name;
                                            }
                                        }
                                        else
                                        {

                                            var custEAN = bayInfo.Customers[0].Name;
                                            if (custEAN != null && custEAN.Length == 18)
                                            {
                                                string instNr = Convert.ToInt64(custEAN.Substring(10, 7)).ToString();
                                                bay.Name = instNr;
                                                bayInfo.OtherEndGeneratedName = bay.Name;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        bay.Name = "Flere inst";
                                        bayInfo.OtherEndGeneratedName = bay.Name;
                                    }


                                }
                                else if (bayInfo.OtherEndBay != null && bayInfo.OtherEndBay.EquipmentContainerRef != null && bayInfo.OtherEndBay.EquipmentContainerRef.Name != null)
                                {
                                    bay.Name = bayInfo.OtherEndBay.EquipmentContainerRef.Name;
                                    bayInfo.OtherEndGeneratedName = bay.Name;
                                }

                            }

                        }
                    }
                }
            }
        }

        private void UpdateLVCableName()
        {
            Logger.Log(LogLevel.Info, "LineProcessor: Update LV cable names with line information...");

            // Find all cables
            foreach (var obj in _g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ACLineSegment && obj.VoltageLevel == 400)
                {
                    // Delete whatever GIS said - it's not updated anyway
                    obj.Name = null;

                    var lineRel = _result.GetDAXLineRelByCIMObject(obj);
                    if (lineRel != null)
                    {
                        obj.Name = lineRel.ToString();
                    }
                }
            }

            // Make sure all LV cables has a unique name
            Logger.Log(LogLevel.Debug, "LineInfoProcesser: Make sure LV AC Line Segments has unique name, to make PSI happy...");
            List<CIMIdentifiedObject> cablesToMakeUnique = new List<CIMIdentifiedObject>();

            // Give LV segments name = objectid if they are null
            foreach (var obj in _g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ACLineSegment && obj.VoltageLevel == 400 && obj.Name == null)
                {
                    // use mrid
                    obj.Name = obj.mRID.ToString().Substring(obj.mRID.ToString().Length - 18, 18);
                }
            }

            foreach (var obj in _g.CIMObjects)
            {
                if (obj.ClassType == CIMClassEnum.ACLineSegment && obj.VoltageLevel == 400)
                    cablesToMakeUnique.Add(obj);
            }

            // Make sure names are unique
            var results = from c in cablesToMakeUnique
                          group c by c.Name into g
                          select new { Name = g.Key, Duplicates = g.ToList() };

            foreach (var r in results)
            {
                if (r.Duplicates.Count > 1)
                {
                    if (r.Name == null)
                    {
                        int counter = 1;
                        foreach (var d in r.Duplicates)
                        {
                            // use mrid
                            d.Name = d.mRID.ToString().Substring(d.mRID.ToString().Length - 18, 18);
                            counter++;
                        }
                    }
                    else
                    {
                        int counter = 1;
                        foreach (var d in r.Duplicates)
                        {
                            d.Name = r.Name + "_" + counter;
                            counter++;
                        }
                    }

                    var dup = r.Duplicates;
                }
            }


            var results2 = from c in cablesToMakeUnique
                          group c by c.Name into g
                          select new { Name = g.Key, Duplicates = g.ToList() };

            foreach (var r in results)
            {
                var name = r.Name;
                var dup = r.Duplicates;

            }
        }

        private void AddBayInfo(List<CIMIdentifiedObject> traceResult, DAXSimpleLine line, CIMEquipmentContainer bay, CIMEquipmentContainer otherEndBay)
        {
            var bayInfo = new DAXBayInfo() { Line = line, OtherEndBay = otherEndBay };

            foreach (var ti in traceResult)
            {
                if (ti.ClassType == CIMClassEnum.EnergyConsumer)
                {
                    if (bayInfo.Customers == null)
                        bayInfo.Customers = new List<CIMConductingEquipment>();

                    bayInfo.Customers.Add((CIMConductingEquipment)ti);
                }
            }

            // Other end node calc
            if (bayInfo.Customers != null && bayInfo.Customers.Count() > 0 && otherEndBay == null)
                bayInfo.OtherEndNode = bayInfo.Customers[0];
            else if (otherEndBay != null)
                bayInfo.OtherEndNode = otherEndBay.GetEquipmentContainerRoot();

            if (!_result._bayInfoByBay.ContainsKey(bay))
            {
                _result._bayInfoByBay.Add(bay, bayInfo);
                bayInfo.NumberOfCables = 1;
            }
            else
            {
                var existingBayInfo = _result._bayInfoByBay[bay];
                existingBayInfo.NumberOfCables++;

                // A bay can only be connected to another bay. If it's already has bayinfo, then it means that it's connected something else too, or it could be parallel cables (not handled yet)
                if (existingBayInfo.NumberOfCables > 2)
                    _tableLogger.Log(Severity.Error, (int)LineProcessingErrors.BayProblemWithLineConnectivity, "LineProcessor: Problems processing bay", bay);
            }
        }       

        private List<CIMIdentifiedObject> Trace(CIMIdentifiedObject start)
        {
            Queue<CIMIdentifiedObject> traverseOrder = new Queue<CIMIdentifiedObject>();
            Stack<CIMIdentifiedObject> stack = new Stack<CIMIdentifiedObject>();
            HashSet<CIMIdentifiedObject> visited = new HashSet<CIMIdentifiedObject>();
            stack.Push(start);
            visited.Add(start);

            Stack<string> levels = new Stack<string>();

            Stack<CIMIdentifiedObject> visitedObjects = new Stack<CIMIdentifiedObject>();

            while (stack.Count > 0)
            {
                CIMIdentifiedObject p = stack.Pop();

                if (p.ClassType == CIMClassEnum.ACLineSegment || p.ClassType == CIMClassEnum.EnergyConsumer)
                    traverseOrder.Enqueue(p);

                // Used to avoid getting more that one object from each container. The problem is with busbars that might have many neighbors.
                HashSet<CIMEquipmentContainer> containerVisited = new HashSet<CIMEquipmentContainer>();

                foreach (CIMIdentifiedObject cimObj in p.GetNeighboursNeighbors2())
                {
                    if (!visited.Contains(cimObj))
                    {
                        visited.Add(cimObj);

                        // If an equipment outside constainer, contiue trace
                        if (cimObj.EquipmentContainerRef == null)
                            stack.Push(cimObj);
                        else
                        {
                            if (!containerVisited.Contains(cimObj.GetEquipmentContainerRoot()))
                            {
                                containerVisited.Add(cimObj.GetEquipmentContainerRoot());
                                traverseOrder.Enqueue(cimObj);
                            }
                        }
                    }
                }
            }

            return traverseOrder.ToList();
        }

        #region IGraphProcessor Members

        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
            if (parameters != null)
            {
                var param = parameters.Find(s => s.Name.ToLower() == "bayinfo_storedprocedure");
                if (param != null)
                    _bayInfoSpName = param.Value;

                param = parameters.Find(s => s.Name.ToLower() == "lineinfoconnectionstring");
                if (param != null)
                    _connectionString = param.Value;

                param = parameters.Find(s => s.Name.ToLower() == "removefirstchar");
                if (param != null && param.Value.ToLower() == "true")
                    _removeFirstChar = true;

            }
        }

        #endregion
    }
}

