namespace WinFormsApp2;

public partial class Admin : SessionForm
{
    private readonly ComboBox role = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175, 213), Width = 185 };
    public Admin()
    {
        InitializeComponent(); RecordSelection.Configure(adminView);
        role.Items.AddRange(["Yönetici", "Personel sorumlusu", "Görüntüleyici"]); role.SelectedIndex = 0;
        Controls.Add(new Label { Text = "Yetki", Location = new Point(45, 216), AutoSize = true }); Controls.Add(role);
        var deactivate = new Button { Text = "Pasife al", AutoSize = true, Location = new Point(45, 310) };
        var activate = new Button { Text = "Etkinleştir", AutoSize = true, Location = new Point(175, 310) };
        Controls.AddRange([deactivate, activate]); EditControls.AddRange([deactivate, activate]);
        deactivate.Click += async (_, _) => await SetActiveAsync(false);
        activate.Click += async (_, _) => await SetActiveAsync(true);
        var reset = new Button { Text = "Parola sıfırla…", AutoSize = true, Location = new Point(45, 355) };
        Controls.Add(reset); EditControls.Add(reset); reset.Click += ResetPassword;
        EditControls.AddRange([ekle_btn, sil_btn]); RefreshAction = LoadDataAsync;
        DeleteAction = () => sil_btn_Click(this, EventArgs.Empty);
    }
    private async void Admin_Load(object sender, EventArgs e) { if (!PreviewOnly) await LoadDataAsync(); }
    private void back_Click(object sender, EventArgs e) => RequestBack();
    private async Task LoadDataAsync() => await RunOperationAsync(async token =>
    {
        var data = await new AccountService(AppDatabase.ConnectionString).ListAsync(User, token);
        if (!IsDisposed && !token.IsCancellationRequested)
        {
            adminView.DataSource = data; adminView.ClearSelection();
            if (adminView.Columns["IsActive"] is { } active) active.HeaderText = "Etkin";
            if (adminView.Columns["MustChangePassword"] is { } required) required.HeaderText = "Parola yenileme gerekli";
            if (adminView.Columns["PasswordResetExpiresUtc"] is { } expiry) expiry.HeaderText = "Geçici parola sonu (UTC)";
        }
    });
    private async void ResetPassword(object? sender, EventArgs e)
    {
        AccountTarget target;
        try { target = AccountTarget.FromGrid(adminView); }
        catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message); return; }
        if (string.Equals(target.Username, User.Username, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "Kendi hesabınız için üstteki Parolamı değiştir düğmesini kullanın."); return; }
        using var dialog = new PasswordResetDialog(User, target);
        var result = dialog.ShowDialog(this);
        if (IsDisposed) return;
        if (dialog.SessionInvalidated) { NotifySessionRevoked(); return; }
        if (result != DialogResult.OK || dialog.Result == null) return;
        using (var secret = new TemporaryPasswordDialog(dialog.Result)) secret.ShowDialog(this);
        if (!IsDisposed) await LoadDataAsync();
    }
    private async Task SetActiveAsync(bool active)
    {
        if (!RecordSelection.TryGetKey(adminView, "username", out string username)) { MessageBox.Show(this, "Listeden bir hesap seçin."); return; }
        string question = active ? $"{username} hesabı etkinleştirilsin mi?" : $"{username} hesabı pasife alınsın mı? Açık oturumları sona erecek ve yeniden giriş yapamayacak.";
        if (MessageBox.Show(this, question, "Hesap durumu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (await RunOperationAsync(token => new AccountService(AppDatabase.ConnectionString).SetActiveAsync(User, username, active, token), active ? "Hesap etkinleştirildi." : "Hesap pasife alındı.") && !IsDisposed) await LoadDataAsync();
    }
    private async void ekle_btn_Click(object sender, EventArgs e)
    {
        string username = txtUsername.Text, password = txtPassword.Text; UserRole selected = (UserRole)role.SelectedIndex;
        bool done = await RunOperationAsync(token => new AccountService(AppDatabase.ConnectionString).AddAsync(User, username, password, selected, token), "Hesap eklendi.");
        if (!IsDisposed) txtPassword.Clear();
        if (done && !IsDisposed) { txtUsername.Clear(); await LoadDataAsync(); }
    }
    private async void sil_btn_Click(object sender, EventArgs e)
    {
        if (!RecordSelection.TryGetKey(adminView, "username", out string username)) return;
        if (MessageBox.Show(this, $"{username} hesabı silinsin mi?", "Hesap sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (await RunOperationAsync(token => new AccountService(AppDatabase.ConnectionString).DeleteAsync(User, username, token), "Hesap silindi.") && !IsDisposed) await LoadDataAsync();
    }
}
