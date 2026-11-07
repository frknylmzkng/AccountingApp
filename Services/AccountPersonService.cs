using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

// Identity evidence is retained after unlinking; it must not become an approval bypass.
internal sealed class AccountPersonService(string connectionString, UserSession user)
{
    private static void Id(SqlCommand c, string name, Guid? value) => c.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value = (object?)value ?? DBNull.Value;
    private static async Task<Guid> TargetAsync(IUnitOfWork w, AccountTarget selected, CancellationToken ct)
    {
        var target = AccountTarget.RequireSnapshot(selected);
        using var c = w.Command("SELECT AccountId FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE username=@name AND SessionVersion=@version AND SessionEpoch=@epoch");
        c.Text("@name", target.Username); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = target.Version; Id(c, "@epoch", target.Epoch);
        return await c.ExecuteScalarAsync(ct) is Guid id ? id : throw new InvalidOperationException(AccountTarget.ConflictMessage);
    }
    internal async Task<DataTable> ReadAsync(AccountTarget target, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return RemoteApi.Decode(await RemoteApi.For(connectionString, user).SendAsync<Wire.Table>("accounts/person/read", new Wire.AccountPersonRead(RemoteApi.Target(target)), ct));
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct);
        await AccountAuthorization.DemandAsync(w, user, AccountAccess.Admin, ct);
        Guid id = await TargetAsync(w, target, ct);
        using var c = w.Command(@"SELECT TOP(1001) L.*,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD
 FROM dbo.AccountPersonLinks L LEFT JOIN dbo.Calisan C ON C.PersonId=L.PersonId LEFT JOIN dbo.Joker J ON J.PersonId=L.PersonId
 WHERE L.AccountId=@id ORDER BY CASE WHEN L.RevokedUtc IS NULL THEN 0 ELSE 1 END,L.CreatedUtc DESC,L.LinkId");
        Id(c, "@id", id); var rows = await c.TableAsync(ct);
        if (rows.Rows.Count > 1000) throw new InvalidOperationException("Bu hesabın eşleme geçmişi 1000 kayıt sınırını aşıyor.");
        await w.CommitAsync(ct); return rows;
    }
    internal async Task ChangeAsync(Wire.AccountPersonChange input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("accounts/person/change", input, ct); return; }
        user.DemandAdmin();
        if (input.CommandId == Guid.Empty || input.PersonId == Guid.Empty || input.LinkId == Guid.Empty) throw new ArgumentException("İşlem/personel/eşleme kimliği geçersiz.");
        var target = AccountTarget.RequireSnapshot(input.Target == null ? null : new(input.Target.Username, input.Target.Version, input.Target.Epoch));
        if (input.LinkId.HasValue != (input.LinkVersion != null)) throw new ArgumentException("Eşlemenin özgün sürümü gerekiyor.");
        if (input.PersonId.HasValue ? input.Group == null || !Enum.IsDefined(input.Group.Value) || input.PersonVersion is not { Length: 8 } : input.Group != null || input.PersonVersion != null) throw new ArgumentException("Personel seçimi/sürümü geçersiz.");
        string text = AuditContext.ValidateReason(input.Reason); using var reason = AuditContext.UseReason(text);
        byte[] hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct);
        // Identity changes and approvals share the allocation gate; neither can validate against a half-changed identity.
        await TimesheetService.AllocationGateAsync(w, ct);
        await AccountAuthorization.LockAccountChangesAsync(w, ct);
        await AccountAuthorization.DemandAsync(w, user, AccountAccess.Admin, ct);
        Guid actor;
        using (var a = w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))")) actor = (Guid)(await a.ExecuteScalarAsync(ct) ?? throw new UnauthorizedAccessException());
        // A lost response can be resolved after re-login, before checking now-stale account versions.
        using (var receipt = w.Command("SELECT ActorId,PayloadHash FROM dbo.AccountPersonCommands WITH(UPDLOCK,HOLDLOCK) WHERE CommandId=@id"))
        {
            Id(receipt, "@id", input.CommandId); using var r = await receipt.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                if (r.GetGuid(0) != actor || !r.GetFieldValue<byte[]>(1).SequenceEqual(hash)) throw new InvalidOperationException("İşlem kimliği farklı içerikle kullanılmış.");
                await r.DisposeAsync(); await w.CommitAsync(ct); return;
            }
        }
        Guid account = await TargetAsync(w, target, ct);
        using var find = w.Command("SELECT LinkId,PersonId,RecordVersion FROM dbo.AccountPersonLinks WITH(UPDLOCK,HOLDLOCK) WHERE AccountId=@account AND RevokedUtc IS NULL");
        Id(find, "@account", account); var links = await find.TableAsync(ct);
        DataRow? previous = links.Rows.Count == 1 ? links.Rows[0] : null;
        if (previous == null ? input.LinkId != null : previous.Field<Guid>("LinkId") != input.LinkId || !((byte[])previous["RecordVersion"]).SequenceEqual(PersonnelConcurrency.RequireVersion(input.LinkVersion))) throw new InvalidOperationException("Hesabın personel eşlemesi değişmiş; hesabı yeniden seçin.");
        if (input.PersonId.HasValue)
        {
            using var person = w.Command($"SELECT COUNT(*) FROM {PersonnelRepository.Table((PersonnelGroup)input.Group!.Value)} WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND RecordVersion=@version AND ArchivedUtc IS NULL");
            Id(person, "@person", input.PersonId); person.Parameters.Add("@version", SqlDbType.Binary, 8).Value = input.PersonVersion!;
            if (Convert.ToInt32(await person.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("Personel seçildikten sonra değişmiş veya arşivlenmiş; yeniden seçin.");
        }
        if (previous?.Field<Guid>("PersonId") == input.PersonId) throw new InvalidOperationException("Eşleme zaten bu durumda; değişiklik yok.");
        if (previous != null)
        {
            using var c = w.Command("UPDATE dbo.AccountPersonLinks SET RevokedUtc=SYSUTCDATETIME(),RevokedByAccountId=@actor,RevokedBy=@name,RevokeReason=@reason WHERE LinkId=@id AND RecordVersion=@version AND RevokedUtc IS NULL");
            Id(c, "@id", input.LinkId); Id(c, "@actor", actor); c.Text("@name", user.Username); c.Text("@reason", text, 500); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = input.LinkVersion!;
            if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Eşleme değişmiş; yeniden seçin.");
        }
        if (input.PersonId.HasValue)
        {
            using var c = w.Command("INSERT dbo.AccountPersonLinks(LinkId,AccountId,PersonId,CreatedByAccountId,CreatedBy,CreateReason) VALUES(NEWID(),@account,@person,@actor,@name,@reason)");
            Id(c, "@account", account); Id(c, "@person", input.PersonId); Id(c, "@actor", actor); c.Text("@name", user.Username); c.Text("@reason", text, 500); await c.ExecuteNonQueryAsync(ct);
        }
        using (var c = w.Command("UPDATE dbo.admin SET SessionEpoch=NEWID() WHERE AccountId=@id")) { Id(c, "@id", account); await c.ExecuteNonQueryAsync(ct); }
        await LoginThrottle.ClearAsync(w, target.Epoch, ct);
        using (var c = w.Command("INSERT dbo.AccountPersonCommands(CommandId,ActorId,PayloadHash) VALUES(@id,@actor,@hash)"))
        { Id(c, "@id", input.CommandId); Id(c, "@actor", actor); c.Parameters.Add("@hash", SqlDbType.Binary, 32).Value = hash; await c.ExecuteNonQueryAsync(ct); }
        await w.CommitAsync(ct);
    }
}
