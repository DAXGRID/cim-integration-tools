using CIM.PhysicalNetworkModel;
using CIM.PhysicalNetworkModel.FeederInfo;
using CIM.PhysicalNetworkModel.Traversal;

namespace CIM.TopologyProcessor
{
    public class FlatFeederInfoCreator
    {
        public IEnumerable<FlatFeederInfo> CreateFeederInfos(CimContext cimContext, FeederInfoContext feederContext)
        {
            var feeders = feederContext.GetConductionEquipmentFeeders();
            var alreadyAdded = new HashSet<Guid>();

            HashSet<Guid> ecAclsToBeMarkedSingleFeed = new HashSet<Guid>();

            foreach (var conductingEquipmentFeeders in feeders)
            {
                int seqNo = 1;
                
                List<FlatFeederInfo> feederInfosToAdd = new List<FlatFeederInfo>();

                /////////////////////////////////////
                // MV feeders
                foreach (var mspFeeder in conductingEquipmentFeeders.Value.Where(f => f.FeederType == FeederType.SecondarySubstation))
                {
                    bool hspFeedersFound = false;

                    // Check if power transformer has hsp feeders
                    if (mspFeeder.ConnectionPoint.PowerTransformer != null)
                    {
                        var hspFeeders = feederContext.GeConductingEquipmentFeeders(mspFeeder.ConnectionPoint.PowerTransformer);
                            
                        foreach (var hspFeeder in hspFeeders)
                        {
                            FlatFeederInfo feederInfo = CreateBasicFeederInfo(seqNo, conductingEquipmentFeeders, feederContext);
                            seqNo++;

                            AddMspFeederInfo(mspFeeder, feederInfo);
                            AddHspFeederInfo(hspFeeder, feederInfo);

                            feederInfo.NodeHopCount = GetHopCount(feederContext, conductingEquipmentFeeders.Key);

                            feederInfosToAdd.Add(feederInfo);
                            hspFeedersFound = true;
                        }
                    }

                    // If no hsp feeders just add msp feeder info only
                    if (!hspFeedersFound)
                    {
                        // Only add feeder info if power transformer found; this to support open switch point inside substations
                        if (mspFeeder.ConnectionPoint.PowerTransformer != null)
                        {
                            FlatFeederInfo feederInfo = CreateBasicFeederInfo(seqNo, conductingEquipmentFeeders, feederContext);
                            seqNo++;
    
                            AddMspFeederInfo(mspFeeder, feederInfo);
    
                            feederInfo.NodeHopCount = GetHopCount(feederContext, conductingEquipmentFeeders.Key);
    
                            feederInfosToAdd.Add(feederInfo);
                        }
                    }
                }

                /////////////////////////////////////
                // HV feeders
                foreach (var hspFeeder in conductingEquipmentFeeders.Value.Where(f => f.FeederType == FeederType.PrimarySubstation))
                {
                    // Only add feeder info if power transformer found; this to support open switch point inside substations
                    if (hspFeeder.ConnectionPoint.PowerTransformer != null)
                    {
                        FlatFeederInfo feederInfo = CreateBasicFeederInfo(seqNo, conductingEquipmentFeeders, feederContext);
                        seqNo++;
    
                        feederInfo.NodeHopCount = GetHopCount(feederContext, conductingEquipmentFeeders.Key);
    
                        AddHspFeederInfo(hspFeeder, feederInfo);
                        feederInfosToAdd.Add(feederInfo);
                   }
                }
              

                /////////////////////////////////////
                // INJECTION feeders
                foreach (var injectionFeeder in conductingEquipmentFeeders.Value.Where(f => f.FeederType == FeederType.NetworkInjection))
                {
                    FlatFeederInfo feederInfo = CreateBasicFeederInfo(seqNo, conductingEquipmentFeeders, feederContext);
                    if (injectionFeeder.ConductingEquipment != null)
                        feederInfo.NetworkInjectionMRID = Guid.Parse(injectionFeeder.ConductingEquipment.mRID);

                    seqNo++;

                    
                    feederInfosToAdd.Add(feederInfo);
                }

                // If seqNo still 1, then we found no feeders
                if (seqNo == 1)
                {
                    // Add feeder info telling that we have no feed
                    FlatFeederInfo feederInfo = CreateBasicFeederInfo(seqNo, conductingEquipmentFeeders, feederContext);
                    feederInfo.Multifeed = false;
                    feederInfo.Nofeed = true;

                    yield return feederInfo;
                }

                // If seqNo > 2 the components is multi feeded
                if (seqNo > 2)
                {
                    bool markAsMultiFeed = true;

                    // Don't mark as multi feed on customers that is multifeeded from same node though one cable only
                    if (conductingEquipmentFeeders.Key is EnergyConsumer)
                    {
                        // Get energy consumer terminals
                        var ecTerminals = cimContext.GetConnections(conductingEquipmentFeeders.Key);

                        // We expect one terminal pointing to a connectivity node
                        if (ecTerminals.Count == 1 && ecTerminals[0].ConnectivityNode != null)
                        {
                            // Get energy consumer terminal 1 connectivity node neighbors
                            var ecCnAclsNeighbors = cimContext.GetConnections(ecTerminals[0].ConnectivityNode).Where(o => o.ConductingEquipment is ACLineSegment).ToList();

                            if (ecCnAclsNeighbors.Count > 1)
                            {
                                markAsMultiFeed = false;
                                foreach (var acls in ecCnAclsNeighbors)
                                    ecAclsToBeMarkedSingleFeed.Add(new Guid(acls.ConductingEquipment.mRID));
                            }
                        }
                    }

                    if (markAsMultiFeed)
                    {
                        foreach (var f in feederInfosToAdd)
                        {
                            f.Multifeed = true;
                        }
                    }
                }

                foreach (var feederInfo in feederInfosToAdd)
                    yield return feederInfo;
            }

            // Add components that has no feeder
            foreach (var cimObj in cimContext.GetAllObjects())
            {
                if (cimObj is ConductingEquipment)
                {
                    var ce = cimObj as ConductingEquipment;

                    if (feederContext.GeConductingEquipmentFeeders(ce).Count == 0)
                    {
                        var feederInfo = new FlatFeederInfo()
                        {
                            SeqNo = 1,
                            EquipmentMRID = Guid.Parse(ce.mRID),
                            EquipmentClass = ce.GetType().Name,
                            EquipmentPSRType = ce.PSRType,
                            VoltageLevel = (int)ce.BaseVoltage,
                            Multifeed = false,
                            Nofeed = true
                        };

                        yield return feederInfo;
                    }
                }
            }
        }

