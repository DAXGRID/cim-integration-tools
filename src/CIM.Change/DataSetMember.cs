namespace CIM.Change
{
    public class DataSetMember : IdentifiedObject
    {
        public string TargetObject { get; set; }

        public ChangeSetMember Change { get; set; }

        public ObjectReverseModification ReverseChange { get; set; }
    }
}
