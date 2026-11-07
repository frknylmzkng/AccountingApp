using System.Data;

namespace WinFormsApp2;

internal sealed record ExportReportChoice(string Title, string Scope, Func<CancellationToken, Task<DataTable>> Load)
{ public override string ToString() => Title; }
internal sealed class ExportReportDialog : Form
{
    private readonly ComboBox reports = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    internal ExportReportChoice Selected => (ExportReportChoice)reports.SelectedItem!;
    internal ExportReportDialog(IReadOnlyList<ExportReportChoice> choices)
    {
        Text = "Dışa aktarılacak rapor"; ClientSize = new(640, 300); MinimumSize = new(500, 320);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        var scope = new Label { AutoSize = false, Dock = DockStyle.Fill, Padding = new(0, 14, 0, 0) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var next = new Button { Text = "Raporu hazırla", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Vazgeç", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([next, cancel]); CancelButton = cancel; AcceptButton = next;
        reports.Items.AddRange(choices.Cast<object>().ToArray());
        reports.SelectedIndexChanged += (_, _) => scope.Text = Selected.Scope + "\n\nBir sonraki adımda aktarılacak sütunları seçebilirsiniz.";
        layout.Controls.Add(reports, 0, 0); layout.Controls.Add(scope, 0, 1); layout.Controls.Add(buttons, 0, 2); Controls.Add(layout);
        if (choices.Count > 0) reports.SelectedIndex = 0; else next.Enabled = false;
    }
}
internal sealed class ExportColumnsDialog : Form
{
    private readonly CheckedListBox columns = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    internal string[] Selected => columns.CheckedItems.Cast<string>().ToArray();
    internal ExportColumnsDialog(DataTable data)
    {
        Text = "Aktarılacak sütunlar"; ClientSize = new(520, 460); MinimumSize = new(400, 330);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        foreach (DataColumn column in data.Columns) columns.Items.Add(column.ColumnName, true);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new(8), WrapContents = true };
        var all = new Button { Text = "Tümü", AutoSize = true }; var none = new Button { Text = "Temizle", AutoSize = true };
        var save = new Button { Text = "Dosyaya aktar", AutoSize = true }; var cancel = new Button { Text = "Vazgeç", AutoSize = true, DialogResult = DialogResult.Cancel };
        all.Click += (_, _) => { for (int i = 0; i < columns.Items.Count; i++) columns.SetItemChecked(i, true); };
        none.Click += (_, _) => { for (int i = 0; i < columns.Items.Count; i++) columns.SetItemChecked(i, false); };
        save.Click += (_, _) => { if (Selected.Length == 0) MessageBox.Show(this, "En az bir sütun seçin."); else DialogResult = DialogResult.OK; };
        buttons.Controls.AddRange([all, none, save, cancel]); Controls.Add(columns); Controls.Add(buttons); CancelButton = cancel;
    }
}
