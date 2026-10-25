namespace WinFormsApp2
{
    public enum AppPage { Login, Dashboard, General, Joker, Search, Summary, Contact, Admin, Payments, Settings, LeavePlan, PasswordChange }

    internal sealed class PageRequestedEventArgs : EventArgs
    {
        public AppPage Page { get; }
        public PageRequestedEventArgs(AppPage page) => Page = page;
    }

    internal sealed class LoginSucceededEventArgs : EventArgs
    {
        public UserSession Session { get; }
        public string Username => Session.Username;
        public UserRole Role => Session.Role;
        public LoginSucceededEventArgs(string username, UserRole role = UserRole.Admin) : this(new UserSession(username, role)) { }
        public LoginSucceededEventArgs(UserSession session) { Session = session; }
    }

    public class SessionForm : Form
    {
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal UserSession? Session { get; set; }
        internal UserSession User => Session ?? throw new UnauthorizedAccessException("Etkin oturum gerekiyor.");
        internal PersonnelService People => new(AppDatabase.ConnectionString, User);
        protected CancellationToken Lifetime => lifetime.Token;
        private readonly CancellationTokenSource lifetime = new();
        private System.Windows.Forms.Timer? clock;
        private bool busy;
        internal bool IsOperationRunning => busy;
        private bool resourcesDisposed;
        private bool chromeInitialized;
        internal bool PersistGridLayout = true;
        internal bool PreviewOnly;
        protected virtual bool ShowNavigationTools => true;
        protected virtual bool CompactNavigation => false;
        internal string? LogoutNotice { get; private set; }
        private ToolStripStatusLabel? hint;
        protected Func<Task>? RefreshAction { get; set; }
        protected Action? ClearAction { get; set; }
        protected Action? DeleteAction { get; set; }
        protected List<Control> EditControls { get; } = [];
        internal void SetHint(string text) { if (hint != null && !IsDisposed) hint.Text = text; }

        protected override void OnLoad(EventArgs e) { InitializeChrome(); base.OnLoad(e); }
        internal void InitializeChrome()
        {
            if (!DesignMode && !chromeInitialized)
            {
                chromeInitialized = true;
                KeyPreview = true; FormBorderStyle = FormBorderStyle.Sizable; StartPosition = FormStartPosition.CenterScreen;
                Size original = ClientSize;
                var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = original };
                var existing = Controls.Cast<Control>().ToArray(); Controls.Clear(); content.Controls.AddRange(existing);
                Controls.Add(content);
                var tools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(5), AutoSize = true };
                void Tool(string label, Action action) { var button = new ToolStripButton(label); button.Click += (_, _) => action(); tools.Items.Add(button); }
                if (ShowNavigationTools && CompactNavigation)
                {
                    if (Session?.MustChangePassword != true) Tool("Ana ekran", RequestBack);
                    Tool("Çıkış", () => RequestLogout());
                }
                else if (ShowNavigationTools)
                {
                Tool("Ana ekran", RequestBack); Tool("Parolamı değiştir", () => RequestPage(AppPage.PasswordChange)); Tool("Yenile · F5", () => _ = RefreshScreenAsync());
                Tool("Tema", () => { var p = UserPreferences.Load(); p.DarkTheme = !p.DarkTheme; p.Save(); AppTheme.Apply(this); });
                Tool("Excel", () => Export(true)); Tool("CSV", () => Export(false)); Tool("Kolonlar", ShowColumns);
                Tool("Ödemeler", () => RequestPage(AppPage.Payments)); Tool("İzin planı", () => RequestPage(AppPage.LeavePlan));
                if (Session?.Role == UserRole.Admin) Tool("Ayarlar / yedek", () => RequestPage(AppPage.Settings));
                Tool("Çıkış", () => RequestLogout());
                }
                if (ShowNavigationTools) Controls.Add(tools);
                var status = new StatusStrip { Dock = DockStyle.Bottom };
                var who = new ToolStripStatusLabel(Session == null ? "Oturum yok" : $"{User.Username} · {User.RoleLabel}");
                hint = new ToolStripStatusLabel("F1 yardım · F2 temizle · F5 yenile · Esc geri") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
                var time = new ToolStripStatusLabel(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")); status.Items.AddRange([who, hint, time]); Controls.Add(status);
                ClientSize = new Size(original.Width, original.Height + (ShowNavigationTools ? tools.PreferredSize.Height : 0) + status.Height);
                MinimumSize = new Size(Math.Min(Width, 800), Math.Min(Height, 600));
                clock = new System.Windows.Forms.Timer { Interval = 1000 }; clock.Tick += (_, _) => time.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"); clock.Start();
                foreach (var control in EditControls) control.Enabled = Session?.CanEdit == true;
                foreach (var grid in AppTheme.Descendants(content).OfType<DataGridView>())
                    grid.DataBindingComplete += (_, _) => GridPreferences.Restore(grid);
                AppTheme.Apply(this);
            }
        }

        protected async Task<bool> RunOperationAsync(Func<CancellationToken, Task> operation, string? success = null)
        {
            if (busy || IsDisposed || lifetime.IsCancellationRequested) return false;
            busy = true; if (hint != null) hint.Text = "İşlem sürüyor…";
            var controls = AppTheme.Descendants(this).Where(c => c is Button or TextBox or ComboBox or DateTimePicker or DataGridView or ToolStrip).ToDictionary(c => c, c => c.Enabled);
            bool sessionRevoked = false;
            foreach (var control in controls.Keys) control.Enabled = false;
            try
            {
                await operation(Lifetime);
                if (!IsDisposed && !lifetime.IsCancellationRequested && hint != null) hint.Text = success ?? "Hazır";
                return true;
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return false; }
            catch (SessionRevokedException)
            {
                sessionRevoked = true;
                return false;
            }
            catch (Exception ex)
            {
                string id = AppLog.Failure(ex, "UI.Operation");
                if (!IsDisposed && !lifetime.IsCancellationRequested)
                {
                    string message = ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException or DatabaseConfigurationException ? ex.Message : "İşlem tamamlanamadı. Listeyi ve işlem geçmişini kontrol edin. Kod: " + id;
                    MessageBox.Show(this, message, "İşlem tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }
            finally
            {
                busy = false;
                if (!IsDisposed) foreach (var pair in controls) if (!pair.Key.IsDisposed) pair.Key.Enabled = pair.Value;
                if (!IsDisposed) foreach (var control in EditControls) control.Enabled = Session?.CanEdit == true && control.Enabled;
                if (!IsDisposed) RefreshEditState();
                if (sessionRevoked && !IsDisposed) NotifySessionRevoked();
            }
        }
        protected async Task RefreshScreenAsync() { if (RefreshAction != null) await RefreshAction(); }
        protected virtual void RefreshEditState() { }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F1) { MessageBox.Show(this, "F2: alanları temizle / yeni kayıt\nF5: listeyi yenile\nDelete: seçili kaydı onayla sil\nEsc: ana ekran\nF6/F7: personel formu taslağını sakla / geri yükle\nTema ve kolonlar kullanıcıya özel saklanır. Ödemeler kalan tutarı azaltır; ödeme iptali yönetici yetkisi ister.", "Kullanım"); return true; }
            if (!busy)
            {
                if (keyData == Keys.Escape) { RequestBack(); return true; }
                if (keyData == Keys.F5) { _ = RefreshScreenAsync(); return true; }
                if (keyData == Keys.F2) { ClearAction?.Invoke(); return true; }
                if (keyData == Keys.Delete && ActiveControl is not TextBox && Session?.CanEdit == true) { DeleteAction?.Invoke(); return true; }
                if ((this is Genel or Joker) && keyData is Keys.F6 or Keys.F7) { Draft(keyData == Keys.F6); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private async void Draft(bool save)
        {
            if (!await RunOperationAsync(async token =>
            {
                User.DemandEdit();
                await AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.Edit, token);
            }) || IsDisposed) return;
            try
            {
                User.DemandEdit();
                string scope = User.Username + "|" + AppDatabase.ConnectionString + "|" + GetType().Name;
                string filename = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope))) + ".draft";
                string path = Path.Combine(UserPreferences.DirectoryPath, "drafts", filename);
                var fields = AppTheme.Descendants(this).Where(c => c is TextBox or DateTimePicker).ToArray();
                if (save)
                {
                    var values = fields.ToDictionary(c => c.Name, c => c is DateTimePicker date ? date.Value.ToString("O") : c.Text);
                    var selectedGrid = AppTheme.Descendants(this).OfType<DataGridView>().FirstOrDefault();
                    if (selectedGrid != null && RecordSelection.TryGetTc(selectedGrid, out _)) values["__OriginalVersion"] = Convert.ToBase64String(RecordSelection.OriginalVersion(selectedGrid));
                    DraftStore.Save(path, scope, values);
                }
                else
                {
                    var values = DraftStore.Load<Dictionary<string, string>>(path, scope) ?? [];
                    ClearAction?.Invoke();
                    // Never attach stale draft fields to a freshly loaded version of the record.
                    var grid = AppTheme.Descendants(this).OfType<DataGridView>().FirstOrDefault();
                    string tc = values.GetValueOrDefault("TC_txt", "");
                    if (grid != null && !RecordSelection.RestoreDraftSelection(grid, tc, values.GetValueOrDefault("__OriginalVersion")) &&
                        (values.ContainsKey("__OriginalVersion") || grid.Rows.Cast<DataGridViewRow>().Any(r => r.DataBoundItem is System.Data.DataRowView bound && bound.Row["TC"].ToString() == tc)))
                    {
                        MessageBox.Show(this, "Taslağın kayıt sürümü güncel listeyle eşleşmiyor. Taslak alanları yüklenecek; mevcut kayıt seçilmedi ve değiştirilmeyecek. Düzenlemek için güncel kaydı yeniden seçip değişikliklerinizi kontrol edin.", "Taslak kontrolü");
                    }
                    foreach (var field in fields) if (values.TryGetValue(field.Name, out string? value))
                    { if (field is DateTimePicker date && DateTime.TryParse(value, out DateTime parsed)) date.Value = parsed; else field.Text = value; }
                }
                if (hint != null) hint.Text = save ? "Taslak bu Windows kullanıcısı için şifrelenerek saklandı." : "Taslak yüklendi; kayıt işlemi yapılmadı.";
            }
            catch (Exception ex) { AppLog.Failure(ex, "Draft"); MessageBox.Show(this, "Taslak saklanamadı veya bu oturum için okunamadı."); }
        }
        private void ShowColumns()
        {
            var grid = AppTheme.Descendants(this).OfType<DataGridView>().FirstOrDefault(); if (grid == null) return;
            var menu = new ContextMenuStrip();
            foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>().Where(c => !RecordSelection.IsInternalColumn(c)))
            {
                var item = new ToolStripMenuItem(column.HeaderText) { Checked = column.Visible, CheckOnClick = true };
                item.CheckedChanged += (_, _) => { column.Visible = item.Checked; GridPreferences.Save(grid); }; menu.Items.Add(item);
            }
            menu.Closed += (_, _) => menu.Dispose(); menu.Show(Cursor.Position);
        }
        private async void Export(bool excel)
        {
            var grid = AppTheme.Descendants(this).OfType<DataGridView>().FirstOrDefault(); if (grid == null || grid.Rows.Count == 0) return;
            using var dialog = new SaveFileDialog { Filter = excel ? "Excel|*.xlsx" : "CSV|*.csv", FileName = GetType().Name + "-" + DateTime.Today.ToString("yyyyMMdd") };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!await RunOperationAsync(token => AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.Read, token)) || IsDisposed) return;
            try { var table = ReportExport.Snapshot(grid); if (excel) ReportExport.Excel(table, dialog.FileName); else ReportExport.Csv(table, dialog.FileName); ReportExport.Open(dialog.FileName); }
            catch (Exception ex) { AppLog.Failure(ex, "Export"); MessageBox.Show(this, "Dosya yazılamadı veya açılamadı."); }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            lifetime.Cancel(); base.OnFormClosed(e);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (busy && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                SetHint("İşlem sürüyor. Tamamlandıktan sonra pencereyi kapatabilirsiniz.");
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                lifetime.Cancel(); clock?.Dispose();
                foreach (var grid in AppTheme.Descendants(this).OfType<DataGridView>().Where(_ => chromeInitialized && PersistGridLayout))
                    try { GridPreferences.Save(grid); } catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { AppLog.Event("Grid.SaveUnavailable"); }
                lifetime.Dispose();
            }
            base.Dispose(disposing);
        }
        internal event EventHandler<PageRequestedEventArgs>? PageRequested;
        internal event EventHandler? LogoutRequested;
        internal event EventHandler? SessionRevoked;

        protected void RequestPage(AppPage page) { if (!busy) PageRequested?.Invoke(this, new PageRequestedEventArgs(page)); }
        protected void RequestBack() => RequestPage(AppPage.Dashboard);
        protected void RequestLogout(string? notice = null) { if (!busy) { LogoutNotice = notice; LogoutRequested?.Invoke(this, EventArgs.Empty); } }
        protected void NotifySessionRevoked() => SessionRevoked?.Invoke(this, EventArgs.Empty);
    }
}
