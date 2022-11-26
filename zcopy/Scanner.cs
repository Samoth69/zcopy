using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zcopy
{
    internal class Scanner
    {
        /// <summary>
        /// Callback when an error happend while scanning
        /// </summary>
        /// <param name="file">concerned file/folder</param>
        /// <param name="err">error message containing more info</param>
        public delegate void DScanErrorCallback(string file, string err);

        /// <summary>
        /// Callback when a file is skipped (file is up to date on destination)
        /// </summary>
        /// <param name="file">full path to file</param>
        public delegate void DScanSkippedCallback(string file);

        /// <summary>
        /// callback when the scanner has finished
        /// </summary>
        public delegate void DScanFinished();

        /// <summary>
        /// Will be called when an error happend
        /// </summary>
        private DScanErrorCallback scanErrorCallback;

        /// <summary>
        /// Will be called when a file has been skipped, check delegate for more details
        /// </summary>
        private DScanSkippedCallback scanSkippedCallback;

        private DScanFinished scanFinishedCallback;

        /// <summary>
        /// Limit the number of entries in ConcurrentQueue
        /// </summary>
        private const int todoMaxBufferSize = 100;

        /// <summary>
        /// ConcurrentQueue shared with Copier Thread
        /// </summary>
        private ConcurrentQueue<FilesToCopy> todo;

        /// <summary>
        /// List of folder to include
        /// If this list is empty, everything in src is included (unless they are in excluded folder)
        /// </summary>
        private IEnumerable<string> includedFolders;

        /// <summary>
        /// List of folder to exclude, prioritized over includedFolders
        /// </summary>
        private IEnumerable<string> excludedFolders;

        /// <summary>
        /// src: source folder
        /// dest: destination folder
        /// </summary>
        private string src, dest;

        public Scanner(string src,
            string dest,
            IEnumerable<string> includedFolders,
            IEnumerable<string> excludedFolders,
            ConcurrentQueue<FilesToCopy> todo,
            DScanErrorCallback scanErrorCallback,
            DScanSkippedCallback scanSkippedCallback,
            DScanFinished scanFinishedCallback)
        {
            this.todo = todo;
            this.scanErrorCallback = scanErrorCallback;
            this.scanSkippedCallback = scanSkippedCallback;
            this.includedFolders = includedFolders;
            this.excludedFolders = excludedFolders;
            this.src = src;
            this.dest = dest;
            this.scanFinishedCallback = scanFinishedCallback;
        }

        public async Task Start()
        {
            EnqueuedFolder currentFolder = new(src);
            string currentDestFolder;

            Queue<EnqueuedFolder> pending = new Queue<EnqueuedFolder>();
            pending.Enqueue(currentFolder);

            while (pending.Count > 0)
            {
                // wait to not overfill copy queue
                while (todo.Count > todoMaxBufferSize) Thread.Sleep(1);

                currentFolder = pending.Dequeue();
                currentDestFolder = dest + currentFolder.Path.Remove(0, src.Length);

                try
                {
                    FilesToCopy filesToCopy = new();
                    DirectoryInfo srcDi = new(currentFolder.Path);
                    if (Directory.Exists(currentDestFolder))
                    {
                        DirectoryInfo destDi = new(currentDestFolder);
                        if (!CompareSec(srcDi.LastWriteTimeUtc, destDi.LastWriteTimeUtc))
                        {
                            filesToCopy.AddCouple(FileType.Directory, currentFolder.Path, srcDi, currentDestFolder);
                        }
                    }
                    else //if the directory doesn't exist on dest, we create it and copy over metadata infos
                    {
                        currentFolder.ExistOnDest = false;
                        filesToCopy.AddCouple(FileType.Directory, currentFolder.Path, srcDi, currentDestFolder);
                    }

                    foreach (string srcFile in Directory.EnumerateFiles(currentFolder.Path))
                    {
                        try
                        {
                            string destFile = Path.Combine(currentDestFolder, Path.GetFileName(srcFile));
                            FileInfo srcFi = new(srcFile);

                            // if the current folder doesn't exist on dest,
                            // we skip the checks
                            if (currentFolder.ExistOnDest)
                            {
                                if (File.Exists(destFile))
                                {
                                    FileInfo destFi = new(destFile);
                                    if (CompareSec(srcFi.LastWriteTimeUtc, destFi.LastWriteTimeUtc) && srcFi.Length == destFi.Length)
                                    {
                                        await Task.Run(() => scanSkippedCallback.Invoke(srcFile));
                                        continue;
                                    }
                                }
                            }

                            filesToCopy.AddCouple(FileType.File, srcFile, srcFi, destFile);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            await Task.Run(() => scanErrorCallback.Invoke(srcFile, ex.Message));
                        }
                    }
                    if (filesToCopy.FileCouples.Any())
                        todo.Enqueue(filesToCopy);
                }
                catch (UnauthorizedAccessException ex)
                {
                    await Task.Run(() => scanErrorCallback.Invoke(currentFolder.Path, ex.Message));
                }

                foreach (string folder in Directory.EnumerateDirectories(currentFolder.Path))
                {
                    string relativeToSrc = folder.Remove(0, src.Length);
                    string firstRelativeToSrc = relativeToSrc;
                    if (relativeToSrc.Contains('\\'))
                        firstRelativeToSrc = relativeToSrc.Substring(0, relativeToSrc.IndexOf("\\"));

                    if (excludedFolders.Contains(firstRelativeToSrc) || (includedFolders.Any() && !includedFolders.Contains(firstRelativeToSrc)))
                    {
                        //LogVerbose($"Skipped {folder}, matched filter folder");
                    }
                    else
                    {
                        //LogVerbose($"added {folder} to queue");
                        pending.Enqueue(new EnqueuedFolder(folder));
                    }
                }
            }
            scanFinishedCallback.Invoke();
        }

        public static bool CompareSec(DateTime dt1, DateTime dt2)
        {
            return dt1.Year == dt2.Year &&
                dt1.Month == dt2.Month &&
                dt1.Day == dt2.Day &&
                dt1.Hour == dt2.Hour &&
                dt1.Minute == dt2.Minute &&
                dt1.Second == dt2.Second;
        }
    }
}
