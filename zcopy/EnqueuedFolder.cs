using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zcopy
{
    /// <summary>
    /// Used by scanner
    /// Allow scanner to enqueue folder and remember if the folder does exist on dest
    /// </summary>
    internal class EnqueuedFolder
    {
        public string Path { get; private set; }
        public bool ExistOnDest { get; set; }

        public EnqueuedFolder(string path)
        {
            Path = path;
            // by default, we consider a folder existing, it can be changed later
            ExistOnDest = true;
        }

        public EnqueuedFolder(string path, bool existOnDest) : this(path)
        {
            ExistOnDest = existOnDest;
        }
    }
}
