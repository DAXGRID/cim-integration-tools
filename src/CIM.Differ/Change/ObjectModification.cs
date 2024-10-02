namespace CIM.Differ.Change
{
    public class ObjectModification : ChangeSetMember
    {
        public PropertyModification[] Modifications { get; set; }
    }
}
