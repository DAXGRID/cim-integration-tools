namespace DAX.IO
{
    public class DAXWriterException : Exception
    {
        public DAXWriterException() { }

        public DAXWriterException(string message)
            : base(message)
        {
        }

        public DAXWriterException(string message, Exception inner)
            : base(message, inner)
        {
        }
    
    }
}
