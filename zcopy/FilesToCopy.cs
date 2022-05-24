using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zcopy
{
    enum FileType : ushort
    {
        File = 0,
        Directory = 1
    }

    internal class FileCouple
    {
        public FileCouple(FileType type, string src, string dest)
        {
            Type = type;
            Src = src;
            Dest = dest;
        }

        public FileCouple(FileType type,
            string src,
            FileSystemInfo? srcFileSystemInfo,
            string dest) : this(type, src, dest)
        {
            SrcFileSystemInfo = srcFileSystemInfo;
        }

        public FileType Type { get; private set; }
        public string Src { get; private set; }
        public FileSystemInfo? SrcFileSystemInfo { get; set; }
        public string Dest { get; private set; }
    }

    internal class FilesToCopy
    {
        public FilesToCopy()
        {
            FileCouples = new();
            DirectoryCouples = new();
        }

        public FilesToCopy(IEnumerable<FileCouple> coll) : this()
        {
            foreach (var couple in coll)
                FileCouples.Enqueue(couple);
        }

        public ConcurrentQueue<FileCouple> FileCouples { get; private set; }

        public ConcurrentQueue<FileCouple> DirectoryCouples { get; private set; }

        public void AddCouple(FileType ft, string src, string dest)
        {
            if (ft == FileType.File)
                FileCouples.Enqueue(new FileCouple(ft, src, dest));
            else if (ft == FileType.Directory)
                DirectoryCouples.Enqueue(new FileCouple(ft, src, dest));
            else
            {
                throw new NotImplementedException("Unknown FileType type");
            }
        }

        public void AddCouple(FileType type,
            string src,
            FileSystemInfo? srcFileSystemInfo,
            string dest)
        {
            if (type == FileType.File)
                FileCouples.Enqueue(new FileCouple(type, src, srcFileSystemInfo, dest));
            else if (type == FileType.Directory)
                DirectoryCouples.Enqueue(new FileCouple(type, src, srcFileSystemInfo, dest));
            else
            {
                throw new NotImplementedException("Unknown FileType type");
            }
        }
    }
}
