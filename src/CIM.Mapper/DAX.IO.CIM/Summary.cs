using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public class Summary
    {
        private Dictionary<Pair, int> theData = new Dictionary<Pair,int>();

        public void add(int ec, String v) {

            Pair thePair = containsKey(ec, v);
            if (thePair == null) {
                Pair newPair = constructPair(ec, v);
                theData.Add(newPair, 1);
            } else {
                int count = theData[thePair];
                theData[thePair] = count + 1;
            }
        }

        private Pair containsKey(int ec, String v) {

            foreach(Pair p in theData.Keys) {
                if (p.getErrorCode() == ec)
                    if (v.Equals(p.getValue()))
                        return p;
            }
            return null;
        }

        public List<String> dump()
        {
            List<String> theDump = new List<string>();

            foreach (var entry in theData)
            {
                Pair p = entry.Key;
                int count = entry.Value;
                String str = p.getErrorCode() + ", " + p.getValue() + ", " + count;
                theDump.Add(str);
            }
            return theDump;
        }

        public List<Tuple<int,string,int>> dumpValues()
        {
            List<Tuple<int, string, int>> theDump = new List<Tuple<int, string, int>>();

            foreach (var entry in theData)
            {
                Pair p = entry.Key;
                int count = entry.Value;
                String str = p.getErrorCode() + ", " + p.getValue() + ", " + count;
                theDump.Add(new Tuple<int, string, int>(p.getErrorCode(), p.getValue(), count));

            }
            return theDump;
        }

        private Pair constructPair(int ec, String v)
        {
            return new Pair(ec, v);
        }

        private class Pair
        {
            public Pair(int ec, String v)
            {
                errorCode = ec;
                value = v;
            }
            private int errorCode;
            private String value;

            public String getValue()
            {
                return value;
            }

            public int getErrorCode()
            {
                return errorCode;
            }
        }

        public int getCount(GeneralErrors errCode)
        {
            int count = 0;
            foreach (var elm in theData)
            {
                if (elm.Key.getErrorCode() == (int)errCode)
                    count += elm.Value;

            }
            return count;
        }
    }
}
