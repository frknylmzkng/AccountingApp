using System.Data;

namespace WinFormsApp2;

internal sealed record PersonnelChoice(Guid Id, PersonnelGroup Group, long Tc, byte[] Version, string Caption);
internal sealed class PersonnelPickerDialog : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill };
    private readonly TextBox tc = new() { Width = 130, MaxLength = 11 }, first = new() { Width = 170, MaxLength = 100 }, last = new() { Width = 170, MaxLength = 100 };
    private readonly ListPager pager;
    private string filterTc = "", filterFirst = "", filterLast = "";
    internal PersonnelChoice? Selected { get; private set; }
    internal bool SessionInvalidated { get; private set; }
    internal PersonnelPickerDialog(UserSession user)
    {
        Session = user; Text = "Personel seç — Arama"; ClientSize = new(1000, 600); PersistGridLayout = false;
        RecordSelection.Configure(grid);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(8), WrapContents = true };
        Button search = new() { Text = "Ara", AutoSize = true }, choose = new() { Text = "Seç", AutoSize = true }, cancel = new() { Text = "Vazgeç", AutoSize = true };
        foreach (var (label, control) in new[] { ("TC", tc), ("Ad", first), ("Soyad", last) })
        { filters.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new(4, 7, 4, 0) }); filters.Controls.Add(control); }
        filters.Controls.AddRange([search, choose, cancel]);
        var records = new Panel { Dock = DockStyle.Fill }; records.Controls.Add(grid); Controls.Add(records); Controls.Add(filters);
        pager = ListPager.Attach(grid, this, ReloadAsync);
        search.Click += async (_, _) =>
        { filterTc = tc.Text.Trim(); filterFirst = first.Text.Trim(); filterLast = last.Text.Trim(); pager.Reset(); await ReloadAsync(); };
        choose.Click += async (_, _) =>
        {
            if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].DataBoundItem is not DataRowView row) return;
            var snapshot = new PersonnelChoice(row.Row.Field<Guid>("PersonId", DataRowVersion.Original),
                row["GRUP"].ToString() == "Joker" ? PersonnelGroup.Joker : PersonnelGroup.General, row.Row.Field<long>("TC", DataRowVersion.Original),
                ((byte[])row.Row["RecordVersion", DataRowVersion.Original]).ToArray(), row["AD"] + " " + row["SOYAD"] + " · " + row["GRUP"]);
            if (await RunOperationAsync(token => AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.GlobalRead, token)))
            { Selected = snapshot; DialogResult = DialogResult.OK; Close(); }
        };
        cancel.Click += (_, _) => { if (!IsOperationRunning) Close(); };
        Load += async (_, _) => { MinimumSize = LogicalToDeviceUnits(new Size(650, 460)); await ReloadAsync(); };
        SessionRevoked += (_, _) => { SessionInvalidated = true; Close(); }; PageRequested += (_, _) => Close();
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var result = await People.PageAsync(null, pager.Index, pager.SizeLimit, token, filterTc, filterFirst, filterLast, includeLeaves: false);
        if (!IsDisposed && !token.IsCancellationRequested) { grid.DataSource = result.Rows; grid.ClearSelection(); pager.Apply(result); }
    });
    protected override void RefreshEditState() => pager?.UpdateState();
}
