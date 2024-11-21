using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO.DataModel
{
    public class FTPCommandResult
    {
        public string CurrentFolder { get; set; }
        public string FileID { get; set; }
        public List<FileObject> FileObjects = new List<FileObject>();
    }

    public class FileObject
    {
        public bool IsDirectory { get; set; }
        public string Name { get; set; }
    }
}
