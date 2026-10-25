using System.Reflection;
using System.Text.Json;

namespace WinFormsApp2;

internal static class AppTheme
{
    internal const string TitleTag = "AccountingApp.Title";
    private static readonly Font ClassicFont = new("Tahoma", 9f);
    private static readonly Font ModernFont = new("Segoe UI", 9f);
    private static readonly Font ClassicHeading = new("Tahoma", 14f, FontStyle.Bold);
    internal static Font TitleFont => ClassicHeading;
    private static readonly Font ModernHeading = new("Segoe UI", 14f, FontStyle.Bold);
    public static IEnumerable<Control> Descendants(Control control)
    { foreach (Control child in control.Controls) { yield return child; foreach (var sub in Descendants(child)) yield return sub; } }
    public static void Apply(Control root, bool? darkOverride = null, bool? classicOverride = null)
    {
        var preferences = UserPreferences.Load();
        bool classic = classicOverride ?? (darkOverride == null && preferences.ClassicTheme);
        bool dark = !classic && (darkOverride ?? preferences.DarkTheme);
        Application.VisualStyleState = classic ? System.Windows.Forms.VisualStyles.VisualStyleState.NoneEnabled : System.Windows.Forms.VisualStyles.VisualStyleState.ClientAndNonClientAreasEnabled;
        if (classic) { ApplyClassic(root); return; }
        Color background = dark ? Color.FromArgb(27, 31, 40) : Color.FromArgb(245, 247, 251);
        Color surface = dark ? Color.FromArgb(39, 45, 57) : Color.White;
        Color foreground = dark ? Color.FromArgb(237, 241, 247) : Color.FromArgb(25, 37, 58);
        root.BackColor = background; root.ForeColor = foreground;
        foreach (var c in Descendants(root))
        {
            c.Font = c.Font.Size >= 12 ? ModernHeading : ModernFont;
            if (Equals(c.Tag, TitleTag)) { c.BackColor = Color.FromArgb(25, 46, 80); c.ForeColor = Color.White; continue; }
            c.ForeColor = foreground;
            if (c is TextBox text)
            {
                c.BackColor = surface;
                if (string.IsNullOrEmpty(text.PlaceholderText)) text.PlaceholderText = text.Name.ToLowerInvariant() switch
                { var n when n.Contains("tc") => "11 haneli TC", var n when n.Contains("iban") => "TR…", var n when n.Contains("money") => "0,00 TL", var n when n.Contains("soyad") => "Soyadı", var n when n.Contains("ad") => "Adı", _ => "" };
            }
            else if (c is Button button)
            { button.FlatStyle = FlatStyle.Flat; button.BackColor = dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 234, 247); button.FlatAppearance.BorderColor = dark ? Color.SlateGray : Color.LightSteelBlue; }
            else if (c is DataGridView grid)
            {
                typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(grid, true);
                grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; grid.ColumnHeadersHeight = 36;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 46, 80); grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.DefaultCellStyle.BackColor = surface; grid.DefaultCellStyle.ForeColor = foreground;
                grid.AlternatingRowsDefaultCellStyle.BackColor = dark ? Color.FromArgb(46, 53, 66) : Color.FromArgb(235, 241, 250);
                grid.BackgroundColor = surface; grid.RowTemplate.Height = 28; grid.AllowUserToOrderColumns = true;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 103, 180); grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.DataBindingComplete -= FormatGrid; grid.DataBindingComplete += FormatGrid;
            }
            else if (c is ToolStrip strip) { StyleStrip(strip, background, foreground, ModernFont); }
            else c.BackColor = c is ComboBox or DateTimePicker ? surface : background;
        }
        root.Invalidate(true);
    }
    private static void ApplyClassic(Control root)
    {
        Color face = Color.FromArgb(212, 208, 200), selection = Color.FromArgb(10, 36, 106);
        root.BackColor = face; root.ForeColor = Color.Black;
        foreach (var c in Descendants(root))
        {
            c.Font = c.Font.Size >= 12 ? ClassicHeading : ClassicFont;
            c.ForeColor = Color.Black;
            if (Equals(c.Tag, TitleTag)) { c.BackColor = selection; c.ForeColor = Color.White; continue; }
            c.BackColor = face;
            if (c is TextBox text)
            {
                text.BorderStyle = BorderStyle.Fixed3D; text.BackColor = Color.White;
                if (string.IsNullOrEmpty(text.PlaceholderText)) text.PlaceholderText = text.Name.ToLowerInvariant() switch
                { var n when n.Contains("tc") => "11 haneli TC", var n when n.Contains("iban") => "TR…", var n when n.Contains("money") => "0,00 TL", _ => "" };
            }
            else if (c is Button button) { button.FlatStyle = FlatStyle.Standard; button.UseVisualStyleBackColor = false; }
            else if (c is ComboBox combo) { combo.FlatStyle = FlatStyle.Standard; combo.BackColor = Color.White; }
            else if (c is TreeView tree) { tree.BackColor = Color.White; tree.BorderStyle = BorderStyle.Fixed3D; tree.HideSelection = false; }
            else if (c is DataGridView grid)
            {
                typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(grid, true);
                grid.EnableHeadersVisualStyles = false; grid.BorderStyle = BorderStyle.Fixed3D;
                grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Raised;
                grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Raised;
                grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; grid.ColumnHeadersHeight = 30;
                grid.ColumnHeadersDefaultCellStyle.BackColor = face; grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = face; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
                grid.DefaultCellStyle.BackColor = Color.White; grid.DefaultCellStyle.ForeColor = Color.Black;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 240);
                grid.DefaultCellStyle.SelectionBackColor = selection; grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.RowHeadersDefaultCellStyle.BackColor = face; grid.GridColor = Color.FromArgb(160, 160, 160);
                grid.BackgroundColor = Color.White; grid.RowTemplate.Height = 25; grid.AllowUserToOrderColumns = true;
                grid.DataBindingComplete -= FormatGrid; grid.DataBindingComplete += FormatGrid;
            }
            else if (c is ToolStrip strip) StyleStrip(strip, face, Color.Black, ClassicFont);
            else if (c is CheckBox check) { check.FlatStyle = FlatStyle.Standard; check.UseVisualStyleBackColor = false; }
            else if (c is GroupBox group) group.FlatStyle = FlatStyle.Standard;
        }
        root.Invalidate(true);
    }
    private static void StyleStrip(ToolStrip strip, Color face, Color text, Font font)
    {
        strip.Renderer = new ToolStripSystemRenderer(); strip.BackColor = face; strip.ForeColor = text; strip.Font = font;
        void Style(ToolStripItem item)
        {
            item.BackColor = face; item.ForeColor = text; item.Font = font;
            if (item is ToolStripStatusLabel label) { label.BorderSides = ToolStripStatusLabelBorderSides.All; label.BorderStyle = Border3DStyle.SunkenOuter; }
            if (item is ToolStripDropDownItem dropdown)
            {
                dropdown.DropDown.Renderer = strip.Renderer; dropdown.DropDown.BackColor = face;
                foreach (ToolStripItem child in dropdown.DropDownItems) Style(child);
            }
        }
        foreach (ToolStripItem item in strip.Items) Style(item);
    }
    private static void FormatGrid(object? sender, DataGridViewBindingCompleteEventArgs e)
    {
        if (sender is not DataGridView grid) return;
        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.ValueType == typeof(decimal)) col.DefaultCellStyle.Format = "N2";
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd.MM.yyyy";
        }
        GridPreferences.Restore(grid);
    }
}
internal sealed record GridColumnState(string Name, int Width, int Index, bool Visible);
internal static class GridPreferences
{
    private static string PathFor(DataGridView grid) => Path.Combine(UserPreferences.DirectoryPath, "grids", grid.FindForm()!.GetType().Name + "-" + grid.Name + ".json");
    public static void Save(DataGridView grid, string? path = null)
    {
        if (grid.Columns.Count == 0 || grid.FindForm() == null) return;
        AtomicFile.Write(path ?? PathFor(grid), JsonSerializer.SerializeToUtf8Bytes(grid.Columns.Cast<DataGridViewColumn>().Where(c => !RecordSelection.IsInternalColumn(c)).Select(c => new GridColumnState(c.Name, c.Width, c.DisplayIndex, c.Visible))));
    }
    public static void Restore(DataGridView grid, string? path = null)
    {
        if (grid.FindForm() == null) return;
        try
        {
            var state = JsonSerializer.Deserialize<List<GridColumnState>>(File.ReadAllText(path ?? PathFor(grid))) ?? [];
            foreach (var item in state.OrderBy(s => s.Index)) if (grid.Columns[item.Name] is DataGridViewColumn column && !RecordSelection.IsInternalColumn(column))
            { column.Width = Math.Clamp(item.Width, 40, 1200); column.Visible = item.Visible; column.DisplayIndex = Math.Clamp(item.Index, 0, grid.Columns.Count - 1); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { AppLog.Event("Grid.PreferencesReset"); }
        finally { RecordSelection.HideInternalColumns(grid); }
    }
}
