namespace WinFormsApp2;

internal static class PersonnelConcurrency
{
    internal const string ColumnName = "RecordVersion";
    internal const string ConflictMessage = "Bu personel kaydı başka bir işlem tarafından değiştirilmiş veya yeniden oluşturulmuş. Girdiğiniz bilgiler korundu. Listeyi yenileyip güncel kaydı yeniden seçin.";

    internal static byte[] RequireVersion(byte[]? version) => version is { Length: 8 }
        ? version.ToArray()
        : throw new InvalidOperationException("Kaydın sürüm bilgisi eksik. Listeyi yenileyip personeli yeniden seçin; girdiğiniz bilgiler korundu.");

    internal static bool MatchesDraft(string? draftVersion, byte[] currentVersion)
    {
        if (string.IsNullOrEmpty(draftVersion)) return false;
        try { return Convert.FromBase64String(draftVersion).AsSpan().SequenceEqual(currentVersion); }
        catch (FormatException) { return false; }
    }
}
