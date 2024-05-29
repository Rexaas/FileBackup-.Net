using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace FileBackup
{
    public partial class Service1 : ServiceBase
    {
        private FileSystemWatcher fileWatcher;
        private string sourceFilePath = @"C:\Program Files (x86)\NCR APTRA\Advance NDC\Data\EJDATA.log";
        private string backupFolderPath = @"E:\EjBackups";
        private string backupFolderPath2 = @"D:\EjBackups";
        private string lastBackupFilePath2;
        private string lastBackupFilePath;
        private DateTime currentDay;
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            InitializeFileSystemWatcher();
        }

        protected override void OnStop()
        {
            fileWatcher.EnableRaisingEvents = false;
            fileWatcher.Dispose();
        }

        private void InitializeFileSystemWatcher()
        {
            fileWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(sourceFilePath),
                Filter = Path.GetFileName(sourceFilePath),
                EnableRaisingEvents = true
            };

            fileWatcher.Changed += OnLogFileChanged;

            lastBackupFilePath = GetLastBackupFilePath(backupFolderPath);
            lastBackupFilePath2 = GetLastBackupFilePath(backupFolderPath2);
            currentDay = DateTime.Now.Date;

            string tmpbackupFileName = $"Ej_{DateTime.Now:ddMMyyyy}.log";
            lastBackupFilePath = Path.Combine(backupFolderPath, tmpbackupFileName);
            string tmpbackupFileName2 = $"Ej_{DateTime.Now:ddMMyyyy}.log";
            lastBackupFilePath2 = Path.Combine(backupFolderPath2, tmpbackupFileName2);
            if (!File.Exists(lastBackupFilePath))
            {
                File.Copy(sourceFilePath, lastBackupFilePath);
                File.Copy(sourceFilePath, lastBackupFilePath2);
            }
        }
        private string GetLastBackupFilePath(string backupFolderPath)
        {
            if (!Directory.Exists(backupFolderPath))
            {
                Console.WriteLine($"Backup directory does not exist. Creating: {backupFolderPath}");
                Directory.CreateDirectory(backupFolderPath);
            }

            string[] backupFiles = Directory.GetFiles(backupFolderPath, "Ej_*.log");
            Array.Sort(backupFiles, StringComparer.InvariantCulture);
            Array.Reverse(backupFiles);

            return backupFiles.Length > 0 ? backupFiles[0] : null;
        }

        private void OnLogFileChanged(object sender, FileSystemEventArgs e)
        {

            DateTime today = DateTime.Now.Date;
            if (today > currentDay)
            {
                Console.WriteLine($"New day detected. Backing up and clearing {sourceFilePath}");

                // Create a backup before deletion
                BackupAndRenameLogFile(sourceFilePath);

                currentDay = today; // Update currentDay
            }
            try
            {
                string logFilePath = e.FullPath;
                string logContent = File.ReadAllText(logFilePath);

                // Check if the backup directory exists; if not, create it
                if (!Directory.Exists(backupFolderPath))
                {
                    Console.WriteLine($"Backup directory does not exist. Creating: {backupFolderPath}");
                    Directory.CreateDirectory(backupFolderPath);
                }
                if (!Directory.Exists(backupFolderPath2))
                {
                    Console.WriteLine($"Backup directory does not exist. Creating: {backupFolderPath2}");
                    Directory.CreateDirectory(backupFolderPath2);
                }

                // Create a new daily backup file with the desired name format
                string backupFileName = $"Ej_{DateTime.Now:ddMMyyyy}.log";
                lastBackupFilePath = Path.Combine(backupFolderPath, backupFileName);
                string backupFileName2 = $"Ej_{DateTime.Now:ddMMyyyy}.log";
                lastBackupFilePath2 = Path.Combine(backupFolderPath2, backupFileName2);

                // Log the start of a new daily backup
                Console.WriteLine($"Start of daily backup: {lastBackupFilePath}");

                // Perform the backup by copying or appending the log content to the backup file
                File.AppendAllText(lastBackupFilePath, logContent);
                File.AppendAllText(lastBackupFilePath2, logContent);
                

                // Read changes using memory-mapped files
                ReadFileChanges(lastBackupFilePath);
                ReadFileChanges(lastBackupFilePath2);

                Console.WriteLine($"Changes backed up to daily backup: {lastBackupFilePath}");
            }
            catch (Exception ex)
            {
                // Handle exceptions, e.g., log or notify about the error
                Console.WriteLine($"Error during backup: {ex.Message}");
            }
        }
        private void ReadFileChanges(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.Read, null, HandleInheritability.None, false))
            using (var memoryMappedViewStream = memoryMappedFile.CreateViewStream())
            using (var reader = new StreamReader(memoryMappedViewStream, Encoding.UTF8))
            {
                // Read changes from the memory-mapped file
                string changes = reader.ReadToEnd();

                // Handle the changes as needed
                Console.WriteLine($"Changes in file: {changes}");

                // Perform backup logic here if necessary
            }
        }

        private void BackupAndRenameLogFile(string logFilePath)
        {
            const int maxRetries = 3;
            int retries = 0;

            while (retries < maxRetries)
            {
                try
                {
                    // Read the content of the log file
                    string logContent = File.ReadAllText(logFilePath);

                    // Read existing content of the backup file
                    string existingBackupContent = File.Exists(lastBackupFilePath) ? File.ReadAllText(lastBackupFilePath) : "";

                    // Concatenate existing content with the log content
                    string updatedBackupContent = existingBackupContent + logContent;

                    // Write the updated content back to the backup file
                    File.WriteAllText(lastBackupFilePath, updatedBackupContent);

                    // Read existing content of the second backup file
                    string existingBackupContent2 = File.Exists(lastBackupFilePath2) ? File.ReadAllText(lastBackupFilePath2) : "";

                    // Concatenate existing content with the log content
                    string updatedBackupContent2 = existingBackupContent2 + logContent;

                    // Write the updated content back to the second backup file
                    File.WriteAllText(lastBackupFilePath2, updatedBackupContent2);

                    // Rename the log file to preserve it
                    string tempLogFilePath = Path.Combine(Path.GetDirectoryName(logFilePath), "EJTemp.log");
                    Console.WriteLine($"Renaming {logFilePath} to {tempLogFilePath}");
                    File.Move(logFilePath, tempLogFilePath);
                    string content = $"Ej Cleared - {DateTime.Now.Date}";
                    File.WriteAllText(logFilePath, content);

                    // Operation succeeded, break out of the loop
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during backup and renaming: {ex.Message}");
                    retries++;

                    // Wait for a short duration before retrying (adjust as needed)
                    Thread.Sleep(1000);
                }
            }

            if (retries == maxRetries)
            {
                Console.WriteLine($"Failed to backup and rename log file after {maxRetries} retries.");
            }
        }

    }
}
