using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DAX.IO;

namespace DAX.IO
{
    public class DAXDataSet
    {
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public List<DAXFeature> Features = new List<DAXFeature>();

        public int Count
        {
            get
            {
                return Features.Count;
            }
        }

        public void Clear()
        {
            Features = null;
        }
    }
}
