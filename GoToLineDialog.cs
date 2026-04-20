using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yotepad
{
    public class GoToLineDialog : Form
    {
        private TextBox _txtLineNumber = new TextBox();
        private Button _btnGoTo = new Button();
        private Button _btnCancel = new Button();
        private Label _lblPrompt = new Label();

        public int LineNumber { get; private set; } = -1;

        public GoToLineDialog(ThemeManager themeManager, int currentLine, int maxLine)
        {
            InitializeComponent(currentLine, maxLine);
            ApplyTheme(themeManager);
        }

        private void InitializeComponent(int currentLine, int maxLine)
        {
            this.Text = "Go To Line";
            this.ClientSize = new Size(280, 110);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            _lblPrompt.Text = $"Line number (1 - {maxLine}):";
            _lblPrompt.Location = new Point(12, 15);
            _lblPrompt.AutoSize = true;

            _txtLineNumber.Text = currentLine.ToString();
            _txtLineNumber.Location = new Point(12, 38);
            _txtLineNumber.Size = new Size(252, 23);
            _txtLineNumber.SelectAll();

            _btnGoTo.Text = "Go To";
            _btnGoTo.Size = new Size(80, 28);
            _btnGoTo.Location = new Point(103, 72);
            _btnGoTo.Click += (s, e) =>
            {
                if (int.TryParse(_txtLineNumber.Text, out int line) && line >= 1 && line <= maxLine)
                {
                    LineNumber = line;
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show($"Please enter a number between 1 and {maxLine}.", 
                        "YotePad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            _btnCancel.Text = "Cancel";
            _btnCancel.Size = new Size(80, 28);
            _btnCancel.Location = new Point(189, 72);
            _btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            this.Controls.Add(_lblPrompt);
            this.Controls.Add(_txtLineNumber);
            this.Controls.Add(_btnGoTo);
            this.Controls.Add(_btnCancel);

            this.AcceptButton = _btnGoTo;
            this.CancelButton = _btnCancel;
        }

        public void ApplyTheme(ThemeManager theme)
        {
            this.BackColor = theme.BackgroundColor;
            this.ForeColor = theme.TextColor;

            _txtLineNumber.BackColor = theme.BackgroundColor;
            _txtLineNumber.ForeColor = theme.TextColor;
            _txtLineNumber.BorderStyle = BorderStyle.FixedSingle;

            _lblPrompt.BackColor = theme.BackgroundColor;
            _lblPrompt.ForeColor = theme.TextColor;

            Button[] buttons = { _btnGoTo, _btnCancel };
            foreach (var btn in buttons)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Color.DimGray;
                btn.BackColor = theme.BackgroundColor;
                btn.ForeColor = theme.TextColor;
            }
        }
    }
}