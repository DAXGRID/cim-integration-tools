using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO
{
    public class TransferCommand
    {
        public string TransferSpecificationName { get; set; } 
        public List<KeyValuePair<string, string>> Parameters = new List<KeyValuePair<string, string>>();
        public string FileID { get; set; }
        public DAXSelectionSet SelectionSet { get; set; } 

        public string GetParameter(string name)
        {
            string lowerName = name.ToLower();

            foreach (var param in Parameters)
            {
                if (param.Key.ToLower() == lowerName)
                    return param.Value;
            }

            return null;
        }
    }
}
