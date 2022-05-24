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

        public async void Start()
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

                            await CopyWithProgress(file);

                            File.SetAttributes(file.Dest, srcFi.Attributes);
                            File.SetCreationTimeUtc(file.Dest, srcFi.CreationTimeUtc);
                            File.SetLastAccessTimeUtc(file.Dest, srcFi.LastAccessTimeUtc);
                            File.SetLastWriteTimeUtc(file.Dest, srcFi.LastWriteTimeUtc);
                        }
                        catch (Exception ex)
                        {
                            await Task.Run(() => copyErrorCallback.Invoke(file, ex.Message));
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Copy file, overwrite existing file
        /// see (basic idea) https://gist.github.com/szunyog/52390c6f8a61615dfc01
        /// and (optimizing filestream) https://stackoverflow.com/a/1247042/9658535
        /// and (async copy) https://stackoverflow.com/a/4139427/9658535
        /// </summary>
        /// <param name="src">source file absolute path</param>
        /// <param name="dest">destination file absolute path</param>
        private async Task CopyWithProgress(FileCouple fi)
        {
            try
            {
                int bufferSize = 1024 * 64;

                using (FileStream inStream = new FileStream(fi.Src, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan & FileOptions.Asynchronous))
                using (FileStream outStream = new FileStream(fi.Dest, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write, bufferSize, FileOptions.SequentialScan & FileOptions.Asynchronous))
                {
                    long totalFileSize = inStream.Length;
                    long copiedSize = 0;
                    int prevPercent = 0;

                    byte[][] buf = { new byte[bufferSize], new byte[bufferSize] };
                    int[] bufl = { 0, 0 };
                    int bufno = 0;
                    Task<int> read = inStream.ReadAsync(buf[bufno], 0, buf[bufno].Length);
                    Task? write = null;

                    while (true)
                    {
                        await read;
                        bufl[bufno] = read.Result;

                        // if zero bytes read, the copy is complete
                        if (bufl[bufno] == 0)
                        {
                            break;
                        }

                        // wait for the in-flight write operation, if one exists, to complete
                        // the only time one won't exist is after the very first read operation completes
                        if (write != null)
                        {
                            await write;
                        }

                        // start the new write operation
                        write = outStream.WriteAsync(buf[bufno], 0, bufl[bufno]);

                        // on ajoute la quantité de données copié dans le compteur
                        copiedSize += bufl[bufno];
                        int percent = (int)(copiedSize / (decimal)totalFileSize * 100);
                        if (percent != prevPercent)
                        {
                            //send update info, we don't data back
                            await Task.Run(() => copyProgressCallback.Invoke(fi, percent));
                            prevPercent = percent;
                        }

                        // toggle the current, in-use buffer
                        // and start the read operation on the new buffer.
                        //
                        // Changed to use XOR to toggle between 0 and 1.
                        // A little speedier than using a ternary expression.
                        bufno ^= 1; // bufno = ( bufno == 0 ? 1 : 0 ) ;
                        read = inStream.ReadAsync(buf[bufno], 0, buf[bufno].Length);

                    }

                    // wait for the final in-flight write operation, if one exists, to complete
                    // the only time one won't exist is if the input stream is empty.
                    if (write != null)
                    {
                        await write;
                    }

                    outStream.Flush();

                }
                await Task.Run(() => copyCompleteCallback.Invoke(fi));
            }
            catch (Exception ex)
            {
                await Task.Run(() => copyErrorCallback.Invoke(fi, ex.Message));
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
