using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DAX.IO
{
    public class GraphProcessorSpecification
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("class")]
        public string ClassName { get; set; }

        [XmlAttribute("assembly")]
        public string AssemblyName { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }

        [XmlElement("Parameter")]
        public List<ConfigParameter> ConfigParameters = new List<ConfigParameter>();
    }
}
