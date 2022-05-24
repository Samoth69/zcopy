using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zcopy
{
    internal class Screen
    {
        public ulong ScannerError = 0;
        public ulong CopierError = 0;
        public ulong Copied = 0;
        public ulong Skipped = 0;

        private ReaderWriterLockSlim LockScreenScanningFile = new();

        /// <summary>
        /// File currently being scanned
        /// </summary>
        private string screenScanningFile = "";

        public string ScreenScanningFile
        {
            get
            {
                LockScreenScanningFile.EnterReadLock();
                try
                {
                    return screenScanningFile;
                }
                finally
                {
                    LockScreenScanningFile.ExitReadLock();
                }
            }
            set
            {
                LockScreenScanningFile.EnterWriteLock();
                screenScanningFile = value;
                LockScreenScanningFile.ExitWriteLock();
            }
        }

        private ReaderWriterLockSlim LockScreenCopyFile = new();

        /// <summary>
        /// File currently being copied
        /// </summary>
        private string screenCopyFile = "";

        public string ScreenCopyFile
        {
            get
            {
                LockScreenCopyFile.EnterReadLock();
                try
                {
                    return screenCopyFile;
                }
                finally
                {
                    LockScreenCopyFile.ExitReadLock();
                }
            }
            set
            {
                LockScreenCopyFile.EnterWriteLock();
                screenCopyFile = value;
                LockScreenCopyFile.ExitWriteLock();
            }
        }

        public int? ScreenPercent = null;

        private Stopwatch ExecTime = new();

        private bool Done = false;

        public void Start()
        {
            ExecTime.Start();

            while (!Done)
            {
                UpdateScreen();
                Thread.Sleep(50);
            }
        }

        public void Stop()
        {
            ExecTime.Stop();
        }

        public TimeSpan GetElaspedTime => ExecTime.Elapsed;

        private int OldFileNameSize = 0;

        /// <summary>
        /// Print copy status, do not print if verbose is on
        /// </summary>
        /// <param name="srcFile"></param>
        /// <param name="destFile"></param>
        /// <param name="progress"></param>
        private void UpdateScreen()
        {
            Console.SetCursorPosition(0, 0);

            string displayFiName = ScreenCopyFile;

            if (OldFileNameSize != 0 && OldFileNameSize > displayFiName.Length)
            {
                Console.Clear();
            }

            if (displayFiName.Length > Console.WindowWidth - 6)
            {
                displayFiName = displayFiName.Substring(0, Console.WindowWidth - 6);
            }

            ConsoleWriteLineWithColor($"file: {displayFiName}", ConsoleColor.Gray);
            if (ScreenPercent != null)
                ConsoleWriteNumberWithSpColor("Progress: ", ScreenPercent, " %", ConsoleColor.White, ConsoleColor.Gray);

            Console.WriteLine();
            ConsoleWriteNumberWithSpColor("Running: ", ExecTime.Elapsed.ToString("d\\.hh\\:mm\\:ss"), null, ConsoleColor.White, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Copied: ", Copied, null, ConsoleColor.Green, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Skipped: ", Skipped, null, ConsoleColor.DarkGreen, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Scanner Error: ", ScannerError, null, ConsoleColor.DarkRed, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Copier Error: ", CopierError, null, ConsoleColor.DarkRed, ConsoleColor.Gray);

            OldFileNameSize = displayFiName.Length;
        }

        private void ConsoleWriteNumberWithSpColor(string? header, object value, string? footer, ConsoleColor accent, ConsoleColor textColor = ConsoleColor.White)
        {
            if (header != null)
                ConsoleWriteWithColor(header, textColor);

            ConsoleWriteWithColor(value, accent);

            if (footer != null)
                ConsoleWriteWithColor(footer, textColor);

            Console.WriteLine();
        }

        private void ConsoleWriteWithColor(object text, ConsoleColor col)
        {
            Console.ForegroundColor = col;
            Console.Write(text);
            Console.ResetColor();
        }

        private void ConsoleWriteLineWithColor(object text, ConsoleColor col)
        {
            ConsoleWriteWithColor(text, col);
            Console.WriteLine();
        }
    }
}
