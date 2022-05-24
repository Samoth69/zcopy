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
        private string? screenScanningFile;
        public string? ScreenScanningFile
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
        private string? screenCopyFile;

        public string? ScreenCopyFile
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

        /// <summary>
        /// Print copy status, do not print if verbose is on
        /// </summary>
        /// <param name="srcFile"></param>
        /// <param name="destFile"></param>
        /// <param name="progress"></param>
        private void UpdateScreen()
        {
            Console.SetCursorPosition(0, 0);

            string? displayCopyName = ScreenCopyFile.Truncate(Console.WindowWidth - 16);
            string? displayScanName = ScreenScanningFile.Truncate(Console.WindowWidth - 16);

            Console.Clear();

            ConsoleWriteLineWithColor($"scanning: {displayScanName}", ConsoleColor.Gray);
            ConsoleWriteLineWithColor($"copying: {displayCopyName}", ConsoleColor.Gray);
            if (ScreenPercent != null)
            {
                ConsoleWriteNumberWithSpColor("  Progress: ", ScreenPercent, " %", ConsoleColor.White, ConsoleColor.Gray);
            }
            else
            {
                ConsoleWriteWithColor("  Progress:", ConsoleColor.Gray);
                ConsoleWriteLineWithColor(" Done", ConsoleColor.White);
            }

            Console.WriteLine();
            ConsoleWriteNumberWithSpColor("Running: ", ExecTime.Elapsed.ToString("d\\.hh\\:mm\\:ss"), null, ConsoleColor.White, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Copied: ", Copied, null, ConsoleColor.Green, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Skipped: ", Skipped, null, ConsoleColor.DarkGreen, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Scanner Error: ", ScannerError, null, ConsoleColor.DarkRed, ConsoleColor.Gray);
            ConsoleWriteNumberWithSpColor("Copier Error: ", CopierError, null, ConsoleColor.DarkRed, ConsoleColor.Gray);
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
