using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DAX.IO;

namespace DAX.IO
{
    public class MappingGuide
    {
        public int ProblemsFound = 0;
        public string SpecificationName = null;
        public string DataReaderName = null;
        public string DataReaderClassName = null;
        public List<KeyValuePair<string, string>> DataReaderAdditionalInfo = new List<KeyValuePair<string, string>>();
        public string DataWriterName = null;
        public string DataWriterClassName = null;
        public List<PreviewObject> PreviewObjects = new List<PreviewObject>();
        public double MinX = 9999999999;
        public double MinY = 9999999999;
        public double MaxX = 0;
        public double MaxY = 0;

        public Dictionary<string, DataSetMappingGuide> DataSetMappingGuides = new Dictionary<string, DataSetMappingGuide>();

        public DataSetMappingGuide AddOrGetDataSetMappingGuide(DataSetMapping dataSetMapping)
        {
            string classKey = dataSetMapping.InputDataSet.ToLower();
            //string classKey = dataSetMapping.InputDataSet;

            if (DataSetMappingGuides.ContainsKey(classKey))
                return DataSetMappingGuides[classKey];
            else
            {
                DataSetMappingGuide dsGuide = new DataSetMappingGuide() { DataSetMapping = dataSetMapping, ClassName = dataSetMapping.InputDataSet };
                DataSetMappingGuides.Add(classKey, dsGuide);
                return dsGuide;
            }
        }

        public DataSetMappingGuide AddOrGetDataSetMappingGuide(string className)
        {
            string classKey = className.ToLower();
            //string classKey = className;

            if (DataSetMappingGuides.ContainsKey(classKey))
                return DataSetMappingGuides[classKey];
            else
            {
                DataSetMappingGuide dsGuide = new DataSetMappingGuide() { ClassName = className };
                DataSetMappingGuides.Add(classKey, dsGuide);
                return dsGuide;
            }
        }


        public void AddOrGetDataSetMappingGuide(string className, DataSetMapping[] dataSetMappings)
        {
            if (dataSetMappings.Length > 0)
                foreach (DataSetMapping dsMapping in dataSetMappings)
                    AddOrGetDataSetMappingGuide(dsMapping);
            else
                AddOrGetDataSetMappingGuide(className);
        }

        public AttributeMappingGuide AddOrGetAttributeMappingGuide(string className, AttributeMapping attrMapping)
        {
            string classKey = className.ToLower();
            string attrKey = attrMapping.InputFieldName.ToLower();
            //string classKey = className;
            //string attrKey = attrMapping.InputFieldName;
            

            if (!DataSetMappingGuides.ContainsKey(classKey))
                AddOrGetDataSetMappingGuide(attrMapping.DataSetMapping);

            if (DataSetMappingGuides[classKey].AttributeMappingGuides.ContainsKey(attrKey))
                return DataSetMappingGuides[classKey].AttributeMappingGuides[attrKey];
            else
            {
                AttributeMappingGuide attrGuide = new AttributeMappingGuide() { AttributeMapping = attrMapping, FieldName = attrMapping.InputFieldName };
                DataSetMappingGuides[classKey].AttributeMappingGuides.Add(attrKey, attrGuide);
                return attrGuide;
            }
        }

        public AttributeMappingGuide AddOrGetAttributeMappingGuide(string className, string attrName)
        {
            string classKey = className.ToLower();
            string attrKey = attrName.ToLower();

            //string classKey = className;
            //string attrKey = attrName;

            DataSetMappingGuide dsGuide = AddOrGetDataSetMappingGuide(className);

            if (dsGuide.AttributeMappingGuides.ContainsKey(attrKey))
                return dsGuide.AttributeMappingGuides[attrKey];
            else
            {
                AttributeMappingGuide attrGuide = new AttributeMappingGuide() { FieldName = attrName };
                dsGuide.AttributeMappingGuides.Add(attrKey, attrGuide);
                return attrGuide;
            }
        }


        public void AddOrGetAttributeMappingGuide(string className, string attrName, AttributeMapping[] attrMappings)
        {
            if (attrMappings.Length > 0)
                foreach (AttributeMapping attrMapping in attrMappings)
                    AddOrGetAttributeMappingGuide(className.ToLower(), attrMapping);
            else
                AddOrGetAttributeMappingGuide(className.ToLower(), attrName);
        }

        public string TextReport()
        {
            string result = "";

            foreach (DataSetMappingGuide dsGuide in DataSetMappingGuides.Values)
            {
                result += "Dataset: " + dsGuide.ClassName + ", count=" + dsGuide.Antal + "";
                if (dsGuide.DataSetMapping != null)
                    result += " -> " + dsGuide.DataSetMapping.OutputDataSet;
                else
                    result += " -> ?";

                result += " " + dsGuide.GetMessageString();

                result += "\r\n";


                foreach (AttributeMappingGuide attrGuide in dsGuide.AttributeMappingGuides.Values)
                {
                    result += "  Attribute: " + attrGuide.FieldName;
                    if (attrGuide.AttributeMapping != null)
                        result += " -> " + attrGuide.AttributeMapping.OutputFieldName;
                    else
                        result += " -> ?";

                    result += " " + attrGuide.GetMessageString();

                    result += "\r\n";
                }

            }


            return result;
        }

