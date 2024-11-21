using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMTerminal : CIMObject
    {
        public CIMConnectivityNode ConnectivityNode { get; set; }

        public int EndNumber { get; set; }

        public bool IsConnected()
        {
            if (ConnectivityNode != null)
                return true;
            else
                return false;
        }
    }
}
