using DAX.NetworkModel.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMEquipmentContainer : CIMPowerSystemResource
    {
        public CIMEquipmentContainer(CIMObjectManager objManager)
            : base(objManager)
        {
        }

        public List<CIMIdentifiedObject> Children = new List<CIMIdentifiedObject>();
        public List<DAXElectricFeeder> Feeders = new List<DAXElectricFeeder>();
        public bool AllowMultiFeed = false;

        public CIMIdentifiedObject GetFirstChild(CIMClassEnum classType)
        {
            foreach (var child in Children)
                if (child.ClassType == classType)
                    return child;

            return null;
        }

        public CIMIdentifiedObject GetFirstChild(CIMClassEnum[] classType)
        {
            foreach (var child in Children)
                if (classType.Contains(child.ClassType))
                    return child;

            return null;
        }


        public CIMIdentifiedObject GetChildByName(string name)
        {
            foreach (var child in Children)
                if (child.Name != null && child.Name == name)
                    return child;

            return null;
        }

    }
}
