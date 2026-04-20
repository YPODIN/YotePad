using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace Yotepad
{
    public class PrintService
    {
        private readonly PrintDocument _printDocument = new PrintDocument();
        private string _textToPrint = string.Empty;
        private Font? _printFont;
        private int _currentCharIndex = 0;

        public PrintService()
        {
            // Set some standard default margins (in hundredths of an inch)
            _printDocument.DefaultPageSettings.Margins = new Margins(100, 100, 100, 100);
            
            _printDocument.BeginPrint += PrintDocument_BeginPrint;
            _printDocument.PrintPage += PrintDocument_PrintPage;
        }

        public void ShowPageSetup()
        {
            using (PageSetupDialog setupDialog = new PageSetupDialog())
            {
                setupDialog.Document = _printDocument;
                setupDialog.ShowDialog();
            }
        }

        public void Print(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return;

            _textToPrint = text;
            _printFont = font;

            using (PrintDialog printDialog = new PrintDialog())
            {
                printDialog.Document = _printDocument;
                printDialog.UseEXDialog = true; // Use the modern Windows print dialog

                if (printDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        _printDocument.Print();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error printing: {ex.Message}", "YotePad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void PrintDocument_BeginPrint(object sender, PrintEventArgs e)
        {
            // Reset the character index every time a new print job starts
            _currentCharIndex = 0;
        }

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (_printFont == null || e.Graphics == null) return;

            // Define the area where we are allowed to print based on the page setup margins
            RectangleF printArea = new RectangleF(
                e.MarginBounds.Left, 
                e.MarginBounds.Top, 
                e.MarginBounds.Width, 
                e.MarginBounds.Height);

            string textRemaining = _textToPrint.Substring(_currentCharIndex);

            // Ask the Graphics object to measure how many characters will fit in the print box
            e.Graphics.MeasureString(
                textRemaining, 
                _printFont, 
                printArea.Size, 
                StringFormat.GenericTypographic, 
                out int charactersFitted, 
                out int linesFilled);

            // Draw the text onto the paper
            e.Graphics.DrawString(
                textRemaining, 
                _printFont, 
                Brushes.Black, 
                printArea, 
                StringFormat.GenericTypographic);

            // Move our index forward by the amount of characters we just printed
            _currentCharIndex += charactersFitted;

            // Tell the printer if it needs to spit out another blank page and keep going
            e.HasMorePages = (_currentCharIndex < _textToPrint.Length);
        }

        public void ShowPrintPreview(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return;

            _textToPrint = text;
            _printFont = font;

            using (PrintPreviewDialog previewDialog = new PrintPreviewDialog())
            {
                previewDialog.Document = _printDocument;
                previewDialog.ShowIcon = false;
                previewDialog.Text = "YotePad Print Preview";
                previewDialog.Width = 800;
                previewDialog.Height = 600;
                previewDialog.StartPosition = FormStartPosition.CenterParent;
                
                previewDialog.ShowDialog();
            }
        }
    }
}