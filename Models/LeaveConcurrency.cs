using System.Data;

namespace WinFormsApp2;

internal sealed record LeaveTarget(long Tc, byte[] PersonVersion, byte[]? LeaveVersion)
{
    internal const string ColumnName = "LeaveVersion";
    internal const string SelectionMessage = "İzin seçimindeki sürüm bilgisi eksik. Listeyi yenileyip personeli yeniden seçin.";
    internal const string ConflictMessage = "Personel veya izin kaydı seçiminizden sonra değiştirilmiş ya da yeniden oluşturulmuş. İzin kaydedilmedi. Listeyi yenileyip güncel kaydı yeniden seçin.";
    internal static LeaveTarget RequireSnapshot(LeaveTarget? target) => target != null && target.Tc >= 10000000000L && target.Tc <= 99999999999L &&
        target.PersonVersion is { Length: 8 } && (target.LeaveVersion == null || target.LeaveVersion.Length == 8)
            ? new(target.Tc, target.PersonVersion.ToArray(), target.LeaveVersion?.ToArray())
            : throw new InvalidOperationException(SelectionMessage);
    internal static LeaveTarget FromRow(DataRow row)
    {
        if (!row.HasVersion(DataRowVersion.Original) || new[] { "TC", PersonnelConcurrency.ColumnName, ColumnName, "IZINBASLANGIC", "IZINBITIS" }.Any(c => !row.Table.Columns.Contains(c)))
            throw new InvalidOperationException(SelectionMessage);
        if (row.Table.Columns.Contains("GRUP") && row["GRUP", DataRowVersion.Original].ToString() != "Genel") throw new InvalidOperationException("İzin yalnızca Genel personel için kaydedilebilir.");
        object value = row[ColumnName, DataRowVersion.Original];
        // Null is a snapshot of an absent leave, not a fallback for missing metadata.
        if (value is DBNull && (row["IZINBASLANGIC", DataRowVersion.Original] is not DBNull || row["IZINBITIS", DataRowVersion.Original] is not DBNull))
            throw new InvalidOperationException(SelectionMessage);
        if (value is not DBNull && value is not byte[]) throw new InvalidOperationException(SelectionMessage);
        return RequireSnapshot(new(Convert.ToInt64(row["TC", DataRowVersion.Original]), row[PersonnelConcurrency.ColumnName, DataRowVersion.Original] as byte[] ?? [], value as byte[]));
    }
    internal static LeaveTarget[] FromSelection(DataGridView grid)
    {
        var targets = new List<LeaveTarget>();
        foreach (DataGridViewRow selected in grid.SelectedRows)
        {
            if (selected.DataBoundItem is not DataRowView row) throw new InvalidOperationException(SelectionMessage);
            targets.Add(FromRow(row.Row));
        }
        return targets.GroupBy(t => t.Tc).Select(g =>
        {
            var first = g.First();
            if (g.Any(t => !t.PersonVersion.SequenceEqual(first.PersonVersion) || !(t.LeaveVersion ?? []).SequenceEqual(first.LeaveVersion ?? [])))
                throw new InvalidOperationException("Bir personelin birden fazla izin kaydı topluca değiştirilemez.");
            return first;
        }).ToArray();
    }
}
