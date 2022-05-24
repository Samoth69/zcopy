using Mono.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace zcopy
{
    internal class Program
    {
        // if == 1, increase log from the app
        private static byte verbose = 0;

        private static Screen screen = new();

        static int Main(string[] args)
        {
            Console.Clear();

            // will be true if help should be shown and then exit
            bool shouldShowHelp = false;

            // list of relative path from src to exclude from copy
            HashSet<string> excludedFolders = new HashSet<string>();

            HashSet<string> includedFolders = new HashSet<string>();

            // excluding not wanted stuff
            excludedFolders.Add("$RECYCLE.BIN");
            excludedFolders.Add("System Volume Information");

            // path to exclude list folder
            string? excludedFoldersFilePath = null;

            string? includedFoldersFilePath = null;

            // options of the app
            var options = new OptionSet
            {
                { "ex|exclude=", "path to text file containing absolute folders path to exclude, one folder per line.", ef => excludedFoldersFilePath = ef },
                { "in|include=", "path to text file containing absolute folders path to exclude, one folder per line.", i => includedFoldersFilePath = i },
                { "v|verbose", "increase debug message verbosity", v => {
                    if (v != null)
                        ++verbose;
                } },
                { "h|help", "show this message and exit", h => shouldShowHelp = h != null },
            };

            // extra args parsed by mono.options
            // should contains source and destination path
            List<string> extra;

            // will switch to true if generic help message should be displayed
            // will also return -1 exit code
            bool errored = false;
            try
            {
                // parse the command line
                extra = options.Parse(args);

                if (shouldShowHelp)
                {
                    // show some app description message
                    Console.WriteLine("Usage: zcopy [OPTIONS]+ SOURCE DESTINATION");
                    Console.WriteLine("Fast copy/backup of files from source to destination");
                    Console.WriteLine("Return code different from 0 mean the copy failed for some reason");
                    Console.WriteLine();
                    Console.WriteLine("SOURCE:");
                    Console.WriteLine("  Source directory to copy, with all inside content");
                    Console.WriteLine("DESTINATION:");
                    Console.WriteLine("  Destination folder for copy: if file already exist, compare size and last editted date, newer files as well as size different files are copied over");
                    Console.WriteLine();

                    // output the options
                    Console.WriteLine("Options:");
                    options.WriteOptionDescriptions(Console.Out);
                }
                else
                {
                    if (extra.Count >= 2)
                    {
                        string srcPath = extra[0];
                        string destPath = extra[1];
                        if (!StartupChecks(srcPath, destPath))
                        {
                            errored = true;
                        }
                        else
                        {
                            // will be skipped if excludedFolderFilePath is null
                            if (File.Exists(excludedFoldersFilePath))
                            {
                                foreach (string line in File.ReadAllLines(excludedFoldersFilePath))
                                {
                                    WriteLineLog($"Added {line} as folder blacklist");
                                    excludedFolders.Add(line);
                                }
                            }

                            if (File.Exists(includedFoldersFilePath))
                            {
                                foreach (string line in File.ReadAllLines(includedFoldersFilePath))
                                {
                                    WriteLineLog($"Added {line} as folder whitelist");
                                    includedFolders.Add(line);
                                }
                            }

                            // if DoCopy fail for some reason, we mark errored has true
                            //errored = !DoCopy(srcPath, destPath, excludedFolders, includedFolders);
                            StartThreads(srcPath, destPath, excludedFolders, includedFolders);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Please provide source and destination paths");
                        errored = true;
                    }
                }
            }
            catch (OptionException e)
            {
                // output some error message
                Console.WriteLine(e.Message);
                errored = true;
            }

            if (errored)
            {
                Console.WriteLine("Try `zcopy --help' for more information.");
                return -1;
            }
            return 0;
        }

        /// <summary>
        /// Check provided source and destination folder before starting copy
        /// </summary>
        /// <param name="src">absolute path to source</param>
        /// <param name="dest">absolute path to destination</param>
        /// <returns>true if everything ok, otherwise false will be returned with aditionnal information printed in Console.Out</returns>
        private static bool StartupChecks(string src, string dest)
        {
            if (!Directory.Exists(src))
            {
                Console.WriteLine("Err: Source directory doesn't exist (May be permissions releated)");
                return false;
            }
            if (!Directory.Exists(dest))
            {
                Console.WriteLine("Err: Destination directory doesn't exist (May be permissions releated)");
                return false;
            }

            if (!Utils.IsDirectoryWritable(dest))
            {
                Console.WriteLine("Err: Destination directory doesn't seem writable");
                return false;
            }

            return true;
        }

        private static void StartThreads(string src, string dest, ICollection<string> excludeFilter, ICollection<string> includeFilter)
        {
            Stopwatch watch = new();
            ConcurrentQueue<FilesToCopy> queue = new();
            Scanner scanner = new(src, dest, includeFilter, excludeFilter, queue, ScannerErrorCallback, ScannerSkippedCallback);
            Copier copier = new(queue, CopierProgressCallback, CopierErrorCallback, CopierCompleteCallback);

            Thread scannerThread = new Thread(new ThreadStart(scanner.Start));
            Thread copyThread = new Thread(new ThreadStart(copier.Start));
            Thread screenThread = new Thread(new ThreadStart(screen.Start));

            WriteLineLog("Starting Scanner thread");
            scannerThread.Start();

            WriteLineLog("Starting Copy thread");
            copyThread.Start();

            if (verbose != 1)
                screenThread.Start();

            scannerThread.Join();
            WriteLineLog("Scanner Thread exited");

            //notify copier to stop main loop
            copier.Stop();

            copyThread.Join();
            WriteLineLog("Copy Thread exited");

            screen.Stop();

            WriteLineLog($"ExecTime: {screen.GetElaspedTime}");
            WriteLineLog($"ScannerError: {screen.ScannerError}");
            WriteLineLog($"CopierError: {screen.CopierError}");
            WriteLineLog($"Copied: {screen.Copied}");
            WriteLineLog($"Skipped: {screen.Skipped}");
        }

        private static void ScannerErrorCallback(string file, string err)
        {
            screen.ScannerError++;
            WriteLineLog($"Err Scanner: {file} {err}");
            screen.ScreenScanningFile = file;
        }

        private static void ScannerSkippedCallback(string file)
        {
            screen.Skipped++;
            WriteLineLog($"Scanner: skipped {file}");
            screen.ScreenScanningFile = file;
        }

        private static void CopierProgressCallback(FileCouple fi, int percent)
        {
            if (verbose == 1)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                WriteLog($"Copy: {percent}% {fi.Src}");
            }
            screen.ScreenCopyFile = fi.Src;
            screen.ScreenPercent = percent;
        }

        private static void CopierErrorCallback(FileCouple fi, string err)
        {
            screen.CopierError++;
            WriteLineLog($"Err Copier: {fi.Src} {err}");
            screen.ScreenCopyFile = fi.Src;
            screen.ScreenPercent = null;
        }

        private static void CopierCompleteCallback(FileCouple fi)
        {
            screen.Copied++;
            WriteLineLog("");
            screen.ScreenCopyFile = fi.Src;
            screen.ScreenPercent = null;
        }

        /// <summary>
        /// Log message if verbose is on, do nothing otherwise
        /// </summary>
        /// <param name="text">text to log</param>
        private static void WriteLineLog(string text)
        {
            if (verbose == 1)
            {
                Console.WriteLine(text);
            }
        }

        private static void WriteLog(string text)
        {
            if (verbose == 1)
            {
                Console.Write(text);
            }
        }        
    }
}