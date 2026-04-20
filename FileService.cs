using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Yotepad
{
    public enum LineEndingType
    {
        CRLF,
        LF,
        CR
    }

    public class FileService
    {
        public string CurrentFilePath { get; private set; } = string.Empty;
        public Encoding CurrentEncoding { get; set; } = new UTF8Encoding(false); 
        public LineEndingType CurrentLineEnding { get; set; } = LineEndingType.CRLF;
        
        private const string FileFilter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*";

        public FileService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public void SetFilePath(string path) => CurrentFilePath = path;

        public string NewFile()
        {
            CurrentFilePath = string.Empty;
            CurrentEncoding = new UTF8Encoding(false); 
            CurrentLineEnding = LineEndingType.CRLF;
            return string.Empty;
        }

        public string? OpenFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = FileFilter, FilterIndex = 1 })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        return LoadFile(ofd.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening file: {ex.Message}", "YotePad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }
                }
            }
            return null;
        }

        public string LoadFile(string path)
        {
            byte[] rawBytes = File.ReadAllBytes(path);
            CurrentEncoding = DetectEncoding(rawBytes);
            string content = CurrentEncoding.GetString(rawBytes);
            CurrentFilePath = path;
            return ProcessIncomingText(content);
        }

        public bool SaveFile(string content)
        {
            if (string.IsNullOrEmpty(CurrentFilePath)) return SaveFileAs(content);
            try
            {
                File.WriteAllText(CurrentFilePath, FormatForSaving(content), CurrentEncoding);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "YotePad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public bool SaveFileAs(string content)
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = FileFilter, FilterIndex = 1 })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(sfd.FileName, FormatForSaving(content), CurrentEncoding);
                        CurrentFilePath = sfd.FileName;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving file: {ex.Message}", "YotePad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            return false;
        }

        public string GetFileName() => string.IsNullOrEmpty(CurrentFilePath) 
            ? "Untitled" 
            : Path.GetFileName(CurrentFilePath);

        public string ProcessIncomingText(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Detect the predominant line ending in the file
            if (input.Contains("\r\n")) CurrentLineEnding = LineEndingType.CRLF;
            else if (input.Contains("\n")) CurrentLineEnding = LineEndingType.LF;
            else if (input.Contains("\r")) CurrentLineEnding = LineEndingType.CR;
            else CurrentLineEnding = LineEndingType.CRLF;

            // Normalize everything to standard Windows format for the text box UI
            string normalized = input.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
            
            if (normalized.StartsWith(".LOG"))
            {
                string timestamp = Environment.NewLine + DateTime.Now.ToString("h:mm tt M/d/yyyy");
                normalized += timestamp;
            }
            return normalized;
        }

        private string FormatForSaving(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;

            // Strip the text down to pure UNIX, then map it to the user's chosen format
            string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
            return CurrentLineEnding switch
            {
                LineEndingType.LF => normalized,
                LineEndingType.CR => normalized.Replace("\n", "\r"),
                _ => normalized.Replace("\n", "\r\n") // CRLF
            };
        }

        private Encoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(true);
                
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode; 
                
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode; 

            try
            {
                var strictUtf8 = new UTF8Encoding(false, true); 
                strictUtf8.GetString(bytes);
                return new UTF8Encoding(false); 
            }
            catch (ArgumentException)
            {
                return Encoding.GetEncoding(1252);
            }
        }
    }
}