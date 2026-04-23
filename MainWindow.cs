using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Yotepad
{
    public partial class MainWindow : Form
    {
        private readonly SearchService _searchService = new SearchService();
        private FindReplaceDialog? _searchDialog;
        private readonly YoteTextBox _mainTextBox = new YoteTextBox();
        private readonly MenuStrip _topMenu = new MenuStrip();
        private readonly StatusStrip _statusBar = new StatusStrip();
        private readonly ToolStripStatusLabel _lblLocation = new ToolStripStatusLabel();
        
        private readonly ToolStripMenuItem _wordWrapMenuItem = new ToolStripMenuItem("Word Wrap");
        private readonly ToolStripMenuItem _statusBarMenuItem = new ToolStripMenuItem("Status Bar");
        private ToolStripMenuItem _goToLineMenuItem = new ToolStripMenuItem("Go To Line...");
        
        private readonly FileService _fileService = new FileService();
        private readonly RecoveryService _recoveryService = new RecoveryService();
        private readonly ThemeManager _themeManager = new ThemeManager();
        private readonly PrintService _printService = new PrintService();
        
        private bool _isModified = false;
        private IntPtr _iconHandle = IntPtr.Zero;

        private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            // 'General' covers OS theme changes in the registry
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
            {
                // Safely marshal the update back to the main UI thread
                this.Invoke(new Action(() =>
                {
                    // Force the ThemeManager to re-read the Windows registry
                    _themeManager.InitializeTheme();
                    
                    // Repaint the entire window with the new colors
                    RefreshTheme();
                }));
            }
        }

        // Zoom state
        private int _zoomPercent = 100;
        private readonly float _baseFontSize = 11F;
        private ToolStripStatusLabel _lblZoom = new ToolStripStatusLabel("100%");
        private readonly ToolStripDropDownButton _btnEncoding = new ToolStripDropDownButton();
        private readonly ToolStripDropDownButton _btnLineEnding = new ToolStripDropDownButton();
        
        // Recovery timer — resets every time the user types
        private readonly System.Windows.Forms.Timer _recoveryTimer = new System.Windows.Forms.Timer();
        private string _lastRecoveryContent = string.Empty;

        public MainWindow(string filePath = "", Point? startPosition = null, bool skipRecovery = false)
        {
            InitializeComponent();
            InitializeComponents();

            if (startPosition.HasValue)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(startPosition.Value.X + 24, startPosition.Value.Y + 24);
            }

            _themeManager.InitializeTheme();
            RefreshTheme();

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                LoadInitialFile(filePath);
            else
                UpdateUIState();

            // Skip recovery scan if this instance was launched from a recovery action
            if (!skipRecovery)
                CheckForRecoveryFiles();
        }

        private void InitializeComponents()
        {
            this.Size = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosed += (s, e) => 
            { 
                // Unhook the system event to prevent memory leaks
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
                
                _recoveryService.DeleteRecoveryFile();
                if (_iconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(_iconHandle); 
            };

            // Start listening for Windows OS personalization changes
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            SetupStatusBar();
            SetupEditor();
            SetupMenu();
            SetupRecoveryTimer();
            SetApplicationIcon();
        }

        private void SetupRecoveryTimer()
        {
            _recoveryTimer.Interval = 30000; // 30 seconds after last keystroke
            
            // Notice the 'async' keyword we added here
            _recoveryTimer.Tick += async (s, e) => 
            {
                _recoveryTimer.Stop();

                // Only write if content actually changed since last recovery write
                string current = _mainTextBox.Text;
                if (current == _lastRecoveryContent) return;

                // Don't autosave huge files
                if (current.Length > 10_000_000) return;

                // We added 'await' and called the new Async method!
                await _recoveryService.WriteRecoveryFileAsync(current, _fileService.CurrentFilePath); 
                
                _lastRecoveryContent = current;
            };
        }

        private void CheckForRecoveryFiles()
        {
            var files = RecoveryService.ScanForRecoveryFiles();
            if (files.Length == 0) return;

            // Show recovery dialog — slight delay so main window is fully visible first
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                ShowRecoveryDialog(files);
            };
            timer.Start();
        }

        private void ShowRecoveryDialog(RecoveryFile[] files)
        {
            using (var dialog = new RecoveryDialog(files, _themeManager))
            {
                var result = dialog.ShowDialog(this);

                if (result == DialogResult.OK && dialog.FilesToRestore.Count > 0)
                {
                    int staggerIndex = 0;
                    foreach (var file in dialog.FilesToRestore)
                    {
                        RecoveryLauncher.Launch(file, this.Location, staggerIndex);
                        staggerIndex++;
                    }
                }
            }
        }

        private void SetupEditor()
        {
            _mainTextBox.Multiline = true;
            _mainTextBox.Dock = DockStyle.Fill;
            _mainTextBox.Font = new Font("Consolas", 11F);
            _mainTextBox.ScrollBars = ScrollBars.Both;
            _mainTextBox.AcceptsTab = true;
            _mainTextBox.BorderStyle = BorderStyle.None;
            _mainTextBox.HideSelection = false;

            _mainTextBox.TextChanged += (s, e) => 
            { 
                _isModified = true;
                // Reset the recovery timer on every keystroke
                _recoveryTimer.Stop();
                _recoveryTimer.Start();
                UpdateUIState(); 
            };
            _mainTextBox.Click += (s, e) => UpdateUIState();
            _mainTextBox.KeyUp += (s, e) => UpdateUIState();
            _mainTextBox.MouseUp += (s, e) => UpdateUIState();
            _mainTextBox.MouseWheel += (s, e) =>
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    if (e.Delta > 0) ZoomIn();
                    else ZoomOut();
                    ((HandledMouseEventArgs)e).Handled = true;
                }
            };

            this.Controls.Add(_mainTextBox);
            _mainTextBox.BringToFront();
        }

        private void SetupStatusBar()
        {
            _statusBar.SizingGrip = false;
            _statusBar.Items.Add(new ToolStripStatusLabel { Spring = true });
            _statusBar.Items.Add(_lblLocation);
            _statusBar.Items.Add(_lblZoom);

            // Line Ending Button Setup
            _btnLineEnding.ShowDropDownArrow = false; 
            _btnLineEnding.DropDownItems.Add("Windows (CRLF)", null, (s, e) => { _fileService.CurrentLineEnding = LineEndingType.CRLF; UpdateUIState(); });
            _btnLineEnding.DropDownItems.Add("Unix (LF)", null, (s, e) => { _fileService.CurrentLineEnding = LineEndingType.LF; UpdateUIState(); });
            _btnLineEnding.DropDownItems.Add("Macintosh (CR)", null, (s, e) => { _fileService.CurrentLineEnding = LineEndingType.CR; UpdateUIState(); });

            // Encoding Button Setup
            _btnEncoding.ShowDropDownArrow = false;
            var encodings = new (string Name, System.Text.Encoding Enc)[]
            {
                ("ANSI", System.Text.Encoding.GetEncoding(1252)),
                ("UTF-8", new System.Text.UTF8Encoding(false)),
                ("UTF-8 with BOM", new System.Text.UTF8Encoding(true)),
                ("UTF-16 LE", System.Text.Encoding.Unicode),
                ("UTF-16 BE", System.Text.Encoding.BigEndianUnicode)
            };

            foreach (var enc in encodings)
            {
                _btnEncoding.DropDownItems.Add(enc.Name, null, (s, e) => 
                {
                    _fileService.CurrentEncoding = enc.Enc;
                    UpdateUIState();
                });
            }

            _statusBar.Items.Add(_btnLineEnding);
            _statusBar.Items.Add(_btnEncoding);
            this.Controls.Add(_statusBar);
        }
    
        private void SetupMenu()
        {
            // --- ENCODING MENU (Created first so we can add it to File) ---
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var encodingMenu = new ToolStripMenuItem("Encoding");

            var encodings = new (string Name, System.Text.Encoding Enc)[]
            {
                ("ANSI", System.Text.Encoding.GetEncoding(1252)),
                ("UTF-8", new System.Text.UTF8Encoding(false)),
                ("UTF-8 with BOM", new System.Text.UTF8Encoding(true)),
                ("UTF-16 LE", System.Text.Encoding.Unicode),
                ("UTF-16 BE", System.Text.Encoding.BigEndianUnicode)
            };

            foreach (var enc in encodings)
            {
                var item = new ToolStripMenuItem(enc.Name);
                item.Click += (s, e) => 
                {
                    _fileService.CurrentEncoding = enc.Enc;
                    UpdateUIState();
                    
                    foreach (ToolStripMenuItem dropItem in encodingMenu.DropDownItems)
                        dropItem.Checked = false;
                        
                    item.Checked = true;
                };
                encodingMenu.DropDownItems.Add(item);
            }

            // --- FILE MENU ---
            var fileMenu = new ToolStripMenuItem("&File");

            fileMenu.DropDownItems.Add(new ToolStripMenuItem("New", null, (s, e) => 
            { 
                string pos = $"{this.Location.X},{this.Location.Y}";
                System.Diagnostics.Process.Start(Application.ExecutablePath, $"--pos {pos}");
            }) { ShortcutKeys = Keys.Control | Keys.N });

            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Open...", null, (s, e) => 
            { 
                if (ConfirmSaveIfModified()) 
                { 
                    var c = _fileService.OpenFile(); 
                    if (c != null) 
                    { 
                        _mainTextBox.Text = c; 
                        _mainTextBox.SelectionStart = 0;
                        _isModified = false;
                        _recoveryService.DeleteRecoveryFile();

                        if (c.StartsWith(".LOG")) 
                        {
                            _mainTextBox.SelectionStart = _mainTextBox.Text.Length;
                            _mainTextBox.ScrollToCaret();
                            _isModified = true;
                        }
                        UpdateUIState(); 
                    } 
                } 
            }) { ShortcutKeys = Keys.Control | Keys.O });

            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save", null, (s, e) => 
            { 
                if (_fileService.SaveFile(_mainTextBox.Text)) 
                { 
                    _isModified = false;
                    _recoveryService.DeleteRecoveryFile();
                    _lastRecoveryContent = _mainTextBox.Text;
                    UpdateUIState(); 
                } 
            }) { ShortcutKeys = Keys.Control | Keys.S });

            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save As...", null, (s, e) => 
            { 
                if (_fileService.SaveFileAs(_mainTextBox.Text)) 
                { 
                    _isModified = false;
                    _recoveryService.DeleteRecoveryFile();
                    _lastRecoveryContent = _mainTextBox.Text;
                    UpdateUIState(); 
                } 
            }) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.S });

            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(encodingMenu); // <-- Tucked neatly into the File Menu!
            fileMenu.DropDownItems.Add(new ToolStripSeparator());

            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Page Setup...", null, (s, e) => 
            {
                _printService.ShowPageSetup();
            }));

            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Print Preview...", null, (s, e) => 
            {
                _printService.ShowPrintPreview(_mainTextBox.Text, _mainTextBox.Font);
            }));

            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Print...", null, (s, e) => 
            {
                _printService.Print(_mainTextBox.Text, _mainTextBox.Font);
            }) { ShortcutKeys = Keys.Control | Keys.P });
            
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Exit", null, (s, e) => this.Close()) 
            { 
                ShortcutKeys = Keys.Alt | Keys.F4 
            });

            // --- EDIT MENU ---
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Undo", null, (s, e) => _mainTextBox.Undo()) { ShortcutKeys = Keys.Control | Keys.Z });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Cut", null, (s, e) => _mainTextBox.Cut()) { ShortcutKeys = Keys.Control | Keys.X });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Copy", null, (s, e) => _mainTextBox.Copy()) { ShortcutKeys = Keys.Control | Keys.C });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Paste", null, (s, e) => _mainTextBox.Paste()) { ShortcutKeys = Keys.Control | Keys.V });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Delete", null, (s, e) => 
            {
                if (_mainTextBox.SelectionLength > 0)
                {
                    _mainTextBox.SelectedText = "";
                }
                else if (_mainTextBox.SelectionStart < _mainTextBox.TextLength)
                {
                    _mainTextBox.SelectionLength = 1;
                    _mainTextBox.SelectedText = "";
                }
            }) { ShortcutKeyDisplayString = "Del" });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Find...", null, (s, e) => ShowSearchDialog(false)) { ShortcutKeys = Keys.Control | Keys.F });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Replace...", null, (s, e) => ShowSearchDialog(true)) { ShortcutKeys = Keys.Control | Keys.H });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Find Next", null, (s, e) => FindNextShortcut()) { ShortcutKeys = Keys.F3 });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Find Previous", null, (s, e) => FindPreviousShortcut()) { ShortcutKeys = Keys.Shift | Keys.F3 });
            
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            _goToLineMenuItem.ShortcutKeys = Keys.Control | Keys.G;
            _goToLineMenuItem.Click += (s, e) => ShowGoToLine();
            _goToLineMenuItem.Enabled = !_wordWrapMenuItem.Checked;
            editMenu.DropDownItems.Add(_goToLineMenuItem);
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Select All", null, (s, e) => _mainTextBox.SelectAll()) { ShortcutKeys = Keys.Control | Keys.A });
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Time/Date", null, (s, e) => _mainTextBox.SelectedText = DateTime.Now.ToString()) { ShortcutKeys = Keys.F5 });
            
            // --- FORMAT MENU ---
            var formatMenu = new ToolStripMenuItem("F&ormat");
            _wordWrapMenuItem.CheckOnClick = true;
            _wordWrapMenuItem.CheckedChanged += (s, e) => ToggleWordWrap();
            _wordWrapMenuItem.Checked = true;
            formatMenu.DropDownItems.Add(_wordWrapMenuItem);
            formatMenu.DropDownItems.Add(new ToolStripMenuItem("Font...", null, (s, e) => 
            {
                using (FontDialog fd = new FontDialog())
                {
                    fd.Font = _mainTextBox.Font;
                    fd.ShowColor = false;
                    if (fd.ShowDialog() == DialogResult.OK)
                    {
                        _mainTextBox.Font = fd.Font;
                    }
                }
            }));

            // --- VIEW MENU ---
            var viewMenu = new ToolStripMenuItem("&View");
            _statusBarMenuItem.CheckOnClick = true;
            _statusBarMenuItem.Checked = true;
            _statusBarMenuItem.CheckedChanged += (s, e) => _statusBar.Visible = _statusBarMenuItem.Checked;
            viewMenu.DropDownItems.Add(_statusBarMenuItem);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom In", null, (s, e) => ZoomIn()) { ShortcutKeyDisplayString = "Ctrl++" });
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom Out", null, (s, e) => ZoomOut()) { ShortcutKeyDisplayString = "Ctrl+-" });
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("Reset Zoom", null, (s, e) => ZoomReset()) { ShortcutKeyDisplayString = "Ctrl+0" });
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("Toggle Theme", null, (s, e) => { _themeManager.ToggleTheme(); RefreshTheme(); }));

            // --- HELP MENU ---
            var helpMenu = new ToolStripMenuItem("&Help");

            helpMenu.DropDownItems.Add(new ToolStripMenuItem("View Help", null, (s, e) => 
            {
                // Grab the colors directly from the text editor to ensure perfect contrast
                using (var helpDialog = new HelpDialog(_mainTextBox.BackColor, _mainTextBox.ForeColor))
                {
                    helpDialog.ShowDialog(this);
                }
            }));
            
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("About YotePad", null, (s, e) => 
                MessageBox.Show("YotePad\n\nBecause nobody likes Windows 11 Notepad\n\nNobody!\n\nCreated by Yann Perodin (2026)", "About", MessageBoxButtons.OK, MessageBoxIcon.Information)));

            _topMenu.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, formatMenu, viewMenu, helpMenu });
            this.Controls.Add(_topMenu);
        }

        private void ToggleWordWrap()
        {
            _mainTextBox.WordWrap = _wordWrapMenuItem.Checked;
            _goToLineMenuItem.Enabled = !_wordWrapMenuItem.Checked;
            RefreshTheme();
            
            if (_wordWrapMenuItem.Checked)
            {
                _statusBarMenuItem.Enabled = false;
                _statusBar.Visible = false;
            }
            else
            {
                _statusBarMenuItem.Enabled = true;
                _statusBar.Visible = _statusBarMenuItem.Checked;
            }
        }

        private void RefreshTheme() 
        {
            _themeManager.ApplyTheme(this, _mainTextBox, _topMenu, _statusBar);
            _searchDialog?.ApplyTheme(_themeManager); 

            // Force the status bar popups to inherit the themed colors
            if (_btnLineEnding.DropDown is ToolStripDropDownMenu leMenu)
            {
                leMenu.BackColor = _statusBar.BackColor;
                leMenu.ForeColor = _statusBar.ForeColor;
            }

            if (_btnEncoding.DropDown is ToolStripDropDownMenu encMenu)
            {
                encMenu.BackColor = _statusBar.BackColor;
                encMenu.ForeColor = _statusBar.ForeColor;
            }
        }

        private bool ConfirmSaveIfModified()
        {
            if (!_isModified) return true;
            var result = MessageBox.Show($"Do you want to save changes to {_fileService.GetFileName()}?", "YotePad", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Yes) return _fileService.SaveFile(_mainTextBox.Text);
            return result == DialogResult.No;
        }

        private void LoadInitialFile(string path)
        {
            try 
            { 
                // We now route this through the sniffer!
                string content = _fileService.LoadFile(path);
                
                _mainTextBox.Text = content; 
                _mainTextBox.SelectionStart = 0;
                _isModified = false;
                
                if (content.StartsWith(".LOG")) 
                {
                    _mainTextBox.SelectionStart = _mainTextBox.Text.Length;
                    _mainTextBox.ScrollToCaret();
                    _isModified = true;
                }
                UpdateUIState(); 
            } 
            catch { }
        }

        private void ShowSearchDialog(bool replaceMode)
        {
            if (_searchDialog == null || _searchDialog.IsDisposed)
            {
                _searchDialog = new FindReplaceDialog(_themeManager);
                _searchDialog.OnFindNext += (term, matchCase, matchWholeWord) => ExecuteFind(term, matchCase, matchWholeWord, true);
                _searchDialog.OnReplace += ExecuteReplace;
                _searchDialog.OnReplaceAll += ExecuteReplaceAll;
            }

            _searchDialog.SetMode(replaceMode);

            if (_mainTextBox.SelectionLength > 0 && !_mainTextBox.SelectedText.Contains("\n"))
            {
                _searchDialog.SetSearchTerm(_mainTextBox.SelectedText);
            }

            if (!_searchDialog.Visible)
            {
                int x = this.Location.X + (this.Width - _searchDialog.Width) / 2;
                int y = this.Location.Y + (int)(this.Height * 0.20);
                _searchDialog.Location = new Point(x, y);
                _searchDialog.Show(this);
            }
            else _searchDialog.Focus();
        }

        private void ExecuteFind(string term, bool matchCase, bool matchWholeWord, bool searchDown)
        {
            _searchService.UpdateSearchState(term, matchCase, matchWholeWord, searchDown);
            int startIndex = _mainTextBox.SelectionStart;
            if (searchDown) startIndex += _mainTextBox.SelectionLength;

            int foundIndex = _searchService.Find(_mainTextBox.Text, term, startIndex, matchCase, matchWholeWord, searchDown);

            if (foundIndex != -1)
            {
                _mainTextBox.Select(foundIndex, term.Length);
                _mainTextBox.ScrollToCaret();
            }
            else
            {
                MessageBox.Show($"Cannot find \"{term}\"", "YotePad", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExecuteReplace(string term, string replaceTerm, bool matchCase, bool matchWholeWord)
        {
            StringComparison comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (_mainTextBox.SelectionLength > 0 && _mainTextBox.SelectedText.Equals(term, comp))
            {
                _mainTextBox.SelectedText = replaceTerm;
            }
            ExecuteFind(term, matchCase, matchWholeWord, true);
        }

       private void ExecuteReplaceAll(string term, string replaceTerm, bool matchCase, bool matchWholeWord)
        {
            string result = _searchService.ReplaceAll(_mainTextBox.Text, term, replaceTerm, matchCase, matchWholeWord);
            if (_mainTextBox.Text != result)
            {
                _mainTextBox.Text = result;
                _mainTextBox.SelectionStart = 0;
            }
        }

        private void FindNextShortcut()
        {
            if (string.IsNullOrEmpty(_searchService.LastSearchTerm)) ShowSearchDialog(false);
            else ExecuteFind(_searchService.LastSearchTerm, _searchService.LastMatchCase, _searchService.LastMatchWholeWord, _searchService.LastSearchDown);
        }

        private void FindPreviousShortcut()
        {
            if (string.IsNullOrEmpty(_searchService.LastSearchTerm)) ShowSearchDialog(false);
            else ExecuteFind(_searchService.LastSearchTerm, _searchService.LastMatchCase, _searchService.LastMatchWholeWord, false);
        }

        private void ShowGoToLine()
        {
            int currentIndex = _mainTextBox.SelectionStart + _mainTextBox.SelectionLength;
            int currentLine = _mainTextBox.GetLineFromCharIndex(currentIndex) + 1;
            int maxLine = _mainTextBox.GetLineFromCharIndex(_mainTextBox.Text.Length) + 1;

            using (var dialog = new GoToLineDialog(_themeManager, currentLine, maxLine))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    int charIndex = _mainTextBox.GetFirstCharIndexFromLine(dialog.LineNumber - 1);
                    if (charIndex >= 0)
                    {
                        _mainTextBox.SelectionStart = charIndex;
                        _mainTextBox.SelectionLength = 0;
                        _mainTextBox.ScrollToCaret();
                        _mainTextBox.Focus();
                        UpdateUIState();
                    }
                }
            }
        }

        private string GetEncodingDisplayName(System.Text.Encoding enc)
        {
            if (enc is System.Text.UTF8Encoding utf8) return utf8.GetPreamble().Length > 0 ? "UTF-8 with BOM" : "UTF-8";
            if (enc.CodePage == System.Text.Encoding.Unicode.CodePage) return "UTF-16 LE";
            if (enc.CodePage == System.Text.Encoding.BigEndianUnicode.CodePage) return "UTF-16 BE";
            if (enc.CodePage == 1252) return "ANSI";
            return enc.EncodingName;
        }

        private void UpdateUIState()
        {
            string zoom = _zoomPercent != 100 ? $" ({_zoomPercent}%)" : "";
            this.Text = $"{(_isModified ? "*" : "")}{_fileService.GetFileName()} - YotePad{zoom}";
            int index = _mainTextBox.SelectionStart + _mainTextBox.SelectionLength;
            int line = _mainTextBox.GetLineFromCharIndex(index);
            int column = index - _mainTextBox.GetFirstCharIndexFromLine(line);
            _lblLocation.Text = $"Ln {line + 1}, Col {column + 1}";

            // Update Line Ending UI
            _btnLineEnding.Text = _fileService.CurrentLineEnding switch
            {
                LineEndingType.LF => "Unix (LF)",
                LineEndingType.CR => "Macintosh (CR)",
                _ => "Windows (CRLF)"
            };

            foreach (ToolStripMenuItem item in _btnLineEnding.DropDownItems)
            {
                item.Checked = (item.Text == _btnLineEnding.Text);
            }

            // Update Encoding UI
            _btnEncoding.Text = GetEncodingDisplayName(_fileService.CurrentEncoding);
            
            // Sync checkmarks on the new Status Bar button
            foreach (ToolStripMenuItem item in _btnEncoding.DropDownItems)
            {
                item.Checked = (item.Text == _btnEncoding.Text);
            }
            
            // Sync checkmarks in the File menu
            if (_topMenu.Items.Count > 0 && _topMenu.Items[0] is ToolStripMenuItem fileMenu)
            {
                foreach (ToolStripItem item in fileMenu.DropDownItems)
                {
                    if (item is ToolStripMenuItem encMenu && encMenu.Text == "Encoding")
                    {
                        foreach (ToolStripMenuItem subItem in encMenu.DropDownItems)
                        {
                            subItem.Checked = (subItem.Text == _btnEncoding.Text);
                        }
                    }
                }
            }
        }

        private void ZoomIn()
        {
            if (_zoomPercent >= 500) return;
            _zoomPercent += 10;
            ApplyZoom();
        }

        private void ZoomOut()
        {
            if (_zoomPercent <= 10) return;
            _zoomPercent -= 10;
            ApplyZoom();
        }

        private void ZoomReset()
        {
            _zoomPercent = 100;
            ApplyZoom();
        }
    
        private void ApplyZoom()
        {
            float newSize = _baseFontSize * (_zoomPercent / 100f);
            if (newSize < 1f) newSize = 1f;

            // Grab a reference to the old font so we can destroy it safely
            Font oldFont = _mainTextBox.Font;
            
            // Assign the new font
            _mainTextBox.Font = new Font(oldFont.FontFamily, newSize, oldFont.Style);
            
            // Free the unmanaged GDI resource!
            oldFont.Dispose(); 

            _lblZoom.Text = $"{_zoomPercent}%";
            UpdateUIState();
        }

       
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Add:
                case Keys.Control | Keys.Shift | Keys.Oemplus:
                    ZoomIn();
                    return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract:
                    ZoomOut();
                    return true;
                case Keys.Control | Keys.D0:
                case Keys.Control | Keys.NumPad0:
                    ZoomReset();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void SetRestoredFilePath(string originalPath)
        {
            if (!string.IsNullOrEmpty(originalPath))
            {
                _fileService.SetFilePath(originalPath);
            }
            else
            {
                _fileService.SetFilePath(string.Empty);
            }
            _isModified = true;

            // Immediately write recovery file so this content is protected from another crash
            _recoveryService.WriteRecoveryFile(_mainTextBox.Text, _fileService.CurrentFilePath);
            _lastRecoveryContent = _mainTextBox.Text;

            UpdateUIState();
        }

        private void SetApplicationIcon()
        {
            try
            {
                _iconHandle = IntPtr.Zero;
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // CloseReason.UserClosing is only true if a human clicks [X], Alt+F4, or 'Exit' in your menu
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // We call your existing ConfirmSaveIfModified method
                if (!ConfirmSaveIfModified())
                {
                    e.Cancel = true;
                    return;
                }
            }

            // If the CloseReason is anything else (like the Installer/Mutex closing the app),
            // it will skip the save prompt entirely and close instantly.
            base.OnFormClosing(e);
        }
    }

}
