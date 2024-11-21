using DAX.IO.CIM;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.NetworkModel.CIM
{
    public class DAXElectricNode
    {
         private CIMObjectManager _objManager;

        public DAXElectricNode()
        {
        }

        public DAXElectricNode(CIMObjectManager objManager)
        {
            _objManager = objManager;
        }

        public string Name;

        public CIMClassEnum ClassType = CIMClassEnum.Unknown;

        #region CIM Object Reference handling
        
        public int CIMObjectId = -1;
        
        public CIMIdentifiedObject CIMObject 
        {
            get
            {
                return _objManager.GetCIMObjectById(CIMObjectId);
            }
        }

        #endregion

        #region Sources

        
        private DAXElectricNodeSource[] _sources;

        public DAXElectricNodeSource[] Sources
        {
            get
            {
                return _sources;
            }

            set
            {
                _sources = value;
            }
        }

        public void AddSource(DAXElectricNodeSource source)
        {
            if (_sources == null)
            {
                _sources = new DAXElectricNodeSource[] { source };
            }
            else
            {
                foreach (var existingSource in _sources)
                {
                    if (existingSource.Feeder.DownstreamCIMObjectId == source.Feeder.DownstreamCIMObjectId)
                        return;
                }

                List<DAXElectricNodeSource> list = _sources.ToList();
                list.Add(source);
                _sources = list.ToArray();
            }
        }


        #endregion

        #region Transformers

        private DAXElectricTransformer[] _transformers;

        public DAXElectricTransformer[] Transformers
        {
            get
            {
                return _transformers;
            }

            set
            {
                _transformers = value;
            }
        }

        public void AddTransformer(DAXElectricTransformer transformer)
        {
            if (_transformers == null)
            {
                _transformers = new DAXElectricTransformer[] { transformer };
            }
            else
            {
                if (!_transformers.Contains(transformer))
                {
                    List<DAXElectricTransformer> list = _transformers.ToList();
                    list.Add(transformer);
                    _transformers = list.ToArray();
                }
            }
        }

        #endregion

        #region Feeders


        private List<DAXElectricFeeder> _feeders;

        public List<DAXElectricFeeder> Feeders
        {
            get
            {
                return _feeders;
            }

            set
            {
                _feeders = value;
            }
        }


        public void AddFeeder(DAXElectricFeeder feeder)
        {
            if (_feeders == null)
                _feeders = new List<DAXElectricFeeder> { feeder };
            else
                _feeders.Add(feeder);
        }

        #endregion

        public double[] Coords { get; set; }


        public override string ToString()
        {
            return "DAXElectricNode: Name='" + Name + "'";
        }

        public int VoltageLevel { get; set; }

        public string Description { get; set; }

    }
}
