using System;
using System.Drawing;
using System.IO;
using System.Threading; // Required for Mutex
using System.Windows.Forms;

namespace Yotepad
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // The "handshake" for the installer to find the running app
            using (Mutex mutex = new Mutex(false, "YotePadMutex"))
            {
                try { NativeMethods.SetPreferredAppMode(2); } catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // --- Your Argument Parsing Logic ---
                string filePath = "";
                string restorePath = "";
                string originalPath = "";
                Point? startPosition = null;
                bool skipRecovery = false;

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--pos" && i + 1 < args.Length)
                    {
                        string[] parts = args[i + 1].Split(',');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                            startPosition = new Point(x, y);
                        i++;
                    }
                    else if (args[i] == "--restore" && i + 1 < args.Length)
                    {
                        restorePath = args[i + 1].Trim('"');
                        i++;
                    }
                    else if (args[i] == "--original" && i + 1 < args.Length)
                    {
                        originalPath = args[i + 1].Trim('"');
                        i++;
                    }
                    else if (args[i] == "--no-recovery")
                    {
                        skipRecovery = true;
                    }
                    else if (!args[i].StartsWith("--"))
                    {
                        filePath = args[i].Trim('"');
                    }
                }

                if (!string.IsNullOrEmpty(restorePath) && File.Exists(restorePath))
                {
                    var form = new MainWindow(restorePath, startPosition, skipRecovery);
                    form.SetRestoredFilePath(originalPath);
                    try { File.Delete(restorePath); } catch { }
                    Application.Run(form);
                }
                else
                {
                    // Update this to your correct form name (MainWindow or Form1)
                    Application.Run(new MainWindow(filePath, startPosition, skipRecovery));
                }
            }
        }
    }
}