        static void AddHspFeederInfo(Feeder hspFeeder, FlatFeederInfo feederInfo)
        {
            if (hspFeeder.ConnectionPoint.Substation != null)
                feederInfo.PrimarySubstationMRID = Guid.Parse(hspFeeder.ConnectionPoint.Substation.mRID);

            if (hspFeeder.ConnectionPoint.Bay != null)
                feederInfo.PrimarySubstationBayMRID = Guid.Parse(hspFeeder.ConnectionPoint.Bay.mRID);

            if (hspFeeder.ConnectionPoint.PowerTransformer != null)
            {
                feederInfo.PrimarySubstationTransformerMRID = Guid.Parse(hspFeeder.ConnectionPoint.PowerTransformer.mRID);

                if (hspFeeder.ConnectionPoint.PowerTransformer.Feeders.Count == 1 && hspFeeder.ConnectionPoint.PowerTransformer.Feeders.Count( f => f.FeederType == FeederType.NetworkInjection) == 1)
                {
                    if (hspFeeder.ConnectionPoint.PowerTransformer.Feeders[0].ConductingEquipment != null)
                    {
                        feederInfo.NetworkInjectionMRID = Guid.Parse(hspFeeder.ConnectionPoint.PowerTransformer.Feeders[0].ConductingEquipment.mRID);
                    }
                }
            }
        }

        static void AddMspFeederInfo(Feeder mspFeeder, FlatFeederInfo feederInfo)
        {
            if (mspFeeder.ConnectionPoint.Substation != null)
                feederInfo.SecondarySubstationMRID = Guid.Parse(mspFeeder.ConnectionPoint.Substation.mRID);

            if (mspFeeder.ConnectionPoint.Bay != null)
                feederInfo.SecondarySubstationBayMRID = Guid.Parse(mspFeeder.ConnectionPoint.Bay.mRID);

            if (mspFeeder.ConnectionPoint.PowerTransformer != null)
                feederInfo.SecondarySubstationTransformerMRID = Guid.Parse(mspFeeder.ConnectionPoint.PowerTransformer.mRID);
        }

        static FlatFeederInfo CreateBasicFeederInfo(int seqNo, KeyValuePair<ConductingEquipment, List<Feeder>> cnFeeder, FeederInfoContext feederContext)
        {
            var feederInfo = new FlatFeederInfo()
            {
                SeqNo = seqNo,
                EquipmentMRID = Guid.Parse(cnFeeder.Key.mRID),
                EquipmentClass = cnFeeder.Key.GetType().Name,
                EquipmentPSRType = cnFeeder.Key.PSRType,
                VoltageLevel = (int)cnFeeder.Key.BaseVoltage,
                Multifeed = false,
                Nofeed = false
            };

            if (cnFeeder.Key is ConductingEquipment)
            {
                feederInfo.VoltageLevel = (int)((ConductingEquipment)cnFeeder.Key).BaseVoltage;

                var conductingEquipmentfeederInfo = feederContext.GeConductingEquipmentFeederInfo((ConductingEquipment)cnFeeder.Key);

                if (conductingEquipmentfeederInfo != null)
                {
                    feederInfo.CustomerFeederCableMRID = conductingEquipmentfeederInfo.FirstCustomerCableId;
                    feederInfo.TraversalOrder = conductingEquipmentfeederInfo.TraversalOrder;
                }
            }

            var cableBoxFeeder = cnFeeder.Value.Find(f => f.FeederType == FeederType.CableBox);

            if (cableBoxFeeder != null)
            {
                feederInfo.CableBoxMRID = Guid.Parse(cableBoxFeeder.ConnectionPoint.Substation.mRID);
            }

            return feederInfo;
        }

        public int GetHopCount(FeederInfoContext feederContext, ConductingEquipment ce)
        {
            return feederContext.GeConductingEquipmentFeederInfo(ce).SubstationHop;
        }
    }
}
