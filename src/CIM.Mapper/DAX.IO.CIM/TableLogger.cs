using DAX.IO.CIM.DataModel;
using DAX.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public enum Severity
    {
        Critical = 1,
        Error = 2,
        Warning = 3,
        Informational = 4,
        Debug = 5
    }

    public class ErrorCode {
        public Severity severity;
        public short errorCode;
        public string message;
        public double x;
        public double y;
        public string externalId;
        public string classType; //cimClass
        public string objectMRID; //cimRID
        public string nodeClass;
        public string nodePSRType;
        public string nodeMRID;
        public int voltageLevel;
        public string objectName;
        public string nodeName;
        public string length;
        public string type;
        public string kompunder;

        public ErrorCode(Severity severity, short errorCode, string message, double x, double y, string externalId, string classType, string objectName, string objectMRID, string nodeClass, string nodeName, string nodePSRType, string nodeMRID, int voltageLevel, string length, string type, string kompunder)
        {
            this.severity = severity;
            this.errorCode = errorCode;
            this.message = message;
            this.x = x;
            this.y = y;
            this.externalId = externalId;
            this.classType = classType;
            this.objectName = objectName;
            this.objectMRID = objectMRID;
            this.nodeClass = nodeClass;
            this.nodeName = nodeName;
            this.nodePSRType = nodePSRType;
            this.nodeMRID = nodeMRID;
            this.voltageLevel = voltageLevel;
            this.length = length;
            this.type = type;
            this.kompunder = kompunder;
        }
    }


    public class TableLogger
    {

        private string _tableName = null;
        private int _nextObjectId = 0;

        private Summary _theSummary = new Summary();

        private List<ErrorCode> _errorCodeList = null;

        public Summary getSummary() {
            return _theSummary; 
        }

        public TableLogger()
        {
        }
              

        public void constructErrorCodeList(bool doIt)
        {
            if (doIt)
                _errorCodeList = new List<ErrorCode>();
            else
                _errorCodeList = null;
        }

        /// <summary>
        /// Logs an error message plus information on the specified cimObj to the log tabel.
        /// If x and y is not specified, the first coordinate from the cimObj is used.
        /// </summary>
        /// <param name="severity"></param>
        /// <param name="errorCode"></param>
        /// <param name="message"></param>
        /// <param name="cimObj"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Log(Severity severity, short errorCode, string message, CIMIdentifiedObject cimObj, double x = 0, double y = 0, string kompunder = null, CIMIdentifiedObject busBarSection = null)
        {
            if (cimObj != null)
            {
                // node/root container
                var node = cimObj.GetEquipmentContainerRoot();

                // Take x,y from cimObj if not specified as parameters
                double xVal = x;
                double yVal = y;

                if (xVal == 0)
                {
                    if (cimObj.Coords != null && cimObj.Coords.Length > 1)
                    {
                        xVal = cimObj.Coords[0];
                        yVal = cimObj.Coords[1];
                    }
                }

                // If bay take center
                if (cimObj.ClassType == CIMClassEnum.Bay && cimObj.Coords != null && cimObj.Coords.Length == 4)
                {
                    xVal = cimObj.Coords[2] + ((cimObj.Coords[3] - cimObj.Coords[2]) / 2);
                    yVal = cimObj.Coords[0] + ((cimObj.Coords[1] - cimObj.Coords[0]) / 2);
                }

                // Try find voltage level
                int voltageLevel = cimObj.VoltageLevel;

                if (voltageLevel == 0)
                {
                    // Try container
                    if (cimObj.EquipmentContainerRef != null && cimObj.EquipmentContainerRef.VoltageLevel > 0)
                        voltageLevel = cimObj.EquipmentContainerRef.VoltageLevel;
                    else if (node != null && node.VoltageLevel > 0)
                        voltageLevel = node.VoltageLevel;
                }

                String length = cimObj.GetPropertyValueAsString("cim.length");
                String type = cimObj.GetPropertyValueAsString("cim.asset.type");


                bool hasLength = true;
                double theLength = -1;
                if (length == null)
                {
                    length = "no length";
                    hasLength = false;
                }
                else
                    theLength = System.Double.Parse(length);

                if (type == null)
                    type = "no type";

                _theSummary.add(errorCode, voltageLevel.ToString());
                string lengthGroup = "no length group";

                if (hasLength)
                {
                    if (theLength < 5.0)
                    {
                        if (theLength < 0.01)
                        {
                            lengthGroup = "<0.01m";
                        }
                        else
                        {
                            lengthGroup = "0.01m - 5.0m";
                        }
                    }
                    else
                    {
                        lengthGroup = ">5.0";
                    }
                    _theSummary.add(errorCode, lengthGroup);
                }
                _theSummary.add(errorCode, type);

                _theSummary.add(errorCode, type + ":" + lengthGroup + ":" + voltageLevel);

                string logLength = null;
                string logType = null;
                string logKompunder = null;
                if (errorCode >= (short)GeneralErrors.DanglingACLineSegmentMin && errorCode <= (short)GeneralErrors.DanglingACLineSegmentMax)
                {
                    logLength = lengthGroup;
                    logType = type;
                }
                if (errorCode >= (short)GeneralErrors.DanglingConnectivityEdgeMin && errorCode <= (short)GeneralErrors.DanglingConnectivityEdgeMax)
                {
                    logLength = lengthGroup;
                    logType = type;
                    logKompunder = kompunder;
                }

                string busbarSectionOID = "";
                if (busBarSection != null)
                    busbarSectionOID = busBarSection.ExternalId.ToString();

                LogErrorCodeList(severity, errorCode, message, xVal, yVal, cimObj.ExternalId, cimObj.ClassType.ToString(), cimObj.Name, cimObj.mRID.ToString(), node != null ? node.ClassType.ToString() : null, node != null ? node.Name : null, node != null ? node.GetPSRType(CIMMetaDataManager.Repository) : null, node != null ? node.mRID.ToString() : null, voltageLevel, logLength, logType, logKompunder, busbarSectionOID);
            }
        }

        public void LogErrorCodeList(Severity severity, short errorCode, string message, double x, double y, string externalId, string classType, string objectName, string objectMRID, string nodeClass, string nodeName, string nodePSRType, string nodeMRID, int voltageLevel, string length, string type, string kompunder, string bbsOID)
        {
            if (_errorCodeList == null)
                return;
            ErrorCode theErrorCode = new ErrorCode(severity, errorCode, message, x, y, externalId, classType, objectName, objectMRID, nodeClass, nodeName, nodePSRType, nodeMRID, voltageLevel, length, type, kompunder);
            _errorCodeList.Add(theErrorCode);
        }

      
        
        public String DumpSummary(LogLevel logLevel)
        {
            //Logger.Log(logLevel, "Dump Summary");

            List<String> theDump = _theSummary.dump();

            String summary = "";

            foreach (String str in theDump)
            {
                //Logger.Log(logLevel, str);
                summary += str + Environment.NewLine;
            }

            return summary;
        }

        internal List<ErrorCode> getErrorCodeList()
        {
            return _errorCodeList;
        }
    }
}
