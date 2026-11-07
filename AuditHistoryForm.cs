using System.Data;
using System.Text.Json;

namespace WinFormsApp2;

internal sealed class AuditHistoryForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly DataGridView records = new() { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly TextBox details = new() { Multiline = true, ReadOnly = true, ShortcutsEnabled = false, ContextMenuStrip = new ContextMenuStrip(), Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false };
    private readonly Button older = new() { Text = "Daha eski 200 kayıt", AutoSize = true };
    private long? cursor;
    private bool hasOlder;
    internal AuditHistoryForm(UserSession user)
    {
        Session = user; PersistGridLayout = false; Text = "Muhasebe — İşlem geçmişi"; ClientSize = new(1100, 740);
        RecordSelection.Configure(records);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(8), WrapContents = true };
        var latest = new Button { Text = "En güncel kayıtlar", AutoSize = true };
        var close = new Button { Text = "Kapat", AutoSize = true };
        bar.Controls.AddRange([latest, older, close]);
        bar.Controls.Add(new Label { AutoSize = true, Text = "Hassas değerler maskelenir. Değişen alan adları korunur.", Padding = new Padding(8) });
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new(SizeType.Percent, 55)); layout.RowStyles.Add(new(SizeType.Percent, 45));
        layout.Controls.Add(records, 0, 0); layout.Controls.Add(details, 0, 1); Controls.Add(layout); Controls.Add(bar);
        latest.Click += async (_, _) => { cursor = null; await ReloadAsync(); };
        older.Click += async (_, _) => await ReloadAsync();
        close.Click += (_, _) => { if (!IsOperationRunning) Close(); };
        records.SelectionChanged += (_, _) => ShowDetails();
        Load += async (_, _) => { MinimumSize = LogicalToDeviceUnits(new Size(720, 500)); if (!PreviewOnly) await ReloadAsync(); };
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var rows = await new AuditHistoryService(AppDatabase.ConnectionString, User).ReadAsync(cursor, token);
        if (IsDisposed || token.IsCancellationRequested) return;
        records.DataSource = rows; records.ClearSelection(); details.Clear();
        foreach (string name in new[] { "OldValues", "NewValues", "CorrelationId", "ActorAccountId", "SqlPrincipal" }) records.Columns[name]!.Visible = false;
        var headers = new Dictionary<string, string> { ["AuditId"] = "No", ["CreatedUtc"] = "Zaman (UTC)", ["Actor"] = "İşlemi yapan", ["Action"] = "İşlem",
            ["EntityType"] = "Kayıt türü", ["RecordKey"] = "Kayıt kimliği", ["Operation"] = "Değişiklik", ["Reason"] = "Gerekçe", ["ReasonSource"] = "Gerekçe kaynağı", ["Source"] = "Kaynak", ["ChangedFields"] = "Değişen alanlar" };
        foreach (var h in headers) records.Columns[h.Key]!.HeaderText = h.Value;
        cursor = rows.Rows.Count == 0 ? cursor : rows.AsEnumerable().Min(r => r.Field<long>("AuditId"));
        hasOlder = rows.Rows.Count == 200;
    });
    protected override void RefreshEditState() => older.Enabled = hasOlder && !IsOperationRunning;
    private void ShowDetails()
    {
        if (records.SelectedRows.Count != 1 || records.SelectedRows[0].DataBoundItem is not DataRowView row) { details.Clear(); return; }
        string Value(string column) => row[column] is DBNull ? "Bilinmiyor / kaydedilmemiş" : row[column].ToString()!;
        string Json(string column)
        {
            if (row[column] is DBNull) return "Yok / tarihsel kayıtta bulunmuyor";
            using var document = JsonDocument.Parse(Value(column));
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        details.Text = $"Kayıt: {Value("EntityType")} / {Value("RecordKey")}\r\nYapan: {Value("Actor")} ({Value("ActorAccountId")}) · UTC: {Value("CreatedUtc")}\r\nGerekçe: {Value("Reason")} ({Value("ReasonSource")})\r\nİşlem bağı: {Value("CorrelationId")} · SQL hesabı: {Value("SqlPrincipal")}\r\n\r\nDEĞİŞEN ALANLAR\r\n{Value("ChangedFields")}\r\n\r\nÖNCEKİ DEĞERLER (HASSAS ALANLAR MASKELİ)\r\n{Json("OldValues")}\r\n\r\nYENİ DEĞERLER (HASSAS ALANLAR MASKELİ)\r\n{Json("NewValues")}";
    }
}
