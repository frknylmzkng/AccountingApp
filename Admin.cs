namespace WinFormsApp2;

public partial class Admin : SessionForm
{
    private readonly ComboBox role = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175, 213), Width = 185 };
    public Admin()
    {
        InitializeComponent(); RecordSelection.Configure(adminView);
        role.Items.AddRange(["Yönetici", "Personel sorumlusu", "Görüntüleyici"]); role.SelectedIndex = 0;
        Controls.Add(new Label { Text = "Yetki", Location = new Point(45, 216), AutoSize = true }); Controls.Add(role);
        EditControls.AddRange([ekle_btn, sil_btn]); RefreshAction = LoadDataAsync;
        DeleteAction = () => sil_btn_Click(this, EventArgs.Empty);
    }
    private async void Admin_Load(object sender, EventArgs e) { if (!PreviewOnly) await LoadDataAsync(); }
    private void back_Click(object sender, EventArgs e) => RequestBack();
    private async Task LoadDataAsync() => await RunOperationAsync(async token =>
    {
        var data = await new AccountService(AppDatabase.ConnectionString).ListAsync(User, token);
        if (!IsDisposed && !token.IsCancellationRequested) { adminView.DataSource = data; adminView.ClearSelection(); }
    });
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
