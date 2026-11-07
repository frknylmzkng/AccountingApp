using System.Data;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class AccountPersonDialog : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly AccountTarget target;
    private readonly DataGridView history = new() { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly Button select = new() { Text = "Personel seç / değiştir…", AutoSize = true }, unlink = new() { Text = "Etkin bağı kaldır…", AutoSize = true };
    private readonly Label current = new() { AutoSize = true };
    private DataRow? active;
    private bool loaded;
    internal bool SessionInvalidated { get; private set; }
    internal AccountPersonDialog(UserSession user, AccountTarget selected)
    {
        Session = user; target = AccountTarget.RequireSnapshot(selected); PersistGridLayout = false;
        Text = "Muhasebe — Hesap / personel eşlemesi"; ClientSize = new(900, 600); ShowInTaskbar = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(14), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        var note = new Label { AutoSize = true, Text = $"Hesap: {target.Username}\nBir hesabın bir etkin personel bağı olabilir; aynı kişinin birden fazla hesabı bağlanabilir. Eşleme erişim yetkisi vermez. Değişiklik hedef hesabın açık oturumlarını sonlandırır.\nKaldırılan bağlar geçmişte saklanır ve puantajda kendi çalışmasını / başka hesabıyla kendi hazırlığını onaylama kontrolünde kullanılmaya devam eder. Yanlış eşlemeyi düzeltmek eski kimlik kanıtını silmez." };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        var refresh = new Button { Text = "Yenile", AutoSize = true }; var close = new Button { Text = "Kapat", AutoSize = true };
        bar.Controls.AddRange([select, unlink, refresh, close]);
        layout.Controls.Add(note, 0, 0); layout.Controls.Add(current, 0, 1); layout.Controls.Add(bar, 0, 2); layout.Controls.Add(history, 0, 3); Controls.Add(layout);
        void Fit() => note.MaximumSize = current.MaximumSize = new(Math.Max(1, layout.ClientSize.Width - 44), 0);
        layout.SizeChanged += (_, _) => Fit(); Fit(); RecordSelection.Configure(history);
        history.DataBindingComplete += (_, _) =>
        {
            var titles = new Dictionary<string, string> { ["AD"] = "Ad", ["SOYAD"] = "Soyad", ["CreatedUtc"] = "Bağlama UTC", ["CreatedBy"] = "Bağlayan", ["CreateReason"] = "Bağlama gerekçesi", ["RevokedUtc"] = "Kaldırma UTC", ["RevokedBy"] = "Kaldıran", ["RevokeReason"] = "Kaldırma gerekçesi" };
            foreach (DataGridViewColumn c in history.Columns) { if (c.ValueType == typeof(Guid) || c.ValueType == typeof(byte[])) c.Visible = false; if (titles.TryGetValue(c.Name, out var caption)) c.HeaderText = caption; if (c.ValueType == typeof(DateTime)) c.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm:ss"; }
        };
        select.Click += async (_, _) =>
        {
            if (IsOperationRunning || !loaded) return;
            using var picker = new PersonnelPickerDialog(User);
            if (picker.ShowDialog(this) != DialogResult.OK || picker.Selected == null) { if (picker.SessionInvalidated) { SessionInvalidated = true; Close(); } return; }
            await ChangeAsync(picker.Selected);
        };
        unlink.Click += async (_, _) => { if (loaded && active != null && !IsOperationRunning) await ChangeAsync(null); };
        refresh.Click += async (_, _) => await ReloadAsync(); close.Click += (_, _) => Close(); RefreshAction = ReloadAsync;
        Load += async (_, _) => await ReloadAsync(); PageRequested += (_, _) => Close(); SessionRevoked += (_, _) => { SessionInvalidated = true; Close(); };
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(600, 480)); }
    protected override void RefreshEditState() { select.Enabled = loaded && !IsOperationRunning && Session?.Role == UserRole.Admin; unlink.Enabled = select.Enabled && active != null; }
    private async Task ReloadAsync()
    {
        loaded = false; active = null;
        await RunOperationAsync(async ct =>
        {
            var data = await new AccountPersonService(AppDatabase.ConnectionString, User).ReadAsync(target, ct);
            if (IsDisposed) return;
            history.DataSource = data; history.ClearSelection(); active = data.AsEnumerable().SingleOrDefault(r => r.IsNull("RevokedUtc")); loaded = true;
            current.Text = active == null ? "Etkin personel bağı yok. Aşağıda varsa geçmiş bağlar gösterilir." : $"Etkin personel: {active["AD"]} {active["SOYAD"]}";
        });
        if (!IsDisposed) RefreshEditState();
    }
    private Task ChangeAsync(PersonnelChoice? person)
    {
        using var edit = new AccountPersonChangeDialog(User, target, active, person);
        edit.SessionRevoked += (_, _) => SessionInvalidated = true;
        edit.ShowDialog(this);
        if (IsDisposed) return Task.CompletedTask;
        if (edit.Saved) { SessionInvalidated |= string.Equals(User.Username, target.Username, StringComparison.OrdinalIgnoreCase); DialogResult = DialogResult.OK; Close(); }
        else if (SessionInvalidated) Close();
        return Task.CompletedTask;
    }
}

internal sealed class AccountPersonChangeDialog : CalendarEditDialog
{
    private readonly Wire.AccountPersonChange request;
    internal AccountPersonChangeDialog(UserSession user, AccountTarget target, DataRow? active, PersonnelChoice? person)
        : base(user, "Muhasebe — Personel eşlemesini kaydet", "Hesap / personel eşlemesi")
    {
        request = new(Guid.NewGuid(), RemoteApi.Target(target), active?.Field<Guid>("LinkId", DataRowVersion.Original), active?["RecordVersion", DataRowVersion.Original] is byte[] v ? v.ToArray() : null,
            person?.Id, person == null ? null : (Wire.PersonnelGroup)person.Group, person?.Version.ToArray(), "");
        string old = active == null ? "Yok" : $"{active["AD"]} {active["SOYAD"]}";
        Fields.Controls.Add(new Label { AutoSize = true, Text = $"Hesap: {target.Username}\nÖnceki etkin personel: {old}\nYeni etkin personel: " + (person == null ? "Yok — etkin bağ kaldırılacak." : $"{person.Caption} · TC: {person.Tc}") });
        Fields.Controls.Add(new Label { AutoSize = true, Text = "Kaydet ve gerekçe ile eşleme değişir; eski bağ silinmez. Hedef hesabın açık oturumları kapanır. Bu işlem proje, ücret, IBAN veya dışa aktarma yetkisi vermez. Kaldırılmış bağlar puantaj görev ayrımında kanıt olarak kalır.\nYanıt alınamazsa aynı isteği yeniden gönderin; farklı bir eşleme için kapatıp hesap listesini yenileyerek yeniden başlayın." });
        AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason) => request with { Reason = reason };
}
