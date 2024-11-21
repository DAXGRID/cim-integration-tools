using DAX.IO.CIM;
using DAX.IO.CIM.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.NetworkModel.CIM
{
    public class DAXTopologyInfo
    {
        public bool IsStartOfFeeder { get; set; }
        public DAXElectricFeeder theFeeder { get; set; }
        private List<CIMConductingEquipment> _parents { get; set; }
        private List<CIMConductingEquipment> _children { get; set; }

        public List<CIMConductingEquipment> Parents
        {
            get { return _parents; }
        }

        public List<CIMConductingEquipment> Children
        {
            get { return _children; }
        }

        public List<CIMConductingEquipment> GetChildrenRecursive(ITopologyProcessingResult topoData, int voltageLevel)
        {
            List<CIMConductingEquipment> result = new List<CIMConductingEquipment>();
            GetChildrenRecursive(ref result, topoData, voltageLevel);

            return result;
        }
             

        private void GetChildrenRecursive(ref List<CIMConductingEquipment> result, ITopologyProcessingResult topoData, int voltageLevel)
        {
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    if (!result.Contains(child) && (child.VoltageLevel == voltageLevel))
                    {
                        result.Add(child);
                        var childTopo = topoData.GetDAXTopologyInfoByCIMObject(child);
                        if (childTopo != null)
                            childTopo.GetChildrenRecursive(ref result, topoData, voltageLevel);
                    }
                    else
                        break;
                }
            }
        }

        public void AddParent(CIMConductingEquipment parent)
        {
            if (_parents == null)
                _parents = new List<CIMConductingEquipment>();

            if (!_parents.Contains(parent))
            _parents.Add(parent);
        }

        public void AddChild(CIMConductingEquipment child)
        {
            if (_children == null)
                _children = new List<CIMConductingEquipment>();

            if (!_children.Contains(child))
            _children.Add(child);
        }

        public DAXTopologyInfo()
        {
            _parents = new List<CIMConductingEquipment>();
        }
    }
}
