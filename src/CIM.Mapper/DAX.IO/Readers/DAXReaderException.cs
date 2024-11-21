using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO
{
    public class DAXReaderException : Exception
    {
        public DAXReaderException() { }

        public DAXReaderException(string message)
            : base(message)
        {
        }

        public DAXReaderException(string message, Exception inner)
            : base(message, inner)
        {
        }

    }

}
