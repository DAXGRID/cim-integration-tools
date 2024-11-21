using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DAX.IO.CIM.DataModel
{
    public static class CIMReflectionMetaDataImporter
    {
        public static CIMMetaDataRepository CreateRepository(string envelopeAssemblyName, string envelopeClassName)
        {
            System.Runtime.Remoting.ObjectHandle objHandle = Activator.CreateInstance(envelopeAssemblyName, envelopeClassName);

            var profileEnvelope = objHandle.Unwrap();
            
            PropertyInfo[] props = profileEnvelope.GetType().GetProperties();

            int n = 1;

            foreach (var prop in props)
            {
                System.Diagnostics.Debug.WriteLine(prop.Name + " = " + n + ",");
                n++;
            }

            var arrayType = props[0].PropertyType.GetElementType();

            var p2 = arrayType.GetProperties();

            var p3 = p2[0].PropertyType.GetProperties();



            return null;
        }
    }
}
