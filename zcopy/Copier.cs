using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace zcopy
{
    internal class Copier
    {
        public delegate void DCopyProgressCallback(FileCouple fi, int percent);

        public delegate void DCopyErrorCallback(FileCouple fi, string err);

        public delegate void DCopyCompleteCallback(FileCouple fi);

        private DCopyProgressCallback copyProgressCallback;
        private DCopyErrorCallback copyErrorCallback;
        private DCopyCompleteCallback copyCompleteCallback;

        private ConcurrentQueue<FilesToCopy> todo;

        private bool done = false;

        public Copier(ConcurrentQueue<FilesToCopy> jobs, DCopyProgressCallback progressCallback, DCopyErrorCallback errorCallback, DCopyCompleteCallback completeCallback)
        {
            todo = jobs;
            copyProgressCallback = progressCallback;
            copyErrorCallback = errorCallback;
            copyCompleteCallback = completeCallback;
        }

        public void Start()
        {
            while (!done)
            {
                FilesToCopy? filesToCopy;

                while (todo.TryDequeue(out filesToCopy))
                {
                    if (filesToCopy is null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    //creating directories should be cheap on the disk drive
                    filesToCopy.DirectoryCouples.AsParallel().ForAll((file) =>
                    {
                        DirectoryInfo di;
                        if (file.SrcFileSystemInfo is null)
                            di = new(file.Src);
                        else
                            di = (DirectoryInfo)file.SrcFileSystemInfo;

                        Directory.CreateDirectory(file.Dest);

                        Directory.SetCreationTimeUtc(file.Dest, di.CreationTimeUtc);
                        Directory.SetLastAccessTimeUtc(file.Dest, di.LastAccessTimeUtc);
                        Directory.SetLastWriteTimeUtc(file.Dest, di.LastWriteTimeUtc);
                    });

                    foreach (var file in filesToCopy.FileCouples)
                    {
                        try
                        {
                            FileInfo srcFi;
                            if (file.SrcFileSystemInfo is null)
                                srcFi = new(file.Src);
                            else
                                srcFi = (FileInfo)file.SrcFileSystemInfo;

                            CopyWithProgress(file);

                            File.SetAttributes(file.Dest, srcFi.Attributes);
                            File.SetCreationTimeUtc(file.Dest, srcFi.CreationTimeUtc);
                            File.SetLastAccessTimeUtc(file.Dest, srcFi.LastAccessTimeUtc);
                            File.SetLastWriteTimeUtc(file.Dest, srcFi.LastWriteTimeUtc);
                        }
                        catch (Exception ex)
                        {
                            Task.Run(() => copyErrorCallback.Invoke(file, ex.Message));
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Copy file, overwrite existing file
        /// see https://gist.github.com/szunyog/52390c6f8a61615dfc01
        /// and https://stackoverflow.com/a/1247042/9658535
        /// </summary>
        /// <param name="src">source file absolute path</param>
        /// <param name="dest">destination file absolute path</param>
        private void CopyWithProgress(FileCouple fi)
        {
            try
            {
                int bufferSize = 1024 * 64;
                using (FileStream inStream = new FileStream(fi.Src, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
                using (FileStream fileStream = new FileStream(fi.Dest, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write, bufferSize, FileOptions.SequentialScan))
                {
                    int bytesRead = -1;
                    long totalReads = 0;
                    long totalBytes = inStream.Length;
                    byte[] bytes = new byte[bufferSize];
                    int prevPercent = 0;

                    while ((bytesRead = inStream.Read(bytes, 0, bufferSize)) > 0)
                    {
                        fileStream.Write(bytes, 0, bytesRead);
                        totalReads += bytesRead;
                        int percent = System.Convert.ToInt32(totalReads / (decimal)totalBytes * 100);
                        if (percent != prevPercent)
                        {
                            //send update info, we don't data back
                            Task.Run(() => copyProgressCallback.Invoke(fi, percent));
                            prevPercent = percent;
                        }
                    }
                }
                Task.Run(() => copyCompleteCallback.Invoke(fi));
            }
            catch (Exception ex)
            {
                Task.Run(() => copyErrorCallback.Invoke(fi, ex.Message));
            }
        }

        /// <summary>
        /// Mark that this thread need to exit after queue is fully processed
        /// it is thread safe since boolean are an atomic operation ou x86/x64 system
        /// </summary>
        public void Stop()
        {
            done = true;
        }
    }
}
