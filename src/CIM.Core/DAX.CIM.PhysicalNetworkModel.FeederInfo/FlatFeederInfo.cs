using CIM.PhysicalNetworkModel;
using System;

namespace CIM.TopologyProcessor
{
    public class FlatFeederInfo : IdentifiedObject
    {
        public SwitchStateType SwitchStateType { get; set; }
        public Guid EquipmentMRID { get; set; }
        public String EquipmentClass { get; set; }
        public String EquipmentPSRType { get; set; }
        public int VoltageLevel { get; set; }
        public int SeqNo { get; set; }
        public bool Nofeed { get; set; }
        public bool Multifeed { get; set; }
        public Guid CustomerFeederCableMRID { get; set; }
        public Guid CableBoxMRID { get; set; }
        public Guid CableBoxBusbarMRID { get; set; }
        public Guid SecondarySubstationMRID { get; set; }
        public Guid SecondarySubstationBayMRID { get; set; }
        public Guid SecondarySubstationTransformerMRID { get; set; }
        public Guid PrimarySubstationMRID { get; set; }
        public Guid PrimarySubstationBayMRID { get; set; }
        public Guid PrimarySubstationTransformerMRID { get; set; }
        public Guid NetworkInjectionMRID { get; set; }
        public bool NofeedAllowed { get; set; }
        public bool MultifeedAllowed { get; set; }
        public int NodeHopCount { get; set; }
        public int TraversalOrder { get; set; }
  
        public FlatFeederInfo()
        {
            mRID = Guid.NewGuid().ToString();
        }
    }

    public enum SwitchStateType
    {
        GIS = 0,
        Normal = 1,
        Actual = 2
    }
}