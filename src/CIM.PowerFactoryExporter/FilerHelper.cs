using CIM.PhysicalNetworkModel;
using CIM.PhysicalNetworkModel.FeederInfo;
using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Extensions;

namespace DAX.CIM.PFAdapter
{
    public static class FilterHelper
    {
        public static List<IdentifiedObject> Filter(CimContext context, FilterRule rule)
        {
            Dictionary<string, IdentifiedObject> result = new Dictionary<string, IdentifiedObject>();

            HashSet<string> assetRefs = new HashSet<string>();
            HashSet<string> assetInfoRefs = new HashSet<string>();
            HashSet<string> assetModelRefs = new HashSet<string>();
            HashSet<string> manufacturerRefs = new HashSet<string>();

            HashSet<ConnectivityNode> cnAlreadyWritten = new HashSet<ConnectivityNode>();

            FeederInfoContext feederContext = new FeederInfoContext(context);
            feederContext.CreateFeederObjects();

            if (rule.MaxVoltageLevel == 0)
                rule.MaxVoltageLevel = 1000000;


            foreach (var cimObject in context.GetAllObjects())
            {

                if ((cimObject is ConductingEquipment
                    && ((ConductingEquipment)cimObject).BaseVoltage >= rule.MinVoltageLevel && ((ConductingEquipment)cimObject).BaseVoltage <= rule.MaxVoltageLevel)
                    || !(cimObject is ConductingEquipment)
                    || cimObject is PowerTransformer
                    || cimObject is ExternalNetworkInjection
                    || (cimObject is EnergyConsumer && ((PowerSystemResource)cimObject).PSRType == "Aftagepunkt_fællesmaaling")
                    )
                {

                    if (
                    cimObject is ACLineSegment ||
                    cimObject is BusbarSection ||
                    cimObject is LoadBreakSwitch ||
                    cimObject is Breaker ||
                    cimObject is Disconnector ||
                    cimObject is Fuse ||
                    cimObject is Substation ||
                    cimObject is VoltageLevel ||
                    cimObject is Bay ||
                    cimObject is PowerTransformer ||
                    cimObject is PowerTransformerEnd ||
                    cimObject is ExternalNetworkInjection ||
                    cimObject is PetersenCoil ||
                    cimObject is CurrentTransformer ||
                    cimObject is PotentialTransformer ||
                    cimObject is EnergyConsumer ||
                    cimObject is RatioTapChanger ||
                    cimObject is LinearShuntCompensator
                    )
                    {
                        // Find substation
                        Substation partOfSt = null;

                        if (cimObject is Substation)
                            partOfSt = (Substation)cimObject;

                        if (cimObject is BayExt && ((BayExt)cimObject).VoltageLevel == null)
                            continue;

                        if (cimObject.IsInsideSubstation(context))
                            partOfSt = cimObject.GetSubstation(context, true);


                        // Fjern alle stik
                        if (rule.RemoveCustomerCables)
                        {
                            if (cimObject is ACLineSegment aclsSkip)
                            {
                                if (aclsSkip.PSRType == "CustomerCable" || aclsSkip.PSRType == "CustomerOverheadLine")
                                {
                                    continue;
                                }
                            }
                        }


                        if (cimObject is ExternalNetworkInjection)
                        {
                            var eni = cimObject as ExternalNetworkInjection;
                            var neighbors = eni.GetNeighborConductingEquipments(context);

                            if (rule.IncludeSpecificSubstations != null)
                            {
                                // Filter away injection points unless belonging to station

                                if (!rule.IncludeSpecificSubstations.Any(stName => eni.name.StartsWith(stName)))
                                    continue;

                            }

                            if (neighbors.Exists(c => c.IsInsideSubstation(context) && (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(c.GetSubstation(context).name))))
                            {
                                var ce = neighbors.First(c => c.IsInsideSubstation(context) && (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(c.GetSubstation(context).name)));

                                // put injection inside substation
                                eni.BaseVoltage = ce.BaseVoltage;
                                if (ce is BusbarSection)
                                {
                                    eni.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = context.GetObject<VoltageLevel>(ce.EquipmentContainer.@ref).mRID };
                                }
                                else
                                {
                                    eni.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = context.GetObject<Bay>(ce.EquipmentContainer.@ref).VoltageLevel.@ref };
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Tap changer
                        if (cimObject is RatioTapChanger)
                        {
                            var tap = cimObject as RatioTapChanger;

                            if (!(tap.TransformerEnd != null && tap.TransformerEnd.@ref != null && result.ContainsKey(tap.TransformerEnd.@ref)))
                                continue;

                            var ptEnd = context.GetObject<PowerTransformerEnd>(tap.TransformerEnd.@ref);
                        }


                        //  AuxiliaryEquipment - transfer the one pointing to 60 kV breakers only
                        if (cimObject is AuxiliaryEquipment)
                        {
                            var aux = cimObject as AuxiliaryEquipment;

                            // If not connected to terminal, then skip
                            if (aux.Terminal == null)
                                continue;

                            var swTerminal = context.GetObject<Terminal>(aux.Terminal.@ref);
                            var ctObj = context.GetObject<IdentifiedObject>(swTerminal.ConductingEquipment.@ref);

                            if (ctObj is Switch)
                            {
                                Switch ctSw = ctObj as Switch;

                                // If not connedted to breaker, then skip
                                if (!(ctSw is Breaker))
                                    continue;

                                // if not 60000 volt, then skip
                                if (ctSw.BaseVoltage != 60000)
                                    continue;

                                // If bay name contains transformer, skup
                                var swBayName = ctSw.GetBay(context).name;
                                if (swBayName.ToLower().Contains("transformer"))
                                    continue;
                            }
                        }



                        // Generel voltage check
                        if (!(cimObject is ConductingEquipment)
                            || rule.MinVoltageLevel == 0
                            || ((ConductingEquipment)cimObject).BaseVoltage == 0
                            || ((ConductingEquipment)cimObject).BaseVoltage >= rule.MinVoltageLevel
                            || (cimObject is EnergyConsumer && ((PowerSystemResource)cimObject).PSRType == "Aftagepunkt_fællesmaaling"))
                        {
                            // Add high voltage measured customer, even if lv modelled
                            if (cimObject is EnergyConsumer && ((EnergyConsumer)cimObject).PSRType == "Aftagepunkt_fællesmaaling" && ((EnergyConsumer)cimObject).BaseVoltage == 400)
                            {
                                // Don't add low voltage consumers if min voltage > 15000
                                if (rule.MinVoltageLevel > 15000)
                                    continue;

                            }

                            // Check if substation should be filtered away
                            if (cimObject is Substation st)
                            {
                                var priVoltage = st.GetPrimaryVoltageLevel(context);
                                if (priVoltage < rule.MinVoltageLevel)
                                    continue;
                            }

                            // Check if power transformer should be filtered away
                            if (cimObject is PowerTransformer pt)
                            {
                                var ends = pt.GetEnds(context);

                                if (!ends.Exists(e => e.BaseVoltage >= rule.MinVoltageLevel))
                                    continue;
                            }

                            // Check if power transformer should be filtered away
                            if (cimObject is PowerTransformerEnd ptEnd)
                            {
                                var ptOfPtEnd = context.GetObject<PowerTransformer>(ptEnd.PowerTransformer.@ref);

                                var ends = ptOfPtEnd.GetEnds(context);

                                if (!ends.Exists(e => e.BaseVoltage >= rule.MinVoltageLevel))
                                    continue;

                            }


                            // Check if object part of substation should be filtered away due to voltage level
                            if (partOfSt != null)
                            {
                                var priVoltage = partOfSt.GetPrimaryVoltageLevel(context);
                                if (priVoltage < rule.MinVoltageLevel)
                                    continue;
                            }

                            string feededStName = null;

                            bool componentIsWithinSubstationAndShouldBeIncluded = false;


                            // If part of substation, check if we should filter away
                            if (partOfSt != null)
                            {
                                // Don't filter anything away, if not specific substations specified
                                if (rule.IncludeSpecificSubstations != null)
                                {
                                    bool skip = true;

                                    // Check if substation is feeded from included primary substation

                                    if (partOfSt.PSRType == "PrimarySubstation" && rule.IncludeSpecificSubstations.Contains(partOfSt.name))
                                        skip = false;

                                    else if (partOfSt.PSRType == "SecondarySubstation" &&
                                        partOfSt.InternalFeeders != null &&
                                        partOfSt.InternalFeeders.Count > 0)
                                    {

                                        foreach (var feeder in partOfSt.InternalFeeders)
                                        {

                                            if (feeder.ConnectionPoint != null &&
                                                 feeder.ConnectionPoint.Substation != null &&
                                                 feeder.ConnectionPoint.Substation.name != null
                                            )
                                            {
                                                feededStName = feeder.ConnectionPoint.Substation.name;

                                                if (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(feededStName))
                                                {
                                                    skip = false;
                                                    componentIsWithinSubstationAndShouldBeIncluded = true;
                                                }
                                            }
                                        }
                                    }

                                    else if ((partOfSt.PSRType == "CableBox" || partOfSt.PSRType == "T-Junction" || partOfSt.PSRType == "Tower") &&
                                     partOfSt.InternalFeeders != null &&
                                     partOfSt.InternalFeeders.Count > 0 &&
                                     partOfSt.InternalFeeders.Exists(f => f.FeederType == FeederType.SecondarySubstation))
                                    {
                                        foreach (var cableBoxFeeder in partOfSt.InternalFeeders.Where(f => f.FeederType == FeederType.SecondarySubstation))
                                        {
                                            if (cableBoxFeeder.ConnectionPoint != null && cableBoxFeeder.ConnectionPoint.PowerTransformer != null)
                                            {
                                                var secStTrafo = cableBoxFeeder.ConnectionPoint.PowerTransformer;

                                                // If sec station trafo is not feeded, skip
                                                if (!(secStTrafo.Feeders == null || secStTrafo.Feeders.Count == 0))
                                                {
                                                    feededStName = secStTrafo.Feeders[0].ConnectionPoint.Substation.name;

                                                    if (rule.IncludeSpecificSubstations.Contains(feededStName))
                                                        skip = false;
                                                }
                                            }
                                        }
                                    }

                                    if (skip)
                                        continue;
                                }
                            }

                            // If energy consumer, check if we should filter away
                            if (cimObject is EnergyConsumer)
                            {
                                var eq = cimObject as ConductingEquipment;

                                if (rule.IncludeSpecificSubstations != null && !CheckIfComponentIsFeederFromSpecifiedSubstations(eq, rule.IncludeSpecificSubstations))
                                    continue;
                            }

                            // If acls, check if we should filter away
                            if (cimObject is ACLineSegment && !componentIsWithinSubstationAndShouldBeIncluded && (rule.IncludeSpecificLines == null || rule.IncludeSpecificLines.Count > 1))
                            {
                                bool continueToCheck = true;

                                var acls = cimObject as ACLineSegment;

                                // check if feeded from primary substation
                                var aclsFeeders = feederContext.GeConductingEquipmentFeeders(acls);

                                if (aclsFeeders != null && aclsFeeders.Count > 0)
                                {
                                    if (rule.IncludeSpecificSubstations == null)
                                        continueToCheck = false;
                                }

                                // Check if feeded from specified primary substation
                                if (acls.PSRType != "InternalCable" && rule.IncludeSpecificSubstations != null)
                                {
                                    feededStName = null;

                                    // If acls not feeded, skip
                                    if (aclsFeeders == null || aclsFeeders.Count == 0)
                                        continue;
                                    else
                                    {
                                        if (acls.BaseVoltage < 1000)
                                        {
                                            if (!acls.Feeders.Exists(f => f.FeederType == FeederType.SecondarySubstation))
                                                continue;

                                            var secStTrafo = aclsFeeders.First(f => f.FeederType == FeederType.SecondarySubstation).ConnectionPoint.PowerTransformer;

                                            // If sec station trafo is not feede, skip
                                            if (secStTrafo.Feeders == null || secStTrafo.Feeders.Count == 0)
                                                continue;

                                            feededStName = secStTrafo.Feeders[0].ConnectionPoint.Substation.name;
                                        }
                                        else
                                        {
                                            if (aclsFeeders[0].FeederType != FeederType.NetworkInjection)
                                                feededStName = aclsFeeders[0].ConnectionPoint.Substation.name;
                                        }

                                        if (!rule.IncludeSpecificSubstations.Contains(feededStName))
                                            continue;

                                        continueToCheck = false;
                                    }
                                }

                                if (continueToCheck)
                                {
                                    if (acls.PSRType == "InternalCable")
                                    {
                                        var feedingPrimaryStName = GetFeedingPrimarySubstation(acls);

                                        if (rule.IncludeSpecificSubstations != null && !rule.IncludeSpecificSubstations.Contains(feedingPrimaryStName))
                                            continue;
                                    }
                                    else if (acls.name != null && acls.name.Contains("#"))
                                    {
                                        var nameSplit = acls.name.Split('#');

                                        var nameWithoutDelStr = nameSplit[0].ToUpper();

                                        if (rule.IncludeSpecificLines != null && !rule.IncludeSpecificLines.Contains(nameWithoutDelStr))
                                            continue;
                                    }
                                    else
                                        continue;
                                }
                            }

                            // If min voltagelevel > 400, don't include cable boxes and stuff inside cable boxes
                            if (rule.MinVoltageLevel > 400
                                && partOfSt != null
                                &&
                                (partOfSt.PSRType == "CableBox" || partOfSt.PSRType == "T-Junction"))
                            {
                                continue;
                            }

                            if (rule.MinVoltageLevel > 400
                               && partOfSt != null
                               && partOfSt.PSRType == "Tower"
                               && partOfSt.GetPrimaryVoltageLevel(context) < 1000)
                            {
                                continue;
                            }



                            // don't add voltage level, we do this later
                            if (cimObject is VoltageLevel)
                            {
                                continue;
                            }

                            result.Add(cimObject.mRID, cimObject);

                            // Add terminals if conducting equipment
                            if (cimObject is ConductingEquipment)
                            {
                                var ci = cimObject as ConductingEquipment;

                                foreach (var tc in context.GetConnections(ci))
                                {
                                    string stName = "";

                                    if (partOfSt != null && partOfSt.name != null)
                                        stName = partOfSt.name + "_";

                                    //tc.Terminal.phases = PhaseCode.ABCN;
                                    tc.Terminal.name = stName + ci.name + "_T" + tc.Terminal.sequenceNumber;
                                    result.Add(tc.Terminal.mRID, tc.Terminal);

                                    // add connectivity node, if not already added
                                    if (!cnAlreadyWritten.Contains(tc.ConnectivityNode))
                                        result.Add(tc.ConnectivityNode.mRID, tc.ConnectivityNode);

                                    cnAlreadyWritten.Add(tc.ConnectivityNode);
                                }
                            }

                            // Add location
                            if (cimObject is PowerSystemResource)
                            {
                                var psrObj = cimObject as PowerSystemResource;

                                if (psrObj.PSRType != "InternalCable")
                                {
                                    if (psrObj.Location != null && psrObj.Location.@ref != null)
                                    {
                                        var loc = context.GetObject<LocationExt>(psrObj.Location.@ref);
                                        result.Add(loc.mRID, loc);
                                    }
                                }
                            }

                            // Add substation voltage levels
                            if (cimObject is Substation)
                            {
                                var psrObj = cimObject as Substation;

                                var voltageLevels = context.GetSubstationVoltageLevels(psrObj);

                                foreach (var vl in voltageLevels)
                                {
                                    result.Add(vl.mRID, vl);
                                }

                            }
                        }
                    }
                }
            }

            // Add protective equipment (relays) 
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is ProtectionEquipmentExt)
                {
                    var pe = cimObject as ProtectionEquipmentExt;

                    if (pe.ProtectedSwitches != null && pe.ProtectedSwitches.Length > 0 && result.ContainsKey(pe.ProtectedSwitches[0].@ref))
                        result.Add(cimObject.mRID, cimObject);
                }
            }

