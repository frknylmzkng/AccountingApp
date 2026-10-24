using System.Data;

namespace WinFormsApp2;

internal sealed class LeavePlanForm : SessionForm
{
    private readonly DataGridView grid = new() { Name = "LeavePlan", Dock = DockStyle.Fill };
    private readonly DateTimePicker start = new() { Width = 140, Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker end = new() { Width = 140, Format = DateTimePickerFormat.Short };
    public LeavePlanForm()
    {
        Text = "İzin planı · tüm seçili personel tek işlemde"; ClientSize = new Size(1000, 600); RecordSelection.Configure(grid); grid.MultiSelect = true;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        var save = new Button { Text = "Seçili planı onayla", AutoSize = true };
        bar.Controls.AddRange([new Label { Text = "Ctrl ile personel seçin. Başlangıç / bitiş:", AutoSize = true }, start, end, save]); Controls.Add(grid); Controls.Add(bar); EditControls.Add(save);
        RefreshAction = ReloadAsync; Load += async (_, _) => { if (!PreviewOnly) await ReloadAsync(); }; save.Click += Save;
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var data = await People.ListAsync(PersonnelGroup.General, token);
        if (!IsDisposed && !token.IsCancellationRequested) { grid.DataSource = data; grid.ClearSelection(); }
    });
    private async void Save(object? sender, EventArgs e)
    {
        var selected = grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem).OfType<DataRowView>().Where(v => v.Row.HasVersion(DataRowVersion.Original)).Select(v => Convert.ToInt64(v.Row["TC", DataRowVersion.Original])).Distinct().ToArray();
        if (selected.Length == 0) return;
        if (MessageBox.Show(this, $"{selected.Length} personelin izin dönemi {start.Value:dd.MM.yyyy}–{end.Value:dd.MM.yyyy} olarak kaydedilsin mi? Bir kayıt hata verirse planın tamamı geri alınır.", "Plan onayı", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var plan = selected.Select(tc => (tc, new LeavePeriod(start.Value.Date, end.Value.Date))).ToArray();
        if (await RunOperationAsync(token => People.SaveLeavesAsync(plan, token), "İzin planı kaydedildi.") && !IsDisposed) await ReloadAsync();
    }
}
