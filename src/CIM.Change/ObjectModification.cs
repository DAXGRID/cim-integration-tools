namespace CIM.Change;

public class ObjectModification : ChangeSetMember
{
    public PropertyModification[] Modifications { get; set; }
}
