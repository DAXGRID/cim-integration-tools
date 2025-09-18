using DAX.NetworkModel.CIM;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMIdentifiedObject : CIMObject
    {
        public CIMObjectManager ObjectManager = null;

        public CIMIdentifiedObject(CIMObjectManager objectManager)
        {
            ObjectManager = objectManager;
            ObjectManager.AddCIMObject(this);
        }

        /// <summary>
        /// ID used internally to refer to the CIM object from within a graph.
        /// </summary>
        public int InternalId { get; set; }

        /// <summary>
        /// External ID mapping from GIS or other systems. Must be unique across the CIM class.
        /// </summary>
        public string ExternalId { get; set; }

        /// <summary>
        /// Name (see CIM spec).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description (se CIM spec).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Coords from GIS.
        /// </summary>
        public double[] Coords { get; set; }


        private int voltageLevel = 0;

        /// <summary>
        /// Voltage level in volts.
        /// Components sitting in a bay will take the voltage level from the bay.
        /// </summary>
        public int VoltageLevel {
            set
            {
                voltageLevel = value;
            }
            get
            {
                if (EquipmentContainerRef != null && EquipmentContainerRef.ClassType == CIMClassEnum.Bay)
                    return EquipmentContainerRef.VoltageLevel;
                else
                    return voltageLevel;
            }
        }
        
        private int PSRTypeId;

        #region Neighbor (internal connectivity) handling
        private List<CIMIdentifiedObject> _neighbours = new List<CIMIdentifiedObject>();

        /// <summary>
        /// Used for graph travering (without the need to go through terminals).
        /// Only use this function for data validation and traversal.
        /// Don't use this function for serialization. 
        /// Will return objects even that connectivity don't comply to CIM.
        /// </summary>
        internal List<CIMIdentifiedObject> Neighbours
        {
            get { return _neighbours; }

            set { _neighbours = value; }
        }

        /// <summary>
        /// Don't use unless you know what you're doing :)
        /// </summary>
        /// <param name="obj"></param>
        internal void AddNeighbour(CIMIdentifiedObject obj)
        {
            _neighbours.Add(obj);
        }

        /// <summary>
        /// Don't use unless you know what you're doing :)
        /// </summary>
        /// <param name="range"></param>
        internal void AddNeighbour(IEnumerable<CIMIdentifiedObject> range)
        {
            _neighbours.AddRange(range);
        }

        /// <summary>
        /// Don't use unless you know what you're doing :)
        /// </summary>
        /// <param name="obj"></param>
        internal void RemoveNeighbour(CIMIdentifiedObject obj)
        {
            _neighbours.Remove(obj);
        }
        #endregion

        public CIMEquipmentContainer EquipmentContainerRef { get; set; }
        public CIMClassEnum ClassType { get; set; }
        
        internal void SetClass(string className)
        {
            ClassType = (CIMClassEnum)Enum.Parse(typeof(CIMClassEnum), className, true);
        }

        internal void SetClass(CIMClassEnum classType)
        {
            ClassType = classType;
        }

        internal void SetPSRType(CIMMetaDataRepository metaDataRepository, string psrType)
        {
            PSRTypeId = metaDataRepository.CreateCIMPSRType(psrType).Id;
        }

        public string GetPSRType(CIMMetaDataRepository metaDataRepository)
        {
            return metaDataRepository.GetCIMPSRType(PSRTypeId);
        }

        // Property handling
        #region Property methods

        private Dictionary<string, object> attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, object> getAttributes()
        {
            return attributes;
        }

        public void SetPropertyValue(string name, object value)
        {
            attributes[name] = value;
        }

        public object GetPropertyValue(string name)
        {
            if (attributes.ContainsKey(name))
                return attributes[name];
            else
                return null;
        }

        public bool ContainsPropertyValue(string name)
        {
            if (attributes.ContainsKey(name))
                return true;
            else
                return false;
        }

        public string GetPropertyValueAsString(string name)
        {
            if (!attributes.ContainsKey(name))
                return null;

            object val = attributes[name];
            if (val != null)
                return val.ToString();
            else
                return null;
        }


        public int? GetPropertyValueAsInt(string name)
        {
            if (!attributes.ContainsKey(name))
                return null;

            object val = attributes[name];
            int intResult = 0;
            if (val != null && Int32.TryParse(val.ToString(), out intResult))
                return intResult;
            else
                return null;
        }

        public decimal? GetPropertyValueAsDecimal(string name)
        {
            if (!attributes.ContainsKey(name))
                return null;

            object val = attributes[name];
            decimal decimalResult = 0;
            if (val != null && Decimal.TryParse(val.ToString(), out decimalResult))
                return decimalResult;
            else
                return null;
        }

        public void RemoveProperty(string name)
        {
            attributes.Remove(name);
        }

        #endregion


        internal bool coordFlipped = false;

        public List<CIMIdentifiedObject> GetNeighbours(CIMIdentifiedObject excludeThis = null)
        {
            List<CIMIdentifiedObject> result = new List<CIMIdentifiedObject>();

            foreach (var n in Neighbours)
            {
                if (excludeThis == null || (n != excludeThis))
                    result.Add(n);
            }

            return result;
        }

        public List<CIMIdentifiedObject> GetNeighbours(CIMClassEnum classType)
        {
            List<CIMIdentifiedObject> result = new List<CIMIdentifiedObject>();

            foreach (var n in Neighbours)
            {
                if (n.ClassType == classType)
                    result.Add(n);
            }

            return result;
        }

        public List<CIMIdentifiedObject> GetNeighboursNeighbors(CIMClassEnum classType)
        {
            List<CIMIdentifiedObject> result = new List<CIMIdentifiedObject>();

            foreach (var fn in Neighbours)
            {
                if (fn.ClassType == classType)
                    result.Add(fn);

                foreach (var sn in fn.Neighbours)
                {
                    if (sn.ClassType == classType)
                        result.Add(sn);
                }
            }

            return result;
        }

        public List<CIMIdentifiedObject> GetNeighboursNeighbors2()
        {
            List<CIMIdentifiedObject> result = new List<CIMIdentifiedObject>();

            foreach (var fn in Neighbours)
            {
                foreach (var sn in fn.Neighbours)
                {
                    if (sn != this)
                        result.Add(sn);
                }
            }

            return result;
        }

        internal List<CIMIdentifiedObject> GetNeighbours(CIMClassEnum classType, CIMClassEnum classType2)
        {
            List<CIMIdentifiedObject> result = new List<CIMIdentifiedObject>();

            foreach (var n in Neighbours)
            {
                if (n.ClassType == classType || n.ClassType == classType2)
                    result.Add(n);
            }

            return result;
        }

        /// <summary>
        /// Convenient method to return the root container.
        /// If called on CIM object sitting in a bay, the substation will be returned etc.
        /// </summary>
        /// <returns></returns>
        public CIMEquipmentContainer GetEquipmentContainerRoot()
        {
            if (EquipmentContainerRef != null && (EquipmentContainerRef.ClassType == CIMClassEnum.Substation || EquipmentContainerRef.ClassType == CIMClassEnum.Enclosure))
                return EquipmentContainerRef;
            else if (EquipmentContainerRef != null && EquipmentContainerRef.ClassType == CIMClassEnum.Bay && EquipmentContainerRef.EquipmentContainerRef != null && (EquipmentContainerRef.EquipmentContainerRef.ClassType == CIMClassEnum.Substation || EquipmentContainerRef.EquipmentContainerRef.ClassType == CIMClassEnum.Enclosure))
                return EquipmentContainerRef.EquipmentContainerRef;

            return null;
        }

        public override string ToString()
        {
            string val = "";
            val += ClassType.ToString() + ": extenalId='" + ExternalId + "' name='" + Name + "' VoltageLevel='" + VoltageLevel + "' desc='" + Description + "'";

            if (EquipmentContainerRef != null) {
                val += "\r\n  Parent: " + EquipmentContainerRef.ClassType.ToString() + " " + EquipmentContainerRef.Name + " ";

                if (EquipmentContainerRef.EquipmentContainerRef != null)
                val += " -> " + EquipmentContainerRef.EquipmentContainerRef.ClassType.ToString() + " " + EquipmentContainerRef.EquipmentContainerRef.Name;
            }

            val += ClassType.ToString() + " mRID='" + mRID + "'";

            return val;
        }

        public string IdString()
        {
            return "(" + ClassType.ToString() + ": mRID=" + mRID + ", name='" + Name + "' ExternalId='" + ExternalId + "')";
        }
    }
}
