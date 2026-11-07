namespace WinFormsApp2
{
    public enum AppPage { Login, Dashboard, General, Joker, Search, Summary, Contact, Admin, Payments, Settings, LeavePlan, PasswordChange, ProjectFinance, DailyClosings, ProjectWorkforce, ProjectPortal }

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
        private UnsavedChanges? edits;
        private bool applicationClose;
        protected void TrackChanges(string group, params Control[] controls) { (edits ??= new UnsavedChanges(this)).Track(group, controls); }
        protected void GuardRecordSelection(DataGridView grid) => edits?.GuardSelection(grid);
        protected void AcceptTrackedChanges(string? group = null) => edits?.Accept(group);
        protected void MarkTrackedChanges() => edits?.DraftLoaded();
        internal bool HasUnsavedChanges => edits?.HasChanges() == true;
        internal bool ConfirmDiscardChanges(string action, string? except = null) => PreviewOnly || edits?.Confirm(action, except) != false;
        internal void PermitApplicationClose() => applicationClose = true;
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
                var existing = Controls.Cast<Control>().ToArray();
                var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = existing.Length == 1 && existing[0].Dock == DockStyle.Fill ? Size.Empty : original };
                Controls.Clear(); content.Controls.AddRange(existing);
                Controls.Add(content);
                var tools = CreateNavigationMenu();
                if (ShowNavigationTools) { Controls.Add(tools); MainMenuStrip = tools; }
                var status = new StatusStrip { Dock = DockStyle.Bottom };
                var who = new ToolStripStatusLabel(Session == null ? "Oturum yok" : $"{User.Username} · {User.RoleLabel}");
                hint = new ToolStripStatusLabel("F1 yardım · F2 temizle · F5 yenile · Esc geri") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
                var time = new ToolStripStatusLabel(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")); status.Items.AddRange([who, hint, time]); Controls.Add(status);
                ClientSize = new Size(original.Width, original.Height + (ShowNavigationTools ? tools.PreferredSize.Height : 0) + status.Height);
                if (!ShowNavigationTools) tools.Dispose();
                MinimumSize = new Size(Math.Min(Width, 800), Math.Min(Height, 600));
                clock = new System.Windows.Forms.Timer { Interval = 1000 }; clock.Tick += (_, _) => time.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"); clock.Start();
                foreach (var control in EditControls) control.Enabled = Session?.CanEdit == true;
                foreach (var grid in AppTheme.Descendants(content).OfType<DataGridView>())
                    grid.DataBindingComplete += (_, _) => GridPreferences.Restore(grid);
                AppTheme.Apply(this);
            }
        }

        private MenuStrip CreateNavigationMenu()
        {
            var menu = new MenuStrip { Dock = DockStyle.Top, Padding = new Padding(4, 2, 4, 2), AutoSize = true };
            ToolStripMenuItem Item(string text, Action action, Keys shortcut = Keys.None)
            {
                var item = new ToolStripMenuItem(text) { ShortcutKeys = shortcut };
                item.Click += (_, _) => action(); return item;
            }
            if (!ShowNavigationTools) return menu;
            if (CompactNavigation)
            {
                menu.Items.Add(Item("Oturumu kilitle", () => LockRequested?.Invoke(this, EventArgs.Empty), Keys.Control | Keys.Shift | Keys.L));
                if (Session?.MustChangePassword != true) menu.Items.Add(Item("&Ana ekran", RequestBack));
                menu.Items.Add(Item("Çı&kış", () => RequestLogout())); return menu;
            }
            var file = new ToolStripMenuItem("&Dosya");
            file.DropDownItems.Add(Item("&Ana ekran", RequestBack, Keys.Control | Keys.Home));
            file.DropDownItems.Add(Item("&Yenile", () => _ = RefreshScreenAsync(), Keys.F5));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Item("&Parolamı değiştir…", () => RequestPage(AppPage.PasswordChange)));
            file.DropDownItems.Add(Item("Oturumu &kilitle", () => LockRequested?.Invoke(this, EventArgs.Empty), Keys.Control | Keys.Shift | Keys.L));
            if (Session?.Role == UserRole.Admin) file.DropDownItems.Add(Item("Ayarlar ve &yedekleme…", () => RequestPage(AppPage.Settings)));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Item("Çı&kış", () => RequestLogout(), Keys.Control | Keys.Shift | Keys.Q));
            var actions = new ToolStripMenuItem("&İşlemler");
            foreach (var (label, page) in new[] { ("Genel personel", AppPage.General), ("Joker personel", AppPage.Joker), ("Kişi ara", AppPage.Search), ("Ödemeler", AppPage.Payments), ("İzin planı", AppPage.LeavePlan), ("Genel durum", AppPage.Summary) })
                actions.DropDownItems.Add(Item(label, () => RequestPage(page)));
            if (Session?.Role == UserRole.Admin) actions.DropDownItems.Add(Item("Yönetici hesapları", () => RequestPage(AppPage.Admin)));
            actions.DropDownItems.Add(Item("Personel kartı ara…", async () =>
            {
                if(!ConfirmDiscardChanges("Personel kartı arama"))return;
                using var dialog=new PersonnelCardDirectoryForm(User);dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);
                if(!IsDisposed)await RefreshAfterCardAsync();
            }));
            actions.DropDownItems.Add(Item("Hakediş, avans ve proje maliyeti", () => RequestPage(AppPage.ProjectFinance)));
            actions.DropDownItems.Add(Item("Projeler, ekipler ve tarihli atamalar", () => RequestPage(AppPage.ProjectWorkforce)));
            actions.DropDownItems.Add(Item("Kesinleşmiş günlük kapanışlar", () => RequestPage(AppPage.DailyClosings)));
            var reports = new ToolStripMenuItem("&Rapor");
            var excelExport = Item("Excel'e aktar…", () => Export(true));
            var csvExport = Item("CSV'ye aktar…", () => Export(false));
            excelExport.Enabled = csvExport.Enabled = Session?.CanExportPersonalData == true;
            reports.DropDownItems.Add(excelExport); reports.DropDownItems.Add(csvExport);
            if (Session?.Role == UserRole.Admin) reports.DropDownItems.Add(Item("İşlem geçmişi…", () =>
            {
                using var dialog = new AuditHistoryForm(User);
                dialog.SessionRevoked += (_, _) => NotifySessionRevoked();
                dialog.ShowDialog(this);
            }));
            var view = new ToolStripMenuItem("&Görünüm");
            foreach (var (label, classic, dark) in new[] { ("Windows 2000 · Klasik", true, false), ("Açık tema", false, false), ("Koyu tema", false, true) })
            {
                var choice = Item(label, () =>
                {
                    var p = UserPreferences.Load(); p.ClassicTheme = classic; p.DarkTheme = dark; p.Save();
                    foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) AppTheme.Apply(form);
                });
                view.DropDownItems.Add(choice);
                view.DropDownOpening += (_, _) => { var p = UserPreferences.Load(); choice.Checked = p.ClassicTheme == classic && (classic || p.DarkTheme == dark); };
            }
            view.DropDownItems.Add(new ToolStripSeparator()); view.DropDownItems.Add(Item("Kolonları seç…", ShowColumns));
            var help = new ToolStripMenuItem("&Yardım");
            help.DropDownItems.Add(Item("Hakkında ve sürüm bilgisi…", () => { using var dialog = new AboutForm(User); dialog.SessionRevoked += (_, _) => NotifySessionRevoked(); dialog.ShowDialog(this); }));
            help.DropDownItems.Add(Item("Klavye kısayolları", () => MessageBox.Show(this, "F2: Alanları temizle\nF5: Listeyi yenile\nF6 / F7: Personel taslağını kaydet / yükle\nEsc: Ana ekrana dön\nCtrl+Shift+Q: Oturumu kapat", "Klavye kısayolları", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            menu.Items.AddRange([file, actions, reports, view, help]); return menu;
        }

        protected async Task<bool> RunChangeOperationAsync(Func<CancellationToken, Task> operation, string? success = null, string? savedGroup = null)
        {
            if (IsOperationRunning || IsDisposed || !SessionUiState.CanInteract) return false;
            using var dialog = new ChangeReasonDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK || IsDisposed) return false;
            using var scope = AuditContext.UseReason(dialog.Reason);
            bool done = await RunOperationAsync(operation, success);
            if (done && savedGroup != null) AcceptTrackedChanges(savedGroup);
            return done;
        }
        protected async Task<bool> RunOperationAsync(Func<CancellationToken, Task> operation, string? success = null)
        {
            if (busy || IsDisposed || lifetime.IsCancellationRequested || !SessionUiState.CanInteract) return false;
            var operationToken = Lifetime;
            busy = true; if (hint != null) hint.Text = "İşlem sürüyor…";
            var controls = AppTheme.Descendants(this).Where(c => c is Button or TextBox or ComboBox or DateTimePicker or DataGridView or ToolStrip).ToDictionary(c => c, c => c.Enabled);
            bool sessionRevoked = false;
            foreach (var control in controls.Keys) control.Enabled = false;
            try
            {
                using (SessionUiState.Operation()) await operation(operationToken);
                await SessionUiState.WaitAsync(operationToken);
                operationToken.ThrowIfCancellationRequested();
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
                try { await SessionUiState.WaitAsync(operationToken); }
                catch (OperationCanceledException) when (operationToken.IsCancellationRequested) { return false; }
                if (!IsDisposed && !lifetime.IsCancellationRequested)
                {
                    string message = ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException or DatabaseConfigurationException or BackupMirrorException ? ex.Message : "İşlem tamamlanamadı. Listeyi ve işlem geçmişini kontrol edin. Kod: " + id;
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
        protected async Task RefreshScreenAsync()
        {
            if (SessionUiState.CanInteract && ConfirmDiscardChanges("Ekranı yenileme") && RefreshAction != null)
            { await RefreshAction(); }
        }
        internal Task RefreshAfterHistoryAsync() => RefreshScreenAsync();
        // Card navigation already obtained discard consent before opening.
        internal Task RefreshAfterCardAsync() => RefreshAction?.Invoke() ?? Task.CompletedTask;
        protected virtual void RefreshEditState() { }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F1) { MessageBox.Show(this, "F2: alanları temizle / yeni kayıt\nF5: listeyi yenile\nDelete: seçili kaydı onayla sil\nEsc: ana ekran\nF6/F7: personel formu taslağını sakla / geri yükle\nTema ve kolonlar kullanıcıya özel saklanır. Ödemeler kalan tutarı azaltır; ödeme iptali yönetici yetkisi ister.", "Kullanım"); return true; }
            if (!busy)
            {
                if (keyData == Keys.Escape) { RequestBack(); return true; }
                if (keyData == Keys.F5) { _ = RefreshScreenAsync(); return true; }
                if (keyData == Keys.F2) { if (ClearAction != null && ConfirmDiscardChanges("Alanları temizleme")) { ClearAction.Invoke(); AcceptTrackedChanges(); } return true; }
                if (keyData == Keys.Delete && ActiveControl is not TextBox && Session?.CanEdit == true) { DeleteAction?.Invoke(); return true; }
                if ((this is Genel or Joker) && keyData is Keys.F6 or Keys.F7) { Draft(keyData == Keys.F6); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private async void Draft(bool save)
        {
            if (!save && !ConfirmDiscardChanges("Taslağı yükleme")) return;
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
                var fields = AppTheme.Descendants(this).Where(c => c is DateTimePicker || c is TextBox text && !text.UseSystemPasswordChar && text.PasswordChar == '\0').Where(c => (User.CanViewIban || c.Name != "IBAN_txt") && (User.CanViewSalary || c.Name != "money_txt")).ToArray();
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
                if (!save) edits?.DraftLoaded();
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
        private protected virtual IReadOnlyList<ExportReportChoice> ExportReports()
        {
            string Caption(DataGridView grid)
            {
                for (Control? parent = grid.Parent; parent != null && parent != this; parent = parent.Parent)
                    if (parent is GroupBox or TabPage && !string.IsNullOrWhiteSpace(parent.Text)) return parent.Text;
                return !string.IsNullOrWhiteSpace(grid.AccessibleName) ? grid.AccessibleName : (string.IsNullOrWhiteSpace(grid.Name) ? Text : grid.Name);
            }
            return AppTheme.Descendants(this).OfType<DataGridView>().Where(g => g.Columns.Count > 0).Select(grid =>
                new ExportReportChoice(Caption(grid), Equals(grid.Tag, "AccountingApp.Paged")
                    ? "Ekranda yüklenmiş açık sayfa. Diğer sayfalar dahil değildir. Görünen sütunlar aktarılabilir; seçili satırlarla sınırlı değildir."
                    : "Bu tabloda yüklenmiş görünüm. Görünen sütunlar ve satırlar; diğer tablolar dahil değildir.",
                    _ => Task.FromResult(ReportExport.Snapshot(grid)))).ToArray();
        }
        private async void Export(bool excel)
        {
            if (!await RunOperationAsync(token => AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.FullDataExport, token)) || IsDisposed) return;
            var choices = ExportReports();
            if (choices.Count == 0) { MessageBox.Show(this, "Aktarılabilir rapor yok. İlgili listeyi veya raporu yükleyin."); return; }
            using var choose = new ExportReportDialog(choices);
            if (choose.ShowDialog(this) != DialogResult.OK || IsDisposed) return;
            var report = choose.Selected; System.Data.DataTable? loaded = null;
            if (!await RunOperationAsync(async token => loaded = await report.Load(token)) || loaded == null || IsDisposed) { loaded?.Dispose(); return; }
            using var data = loaded;
            using var columns = new ExportColumnsDialog(data);
            if (columns.ShowDialog(this) != DialogResult.OK || IsDisposed) return;
            var allowed = columns.Selected.ToHashSet(StringComparer.Ordinal);
            foreach (System.Data.DataColumn column in data.Columns.Cast<System.Data.DataColumn>().ToArray())
                if (!allowed.Contains(column.ColumnName)) data.Columns.Remove(column);
            data.ExtendedProperties["Export.ReportTitle"] = report.Title;
            data.ExtendedProperties["Export.ReportScope"] = report.Scope;
            string title = new string(report.Title.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').Take(70).ToArray()).Trim().Replace(' ', '-');
            using var dialog = new SaveFileDialog { Filter = excel ? "Excel|*.xlsx" : "CSV|*.csv", DefaultExt = excel ? "xlsx" : "csv", FileName = GetType().Name + "-" + title + "-" + DateTime.Today.ToString("yyyyMMdd") };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!await RunOperationAsync(token => new PersonalDataExportService(AppDatabase.ConnectionString, User).ExportAsync(data, dialog.FileName, excel, token), "Seçilen rapor dışa aktarıldı.") || IsDisposed) return;
            try { ReportExport.Open(dialog.FileName); }
            catch (Exception ex) { AppLog.Failure(ex, "Export.Open"); MessageBox.Show(this, "Dosya kaydedildi; otomatik açılamadı."); }
        }
        internal async Task CopyGridAsync(DataGridView grid)
        {
            if (grid.IsDisposed || grid.SelectedRows.Count == 0) return;
            await RunOperationAsync(async token =>
            {
                User.DemandFullDataExport();
                await using var work = User.Remote == null ? await SqlUnitOfWork.OpenAsync(AppDatabase.ConnectionString, token) : null;
                if (work != null) await AccountAuthorization.DemandAsync(work, User, AccountAccess.FullDataExport, token);
                if (IsDisposed || grid.IsDisposed) return;
                var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible && !RecordSelection.IsInternalColumn(c)).OrderBy(c => c.DisplayIndex).ToArray();
                var rows = grid.SelectedRows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).OrderBy(r => r.Index).ToArray();
                if (columns.Length == 0 || rows.Length == 0) return;
                string Header() => string.Join("\t", columns.Select(c => ReportExport.CsvCell(c.HeaderText)));
                string text = Header() + "\r\n" + string.Join("\r\n", rows.Select(r => string.Join("\t", columns.Select(c => ReportExport.CsvCell(r.Cells[c.Index].Value)))));
                if (User.Remote != null)
                    text = (await User.Remote.SendAsync<AccountingApp.Contracts.ClipboardText>("exports/clipboard", new AccountingApp.Contracts.ClipboardText(text), token)).Text;
                else await new PersonnelService(AppDatabase.ConnectionString, User).AuditAsync(work!, "Export.Clipboard", token);
                token.ThrowIfCancellationRequested();
                Clipboard.SetText(text);
                if (work != null) await work.CommitAsync(token);
            }, "Seçili satır panoya kopyalandı.");
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            lifetime.Cancel(); base.OnFormClosed(e);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!applicationClose && busy && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                SetHint("İşlem sürüyor. Tamamlandıktan sonra pencereyi kapatabilirsiniz.");
            }
            else if (!applicationClose && !e.Cancel && !ConfirmDiscardChanges("Pencereyi kapatma")) e.Cancel = true;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                edits?.Dispose();
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
        internal event EventHandler? LockRequested;

        protected void RequestPage(AppPage page) { if (!busy) PageRequested?.Invoke(this, new PageRequestedEventArgs(page)); }
        protected void RequestBack() => RequestPage(AppPage.Dashboard);
        protected void RequestLogout(string? notice = null) { if (!busy) { LogoutNotice = notice; LogoutRequested?.Invoke(this, EventArgs.Empty); } }
        protected void RequestSessionLock() => LockRequested?.Invoke(this, EventArgs.Empty);
        internal void ForwardChildSessionRevoked() => NotifySessionRevoked();
        protected void NotifySessionRevoked() => SessionRevoked?.Invoke(this, EventArgs.Empty);
    }
}
