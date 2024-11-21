using DAX.IO.CIM.DataModel;
using DAX.NetworkModel.CIM;

namespace DAX.IO.CIM
{
    public static class CIMDebugHelper
    {
        public static CIMMetaDataRepository repository = CIMMetaDataManager.Repository;

        public static string GetNodeDebugInfoAsText(DAXElectricNode node)
        {
            var cimObject = node.CIMObject;

            string result = "";
            int? level = 0;

            result += GetCIMObjectText(cimObject, level);

            if (cimObject is CIMEquipmentContainer)
                result += GetEquipmentContainerText((CIMEquipmentContainer)cimObject, level);

            return result;
        }

        private static string GetCIMObjectText(CIMIdentifiedObject cimObject, int? level)
        {
            if (cimObject.ClassType != CIMClassEnum.ConnectivityNode)
            {
                string result = cimObject.ClassType.ToString();

                string psrType = cimObject.GetPSRType(repository);

                if (psrType != null)
                    result += " PSRType=" + cimObject.GetPSRType(repository);

                if (cimObject.VoltageLevel > 0)
                    result += " VoltageLevel=" + cimObject.VoltageLevel;

                if (cimObject.Name != null)
                    result += " Name='" + cimObject.Name + "'";

                if (cimObject.Description != null)
                    result += " Desciption='" + cimObject.Description + "'";

                result += " ExternalId=" + cimObject.ExternalId + " mRID=" + cimObject.mRID ;

                if (cimObject.Neighbours.Count() > 0)
                {
                    result += "\r\n" + GetIndryk(level.Value + 2) + "[Neighbours]";

                    foreach (var neighbor in cimObject.Neighbours)
                    {
                        if (!cimObject.ObjectManager.IsDeleted(neighbor))
                        {
                            result += "\r\n" + GetIndryk(level.Value + 3) + "Object " + neighbor.IdString();
                        }
                    }
                }
             
 
                return result;
            }

            return "";
        }

        private static string GetEquipmentContainerText(CIMEquipmentContainer ec, int? level)
        {
            level++;
            string result = "\r\n" + GetIndryk(level.Value);

            if (ec.Children.Count == 0)
                result += "[EquipmentContainer is empty]";
            else
                result += "[EquipmentContainer contents]";

            foreach (var child in ec.Children)
            {
                if (!child.ObjectManager.IsDeleted(child) && child.ClassType != CIMClassEnum.ConnectivityNode)
                {
                    result += "\r\n" + GetIndryk(level.Value + 1) + GetCIMObjectText(child, level);

                    level++;

                    if (child is CIMEquipmentContainer)
                        result += GetEquipmentContainerText((CIMEquipmentContainer)child, level);

                    level--;
                }
            }

            level--;

            return result;
        }

        private static string GetIndryk(int level)
        {
            string result = "";
            for (int i = 0; i < level; i++)
                result += "  ";
            return result;
        }
    }
}
