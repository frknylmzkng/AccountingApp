using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed class PersonnelRepository(IUnitOfWork work)
{
    public static string Table(PersonnelGroup group) => group switch { PersonnelGroup.General => "dbo.Calisan", PersonnelGroup.Joker => "dbo.Joker", _ => throw new ArgumentOutOfRangeException(nameof(group)) };
    public async Task<decimal> LockPersonAsync(PersonnelGroup group, long tc, CancellationToken token)
    {
        using var cmd = work.Command($"SELECT PARA FROM {Table(group)} WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc"); cmd.Tc(tc);
        using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Seçilen personel bulunamadı. Listeyi yenileyin.");
        decimal amount = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
        if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Personel kaydı tekil değil.");
        return amount;
    }
    public async Task WriteAsync(PersonnelGroup group, PersonnelDetails person, bool insert, CancellationToken token)
    {
        using var cmd = work.Command(insert
            ? $"INSERT INTO {Table(group)}(TC,AD,SOYAD,IBAN,PARA) VALUES(@tc,@ad,@soyad,@iban,@amount)"
            : $"UPDATE {Table(group)} SET AD=@ad,SOYAD=@soyad,IBAN=@iban,PARA=@amount WHERE TC=@tc");
        cmd.Tc(person.Tc); cmd.Text("@ad", person.FirstName); cmd.Text("@soyad", person.LastName); cmd.Text("@iban", person.Iban, 34); cmd.Money("@amount", person.Amount);
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("Personel kaydı tekil değil.");
    }
    public async Task SaveLeaveAsync(long tc, LeavePeriod leave, CancellationToken token)
    {
        _ = Calculations.LeaveDays(leave.Start, leave.End);
        await LockPersonAsync(PersonnelGroup.General, tc, token).ConfigureAwait(false);
        using var count = work.Command("SELECT COUNT(*) FROM dbo.izinler WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc"); count.Tc(tc);
        int n = Convert.ToInt32(await count.ExecuteScalarAsync(token).ConfigureAwait(false));
        if (n > 1) throw new InvalidOperationException("Birden fazla izin kaydı topluca değiştirilemez.");
        using var cmd = work.Command(n == 0 ? "INSERT INTO dbo.izinler(TC,IZINBASLANGIC,IZINBITIS) VALUES(@tc,@start,@end)" : "UPDATE dbo.izinler SET IZINBASLANGIC=@start,IZINBITIS=@end WHERE TC=@tc");
        cmd.Tc(tc); cmd.Parameters.Add("@start", SqlDbType.Date).Value = leave.Start.Date; cmd.Parameters.Add("@end", SqlDbType.Date).Value = leave.End.Date;
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}

