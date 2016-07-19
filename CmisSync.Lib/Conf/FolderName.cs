using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Conf
{
    public class FolderName
    {
        public string name;
        public string remotePath;

        public FolderName()
        {

        }

        public FolderName(string name, string path)
        {
            this.name = name;
            this.remotePath = path;
        }
    }
}
