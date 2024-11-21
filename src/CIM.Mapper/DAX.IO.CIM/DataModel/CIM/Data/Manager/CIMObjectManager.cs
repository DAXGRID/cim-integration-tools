using DAX.NetworkModel.CIM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMObjectManager
    {
        private int _nextId = 1;

        private CIMObjectContainer objectContainer = new CIMObjectContainer();

        private Dictionary<CIMIdentifiedObject, AdditionalObjectAttributes> additionalAttributes = new Dictionary<CIMIdentifiedObject, AdditionalObjectAttributes>();

        private HashSet<CIMIdentifiedObject> deletedObjects = new HashSet<CIMIdentifiedObject>();

        public void Clear()
        {
            _nextId = 1;
            objectContainer = new CIMObjectContainer();
            additionalAttributes = new Dictionary<CIMIdentifiedObject, AdditionalObjectAttributes>();
            deletedObjects = new HashSet<CIMIdentifiedObject>();
        }

        public int GetNextId()
        {
            _nextId++;
            return _nextId - 1;
        }

        public CIMIdentifiedObject GetCIMObjectById(int id)
        {
            if (objectContainer.CIMObjectById.ContainsKey(id))
                return objectContainer.CIMObjectById[id];
            else
                return null;
        }
       

        public int AddCIMObject(CIMIdentifiedObject obj)
        {
            obj.InternalId = GetNextId();
            objectContainer.CIMObjectById.Add(obj.InternalId, obj);

            return obj.InternalId;
        }

        public AdditionalObjectAttributes AdditionalObjectAttributes(CIMIdentifiedObject obj)
        {
            if (!additionalAttributes.ContainsKey(obj))
                additionalAttributes.Add(obj, new AdditionalObjectAttributes());

            return additionalAttributes[obj];
        }

        public void Delete(CIMIdentifiedObject obj)
        {
            if (!deletedObjects.Contains(obj))
                deletedObjects.Add(obj);
        }

        public bool IsDeleted(CIMIdentifiedObject obj)
        {
            if (deletedObjects.Contains(obj))
                return true;
            else
                return false;
        }

        public List<CIMIdentifiedObject> GetObjects()
        {
            List<CIMIdentifiedObject> result = new List<CIMIdentifiedObject>();

            foreach (var obj in objectContainer.CIMObjectById.Values)
            {
                if (!IsDeleted(obj))
                    result.Add(obj);
            }

            return result;
        }
        
    }

    public class AdditionalObjectAttributes
    {
        public int Vertex1Id = 0;
        public int Vertex2Id = 0;
        public bool IsFeederEntryObject = false;
        public bool IsFeederExitObject = false;
        public bool IsTransformerFeederObject = false;
        public bool deleted = false;
        public bool IsFrom = false;
    }

    public class CIMObjectContainer
    {
        public DAXElectricNode Node;

        public Dictionary<int, CIMIdentifiedObject> CIMObjectById = new Dictionary<int, CIMIdentifiedObject>();
    }
}
