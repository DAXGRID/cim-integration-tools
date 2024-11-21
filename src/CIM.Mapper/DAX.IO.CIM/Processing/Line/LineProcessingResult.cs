using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM.Processing.Line
{
    public class LineProcessingResult : ILineProcessingResult
    {
        internal Dictionary<CIMIdentifiedObject, DAXLineRelation> _lineRelByCIMObj = new Dictionary<CIMIdentifiedObject, DAXLineRelation>();
        internal Dictionary<CIMEquipmentContainer, DAXSimpleLine> _lineByBay = new Dictionary<CIMEquipmentContainer, DAXSimpleLine>();
        internal Dictionary<CIMEquipmentContainer, DAXBayInfo> _bayInfoByBay = new Dictionary<CIMEquipmentContainer, DAXBayInfo>();

        public DAXLineRelation GetDAXLineRelByCIMObject(CIMIdentifiedObject cimObj)
        {
            if (_lineRelByCIMObj.ContainsKey(cimObj))
                return _lineRelByCIMObj[cimObj];
            else
                return null;
        }

        public DAXSimpleLine GetDAXLineByBay(CIMEquipmentContainer bay)
        {
            if (_lineByBay.ContainsKey(bay))
                return _lineByBay[bay];
            else
                return null;
        }

        public DAXBayInfo GetDAXBayInfo(CIMEquipmentContainer bay)
        {
            if (_bayInfoByBay.ContainsKey(bay))
                return _bayInfoByBay[bay];
            else
                return null;
        }

    }
}
