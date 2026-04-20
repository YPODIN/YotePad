using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Yotepad
{
    public class RecoveryService : IDisposable
    {
        private static readonly string RecoveryFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YannPerodin", "YotePad", "Recovery"
    );

        private readonly string _recoveryFilePath;
        private readonly string _lockFilePath;
        private FileStream? _lockStream = null;
        private bool _hasWrittenRecovery = false;

        public RecoveryService()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            _recoveryFilePath = Path.Combine(RecoveryFolder, $"recovery_{timestamp}_{pid}.ypr");
            _lockFilePath = _recoveryFilePath + ".lock";

            Directory.CreateDirectory(RecoveryFolder);

            try
            {
                _lockStream = new FileStream(
                    _lockFilePath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
            }
            catch { }
        }

        public void WriteRecoveryFile(string content, string originalPath)
        {
            try
            {
                string header = $"YOTEPAD_RECOVERY|{originalPath}";
                string fullContent = header + "\n" + content;
                File.WriteAllText(_recoveryFilePath, fullContent);
                _hasWrittenRecovery = true;
            }
            catch { }
        }

        public async Task WriteRecoveryFileAsync(string content, string originalPath)
        {
            try
            {
                string header = $"YOTEPAD_RECOVERY|{originalPath}";
                string fullContent = header + "\n" + content;
                
                // This is the magic line. It writes the file without blocking the UI.
                await File.WriteAllTextAsync(_recoveryFilePath, fullContent);
                
                _hasWrittenRecovery = true;
            }
            catch { }
        }

        public void DeleteRecoveryFile()
        {
            try
            {
                _lockStream?.Close();
                _lockStream?.Dispose();
                _lockStream = null;

                if (File.Exists(_lockFilePath))
                    File.Delete(_lockFilePath);

                if (_hasWrittenRecovery && File.Exists(_recoveryFilePath))
                    File.Delete(_recoveryFilePath);
            }
            catch { }
        }

        public void Dispose()
        {
            _lockStream?.Close();
            _lockStream?.Dispose();
            _lockStream = null;
        }

        public static RecoveryFile[] ScanForRecoveryFiles()
        {
            try
            {
                if (!Directory.Exists(RecoveryFolder)) return Array.Empty<RecoveryFile>();

                foreach (string orphan in Directory.GetFiles(RecoveryFolder, "*.ypr.restoring"))
                {
                    try { File.Delete(orphan); } catch { }
                }

                foreach (string lockFile in Directory.GetFiles(RecoveryFolder, "*.ypr.lock"))
                {
                    if (!IsFileLocked(lockFile))
                    {
                        try { File.Delete(lockFile); } catch { }
                    }
                }

                var files = Directory.GetFiles(RecoveryFolder, "*.ypr");
                var results = new List<RecoveryFile>();

                foreach (string file in files)
                {
                    try
                    {
                        string lockPath = file + ".lock";
                        if (File.Exists(lockPath) && IsFileLocked(lockPath)) continue;

                        string raw = File.ReadAllText(file);
                        int newline = raw.IndexOf('\n');
                        if (newline == -1) continue;

                        string header = raw.Substring(0, newline);
                        string content = raw.Substring(newline + 1);

                        if (!header.StartsWith("YOTEPAD_RECOVERY|")) continue;

                        string originalPath = header.Substring("YOTEPAD_RECOVERY|".Length);

                        results.Add(new RecoveryFile
                        {
                            RecoveryFilePath = file,
                            OriginalFilePath = originalPath,
                            Content = content,
                            Timestamp = File.GetLastWriteTime(file)
                        });
                    }
                    catch { }
                }

                return results.ToArray();
            }
            catch { return Array.Empty<RecoveryFile>(); }
        }

        public static void DeleteRecoveryFileAt(string path)
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + ".lock"); } catch { }
        }

        private static bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }
    }

    public class RecoveryFile
    {
        public string RecoveryFilePath { get; set; } = string.Empty;
        public string OriginalFilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        public string DisplayName => string.IsNullOrEmpty(OriginalFilePath)
            ? $"Untitled — {Timestamp:MMM d, h:mm tt}"
            : $"{Path.GetFileName(OriginalFilePath)} — {Timestamp:MMM d, h:mm tt}";
    }
}