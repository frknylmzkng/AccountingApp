using System.Data;

namespace WinFormsApp2;

internal sealed record PaymentTarget(PersonnelGroup Group, long Tc, byte[] PersonVersion)
{
    internal const string SelectionMessage = "Ödeme seçimindeki kayıt sürümü eksik. Listeyi yenileyip personeli yeniden seçin.";
    internal const string ConflictMessage = "Personel kaydı seçiminizden sonra değiştirilmiş veya yeniden oluşturulmuş. Ödeme yapılmadı. Listeyi yenileyip güncel kaydı yeniden seçin.";
    internal const string LegacyMessage = "Bu bekleyen ödeme eski sürümde hazırlanmış; kalıcı personel kimliği doğrulanamıyor. Yeni ödeme yapılmadı. Sonucu onaylayın, listeyi yenileyip personeli yeniden seçin.";

    internal static PaymentTarget RequireSnapshot(PaymentTarget? target) => target != null && Enum.IsDefined(target.Group) &&
        target.Tc is >= 10000000000L and <= 99999999999L && target.PersonVersion is { Length: 8 }
            ? new(target.Group, target.Tc, target.PersonVersion.ToArray())
            : throw new InvalidOperationException(SelectionMessage);

    internal static PaymentTarget FromSelection(DataGridView grid)
    {
        if (grid.SelectedRows.Count != 1 || grid.SelectedRows[0].DataBoundItem is not DataRowView selected)
            throw new InvalidOperationException(SelectionMessage);
        var row = selected.Row;
        if (!row.HasVersion(DataRowVersion.Original) || new[] { "TC", "GRUP", PersonnelConcurrency.ColumnName }.Any(c => !row.Table.Columns.Contains(c)) ||
            row["TC", DataRowVersion.Original] is not long tc || row[PersonnelConcurrency.ColumnName, DataRowVersion.Original] is not byte[] version)
            throw new InvalidOperationException(SelectionMessage);
        var group = (row["GRUP", DataRowVersion.Original] as string) switch
        {
            "Genel" => PersonnelGroup.General,
            "Joker" => PersonnelGroup.Joker,
            _ => throw new InvalidOperationException(SelectionMessage)
        };
        return RequireSnapshot(new(group, tc, version));
    }
}
