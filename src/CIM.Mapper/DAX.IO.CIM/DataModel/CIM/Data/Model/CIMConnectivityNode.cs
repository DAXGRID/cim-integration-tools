using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMConnectivityNode : CIMIdentifiedObject
    {
        public CIMConnectivityNode(CIMObjectManager objManager)
            : base(objManager)
        {
            ClassType = CIMClassEnum.ConnectivityNode;
            //mRID = Guid.NewGuid();

        }

        public List<CIMIdentifiedObject> GetConnectedObjects()
        {
            return Neighbours;
        }
    }
}
