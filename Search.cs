namespace WinFormsApp2;

public partial class Search : SessionForm
{
    public Search()
    {
        InitializeComponent(); ArrangeSearch(); RecordSelection.Configure(dataGridViewAramaSonuclari);
        RefreshAction = SearchAsync; ClearAction = () => { txtTC.Clear(); txtAd.Clear(); txtSoyad.Clear(); txtTC.Focus(); };
    }
    private async Task SearchAsync() => await RunOperationAsync(async token =>
    {
        var table = await People.ListAsync(null, token, txtTC.Text.Trim(), txtAd.Text.Trim(), txtSoyad.Text.Trim());
        if (!IsDisposed && !token.IsCancellationRequested)
        {
            dataGridViewAramaSonuclari.DataSource = table;
            if (searchResults != null) searchResults.Text = $"Arama sonuçları — {table.Rows.Count} sonuç";
        }
    });
    private async void Ara_Click(object sender, EventArgs e) => await SearchAsync();
    private void back_Click(object sender, EventArgs e) => RequestBack();
}
