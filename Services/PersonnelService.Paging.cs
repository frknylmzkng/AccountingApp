using System.Data;
using System.Globalization;

namespace WinFormsApp2;

internal sealed record PersonnelPage(DataTable Rows, long Total, long Index, int Size);
internal sealed record PersonnelSummary(long GeneralCount, long JokerCount, long OnLeaveCount, decimal? Balance, decimal? Average, PersonnelPage Alerts);

internal sealed partial class PersonnelService
{
    internal async Task<PersonnelPage> LedgerPageAsync(long index = 0, int size = 50, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).PageAsync($"ledger?index={index}&size={size}", token).ConfigureAwait(false);
        if (index < 0 || size is < 1 or > 200) throw new ArgumentException("Geçersiz defter sayfası.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        long total;
        using (var count = work.Command("SELECT COUNT_BIG(*) FROM dbo.PaymentLedger"))
            total = Convert.ToInt64(await count.ExecuteScalarAsync(token).ConfigureAwait(false));
        long actual = Math.Min(index, total == 0 ? 0 : (total - 1) / size);
        using var query = work.Command(@"WITH PeriodLedger AS(SELECT P.*,L.EmploymentId FROM dbo.PaymentLedger P
            LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)),
            Ledger AS(SELECT LedgerId,TC,PersonnelGroup,CreatedUtc,Kind,Delta,PersonId,EmploymentId,PaymentPurpose,ProjectId,FinanceEntryId,
            SUM(Delta) OVER(PARTITION BY TC,PersonnelGroup,PersonId,EmploymentId ORDER BY LedgerId ROWS UNBOUNDED PRECEDING) RunningBalance,
            Actor,RequestId,ReversedUtc FROM PeriodLedger)
            SELECT * FROM Ledger ORDER BY LedgerId DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY");
        query.Parameters.Add("@skip", SqlDbType.BigInt).Value = actual * size; query.Parameters.Add("@take", SqlDbType.Int).Value = size;
        var rows = await query.TableAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false);
        return new(SensitiveData.Mask(rows, user), total, actual, size);
    }
    internal Task<PersonnelPage> PageAsync(PersonnelGroup? group, long index = 0, int size = 50, CancellationToken token = default,
        string tc = "", string first = "", string last = "", bool includeArchived = false, bool archivedOnly = false, bool includeLeaves = true)
        => DesktopConnection.IsRemote(connectionString)
            ? RemoteApi.For(connectionString, user).PageAsync($"personnel?index={index}&size={size}&tc={RemoteApi.Q(tc)}&first={RemoteApi.Q(first)}&last={RemoteApi.Q(last)}&includeArchived={includeArchived}&archivedOnly={archivedOnly}&includeLeaves={includeLeaves}" + (group.HasValue ? "&group=" + group.Value : ""), token)
            : ReadPageAsync(group, index, size, token, tc, first, last, includeArchived, archivedOnly, includeLeaves);

    private async Task<PersonnelPage> ReadPageAsync(PersonnelGroup? group, long index, int? size, CancellationToken token,
        string tc, string first, string last, bool includeArchived, bool archivedOnly, bool includeLeaves)
    {
        if (index < 0 || size is < 1 or > 200 || (group.HasValue && !Enum.IsDefined(group.Value))) throw new ArgumentException("Geçersiz personel sayfası.");
        first = first.Trim(); last = last.Trim(); tc = tc.Trim();
        if (first.Length > 100 || last.Length > 100) throw new ArgumentException("Arama alanları en fazla 100 karakter olabilir.");
        const string fields = "TC,AD,SOYAD,IBAN,PARA,RecordVersion,PersonId,CurrentEmploymentId,ArchivedUtc,ArchivedBy";
        string source = group switch
        {
            PersonnelGroup.General => $"SELECT {fields},N'Genel' GRUP FROM dbo.Calisan",
            PersonnelGroup.Joker => $"SELECT {fields},N'Joker' GRUP FROM dbo.Joker",
            _ => $"SELECT {fields},N'Genel' GRUP FROM dbo.Calisan UNION ALL SELECT {fields},N'Joker' GRUP FROM dbo.Joker"
        };
        var predicates = new List<string>();
        if (archivedOnly) predicates.Add("ArchivedUtc IS NOT NULL");
        else if (!includeArchived) predicates.Add("ArchivedUtc IS NULL");
        long parsedTc = 0;
        bool validTc = tc.Length == 11 && tc.All(c => c is >= '0' and <= '9') && long.TryParse(tc, NumberStyles.None, CultureInfo.InvariantCulture, out parsedTc);
        if (tc.Length > 0) predicates.Add(validTc ? "TC=@tc" : "1=0");
        if (first.Length > 0) predicates.Add("AD LIKE @first ESCAPE '~'");
        if (last.Length > 0) predicates.Add("SOYAD LIKE @last ESCAPE '~'");
        string cte = $"WITH Persons AS(SELECT * FROM ({source}) P" + (predicates.Count == 0 ? "" : " WHERE " + string.Join(" AND ", predicates)) + ")";
        void Bind(Microsoft.Data.SqlClient.SqlCommand cmd)
        {
            if (validTc) cmd.Tc(parsedTc);
            static string Like(string value) => "%" + value.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";
            if (first.Length > 0) cmd.Text("@first", Like(first), 210);
            if (last.Length > 0) cmd.Text("@last", Like(last), 210);
        }
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        long total;
        using (var count = work.Command(cte + " SELECT COUNT_BIG(*) FROM Persons"))
        { Bind(count); total = Convert.ToInt64(await count.ExecuteScalarAsync(token).ConfigureAwait(false)); }
        long actual = size.HasValue ? Math.Min(index, total == 0 ? 0 : (total - 1) / size.Value) : 0;
        string page = size.HasValue ? ",SelectedPersons AS(SELECT * FROM Persons ORDER BY TC,GRUP,PersonId OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY)" : "";
        string leaves = includeLeaves
            ? "I.IZINBASLANGIC,I.IZINBITIS,I.IzinSuresi,I.LeaveVersion"
            : "CAST(NULL AS date) IZINBASLANGIC,CAST(NULL AS date) IZINBITIS,CAST(NULL AS int) IzinSuresi,CAST(NULL AS binary(8)) LeaveVersion";
        string join = includeLeaves ? " LEFT JOIN dbo.izinler I ON I.PersonId=P.PersonId AND I.EmploymentId=P.CurrentEmploymentId AND P.GRUP=N'Genel'" : "";
        using var query = work.Command(cte + page + $" SELECT P.*,{leaves} FROM {(size.HasValue ? "SelectedPersons" : "Persons")} P{join} ORDER BY P.TC,P.GRUP,P.PersonId" +
            (includeLeaves ? ",I.IZINBASLANGIC,I.IzinId" : ""));
        Bind(query);
        if (size.HasValue) { query.Parameters.Add("@skip", SqlDbType.BigInt).Value = actual * size.Value; query.Parameters.Add("@take", SqlDbType.Int).Value = size.Value; }
        var rows = await query.TableAsync(token).ConfigureAwait(false);
        foreach (string column in new[] { "TC", PersonnelConcurrency.ColumnName, LeaveTarget.ColumnName, "PersonId" }) rows.Columns[column]!.ReadOnly = true;
        await work.CommitAsync(token).ConfigureAwait(false);
        return new(SensitiveData.Mask(rows, user), total, actual, size ?? Math.Max(1, rows.Rows.Count));
    }

    internal async Task<PersonnelSummary> SummaryAsync(DateTime day, long alertIndex = 0, int size = 50, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString))
        {
            var s = await RemoteApi.For(connectionString, user).SendAsync<AccountingApp.Contracts.Summary>($"summary?day={RemoteApi.Day(day)}&index={alertIndex}&size={size}", null, token).ConfigureAwait(false);
            return new(s.GeneralCount, s.JokerCount, s.OnLeaveCount, s.Balance, s.Average, new(RemoteApi.Decode(s.Alerts.Data), s.Alerts.Total, s.Alerts.Index, s.Alerts.Size));
        }
        if (alertIndex < 0 || size is < 1 or > 200) throw new ArgumentException("Geçersiz özet sayfası.");
        day = day.Date; var lower = day.AddDays(-30); var upper = day.AddDays(30);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        long general, joker, onLeave; decimal balance, average;
        using (var stats = work.Command(@"WITH P AS(
            SELECT PersonId,PARA,0 G FROM dbo.Calisan WHERE ArchivedUtc IS NULL
            UNION ALL SELECT PersonId,PARA,1 G FROM dbo.Joker WHERE ArchivedUtc IS NULL)
            SELECT COALESCE(SUM(CASE WHEN G=0 THEN CAST(1 AS bigint) ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN G=1 THEN CAST(1 AS bigint) ELSE 0 END),0),
                COALESCE(SUM(CAST(PARA AS decimal(28,2))),0),COALESCE(AVG(CAST(PARA AS decimal(28,2))),0) FROM P;
            SELECT COUNT_BIG(*) FROM dbo.Calisan C WHERE C.ArchivedUtc IS NULL AND EXISTS(
                SELECT 1 FROM dbo.izinler I WHERE I.PersonId=C.PersonId AND I.EmploymentId=C.CurrentEmploymentId AND I.IZINBASLANGIC<=@day AND I.IZINBITIS>=@day);"))
        {
            stats.Parameters.Add("@day", SqlDbType.Date).Value = day;
            using var r = await stats.ExecuteReaderAsync(token).ConfigureAwait(false); await r.ReadAsync(token).ConfigureAwait(false);
            general = r.GetInt64(0); joker = r.GetInt64(1); balance = r.GetDecimal(2); average = r.GetDecimal(3);
            await r.NextResultAsync(token).ConfigureAwait(false); await r.ReadAsync(token).ConfigureAwait(false); onLeave = r.GetInt64(0);
        }
        const string alerts = @"WITH Alerts AS(SELECT C.TC,C.AD,C.SOYAD,C.IBAN,C.PARA,N'Genel' GRUP,C.PersonId,C.RecordVersion,
            I.IzinId,I.IZINBASLANGIC,I.IZINBITIS,I.IzinSuresi,I.LeaveVersion,
            CASE WHEN I.IZINBITIS<@day THEN 0 ELSE 1 END SortKey,
            CASE WHEN I.IZINBITIS<@day THEN N'Süresi bitti' WHEN I.IZINBASLANGIC<=@day THEN N'İzin sürüyor' ELSE N'Yaklaşıyor' END DURUM
            FROM dbo.izinler I INNER JOIN dbo.Calisan C ON C.PersonId=I.PersonId AND I.EmploymentId=C.CurrentEmploymentId
            WHERE C.ArchivedUtc IS NULL AND I.IZINBASLANGIC<=@upper AND I.IZINBITIS>=@lower)";
        void Bind(Microsoft.Data.SqlClient.SqlCommand cmd)
        { cmd.Parameters.Add("@day", SqlDbType.Date).Value = day; cmd.Parameters.Add("@lower", SqlDbType.Date).Value = lower; cmd.Parameters.Add("@upper", SqlDbType.Date).Value = upper; }
        long count;
        using (var cmd = work.Command(alerts + " SELECT COUNT_BIG(*) FROM Alerts")) { Bind(cmd); count = Convert.ToInt64(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false)); }
        long actual = Math.Min(alertIndex, count == 0 ? 0 : (count - 1) / size);
        using var query = work.Command(alerts + " SELECT * FROM Alerts ORDER BY SortKey,IZINBASLANGIC,TC,IzinId OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY");
        Bind(query); query.Parameters.Add("@skip", SqlDbType.BigInt).Value = actual * size; query.Parameters.Add("@take", SqlDbType.Int).Value = size;
        var data = await query.TableAsync(token).ConfigureAwait(false); data.Columns.Remove("SortKey");
        await work.CommitAsync(token).ConfigureAwait(false);
        return new(general, joker, onLeave, user.CanViewSalary ? balance : null, user.CanViewSalary ? average : null, new(SensitiveData.Mask(data, user), count, actual, size));
    }
}
