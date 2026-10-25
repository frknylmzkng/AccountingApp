using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    internal sealed class PersonnelStore
    {
        private readonly string connectionString;

        public PersonnelStore(string connectionString) => this.connectionString = connectionString;

        public void Add(PersonnelGroup group, PersonnelDetails person, LeavePeriod? leave = null)
        {
            string table = GetTable(group);
            ValidateLeave(group, leave);
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

                if (leave != null)
                    InsertLeave(connection, transaction, person.Tc, leave);
            });
        }

        public void Update(PersonnelGroup group, PersonnelDetails person, byte[] expectedVersion)
        {
            byte[] version = PersonnelConcurrency.RequireVersion(expectedVersion);
            string table = GetTable(group);
            DatabaseTransaction.Run(connectionString, (connection, transaction) =>
            {
                EnsureSingleRecord(connection, transaction, table, person.Tc);
                using var update = new SqlCommand($@"
                    UPDATE {table} SET AD = @AD, SOYAD = @SOYAD, IBAN = @IBAN, PARA = @PARA
                    WHERE TC = @TC AND RecordVersion = @version", connection, transaction);
                AddPersonParameters(update, person);
                update.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
                if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
            });
        }

        public void SaveLeave(long tc, LeavePeriod leave)
        {
            ArgumentNullException.ThrowIfNull(leave);
            ValidateLeave(PersonnelGroup.General, leave);
            DatabaseTransaction.Run(connectionString, (connection, transaction) =>
            {
                EnsureSingleRecord(connection, transaction, "Calisan", tc);
                using var count = new SqlCommand(@"
                    SELECT COUNT(*) FROM izinler WITH (UPDLOCK, HOLDLOCK) WHERE TC = @TC",
                    connection, transaction);
                count.Parameters.Add("@TC", SqlDbType.BigInt).Value = tc;
                int leaveCount = Convert.ToInt32(count.ExecuteScalar());
                if (leaveCount > 1)
                    throw new InvalidOperationException(
                        "Bu personelin birden fazla izin kaydı var. İzinler topluca değiştirilemez.");

                if (leaveCount == 0)
                    InsertLeave(connection, transaction, tc, leave);
                else
                {
                    using var update = new SqlCommand(@"
                        UPDATE izinler SET IZINBASLANGIC = @BASLANGIC, IZINBITIS = @BITIS
                        WHERE TC = @TC", connection, transaction);
                    AddLeaveParameters(update, tc, leave);
                    update.ExecuteNonQuery();
                }
            });
        }

        public void Delete(PersonnelGroup group, long tc)
        {
            string table = GetTable(group);
            DatabaseTransaction.Run(connectionString, (connection, transaction) =>
            {
                EnsureSingleRecord(connection, transaction, table, tc);
                if (group == PersonnelGroup.General)
                {
                    using var deleteLeave = new SqlCommand("DELETE FROM izinler WHERE TC = @TC",
                        connection, transaction);
                    deleteLeave.Parameters.Add("@TC", SqlDbType.BigInt).Value = tc;
                    deleteLeave.ExecuteNonQuery();
                }

                // The table comes only from the requested enum, never from a displayed value.
                using var delete = new SqlCommand($"DELETE FROM {table} WHERE TC = @TC",
                    connection, transaction);
                delete.Parameters.Add("@TC", SqlDbType.BigInt).Value = tc;
                delete.ExecuteNonQuery();
            });
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
            string table, long tc)
        {
            using var check = new SqlCommand(
                $"SELECT COUNT(*) FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE TC = @TC",
                connection, transaction);
            check.Parameters.Add("@TC", SqlDbType.BigInt).Value = tc;
            if (Convert.ToInt32(check.ExecuteScalar()) != 1)
                throw new InvalidOperationException(
                    "Seçilen kayıt bu grupta bulunamadı veya tekil değil. Listeyi yeniden açıp kaydı seçin.");
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
                INSERT INTO izinler (TC, IZINBASLANGIC, IZINBITIS)
                VALUES (@TC, @BASLANGIC, @BITIS)", connection, transaction);
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
