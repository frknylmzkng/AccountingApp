using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed class PersonnelRepository(IUnitOfWork work)
{
    public static string Table(PersonnelGroup group) => group switch { PersonnelGroup.General => "dbo.Calisan", PersonnelGroup.Joker => "dbo.Joker", _ => throw new ArgumentOutOfRangeException(nameof(group)) };
    public async Task<decimal> LockPersonAsync(PersonnelGroup group, long tc, CancellationToken token, byte[]? expectedVersion = null)
    {
        using var cmd = work.Command($"SELECT PARA FROM {Table(group)} WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc" + (expectedVersion == null ? "" : " AND RecordVersion=@version")); cmd.Tc(tc);
        if (expectedVersion != null) cmd.Parameters.Add("@version", SqlDbType.Binary, 8).Value = PersonnelConcurrency.RequireVersion(expectedVersion);
        using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException(expectedVersion == null ? "Seçilen personel bulunamadı. Listeyi yenileyin." : PersonnelConcurrency.ConflictMessage);
        decimal amount = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
        if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Personel kaydı tekil değil.");
        return amount;
    }
    public async Task WriteAsync(PersonnelGroup group, PersonnelDetails person, bool insert, CancellationToken token, byte[]? expectedVersion = null)
    {
        byte[]? version = insert ? null : PersonnelConcurrency.RequireVersion(expectedVersion);
        using var cmd = work.Command(insert
            ? $"INSERT INTO {Table(group)}(TC,AD,SOYAD,IBAN,PARA) VALUES(@tc,@ad,@soyad,@iban,@amount)"
            : $"UPDATE {Table(group)} SET AD=@ad,SOYAD=@soyad,IBAN=@iban,PARA=@amount WHERE TC=@tc AND RecordVersion=@version");
        cmd.Tc(person.Tc); cmd.Text("@ad", person.FirstName); cmd.Text("@soyad", person.LastName); cmd.Text("@iban", person.Iban, 34); cmd.Money("@amount", person.Amount);
        if (!insert) cmd.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version!;
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1)
            throw new InvalidOperationException(insert ? "Personel kaydı tekil değil." : PersonnelConcurrency.ConflictMessage);
    }
    public async Task SaveLeaveAsync(LeaveTarget selected, LeavePeriod leave, CancellationToken token)
    {
        var target = LeaveTarget.RequireSnapshot(selected);
        long tc = target.Tc;
        _ = Calculations.LeaveDays(leave.Start, leave.End);
        await LockPersonAsync(PersonnelGroup.General, tc, token, target.PersonVersion).ConfigureAwait(false);
        using (var current = work.Command("SELECT LeaveVersion FROM dbo.izinler WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc"))
        {
            current.Tc(tc); using var reader = await current.ExecuteReaderAsync(token).ConfigureAwait(false);
            bool exists = await reader.ReadAsync(token).ConfigureAwait(false);
            if (exists != (target.LeaveVersion != null) || (exists && !reader.GetFieldValue<byte[]>(0).SequenceEqual(target.LeaveVersion!)))
                throw new InvalidOperationException(LeaveTarget.ConflictMessage);
            if (exists && await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Birden fazla izin kaydı topluca değiştirilemez.");
        }
        using var cmd = work.Command(target.LeaveVersion == null ? "INSERT INTO dbo.izinler(TC,IZINBASLANGIC,IZINBITIS) VALUES(@tc,@start,@end)" : "UPDATE dbo.izinler SET IZINBASLANGIC=@start,IZINBITIS=@end WHERE TC=@tc AND LeaveVersion=@version");
        cmd.Tc(tc); cmd.Parameters.Add("@start", SqlDbType.Date).Value = leave.Start.Date; cmd.Parameters.Add("@end", SqlDbType.Date).Value = leave.End.Date;
        if (target.LeaveVersion != null) cmd.Parameters.Add("@version", SqlDbType.Binary, 8).Value = target.LeaveVersion;
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException(LeaveTarget.ConflictMessage);
    }
}

