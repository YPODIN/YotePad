// RecoveryDialog.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Yotepad
{
    public class RecoveryDialog : Form
    {
        private readonly RecoveryFile[] _files;
        private readonly ThemeManager _themeManager;
        private readonly List<RecoveryRowControl> _rows = new List<RecoveryRowControl>();
        private Panel _rowPanel = new Panel();
        private Button _btnRestoreSelected = new Button();
        private Button _btnDiscardAll = new Button();

        // Returns the files the user chose to restore
        public List<RecoveryFile> FilesToRestore { get; private set; } = new List<RecoveryFile>();

        public RecoveryDialog(RecoveryFile[] files, ThemeManager themeManager)
        {
            _files = files;
            _themeManager = themeManager;
            InitializeComponent();
            ApplyTheme();
            PopulateRows();
        }

        private void InitializeComponent()
        {
            this.Text = "YotePad — Session Recovery";
            this.Size = new Size(560, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.TopMost = true;

            // Header label
            Label lblHeader = new Label
            {
                Text = "YotePad found unsaved files from a previous session.",
                Location = new Point(12, 12),
                Size = new Size(520, 20),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };

            Label lblSub = new Label
            {
                Text = "Select the files you want to restore:",
                Location = new Point(12, 34),
                Size = new Size(520, 18),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };

            // Scrollable row panel
            _rowPanel.Location = new Point(12, 58);
            _rowPanel.Size = new Size(520, 260);
            _rowPanel.AutoScroll = true;
            _rowPanel.BorderStyle = BorderStyle.FixedSingle;

            // Bottom buttons
            _btnRestoreSelected.Text = "Apply";
            _btnRestoreSelected.Size = new Size(90, 28);
            _btnRestoreSelected.Location = new Point(330, 340);
            _btnRestoreSelected.Click += BtnRestoreSelected_Click;

            _btnDiscardAll.Text = "Discard All";
            _btnDiscardAll.Size = new Size(100, 28);
            _btnDiscardAll.Location = new Point(432, 340);
            _btnDiscardAll.Click += BtnDiscardAll_Click;

            this.Controls.Add(lblHeader);
            this.Controls.Add(lblSub);
            this.Controls.Add(_rowPanel);
            this.Controls.Add(_btnRestoreSelected);
            this.Controls.Add(_btnDiscardAll);
        }

        private void PopulateRows()
        {
            int y = 4;
            foreach (var file in _files)
            {
                var row = new RecoveryRowControl(file, _themeManager);
                row.Location = new Point(4, y);
                row.Width = _rowPanel.Width - 24;
                row.OnPreview += ShowPreview;
                _rows.Add(row);
                _rowPanel.Controls.Add(row);
                y += row.Height + 4;
            }
        }

        private void ReflowRows()
        {
            int y = 4;
            foreach (var row in _rows)
            {
                row.Location = new Point(4, y);
                y += row.Height + 4;
            }
        }

        private void ShowPreview(RecoveryFile file)
        {
            // Spawn preview to the right of the recovery dialog
            Point spawnLocation = new Point(
                this.Location.X + this.Width + 10,
                this.Location.Y
            );

            var preview = new PreviewWindow(file, _themeManager, spawnLocation);
            preview.Owner = this;

            preview.OnRecover += (f) =>
            {
                // Launch a new instance directly from preview — no routing through FilesToRestore
                RecoveryLauncher.Launch(f, this.Location, 0);

                var row = _rows.Find(r => r.RecoveryFile == f);
                if (row != null)
                {
                    _rowPanel.Controls.Remove(row);
                    _rows.Remove(row);
                    ReflowRows();
                }
                preview.Close();

                // Close recovery dialog if this was the last file
                if (_rows.Count == 0) this.DialogResult = DialogResult.OK;
            };

            preview.OnDelete += (f) =>
            {
                RecoveryService.DeleteRecoveryFileAt(f.RecoveryFilePath);
                var row = _rows.Find(r => r.RecoveryFile == f);
                if (row != null)
                {
                    _rowPanel.Controls.Remove(row);
                    _rows.Remove(row);
                    ReflowRows();
                }
                preview.Close();
                if (_rows.Count == 0) this.DialogResult = DialogResult.Cancel;
            };

            preview.Show(); // Non-blocking — floats alongside recovery dialog
        }

        private void BtnRestoreSelected_Click(object? sender, EventArgs e)
        {
            foreach (var row in _rows)
            {
                if (row.IsChecked)
                    FilesToRestore.Add(row.RecoveryFile);
                else
                    RecoveryService.DeleteRecoveryFileAt(row.RecoveryFile.RecoveryFilePath);
            }
            this.DialogResult = DialogResult.OK;
        }

        private void BtnDiscardAll_Click(object? sender, EventArgs e)
        {
            foreach (var row in _rows)
                RecoveryService.DeleteRecoveryFileAt(row.RecoveryFile.RecoveryFilePath);
            this.DialogResult = DialogResult.Cancel;
        }

        public void ApplyTheme()
        {
            this.BackColor = _themeManager.BackgroundColor;
            this.ForeColor = _themeManager.TextColor;
            _rowPanel.BackColor = _themeManager.MenuBackgroundColor;

            Button[] buttons = { _btnRestoreSelected, _btnDiscardAll };
            foreach (var btn in buttons)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Color.DimGray;
                btn.BackColor = _themeManager.BackgroundColor;
                btn.ForeColor = _themeManager.TextColor;
            }

            foreach (Control c in this.Controls)
            {
                if (c is Label lbl)
                {
                    lbl.BackColor = _themeManager.BackgroundColor;
                    lbl.ForeColor = _themeManager.TextColor;
                }
            }

            foreach (var row in _rows)
                row.ApplyTheme();
        }
    }

    // A single row in the recovery list
    public class RecoveryRowControl : Panel
    {
        public RecoveryFile RecoveryFile { get; }
        public bool IsChecked => _chk.Checked;

        public event Action<RecoveryFile>? OnPreview;
        
        private CheckBox _chk = new CheckBox();
        private Label _lblName = new Label();
        private Button _btnPreview = new Button();
        private readonly ThemeManager _themeManager;

        public RecoveryRowControl(RecoveryFile file, ThemeManager themeManager)
        {
            RecoveryFile = file;
            _themeManager = themeManager;
            this.Height = 32;
            this.Padding = new Padding(0);

            _chk.Checked = true;
            _chk.Size = new Size(20, 20);
            _chk.Location = new Point(6, 6);

            _lblName.Text = file.DisplayName;
            _lblName.Location = new Point(30, 8);
            _lblName.Size = new Size(360, 18);
            _lblName.AutoEllipsis = true;

            // Tooltip for full filename on hover
            var tooltip = new ToolTip();
            tooltip.SetToolTip(_lblName, file.DisplayName);

            _btnPreview.Text = "Preview";
            _btnPreview.Size = new Size(70, 24);
            _btnPreview.Location = new Point(396, 4);
            _btnPreview.Click += (s, e) => OnPreview?.Invoke(RecoveryFile);

            this.Controls.Add(_chk);
            this.Controls.Add(_lblName);
            this.Controls.Add(_btnPreview);

            ApplyTheme();
        }

        public void ApplyTheme()
        {
            this.BackColor = _themeManager.MenuBackgroundColor;
            this.ForeColor = _themeManager.TextColor;
            _lblName.BackColor = _themeManager.MenuBackgroundColor;
            _lblName.ForeColor = _themeManager.TextColor;
            _chk.BackColor = _themeManager.MenuBackgroundColor;
            _chk.ForeColor = _themeManager.TextColor;
            _btnPreview.FlatStyle = FlatStyle.Flat;
            _btnPreview.FlatAppearance.BorderColor = Color.DimGray;
            _btnPreview.BackColor = _themeManager.BackgroundColor;
            _btnPreview.ForeColor = _themeManager.TextColor;
        }
    }
}