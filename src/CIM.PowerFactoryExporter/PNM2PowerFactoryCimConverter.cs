using CIM.PhysicalNetworkModel.Traversal;
using CIM.PhysicalNetworkModel.Traversal.Internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PowerFactoryExporter
{
    public class PNM2PowerFactoryCimConverter
    {
        private IEnumerable<PhysicalNetworkModel.IdentifiedObject> _inputCimObjects;
        private List<IPreProcessor> _preProcessors = new List<IPreProcessor>();
        private CimContext _context;

        public PNM2PowerFactoryCimConverter(IEnumerable<PhysicalNetworkModel.IdentifiedObject> cimObjects, List<IPreProcessor> preProcessors = null)
        {
            _inputCimObjects = cimObjects;
            _context = new InMemCimContext(cimObjects);

            if (preProcessors != null)
                _preProcessors = preProcessors;
        }

        public IEnumerable<PhysicalNetworkModel.IdentifiedObject> GetCimObjects()
        {
            var input = _inputCimObjects.ToList();
            var output = _inputCimObjects.ToList();

            foreach (var preProcessor in _preProcessors)
            {
                output = preProcessor.Transform(_context, input).ToList();
                input = output;
            }

            foreach (var obj in output)
                yield return obj;
        }
    }
}
