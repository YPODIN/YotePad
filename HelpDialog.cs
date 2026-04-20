using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Yotepad
{
    public class HelpDialog : Form
    {
        // Import the native Windows APIs to force dark scrollbars
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Add the '?' to the final string parameter here:
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string? pszSubIdList);

        public HelpDialog(Color backColor, Color foreColor)
        {
            this.Text = "YotePad Help";
            this.Size = new Size(550, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            var txtHelp = new YoteTextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10F),
                Text = GetHelpText(),
                BorderStyle = BorderStyle.None,
                Margin = new Padding(10),
                TabStop = false // Prevent auto-selection on load
            };

            this.Controls.Add(txtHelp);

            this.BackColor = backColor;
            this.ForeColor = foreColor;
            txtHelp.BackColor = backColor;
            txtHelp.ForeColor = foreColor;

            this.Shown += (s, e) => 
            {
                txtHelp.SelectionStart = 0;
                txtHelp.SelectionLength = 0;
            };

            // Hook into the Windows OS to force dark mode elements if the background is dark
            if (backColor.R < 100) 
            {
                // Force the Window title bar to use dark mode (Windows 11)
                int useImmersiveDarkMode = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref useImmersiveDarkMode, sizeof(int));
                
                // Force the internal scrollbar to use the dark explorer theme
                SetWindowTheme(txtHelp.Handle, "DarkMode_Explorer", null);
            }
        }

        private string GetHelpText()
        {
            return "Welcome to YotePad\r\n\r\n" +
                "YotePad is a lightweight, responsive text editor built for Windows. While the initial cold launch may take a moment to initialize the .NET environment, subsequent launches are optimized for speed and reliability, providing a high-performance replacement for the modern Windows 11 Notepad.\r\n\r\n" +
                "The Anti-Bloat Philosophy\r\n\r\n" +
                "YotePad was built specifically to strip away the distractions of the modern Windows 11 editor. There is no AI-driven text generation, no tabbed-interface clutter, no telemetry, and no OneDrive integration. It is a clean, single-instance-first tool designed to do one thing: handle text without the 'crap.'\r\n\r\n" +
                "Core Utilities\r\n\r\n" +
                "• Surgical Auto Recovery: YotePad protects your work without cluttering your drive. It maintains a single, active recovery snapshot in the background. Unlike traditional autosave that fills folders with backup copies, YotePad’s recovery engine is self-cleaning: it automatically deletes its temporary footprints the moment you save your file or close the app normally. It only leaves a trace if an actual crash occurs.\r\n\r\n" +
                "• Smart Encoding: A custom byte-level sniffer automatically detects UTF-8, UTF-16, and legacy ANSI files. You can also manually switch encodings at any time by clicking the encoding label in the status bar.\r\n\r\n" +
                "• Line Ending Control: Seamlessly view and switch between Windows (CRLF), Unix (LF), and Macintosh (CR) formats by clicking directly on the line-ending label in the status bar. This ensures instant compatibility when moving files between different operating systems.\r\n\r\n" +
                "• OS Theme Sync: YotePad adapts to your workflow environment by instantly syncing with Windows Light and Dark mode preferences without requiring a restart.\r\n\r\n" +
                "Advanced Behavior\r\n\r\n" +
                "• The .LOG Feature: Emulating a classic behavior, any file starting with .LOG on the very first line will automatically append a new timestamp to the end of the document every time it is opened. This makes YotePad an ideal tool for engineering logs, journals, or time-tracking.\r\n\r\n" +
                "• Visual Overwrite: Pressing the Insert key toggles between a standard thin caret and a chunky \"retro\" block cursor. This provides an immediate visual cue when you are in Overtype mode to prevent accidental text deletion.\r\n\r\n" +
                "• Filtered Search: The Find and Replace tool includes a \"Match Whole Word\" filter. This is essential for safely refactoring code or technical notes, ensuring that changing a short word doesn't accidentally mangle a longer string containing those same characters.\r\n\r\n" +
                "• Printing Support: Provides standard printing capabilities, including Page Setup and a dedicated internal Print Preview window for verifying your document layout before sending it to a physical or PDF printer.";
        }
    }
}


