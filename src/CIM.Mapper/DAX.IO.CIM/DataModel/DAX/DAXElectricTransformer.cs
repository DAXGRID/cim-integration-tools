using DAX.IO.CIM;

namespace DAX.NetworkModel.CIM
{
    public class DAXElectricTransformer
    {
        private CIMObjectManager _objManager;

        public DAXElectricTransformer()
        {
        }

        public DAXElectricTransformer(CIMObjectManager objManager)
        {
            _objManager = objManager;
        }

        public int FeederObjectId = 0;

        public DAXElectricNode Node;

        public string Name;

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

        private DAXElectricFeeder[] _sources;

        public DAXElectricFeeder[] Sources
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

        public void AddSource(DAXElectricFeeder feeder)
        {
            if (_sources == null)
            {
                _sources = new DAXElectricFeeder[] { feeder };
            }
            else
            {
                if (!_sources.Contains(feeder))
                {
                    List<DAXElectricFeeder> list = _sources.ToList();
                    list.Add(feeder);
                    _sources = list.ToArray();
                }
            }
        }

        #endregion

    }
}
