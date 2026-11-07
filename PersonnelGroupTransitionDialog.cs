using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class PersonnelGroupTransitionDialog : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly Wire.PersonnelGroupTransition original;
    private Wire.PersonnelGroupTransition? submitted;
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Short, Width = 180 };
    private readonly Button save = new() { Text = "Tarihli geçişi kaydet", AutoSize = true };
    internal bool Completed { get; private set; }
    internal PersonnelGroupTransitionDialog(UserSession user, PersonnelGroup group, Guid person, byte[] version, Guid employment, byte[] employmentVersion)
    {
        Session = user; PersistGridLayout = false; Text = "Muhasebe — Tarihli personel grubu geçişi"; ClientSize = new(700, 360);
        original = new(Guid.NewGuid(), (Wire.PersonnelGroup)group, person, version.ToArray(), employment, employmentVersion.ToArray(), default, "");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var info = new Label { AutoSize = true, MaximumSize = new(650, 0), Text = $"{(group == PersonnelGroup.General ? "Genel → Joker" : "Joker → Genel")}\n\nSeçilen gün yeni grubun ilk günüdür. Eski dönem önceki gün kapanır. Kişi kimliği ve geçmiş korunur; bu işlem işten ayrılış bildirimi değildir.\n\nHer iki grup bakiyesi sıfır olmalı. Eski dönemin izin/atama/hakedişi geçiş gününe uzanmamalı. O gün veya sonrasında parasal hareket varsa geçiş reddedilir. Gelecek tarihli otomatik geçiş desteklenmez.\n\nİlk gönderimden sonra tarih ve işlem kimliği sabitlenir. Yanıt belirsizse aynı düğmeyle aynı isteği yeniden gönderin." };
        var cancel = new Button { Text = "Kapat", AutoSize = true }; cancel.Click += (_, _) => Close();
        panel.Controls.AddRange([info, new Label { Text = "Yeni grubun başlangıç tarihi", AutoSize = true }, date, save, cancel]); Controls.Add(panel);
        TrackChanges("Grup geçiş tarihi", date); save.Click += async (_, _) => await SaveAsync();
        panel.SizeChanged += (_, _) => info.MaximumSize = new(Math.Max(1, panel.ClientSize.Width - 48), 0);
        SessionRevoked += (_, _) => Close(); PageRequested += (_, _) => Close();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(530, 400)); }
    protected override void RefreshEditState()
    { save.Enabled = !IsOperationRunning && User.CanEdit; date.Enabled = !IsOperationRunning && submitted == null; save.Text = submitted == null ? "Tarihli geçişi kaydet" : "Aynı geçişi yeniden gönder"; }
    private async Task SaveAsync()
    {
        var selected = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Unspecified);
        bool done;
        if (submitted != null)
        {
            using var reason = AuditContext.UseReason(submitted.Reason);
            done = await RunOperationAsync(ct => new EmploymentService(AppDatabase.ConnectionString, User).TransitionGroupAsync(submitted, ct), "Grup geçişinin kayıtlı sonucu alındı.");
        }
        else done = await RunChangeOperationAsync(async ct =>
        {
            submitted ??= original with { EffectiveDate = selected, Reason = RemoteApi.Reason };
            await new EmploymentService(AppDatabase.ConnectionString, User).TransitionGroupAsync(submitted, ct);
        }, "Grup geçişi kaydedildi. Güncel gruptaki personel kartını listeden açabilirsiniz.", "Personel grubu geçişi");
        if (done)
        { Completed = true; AcceptTrackedChanges(); Close(); }
    }
}
