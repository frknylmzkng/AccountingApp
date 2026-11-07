namespace WinFormsApp2;

public partial class Search : SessionForm
{
    private readonly ListPager pager;
    private string filterTc = "", filterFirst = "", filterLast = "";
    public Search()
    {
        InitializeComponent(); ArrangeSearch(); RecordSelection.Configure(dataGridViewAramaSonuclari);
        PersonnelCardUi.Attach(this, dataGridViewAramaSonuclari);
        pager = ListPager.Attach(dataGridViewAramaSonuclari, this, ReloadPageAsync);
        RefreshAction = SearchAsync; ClearAction = () => { txtTC.Clear(); txtAd.Clear(); txtSoyad.Clear(); txtTC.Focus(); };
    }
    private async Task SearchAsync()
    {
        filterTc = txtTC.Text.Trim(); filterFirst = txtAd.Text.Trim(); filterLast = txtSoyad.Text.Trim();
        pager.Reset(); await ReloadPageAsync();
    }
    private async Task ReloadPageAsync() => await RunOperationAsync(async token =>
    {
        var result = await People.PageAsync(null, pager.Index, pager.SizeLimit, token, filterTc, filterFirst, filterLast);
        if (!IsDisposed && !token.IsCancellationRequested)
        {
            dataGridViewAramaSonuclari.DataSource = result.Rows; dataGridViewAramaSonuclari.ClearSelection();
            pager.Apply(result);
            if (searchResults != null) searchResults.Text = $"Arama sonuçları — {result.Total:N0} personel";
        }
    });
    protected override void RefreshEditState() => pager?.UpdateState();
    private async void Ara_Click(object sender, EventArgs e) => await SearchAsync();
    private void back_Click(object sender, EventArgs e) => RequestBack();
}
