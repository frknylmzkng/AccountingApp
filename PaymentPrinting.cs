using System.Drawing.Printing;
using QRCoder;

namespace WinFormsApp2;

internal sealed record PrintReport(string Title, IReadOnlyList<string> Lines, string Reference, decimal? Difference = null);
internal static class PaymentPrinting
{
    public static string Mask(string text) => text.Length <= 4 ? new string('*', text.Length) : new string('*', text.Length - 4) + text[^4..];
    public static PrintDocument Create(PrintReport report, bool thermal, string? logoPath = null)
    {
        var document = new PrintDocument { DocumentName = report.Title };
        document.DefaultPageSettings.PaperSize = thermal ? new PaperSize("80 mm", 315, 1169) : new PaperSize("A4", 827, 1169);
        document.DefaultPageSettings.Margins = thermal ? new Margins(12, 12, 15, 15) : new Margins(55, 55, 55, 55);
        int offset = 0, page = 0;
        document.BeginPrint += (_, _) => { offset = 0; page = 0; };
        document.PrintPage += (_, e) =>
        {
            if (e.Graphics == null) return;
            offset = Draw(e.Graphics, e.MarginBounds, report, offset, ++page, logoPath, out bool more); e.HasMorePages = more;
        };
        return document;
    }
    internal static int Draw(Graphics graphics, Rectangle bounds, PrintReport report, int offset, int page, string? logoPath, out bool more)
    {
        using var font = new Font("Segoe UI", 9); using var title = new Font("Segoe UI", 12, FontStyle.Bold);
        float y = bounds.Top; float line = font.GetHeight(graphics) + 5;
        if (page == 1 && !string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            try { using var logo = Image.FromFile(logoPath); graphics.DrawImage(logo, bounds.Left, y, 55, 40); y += 45; }
            catch (Exception ex) when (ex is ArgumentException or IOException or OutOfMemoryException) { AppLog.Event("Print.LogoUnavailable"); }
        }
        graphics.DrawString(report.Title, title, Brushes.Black, new RectangleF(bounds.Left, y, bounds.Width, 45)); y += 48;
        graphics.DrawString($"{DateTime.Now:dd.MM.yyyy HH:mm} · Sayfa {page}", font, Brushes.Black, bounds.Left, y); y += line * 2;
        while (offset < report.Lines.Count)
        {
            string text = report.Lines[offset]; var size = graphics.MeasureString(text, font, bounds.Width);
            if (y + size.Height + line > bounds.Bottom - 140) break;
            graphics.DrawString(text, font, Brushes.Black, new RectangleF(bounds.Left, y, bounds.Width, size.Height + 3)); y += size.Height + 7; offset++;
        }
        more = offset < report.Lines.Count;
        if (!more)
        {
            if (report.Difference is decimal difference)
            {
                graphics.DrawString($"Beyan − kayıt farkı: {difference:N2} TL", font, difference == 0 ? Brushes.DarkGreen : Brushes.DarkRed, new RectangleF(bounds.Left, y, bounds.Width, 35)); y += 38;
            }
            graphics.DrawString("Düzenleyen: __________\nTeslim alan / kontrol: __________", font, Brushes.Black, bounds.Left, y);
            using var data = QRCodeGenerator.GenerateQrCode(report.Reference, QRCodeGenerator.ECCLevel.Q); using var qr = new QRCode(data); using var bitmap = qr.GetGraphic(3);
            graphics.DrawImage(bitmap, bounds.Right - 65, bounds.Bottom - 65, 65, 65);
            graphics.DrawString("Ref: " + report.Reference, font, Brushes.Black, new RectangleF(bounds.Left, bounds.Bottom - 28, bounds.Width - 70, 28));
        }
        return offset;
    }
    public static void Preview(IWin32Window owner, PrintReport report, bool thermal)
    {
        using var document = Create(report, thermal, UserPreferences.Load().LogoPath);
        using var preview = new PrintPreviewDialog { Document = document, Width = 1050, Height = 800, UseAntiAlias = true };
        preview.ShowDialog(owner);
    }
}
