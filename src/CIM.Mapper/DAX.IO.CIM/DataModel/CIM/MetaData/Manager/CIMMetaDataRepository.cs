using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.CIM
{
    public class CIMMetaDataRepository
    {
        private Dictionary<int, CIMClassDef> _classDefs = new Dictionary<int, CIMClassDef>();
        private int _nextClassDefId = 1;

        private Dictionary<int, CIMPSRType> _psrTypes = new Dictionary<int, CIMPSRType>();
        private int _nextPsrTypeId = 1;

        // lookup
        private Dictionary<string, CIMPSRType> _psrTypeByName = new Dictionary<string, CIMPSRType>();
        private Dictionary<int, CIMPSRType> _psrTypeById = new Dictionary<int, CIMPSRType>();

        private readonly Mutex _mutex = new();

        public CIMClassDef CreateClassDef()
        {
            var classDef = new CIMClassDef() { Id = _nextClassDefId };
            _classDefs.Add(classDef.Id, classDef);
            _nextClassDefId++;
            return classDef;
        }

        public CIMClassDef GetClassDefById(int id)
        {
            if (_classDefs.ContainsKey(id))
                return _classDefs[id];

            return null;
        }

        public CIMPSRType CreateCIMPSRType(string name)
        {
            _mutex.WaitOne();

            if (_psrTypeByName.ContainsKey(name))
                return _psrTypeByName[name];

            var psrType = new CIMPSRType() { Id = _nextPsrTypeId, Name = name };
            _psrTypes.Add(psrType.Id, psrType);
            _psrTypeByName.Add(psrType.Name, psrType);
            _psrTypeById.Add(psrType.Id, psrType);
            _nextPsrTypeId++;

            _mutex.ReleaseMutex();

            return psrType;
        }

        public string GetCIMPSRType(int id)
        {
            if (_psrTypeById.ContainsKey(id))
                return _psrTypeById[id].Name;

            return null;
        }

        
    }
}