            //////////////////////////////////////////////////////////////////////////////////7
            // Add Asset stuff
            //////////////////////////////////////////////////////////////////////////////////7

            // Asset ref
            foreach (var cimObject in result.Values)
            {
                if (cimObject is PowerSystemResource)
                {
                    var psr = cimObject as PowerSystemResource;

                    if (psr.Assets != null && psr.Assets.@ref != null)
                        assetRefs.Add(psr.Assets.@ref);
                }
            }

            // Asset
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is Asset)
                {
                    var asset = cimObject as Asset;

                    if (!assetRefs.Contains(asset.mRID))
                        continue;

                    if (asset.AssetInfo != null && asset.AssetInfo.@ref != null && !assetInfoRefs.Contains(asset.AssetInfo.@ref))
                        assetInfoRefs.Add(asset.AssetInfo.@ref);

                    result.Add(asset.mRID, asset);
                }
            }

            // Asset info
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is AssetInfo assetInfo)
                {
                    if (!assetInfoRefs.Contains(assetInfo.mRID))
                        continue;

                    if (assetInfo.AssetModel != null && assetInfo.AssetModel.@ref != null && !assetModelRefs.Contains(assetInfo.AssetModel.@ref))
                        assetModelRefs.Add(assetInfo.AssetModel.@ref);

                    result.Add(assetInfo.mRID, assetInfo);
                }
            }

            // Asset model
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is ProductAssetModel assetModel)
                {
                    if (!assetModelRefs.Contains(assetModel.mRID))
                        continue;

                    if (assetModel.Manufacturer != null && assetModel.Manufacturer.@ref != null && !manufacturerRefs.Contains(assetModel.Manufacturer.@ref))
                        manufacturerRefs.Add(assetModel.Manufacturer.@ref);

                    result.Add(assetModel.mRID, assetModel);
                }
            }

            // Manufacturer
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is Manufacturer)
                {
                    var manu = cimObject as Manufacturer;

                    if (!manufacturerRefs.Contains(manu.mRID))
                        continue;

                    result.Add(manu.mRID, manu);
                }
            }

            return result.Values.ToList();
        }

        private static bool CheckIfComponentIsFeederFromSpecifiedSubstations(ConductingEquipment eq, HashSet<string> includeSpecificSubstations)
        {
            var feedingSt = GetFeedingPrimarySubstation(eq);

            if (feedingSt == null)
                return false;

            if (includeSpecificSubstations.Contains(feedingSt))
                return true;
            else
                return false;
        }

        public static string GetFeedingPrimarySubstation(ConductingEquipment ci)
        {
            if (ci.Feeders == null || ci.Feeders.Count == 0)
                return null;

            if (ci.BaseVoltage < 1000)
            {
                if (!ci.Feeders.Exists(f => f.FeederType == FeederType.SecondarySubstation))
                    return null;

                var secStTrafo = ci.Feeders.First(f => f.FeederType == FeederType.SecondarySubstation).ConnectionPoint.PowerTransformer;

                // If sec station trafo is not feede, skip
                if (secStTrafo.Feeders == null || secStTrafo.Feeders.Count == 0)
                    return null;

                return secStTrafo.Feeders[0].ConnectionPoint.Substation.name;
            }
            else
            {
                if (ci.Feeders[0].FeederType != FeederType.NetworkInjection)
                    return ci.Feeders[0].ConnectionPoint.Substation.name;
            }

            return null;
        }
    }

    public class FilterRule
    {
        public double MinVoltageLevel { get; set; }
        public double MaxVoltageLevel { get; set; }
        public HashSet<string> IncludeSpecificSubstations { get; set; }
        public HashSet<string> IncludeSpecificLines { get; set; }
        public bool RemoveCustomerCables { get; internal set; }
    }
}
