using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO
{
    public class DAXGraphException : Exception
    {
        public DAXGraphException() { }

        public DAXGraphException(string message)
            : base(message)
        {
        }

        public DAXGraphException(string message, Exception inner)
            : base(message, inner)
        {
        }

    }

}
