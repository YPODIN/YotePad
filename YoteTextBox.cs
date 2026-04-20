using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yotepad
{
    public class YoteTextBox : TextBox
    {
        private const int WM_PASTE = 0x0302;
        
        public bool IsOverwriteMode { get; private set; } = false;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PASTE && Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
                this.SelectedText = text;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Insert)
            {
                IsOverwriteMode = !IsOverwriteMode;
                UpdateCaretAppearance();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (IsOverwriteMode && 
                this.SelectionLength == 0 && 
                this.SelectionStart < this.TextLength && 
                !char.IsControl(e.KeyChar))
            {
                char nextChar = this.Text[this.SelectionStart];
                
                if (nextChar != '\r' && nextChar != '\n')
                {
                    this.SelectionLength = 1;
                }
            }
            base.OnKeyPress(e);
        }

        // The OS constantly tries to reset the caret to a line. 
        // We must reassert our block caret after these events.
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            UpdateCaretAppearance();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            UpdateCaretAppearance();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            UpdateCaretAppearance();
        }

        private void UpdateCaretAppearance()
        {
            if (IsOverwriteMode)
            {
                // Measure roughly how wide a character is in the current font
                int width = TextRenderer.MeasureText("W", this.Font).Width / 2;
                int height = this.Font.Height;
                
                // Passing IntPtr.Zero creates a solid black/white inverted block
                NativeMethods.CreateCaret(this.Handle, IntPtr.Zero, width, height);
                NativeMethods.ShowCaret(this.Handle);
            }
            else
            {
                // Revert to a standard 1-pixel wide line caret
                NativeMethods.CreateCaret(this.Handle, IntPtr.Zero, 1, this.Font.Height);
                NativeMethods.ShowCaret(this.Handle);
            }
        }
    }
}