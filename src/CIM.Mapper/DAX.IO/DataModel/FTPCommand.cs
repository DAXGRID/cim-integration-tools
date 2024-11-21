using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAX.IO
{
    public enum FTPCommandType
    {
        Upload = 1,
        Download = 2,
        Dir = 3
    }

    public class FTPCommand
    {
        public FTPCommandType CommandType { get; set; }
        public string CurrentFolder { get; set; }
        public string HostName { get; set; }
        public string UserName { get; set; }
        public string UserPassword { get; set; }
        public string FileID { get; set; }
        public string FileName { get; set; }
    }
}
