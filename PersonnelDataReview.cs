using System.Data;

namespace WinFormsApp2;

internal static class PersonnelDataReview
{
    internal static void Attach(SessionForm owner, DataGridView records, TextBox iban)
    {
        var tip = new ToolTip(); tip.SetToolTip(iban, IbanInput.Help);
        tip.SetToolTip(records, "Sağ tık: Veri düzeltme listesi veya Personel arşivi ve geçmişi.");
        var menu = new ContextMenuStrip();
        var review = new ToolStripMenuItem("Veri düzeltme listesi…"); menu.Items.Add(review);
        records.ContextMenuStrip = menu;
        var archive = new ToolStripMenuItem("Personel arşivi ve geçmişi…"); menu.Items.Add(archive);
        archive.Click += async (_, _) =>
        {
            using var history = new PersonnelHistoryForm(owner is Genel ? PersonnelGroup.General : PersonnelGroup.Joker, owner.User);
            history.ShowDialog(owner);
            if (!owner.IsDisposed) await owner.RefreshAfterHistoryAsync();
        };
        PersonnelCardUi.Attach(owner, records, owner is Genel ? PersonnelGroup.General : PersonnelGroup.Joker);
        review.Click += async (_, _) =>
        {
            try
            {
                owner.User.DemandViewIban();
                await AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, owner.User, AccountAccess.GlobalRead, CancellationToken.None);
                if (owner.IsDisposed || records.IsDisposed) return;
                using var data = Build(records.DataSource as DataTable);
                using var dialog = new Form
                {
                    Text = "Personel — Veri düzeltme listesi", StartPosition = FormStartPosition.CenterParent,
                    ClientSize = new(900, 480), MinimumSize = new(560, 350), ShowInTaskbar = false
                };
                var heading = new Label
                {
                    Dock = DockStyle.Top, Height = 78, Padding = new(12),
                    Text = $"Açık sayfadaki {data.Rows.Count} personel için kontrol gerekiyor. Diğer sayfalar bu kontrole dahil değildir. Güncellemek için F5 kullanın.\nBu ekran kayıtları değiştirmez. Personeli ana listeden seçip bilgilerini düzenleyin.\n" + IbanInput.Help
                };
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill, DataSource = data, ReadOnly = true, AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
                };
                RecordSelection.ConfigureClipboard(grid, owner);
                dialog.Controls.Add(grid); dialog.Controls.Add(heading); AppTheme.Apply(dialog);
                void FitHeading() => heading.Height = TextRenderer.MeasureText(heading.Text, heading.Font,
                    new Size(Math.Max(1, dialog.ClientSize.Width - heading.Padding.Horizontal), int.MaxValue),
                    TextFormatFlags.WordBreak).Height + heading.Padding.Vertical;
                dialog.ClientSizeChanged += (_, _) => FitHeading(); FitHeading();
                dialog.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                AppLog.Failure(ex, "Personnel.DataReview");
                if (!owner.IsDisposed) MessageBox.Show(owner, ex is UnauthorizedAccessException ? ex.Message : "Kontrol listesi açılamadı. Listeyi yenileyip tekrar deneyin.", "Veri kontrolü");
            }
        };
        records.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewRow row in records.Rows)
                if (row.DataBoundItem is DataRowView view) row.ErrorText = Issues(view.Row);
        };
        owner.Disposed += (_, _) => { tip.Dispose(); menu.Dispose(); };
    }

    private static string Issues(DataRow row)
    {
        List<string> issues = [];
        foreach (string column in new[] { "AD", "SOYAD" })
        {
            if (!row.Table.Columns.Contains(column)) continue;
            string name = row[column].ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50 || name.Any(char.IsControl))
                issues.Add(column + ": 1–50 karakter ve kontrol karakteri olmamalı.");
            else if (name != name.Trim()) issues.Add(column + ": başta/sonda boşluk var.");
        }
        if (row.Table.Columns.Contains("IBAN"))
        {
            string stored = row["IBAN"].ToString() ?? "";
            if (!IbanInput.TryNormalize(stored, out string normalized, out string error)) issues.Add(error);
            else if (stored != normalized) issues.Add("IBAN: boşluk/büyük harf düzenlemesi gerekiyor.");
        }
        return string.Join(" ", issues);
    }

    internal static DataTable Build(DataTable? source)
    {
        var report = new DataTable();
        foreach (string column in new[] { "TC", "Ad", "Soyad", "IBAN", "Düzeltme nedeni" }) report.Columns.Add(column);
        if (source == null || !source.Columns.Contains("TC")) return report;
        // General personnel can have several leave rows. Report each person once.
        foreach (var row in source.AsEnumerable().GroupBy(r => r["TC"].ToString()).Select(g => g.First()))
        {
            string error = Issues(row);
            if (error.Length == 0) continue;
            string Value(string name) => source.Columns.Contains(name) ? row[name].ToString() ?? "" : "";
            report.Rows.Add(Value("TC"), Value("AD"), Value("SOYAD"), Value("IBAN"), error);
        }
        return report;
    }
}
