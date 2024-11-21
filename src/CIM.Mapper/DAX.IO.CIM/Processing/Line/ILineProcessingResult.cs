using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM.Processing.Line
{
    public interface ILineProcessingResult : IGraphProcessingResult
    {
        DAXLineRelation GetDAXLineRelByCIMObject(CIMIdentifiedObject cimObj);
        DAXSimpleLine GetDAXLineByBay(CIMEquipmentContainer bay);
        DAXBayInfo GetDAXBayInfo(CIMEquipmentContainer bay);
    }
}
