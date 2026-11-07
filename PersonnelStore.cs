using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    internal sealed class PersonnelStore
    {
        private readonly string connectionString;

        public PersonnelStore(string connectionString) => this.connectionString = connectionString;

        public void Add(PersonnelGroup group, PersonnelDetails person, LeavePeriod? leave = null, DateTime? employmentStart = null)
        {
            string table = GetTable(group);
            ValidateLeave(group, leave);
            person = PersonnelInput.Normalize(person);
            DateTime start = EmploymentService.Day(employmentStart) ?? throw new ArgumentException("İşe giriş tarihi gerekiyor.");
            DatabaseTransaction.Run(connectionString, (connection, transaction) =>
            {
                using var check = new SqlCommand(@"
                    SELECT CASE
                        WHEN EXISTS (SELECT 1 FROM Calisan WITH (UPDLOCK, HOLDLOCK) WHERE TC = @TC) THEN N'Genel'
                        WHEN EXISTS (SELECT 1 FROM Joker WITH (UPDLOCK, HOLDLOCK) WHERE TC = @TC) THEN N'Joker'
                        ELSE NULL END", connection, transaction);
                check.Parameters.Add("@TC", SqlDbType.BigInt).Value = person.Tc;
                object? existingGroup = check.ExecuteScalar();
                if (existingGroup != null && existingGroup != DBNull.Value)
                    throw new InvalidOperationException($"Bu TC zaten {existingGroup} grubunda kayıtlı!");

                using var insert = new SqlCommand($@"
                    INSERT INTO {table} (TC, AD, SOYAD, IBAN, PARA)
                    VALUES (@TC, @AD, @SOYAD, @IBAN, @PARA)", connection, transaction);
                AddPersonParameters(insert, person);
                insert.ExecuteNonQuery();
                using var initial = new SqlCommand($"UPDATE E SET StartDate=@start FROM dbo.EmploymentPeriods E JOIN {table} C ON C.CurrentEmploymentId=E.EmploymentId WHERE C.TC=@tc AND E.IsLegacy=0 AND E.StartDate IS NULL", connection, transaction);
                initial.Parameters.Add("@start", SqlDbType.Date).Value = start;
                initial.Parameters.Add("@tc", SqlDbType.BigInt).Value = person.Tc;
                if (initial.ExecuteNonQuery() != 1) throw new InvalidOperationException("İlk çalışma dönemi kaydedilemedi.");

                if (leave != null)
                    InsertLeave(connection, transaction, person.Tc, leave);
            });
        }

        public void Update(PersonnelGroup group, PersonnelDetails person, byte[] expectedVersion)
        {
            byte[] version = PersonnelConcurrency.RequireVersion(expectedVersion);
            string table = GetTable(group);
            person = PersonnelInput.Normalize(person);
            DatabaseTransaction.Run(connectionString, (connection, transaction) =>
            {
                EnsureSingleRecord(connection, transaction, table, person.Tc);
                using var update = new SqlCommand($@"
                    UPDATE {table} SET AD = @AD, SOYAD = @SOYAD, IBAN = @IBAN, PARA = @PARA
                    WHERE TC = @TC AND ArchivedUtc IS NULL AND RecordVersion = @version", connection, transaction);
                AddPersonParameters(update, person);
                update.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
                if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
            });
        }

        public void SaveLeave(LeaveTarget selected, LeavePeriod leave)
        {
            ArgumentNullException.ThrowIfNull(leave);
            ValidateLeave(PersonnelGroup.General, leave);
            SaveLeaveCoreAsync(LeaveTarget.RequireSnapshot(selected), leave).GetAwaiter().GetResult();
        }
        private async Task SaveLeaveCoreAsync(LeaveTarget selected, LeavePeriod leave)
        {
            await using var work = await SqlUnitOfWork.OpenAsync(connectionString, CancellationToken.None).ConfigureAwait(false);
            await new PersonnelRepository(work).SaveLeaveAsync(selected, leave, CancellationToken.None, "legacy-store").ConfigureAwait(false);
            await work.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        public void Delete(PersonnelGroup group, long tc, byte[]? expectedVersion, DateTime? employmentEnd = null)
            => ArchiveCoreAsync(group, tc, PersonnelConcurrency.RequireVersion(expectedVersion), employmentEnd).GetAwaiter().GetResult();
        private async Task ArchiveCoreAsync(PersonnelGroup group, long tc, byte[] version, DateTime? end)
        {
            await using var work = await SqlUnitOfWork.OpenAsync(connectionString, CancellationToken.None).ConfigureAwait(false);
            var repo = new PersonnelRepository(work);
            await repo.LockPersonAsync(group, tc, CancellationToken.None, version).ConfigureAwait(false);
            Guid person = await repo.IdentityAsync(group, tc, CancellationToken.None).ConfigureAwait(false);
            await EmploymentService.CloseAsync(work, group, person, end, CancellationToken.None).ConfigureAwait(false);
            using var archive = work.Command($"UPDATE {GetTable(group)} SET ArchivedUtc=SYSUTCDATETIME(),ArchivedBy=N'legacy-store' WHERE TC=@TC AND RecordVersion=@version AND ArchivedUtc IS NULL");
            archive.Tc(tc); archive.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
            if (await archive.ExecuteNonQueryAsync().ConfigureAwait(false) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
            using var history = work.Command($@"INSERT dbo.PersonnelArchiveEvents(PersonId,PersonnelGroup,Action,Actor,Balance)
                SELECT PersonId,@group,N'Archive',N'legacy-store',PARA FROM {GetTable(group)} WHERE TC=@tc;
                INSERT dbo.PaymentLedger(PersonId,TC,PersonnelGroup,Delta,Kind,Actor)
                SELECT PersonId,TC,@group,0,N'Closure',N'legacy-store' FROM {GetTable(group)} WHERE TC=@tc;");
            history.Tc(tc); history.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
            await history.ExecuteNonQueryAsync().ConfigureAwait(false); await work.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static string GetTable(PersonnelGroup group) => group switch
        {
            PersonnelGroup.General => "Calisan",
            PersonnelGroup.Joker => "Joker",
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };

        private static void ValidateLeave(PersonnelGroup group, LeavePeriod? leave)
        {
            if (leave == null) return;
            if (group != PersonnelGroup.General)
                throw new ArgumentException("İzin yalnızca Genel personel için kaydedilebilir.", nameof(leave));
            if (!LeaveInput.TryCreate(leave.Start, leave.End, out _, out string error))
                throw new ArgumentException(error, nameof(leave));
        }

        private static void EnsureSingleRecord(SqlConnection connection, SqlTransaction transaction,
            string table, long tc, byte[]? expectedVersion = null)
        {
            using var check = new SqlCommand(
                $"SELECT COUNT(*) FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE TC = @TC AND ArchivedUtc IS NULL" + (expectedVersion == null ? "" : " AND RecordVersion = @version"),
                connection, transaction);
            check.Parameters.Add("@TC", SqlDbType.BigInt).Value = tc;
            if (expectedVersion != null) check.Parameters.Add("@version", SqlDbType.Binary, 8).Value = expectedVersion;
            if (Convert.ToInt32(check.ExecuteScalar()) != 1)
                throw new InvalidOperationException(
                    expectedVersion == null ? "Seçilen kayıt bu grupta bulunamadı veya tekil değil. Listeyi yeniden açıp kaydı seçin." : PersonnelConcurrency.ConflictMessage);
        }

        private static void AddPersonParameters(SqlCommand command, PersonnelDetails person)
        {
            command.Parameters.Add("@TC", SqlDbType.BigInt).Value = person.Tc;
            command.Parameters.AddWithValue("@AD", person.FirstName);
            command.Parameters.AddWithValue("@SOYAD", person.LastName);
            command.Parameters.AddWithValue("@IBAN", person.Iban);
            // Precision and scale are inferred from the already parsed decimal value.
            command.Parameters.Add("@PARA", SqlDbType.Decimal).Value = person.Amount;
        }

        private static void InsertLeave(SqlConnection connection, SqlTransaction transaction,
            long tc, LeavePeriod leave)
        {
            using var insert = new SqlCommand(@"
                INSERT INTO izinler (PersonId, EmploymentId, TC, IZINBASLANGIC, IZINBITIS, LastChangedBy)
                SELECT PersonId, CurrentEmploymentId, @TC, @BASLANGIC, @BITIS, N'legacy-store' FROM dbo.Calisan WHERE TC=@TC AND ArchivedUtc IS NULL", connection, transaction);
            AddLeaveParameters(insert, tc, leave);
            insert.ExecuteNonQuery();
        }

        private static void AddLeaveParameters(SqlCommand command, long tc, LeavePeriod leave)
        {
            command.Parameters.Add("@TC", SqlDbType.BigInt).Value = tc;
            command.Parameters.Add("@BASLANGIC", SqlDbType.Date).Value = leave.Start.Date;
            command.Parameters.Add("@BITIS", SqlDbType.Date).Value = leave.End.Date;
        }
    }
}