        public string TabbedTextReport()
        {
            string result = "";

            foreach (DataSetMappingGuide dsGuide in DataSetMappingGuides.Values)
            {
                string msg = "Problem: " + dsGuide.GetMessageString();

                result += dsGuide.ClassName + " (" + dsGuide.Antal + " stk.)";
                if (dsGuide.DataSetMapping != null)
                    result += "\t" + dsGuide.DataSetMapping.OutputDataSet;
                else
                    result += "\tNo mapping";

                if (msg != null && msg != "Problem: ")
                    result += "\t" + msg;


                result += "\r\n--------------------------------------------------------------------------------------------------------------------------------------------------------------------------";
                result += "\r\n";


                foreach (AttributeMappingGuide attrGuide in dsGuide.AttributeMappingGuides.Values)
                {
                    msg = "Problem: " + attrGuide.GetMessageString();

                    result += attrGuide.FieldName;
                    if (attrGuide.AttributeMapping != null)
                        result += "\t" + attrGuide.AttributeMapping.OutputFieldName;
                    else
                        result += "\tNo mapping";

                    if (msg != null && msg != "Problem: ")
                        result += "\t" + msg;

                    result += "\r\n";
                }

                result += "\r\n\r\n";

            }

            return result;
        }


        public string GetReaderAdditionalInfo(string key) 
        {
            if (DataReaderAdditionalInfo != null)
            {
                foreach (var keyValue in DataReaderAdditionalInfo)
                {
                    if (keyValue.Key.ToLower() == key.ToLower())
                        return keyValue.Value;
                }
            }

            return null;
        }

        public void UpdateMinMaxValues(DAXFeature feature) 
        {
            if (feature.Coordinates != null && feature.Coordinates.Length > 0)
            {
                foreach (var coord in feature.Coordinates)
                {
                    if (coord.X < MinX)
                        MinX = coord.X;
                    if (coord.X > MaxX)
                        MaxX = coord.X;
                    if (coord.Y < MinY)
                        MinY = coord.Y;
                    if (coord.Y > MaxY)
                        MaxY = coord.Y;
                }

            }

        }

    }

    public class DataSetMappingGuide
    {
        public string ClassName { get; set; }
        public int Antal = 0;
        public DataSetMapping DataSetMapping = null;
        public Dictionary<string, AttributeMappingGuide> AttributeMappingGuides = new Dictionary<string, AttributeMappingGuide>();
        public Dictionary<string, MappingGuideMessage> Messages = new Dictionary<string, MappingGuideMessage>();

        public void AddMessage(MappingGuide guide, MessageLevel level, string message)
        {
            if (!Messages.ContainsKey(message))
            {
                Messages.Add(message, new MappingGuideMessage() { Level = level, Message = message });
                guide.ProblemsFound++;
            }
        }

        public string GetMessageString()
        {
            string result = "";
            foreach (MappingGuideMessage msg in Messages.Values)
            {
                if (result != "")
                    result += ". ";
                result += msg.Message;
            }

            return result;
        }

        public AttributeMappingGuide GetKeyAttributes()
        {
            foreach (var attr in AttributeMappingGuides.Values)
            {
                if (attr.AttributeMapping != null && attr.AttributeMapping.Key == true)
                    return attr;
            }

            return null;
        }
    }


    public class AttributeMappingGuide
    {
        public string FieldName { get; set; }
        public AttributeMapping AttributeMapping = null;
        public Dictionary<string, MappingGuideMessage> Messages = new Dictionary<string, MappingGuideMessage>();

        public void AddMessage(MappingGuide guide, MessageLevel level, string message)
        {
            if (!Messages.ContainsKey(message))
            {
                Messages.Add(message, new MappingGuideMessage() { Level = level, Message = message });
                guide.ProblemsFound++;

            }
        }

        public string GetMessageString()
        {
            string result = "";
            foreach (MappingGuideMessage msg in Messages.Values)
            {
                if (result != "")
                    result += ". ";
                result += msg.Message;
            }

            return result;
        }

    }

    public enum MessageLevel
    {
        Error = 1,
        Warning = 2,
        Debug = 3
    }

    public class MappingGuideMessage
    {
        public MessageLevel Level { get; set; }
        public string Message { get; set; }
    }

    public class PreviewObject
    {
        public DAXGeometryType GeometryType { get; set; }
        public string Coordinates { get; set; }
    }
}
