using System.Data;

namespace WinFormsApp2;

internal sealed partial class LeavePlanForm : SessionForm
{
    private readonly ListPager pager;
    private readonly DataGridView grid = new() { Name = "LeavePlan", Dock = DockStyle.Fill };
    private readonly DateTimePicker start = new() { Width = 140, Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker end = new() { Width = 140, Format = DateTimePickerFormat.Short };
    public LeavePlanForm()
    {
        RecordSelection.Configure(grid); grid.MultiSelect = true;
        var save = new Button { Text = "Seçili planı onayla" };
        ArrangeLeavePlan(save); EditControls.Add(save);
        pager = ListPager.Attach(grid, this, ReloadAsync);
        TrackChanges("İzin planı", start, end, grid);
        RefreshAction = ReloadAsync; Load += async (_, _) => { if (!PreviewOnly) await ReloadAsync(); }; save.Click += Save;
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var page = await People.PageAsync(PersonnelGroup.General, pager.Index, pager.SizeLimit, token);
        if (!IsDisposed && !token.IsCancellationRequested) { grid.DataSource = page.Rows; grid.ClearSelection(); pager.Apply(page); }
    });
    protected override void RefreshEditState() { pager?.UpdateState(); start.Enabled = end.Enabled = Session?.CanEdit == true && !IsOperationRunning; }
    private async void Save(object? sender, EventArgs e)
    {
        LeaveTarget[] selected;
        try { selected = LeaveTarget.FromSelection(grid); }
        catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message); return; }
        if (selected.Length == 0) return;
        var leave = new LeavePeriod(start.Value.Date, end.Value.Date);
        if (MessageBox.Show(this, $"{selected.Length} personelin izin dönemi {leave.Start:dd.MM.yyyy}–{leave.End:dd.MM.yyyy} olarak kaydedilsin mi? Bir kayıt hata verirse planın tamamı geri alınır.", "Plan onayı", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var plan = selected.Select(target => (target, leave)).ToArray();
        if (await RunChangeOperationAsync(token => People.SaveLeavesAsync(plan, token), "İzin planı kaydedildi.", "İzin planı") && !IsDisposed) await ReloadAsync();
    }
}
