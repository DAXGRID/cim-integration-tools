using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace DAX.IO
{
    public class DAXMetaData
    {
        private Dictionary<string, DAXClassDef> _featureClassDefinitions = new Dictionary<string, DAXClassDef>();
        private bool _canHandleAllOutputTypes = false;
        private bool _canHandleAllAttributes = false;

        public bool CanHandleAllOutputTypes
        {
            get 
            {
                return _canHandleAllOutputTypes;
            }

            set {
                _canHandleAllOutputTypes = value;
            }
        }

        public bool CanHandleAllAttributes
        {
            get
            {
                return _canHandleAllAttributes;
            }

            set
            {
                _canHandleAllAttributes = value;
            }
        }


        public DAXClassDef AddOrGetFeatureClassDefinition(string categoryName, string featureClassName, string featureClassAliasName) 
        {
            // Don't remove this ToLower
            string classKey = featureClassName.ToLower();

            if (_featureClassDefinitions.ContainsKey(classKey)) 
            {
                return _featureClassDefinitions[classKey];
            }
            else {
                DAXClassDef fcDef = new DAXClassDef() { Category = categoryName, Name = featureClassName, AliasName = featureClassAliasName };
                _featureClassDefinitions.Add(classKey, fcDef);
                return fcDef;
            }
        }

        public DAXClassDef GetFeatureClassDefinition(string featureClassName)
        {
            if (featureClassName == null)
                return null;

            string origClassKey = featureClassName.ToLower();
            //string origClassKey = featureClassName;

            if (_featureClassDefinitions.ContainsKey(origClassKey))
                return _featureClassDefinitions[origClassKey];
            else
            {
                // Try append database and dataowner name (ArcGIS way of dealing with featureclass names) to the classkey
                if (_featureClassDefinitions.Count > 0 && !origClassKey.Contains("."))
                {
                    foreach (var featureDef in _featureClassDefinitions)
                    {
                        string[] dotSplit = featureDef.Value.Name.Split('.');
                        if (dotSplit.Length == 3)
                        {
                            string newClassKey = (dotSplit[0] + "." + dotSplit[1] + "." + origClassKey);
                            if (_featureClassDefinitions.ContainsKey(newClassKey))
                                return _featureClassDefinitions[newClassKey];
                        }
                    }
                }

                // Try remove ?_fcname
                if (origClassKey.Contains("_"))
                {
                    int indexOfUnderScore = origClassKey.IndexOf('_') + 1;
                    string newClassKey = origClassKey.Substring(indexOfUnderScore, origClassKey.Length - indexOfUnderScore);

                    if (_featureClassDefinitions.ContainsKey(newClassKey))
                        return _featureClassDefinitions[newClassKey];

                    origClassKey = newClassKey;
                }

                // Try append database and dataowner name (ArcGIS way of dealing with featureclass names) to the classkey
                if (_featureClassDefinitions.Count > 0 && !origClassKey.Contains("."))
                {
                    foreach (var featureDef in _featureClassDefinitions)
                    {
                        string[] dotSplit = featureDef.Value.Name.Split('.');
                        if (dotSplit.Length == 3)
                        {
                            string newClassKey = (dotSplit[0] + "." + dotSplit[1] + "." + origClassKey);
                            if (_featureClassDefinitions.ContainsKey(newClassKey))
                                return _featureClassDefinitions[newClassKey];
                        }
                    }
                }

            }

            return null;
        }

        public DAXAttributeDef GetFeatureAttributeDefinition(string featureClassName, string attributName)
        {
            string classKey = featureClassName.ToLower();
            string attrKey = attributName.ToLower();

            //string classKey = featureClassName;
            //string attrKey = attributName;


            DAXClassDef fcDef = GetFeatureClassDefinition(classKey);

            if (fcDef != null)
            {
                if (fcDef.AttributeDefinitions.ContainsKey(attrKey))
                    return fcDef.AttributeDefinitions[attrKey];

                // Try lower case
                attrKey = attrKey.ToLower();
                if (fcDef.AttributeDefinitions.ContainsKey(attrKey))
                    return fcDef.AttributeDefinitions[attrKey];

                // Try replace space with underscore
                attrKey = attrKey.Replace(' ', '_');
                if (fcDef.AttributeDefinitions.ContainsKey(attrKey))
                    return fcDef.AttributeDefinitions[attrKey];

                // Last chance - try replace -, æ, ø and å 
                attrKey = attrKey.Replace('-', '_').Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa");
                if (fcDef.AttributeDefinitions.ContainsKey(attrKey))
                    return fcDef.AttributeDefinitions[attrKey];

                // We give up
                return null;
            }
            else
                return null;
        }


        public DAXAttributeDef AddOrGetAttributeDefinition(string featureClassName, string attributeName, string attributeAliasName) 
        {
            // Don't remove this ToLower
            string classKey = featureClassName.ToLower();

            DAXClassDef classDef = GetFeatureClassDefinition(classKey);

            if (classDef != null)
            {
                // Don't remove this ToLower
                string attrKey = attributeName.ToLower();

                if (classDef.AttributeDefinitions.ContainsKey(attrKey))
                {
                    return classDef.AttributeDefinitions[attrKey];
                }
                else
                {
                    DAXAttributeDef attrDef = new DAXAttributeDef() { ClassDefinition = classDef, Name = attributeName, AliasName = attributeAliasName };
                    classDef.AttributeDefinitions.Add(attrKey, attrDef);
                    return attrDef;
                }
            }

            return null;
        }

        public int NumberOfClassDefinitions()
        {
            return _featureClassDefinitions.Count;
        }
    }

    public enum DAXClassType
    {
        Unknown = 0,
        Table = 1,
        Point = 2,
        Line = 3,
        Polygon = 4
    }


    public class DAXClassDef
    {
        public string Category { get; set; }
        public DAXClassType ClassType { get; set; }
        public string Name { get; set; }
        public string AliasName { get; set; }
        public int Id = 0;
        public Dictionary<string, DAXAttributeDef> AttributeDefinitions = new Dictionary<string, DAXAttributeDef>();
    }

    public enum DAXAttributeType
    {
        Unknown = 0,
        String = 1,
        Int16 = 2,
        Int32 = 3,
        Int64 = 4,
        Single = 5,
        Double = 7,
        DateTime = 8,
        Boolean = 9,
        Guid = 10,
        Decimal = 11,
        Byte = 12,
        MSSQLGeometry = 50
    }

    public class DAXAttributeDef
    {
        public DAXAttributeType AttributeType = DAXAttributeType.Unknown;
        public int Length = 0;
        public int Id = -1;
        public DAXClassDef ClassDefinition { get; set; }
        public string Name { get; set; }
        public string AliasName { get; set; }


        public DbType GetDbType()
        {
            if (AttributeType == DAXAttributeType.String)
                return DbType.String;
            else if (AttributeType == DAXAttributeType.Int16)
                return DbType.Int16;
            else if (AttributeType == DAXAttributeType.Int32)
                return DbType.Int32;
            else if (AttributeType == DAXAttributeType.Int64)
                return DbType.Int64;
            else if (AttributeType == DAXAttributeType.Single)
                return DbType.Single;
            else if (AttributeType == DAXAttributeType.Double)
                return DbType.Double;
            else if (AttributeType == DAXAttributeType.DateTime)
                return DbType.DateTime;
            else if (AttributeType == DAXAttributeType.Boolean)
                return DbType.Boolean;
            else if (AttributeType == DAXAttributeType.Guid)
                return DbType.Guid;
            else if (AttributeType == DAXAttributeType.Byte)
                return DbType.Byte;

            return DbType.Object;
        }
    }

}
