using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using DotNetCoreBackup.ApplicationSettings;
using Microsoft.Extensions.Configuration;
using NLog;

namespace DotNetCoreBackup
{
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static long _totalBytesOfAllFiles;
        private static long _currentByteCounter;

        private static void Main(string[] args)
        {
            Logger.Info("Backup started.");

            var startTime = DateTime.Now;

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            var config = configuration.Get<Config>();

            _totalBytesOfAllFiles = GetSizeOfFiles(config.BackupParameterCollection);

            Console.WriteLine($"Backup {_totalBytesOfAllFiles} bytes of data ...");

            foreach (var backupInfo in config.BackupParameterCollection)
            {
                ConsoleFullWriteLine($"Copy from {backupInfo.Source} to zip file in {backupInfo.TempFolder}");
                Logger.Info($"Copy from {backupInfo.Source} to zip file in {backupInfo.TempFolder}");
                CompressFilesFromSourceToDestination(backupInfo.Source, backupInfo.TempFolder, startTime);

                Thread.Sleep(2000);

                ConsoleFullWriteLine($"Backup from {backupInfo.TempFolder} to {backupInfo.Destination}");
                Logger.Info($"Backup from {backupInfo.TempFolder} to {backupInfo.Destination}");
                CopyFilesFromSourceToDestination(backupInfo.TempFolder, backupInfo.Destination, startTime);

                Thread.Sleep(2000);

                ConsoleFullWriteLine($"Cleaning the Temp folder {backupInfo.TempFolder} ...");
                Logger.Info($"Cleaning the Temp folder {backupInfo.TempFolder} ...");
                Directory.Delete(backupInfo.TempFolder, true);
            }

            ConsoleFullWriteLine($"Backup finished in {(DateTime.Now - startTime).TotalSeconds / 60:#,###0.00} minute(s) ");
            Logger.Info($"Backup finished in {(DateTime.Now - startTime).TotalSeconds / 60:#,###0.00} minute(s) ");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Closing...");
        }

        private static void ConsoleFullWriteLine(string value)
        {
            Console.WriteLine(value.PadRight(Console.BufferWidth));
        }

        private static long GetSizeOfFiles(BackupParameter[] configBackupParameterCollection)
        {
            return configBackupParameterCollection.Sum(backupInfo =>
                GetSizeOfSubFolderFiles(new DirectoryInfo(backupInfo.Source)));
        }

        private static long GetSizeOfSubFolderFiles(DirectoryInfo parentDirectoryInfo)
        {
            return parentDirectoryInfo.GetFiles().Sum(fileInfo => fileInfo.Length) +
                   parentDirectoryInfo.GetDirectories().Sum(GetSizeOfSubFolderFiles);
        }

        private static void AppendToArchiveFile(string rootFolder, string sourceFile, ZipArchive archive,
            DateTime startTime)
        {
            const int bufferSize = 1024;

            var entryName =
                sourceFile.Substring(rootFolder.Length + 1, sourceFile.Length - rootFolder.Length - 1);

            var archiveEntry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);


            using (var reader = new BinaryReader(File.Open(sourceFile, FileMode.Open)))
            using (var writer = new BinaryWriter(archiveEntry.Open()))
            {
                while (true)
                {
                    var buf = new byte[bufferSize];
                    var sz = reader.Read(buf, 0, bufferSize);
                    if (sz <= 0)
                        break;
                    writer.Write(buf, 0, sz);
                    writer.Flush();
                    if (sz < bufferSize)
                        break; // eof reached
                }
            }


            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = ConsoleColor.Yellow;

            var currentSecondCounter = (DateTime.Now - startTime).TotalSeconds;
            ConsoleFullWriteLine($"Compression Progress {_currentByteCounter * 100 / _totalBytesOfAllFiles:f}%");
            ConsoleFullWriteLine($"{_currentByteCounter / 1000:#,###0} KB of {_totalBytesOfAllFiles / 1000:#,###0} KB");
            ConsoleFullWriteLine($"Elapsed time: {FormatTime(currentSecondCounter)}.");
            ConsoleFullWriteLine($"Remaining time: {FormatTime(currentSecondCounter / _currentByteCounter * (_totalBytesOfAllFiles - _currentByteCounter))}.");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.SetCursorPosition(0, 10);
            ConsoleFullWriteLine($"Zip added {sourceFile}.");
            ConsoleFullWriteLine("");
            ConsoleFullWriteLine("");
        }

        private static string FormatTime(double seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\.fff");
        }

        private static void CompressFilesFromSourceToDestination(string sourceFolder, string destinationFolder,
            DateTime startDateTime)
        {
            var archiveFileName =
                Path.Combine(destinationFolder, startDateTime.ToString("yyyyMMdd_HHmmss") + ".zip");

            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            if (!File.Exists(archiveFileName))
                using (new StreamWriter(archiveFileName, true))
                {
                } //Create the Empty file.

            using (var zipStream = new FileStream(archiveFileName, FileMode.Open))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Update))
            {
                ZipFileFromDirectory(sourceFolder, sourceFolder, archive, startDateTime);
            }
        }

        private static void ZipFileFromDirectory(string rootFolder, string sourceFolder,
            ZipArchive archive, DateTime startTime)
        {
            var start = DateTime.Now;

            foreach (var sourceFile in Directory.GetFiles(sourceFolder))
                try
                {
                    _currentByteCounter += new FileInfo(sourceFile).Length;
                    AppendToArchiveFile(rootFolder, sourceFile, archive, startTime);
                }
                catch (UnauthorizedAccessException accessException)
                {
                    Logger.Error(accessException);
                }
                catch (IOException ioException)
                {
                    if (ioException.Message.EndsWith("used by another process."))
                        Logger.Error(ioException);
                    else
                        throw;
                }


            foreach (var directory in Directory.GetDirectories(sourceFolder))
                ZipFileFromDirectory(rootFolder, directory, archive, startTime);

            Logger.Trace($"Zip finished for {sourceFolder} in {(DateTime.Now - start).TotalSeconds:F} second(s).");
        }

        private static void CopyFilesFromSourceToDestination(string sourceFolder, string destinationFolder,
            DateTime startDateTime)
        {
            var name = startDateTime.ToString("yyyyMMdd_HHmmss");
            var destinationBackupFolder = Path.Combine(destinationFolder, name);
            Copy(sourceFolder, destinationBackupFolder);
        }

        private static void Copy(string sourceDir, string targetDir)
        {
            var start = DateTime.Now;
            Logger.Trace($"Copy started for {sourceDir}");
            try
            {
                Directory.CreateDirectory(targetDir);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
                try
                {
                    File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

            foreach (var directory in Directory.GetDirectories(sourceDir))
                Copy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));

            Logger.Trace($"Copy finished for {targetDir} in {(DateTime.Now - start).TotalSeconds} second(s).");
        }
    }
}