using System.Data;

namespace WinFormsApp2;

internal static class PersonnelCardUi
{
    internal static async Task OpenAsync(SessionForm owner, DataGridView grid, PersonnelGroup? group = null)
    {
        if (owner.IsOperationRunning || !owner.ConfirmDiscardChanges("Personel kartını açma")) return;
        if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].DataBoundItem is not DataRowView view) { MessageBox.Show(owner, "Önce bir personel satırı seçin."); return; }
        var row = view.Row;
        if (!row.HasVersion(DataRowVersion.Original) || !row.Table.Columns.Contains("PersonId")) { MessageBox.Show(owner, "Personel kimliği eksik; listeyi yenileyin."); return; }
        Guid person = row.Field<Guid>("PersonId", DataRowVersion.Original);
        PersonnelGroup selected = group ?? (row["GRUP", DataRowVersion.Original].ToString() == "Joker" ? PersonnelGroup.Joker : PersonnelGroup.General);
        using var dialog = new PersonnelCardForm(selected, person, owner.User);
        dialog.SessionRevoked += (_, _) => owner.ForwardChildSessionRevoked(); dialog.ShowDialog(owner);
        if (!owner.IsDisposed) await owner.RefreshAfterCardAsync();
    }
    internal static void Attach(SessionForm owner, DataGridView grid, PersonnelGroup? group = null)
    {
        var menu = grid.ContextMenuStrip;
        bool own = menu == null; menu ??= new ContextMenuStrip(); grid.ContextMenuStrip = menu;
        var item = new ToolStripMenuItem("Seçili personel kartını aç…"); menu.Items.Add(item);
        menu.Opening += (_, _) => item.Enabled = !owner.IsOperationRunning && grid.SelectedRows.Count == 1;
        item.Click += async (_, _) => await OpenAsync(owner, grid, group);
        if (own) owner.Disposed += (_, _) => menu.Dispose();
    }
}

internal sealed class PersonnelCardDirectoryForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, Name = "CardDirectory", AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly TextBox tc = new() { Width = 150, MaxLength = 11 }, first = new() { Width = 150, MaxLength = 100 }, last = new() { Width = 150, MaxLength = 100 };
    private readonly CheckBox archived = new() { Text = "Arşiv dahil", AutoSize = true, Checked = true };
    private readonly ListPager pager;
    private string filterTc = "", filterFirst = "", filterLast = ""; private bool includeArchived = true;
    internal PersonnelCardDirectoryForm(UserSession user)
    {
        Session = user; PersistGridLayout = false; Text = "Muhasebe — Personel kartı ara"; ClientSize = new(1100, 640); RecordSelection.Configure(grid);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new(8) };
        foreach (var (caption, field) in new[] { ("TC", tc), ("Ad", first), ("Soyad", last) }) { bar.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new(4, 8, 4, 0) }); bar.Controls.Add(field); }
        var search = new Button { Text = "Ara", AutoSize = true }; var open = new Button { Text = "Seçili kartı aç…", AutoSize = true }; var close = new Button { Text = "Kapat", AutoSize = true };
        bar.Controls.AddRange([archived, search, open, close]); Controls.Add(grid); Controls.Add(bar); pager = ListPager.Attach(grid, this, ReloadAsync);
        search.Click += async (_, _) => { filterTc = tc.Text.Trim(); filterFirst = first.Text.Trim(); filterLast = last.Text.Trim(); includeArchived = archived.Checked; pager.Reset(); await ReloadAsync(); };
        open.Click += async (_, _) => await PersonnelCardUi.OpenAsync(this, grid); close.Click += (_, _) => Close(); PersonnelCardUi.Attach(this, grid);
        Load += async (_, _) => await ReloadAsync(); RefreshAction = ReloadAsync; PageRequested += (_, _) => Close(); SessionRevoked += (_, _) => Close();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(700, 450)); }
    private async Task ReloadAsync() => await RunOperationAsync(async ct =>
    {
        var page = await People.PageAsync(null, pager.Index, pager.SizeLimit, ct, filterTc, filterFirst, filterLast, includeArchived: includeArchived, includeLeaves: false);
        if (IsDisposed) return;
        page.Rows.Columns.Add("KartDurumu"); foreach (DataRow row in page.Rows.Rows) row["KartDurumu"] = row.IsNull("ArchivedUtc") ? "Etkin" : "Arşivde"; page.Rows.AcceptChanges();
        grid.DataSource = page.Rows; grid.ClearSelection(); pager.Apply(page);
        foreach (string name in new[] { "IZINBASLANGIC", "IZINBITIS", "IzinSuresi" }) if (grid.Columns.Contains(name)) grid.Columns[name]!.Visible = false;
        foreach (var (name, caption) in new[] { ("TC", "TC"), ("AD", "Ad"), ("SOYAD", "Soyad"), ("IBAN", "IBAN"), ("PARA", "Güncel bakiye TL"), ("GRUP", "Grup") }) if (grid.Columns.Contains(name)) grid.Columns[name]!.HeaderText = caption;
    });
    protected override void RefreshEditState() => pager?.UpdateState();
}
