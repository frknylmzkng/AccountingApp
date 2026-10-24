using System.Globalization;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Category("LocalDb"), NonParallelizable]
public class TransactionTests
{
    // This fixture creates only its own GUID-named database; it never opens Muhasebe2.mdf.
    private readonly string databaseName = "AccountingAppRegression_" + Guid.NewGuid().ToString("N");
    private static string MasterConnection => TestDatabaseServer.Master;
    private string connectionString = "";
    private bool databaseCreated;
    private PersonnelStore Store => new PersonnelStore(connectionString);
    private AdminStore Admins => new AdminStore(connectionString);
    private static readonly LeavePeriod ValidLeave = new LeavePeriod(new DateTime(2026, 1, 10), new DateTime(2026, 1, 12));
    private static readonly LeavePeriod InvalidLeave = new LeavePeriod(new DateTime(2026, 1, 12), new DateTime(2026, 1, 10));
    private static PersonnelDetails Person(long tc = 11111111111L, decimal amount = 1500.50m) =>
        new PersonnelDetails(tc, "Ada", "Test", "TR000000000000000000000000", amount);

    [OneTimeSetUp]
    public void CreateIsolatedDatabase()
    {
        string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TestDatabases"));
        Directory.CreateDirectory(directory);
        string dataFile = Path.Combine(directory, databaseName + ".mdf").Replace("'", "''");
        string logFile = Path.Combine(directory, databaseName + "_log.ldf").Replace("'", "''");
        using (var master = new SqlConnection(MasterConnection))
        {
            master.Open();
            using var create = new SqlCommand($@"
                CREATE DATABASE [{databaseName}]
                ON PRIMARY (NAME=N'{databaseName}', FILENAME=N'{dataFile}')
                LOG ON (NAME=N'{databaseName}_log', FILENAME=N'{logFile}')", master);
            create.ExecuteNonQuery();
            databaseCreated = true;
        }
        var builder = new SqlConnectionStringBuilder(MasterConnection) { InitialCatalog = databaseName };
        connectionString = builder.ConnectionString;
        Execute(@"
            CREATE TABLE Calisan (TC bigint PRIMARY KEY, AD nvarchar(100) NOT NULL,
                SOYAD nvarchar(100) NOT NULL, IBAN nvarchar(34) NOT NULL, PARA decimal(18,2) NOT NULL);
            CREATE TABLE Joker (TC bigint PRIMARY KEY, AD nvarchar(100) NOT NULL,
                SOYAD nvarchar(100) NOT NULL, IBAN nvarchar(34) NOT NULL, PARA decimal(18,2) NOT NULL);
            CREATE TABLE izinler (Id int IDENTITY PRIMARY KEY, TC bigint NOT NULL REFERENCES Calisan(TC),
                IZINBASLANGIC date NOT NULL, IZINBITIS date NOT NULL,
                IzinSuresi AS DATEDIFF(day, IZINBASLANGIC, IZINBITIS),
                CONSTRAINT ValidDates CHECK (IZINBITIS >= IZINBASLANGIC));
            CREATE TABLE admin (username nvarchar(100) PRIMARY KEY, password nvarchar(100) NOT NULL);");
    }

    [SetUp]
    public void ClearOnlyTestRecords()
    {
        Execute(@"DROP TRIGGER IF EXISTS BlockEmployeeDelete;
            DROP TRIGGER IF EXISTS BlockLeaveChange;
            DROP TRIGGER IF EXISTS BlockPasswordMigration;
            DELETE FROM izinler; DELETE FROM Calisan; DELETE FROM Joker; DELETE FROM admin;");
    }

    [OneTimeTearDown]
    public void DropOnlyIsolatedDatabase()
    {
        if (!databaseCreated) return;
        using var master = new SqlConnection(MasterConnection);
        master.Open();
        using var drop = new SqlCommand($@"
            ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{databaseName}];", master);
        drop.ExecuteNonQuery();
    }

    [TestCase(true, "tr-TR", "1500.50")]
    [TestCase(true, "en-US", "1500,50")]
    [TestCase(false, "tr-TR", "1.500,50")]
    [TestCase(false, "en-US", "1,500.50")]
    public void InputAmountIsSavedAndUpdatedExactly(bool general, string culture, string input)
    {
        PersonnelGroup group = general ? PersonnelGroup.General : PersonnelGroup.Joker;
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.That(PersonnelInput.TryCreate("11111111111", "Ada", "Test",
                "TR000000000000000000000000", input, out var person, out _), Is.True);
            Store.Add(group, person!, group == PersonnelGroup.General ? ValidLeave : null);
            string table = group == PersonnelGroup.General ? "Calisan" : "Joker";
            Assert.That(Scalar($"SELECT PARA FROM {table}"), Is.EqualTo(1500.50m));
            Store.Update(group, person! with { Amount = 2500.75m });
            Assert.That(Scalar($"SELECT PARA FROM {table}"), Is.EqualTo(2500.75m));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestCase(true, "0")]
    [TestCase(false, "0")]
    [TestCase(true, "9999999999999999.99")]
    [TestCase(false, "-9999999999999999.99")]
    public void SqlDriverPreservesZeroAndBoundaryDecimalAmounts(bool general, string text)
    {
        decimal amount = decimal.Parse(text, CultureInfo.InvariantCulture);
        PersonnelGroup group = general ? PersonnelGroup.General : PersonnelGroup.Joker;
        string table = general ? "Calisan" : "Joker";
        Store.Add(group, Person(amount: amount));
        Assert.That(Scalar($"SELECT PARA FROM {table}"), Is.EqualTo(amount));
        Store.Update(group, Person(amount: 0m));
        Assert.That(Scalar($"SELECT PARA FROM {table}"), Is.EqualTo(0m));
    }

    [Test]
    public void FailedLeaveInsertRollsBackEmployeeInsert()
    {
        Execute(@"CREATE TRIGGER BlockLeaveChange ON izinler AFTER INSERT AS
            BEGIN; THROW 51003, 'Deliberate test leave insert failure', 1; END;");
        Assert.Throws<SqlException>(() => Store.Add(PersonnelGroup.General, Person(), ValidLeave));
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(0));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(0));
    }

    [Test]
    public void FailedLeaveUpdatePreservesPreviousEmployeeAndLeaveValues()
    {
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Execute(@"CREATE TRIGGER BlockLeaveChange ON izinler AFTER UPDATE AS
            BEGIN; THROW 51003, 'Deliberate test leave update failure', 1; END;");
        Assert.Throws<SqlException>(() => Store.SaveLeave(Person().Tc,
            new LeavePeriod(ValidLeave.Start, ValidLeave.End.AddDays(2))));
        Assert.Multiple(() =>
        {
            Assert.That(Scalar("SELECT AD FROM Calisan"), Is.EqualTo("Ada"));
            Assert.That(Scalar("SELECT PARA FROM Calisan"), Is.EqualTo(1500.50m));
            Assert.That(Scalar("SELECT IZINBITIS FROM izinler"), Is.EqualTo(ValidLeave.End));
        });
    }

    [Test]
    public void GeneralEmployeeCanBeAddedWithoutCreatingLeave()
    {
        Store.Add(PersonnelGroup.General, Person());
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(0));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EditingPersonDoesNotCreateOrAlterLeave(bool hasLeave)
    {
        Store.Add(PersonnelGroup.General, Person(), hasLeave ? ValidLeave : null);
        Store.Update(PersonnelGroup.General, Person() with { Amount = 9000m, FirstName = "Updated" });
        Assert.That(Scalar("SELECT PARA FROM Calisan"), Is.EqualTo(9000m));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(hasLeave ? 1 : 0));
        if (hasLeave)
        {
            Assert.That(Scalar("SELECT IZINBASLANGIC FROM izinler"), Is.EqualTo(ValidLeave.Start));
            Assert.That(Scalar("SELECT IZINBITIS FROM izinler"), Is.EqualTo(ValidLeave.End));
        }
    }

    [Test]
    public void SavingLeaveOnlyChangesTheSelectedPersonsLeave()
    {
        Store.Add(PersonnelGroup.General, Person());
        Store.Add(PersonnelGroup.General, Person(22222222222L), ValidLeave);
        Store.SaveLeave(Person().Tc, ValidLeave);
        var changed = new LeavePeriod(ValidLeave.Start.AddMonths(1), ValidLeave.End.AddMonths(1));
        Store.SaveLeave(Person().Tc, changed);
        Assert.Multiple(() =>
        {
            Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(2));
            Assert.That(Scalar("SELECT IZINBITIS FROM izinler WHERE TC=11111111111"), Is.EqualTo(changed.End));
            Assert.That(Scalar("SELECT IZINBITIS FROM izinler WHERE TC=22222222222"), Is.EqualTo(ValidLeave.End));
            Assert.That(Scalar("SELECT COUNT(*) FROM Calisan WHERE AD=N'Ada' AND PARA=1500.50"), Is.EqualTo(2));
        });
    }

    [Test]
    public void InvalidDatesAreRejectedBeforeEitherEmployeeOrLeaveChanges()
    {
        Assert.Throws<ArgumentException>(() => Store.Add(PersonnelGroup.General, Person(), InvalidLeave));
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(0));
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Assert.Throws<ArgumentException>(() => Store.SaveLeave(Person().Tc, InvalidLeave));
        Assert.That(Scalar("SELECT IZINBITIS FROM izinler"), Is.EqualTo(ValidLeave.End));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LeaveCannotBeSavedForMissingOrJokerEmployee(bool joker)
    {
        if (joker) Store.Add(PersonnelGroup.Joker, Person());
        Assert.Throws<InvalidOperationException>(() => Store.SaveLeave(Person().Tc, ValidLeave));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(0));
    }

    [Test]
    public void SameDayLeaveIsAllowed()
    {
        Store.Add(PersonnelGroup.General, Person());
        Store.SaveLeave(Person().Tc, new LeavePeriod(ValidLeave.Start, ValidLeave.Start));
        Assert.That(Scalar("SELECT IZINBITIS FROM izinler"), Is.EqualTo(ValidLeave.Start));
    }

    [Test]
    public void MultipleExistingLeavesCannotBeOverwrittenTogether()
    {
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Execute("INSERT INTO izinler (TC,IZINBASLANGIC,IZINBITIS) VALUES (11111111111,'20260201','20260203')");
        Store.Update(PersonnelGroup.General, Person() with { Amount = 2000m });
        Assert.Throws<InvalidOperationException>(() => Store.SaveLeave(Person().Tc,
            new LeavePeriod(new DateTime(2026, 3, 1), new DateTime(2026, 3, 3))));
        Assert.That(Scalar("SELECT COUNT(DISTINCT IZINBASLANGIC) FROM izinler"), Is.EqualTo(2));
        Assert.That(Scalar("SELECT MIN(IZINBASLANGIC) FROM izinler"), Is.EqualTo(ValidLeave.Start));
        Assert.That(Scalar("SELECT MAX(IZINBASLANGIC) FROM izinler"), Is.EqualTo(new DateTime(2026, 2, 1)));
    }

    [Test]
    public async Task ConcurrentFirstLeaveSavesCannotCreateDuplicateRows()
    {
        Store.Add(PersonnelGroup.General, Person());
        await Task.WhenAll(Task.Run(() => Store.SaveLeave(Person().Tc, ValidLeave)),
            Task.Run(() => Store.SaveLeave(Person().Tc, ValidLeave)));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(1));
    }

    [Test]
    public void FailedExplicitLeaveInsertPreservesTheEmployeeWithoutAddingLeave()
    {
        Store.Add(PersonnelGroup.General, Person());
        Execute(@"CREATE TRIGGER BlockLeaveChange ON izinler AFTER INSERT AS
            BEGIN; THROW 51003, 'Deliberate test leave insert failure', 1; END;");
        Assert.Throws<SqlException>(() => Store.SaveLeave(Person().Tc, ValidLeave));
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(0));
    }

    [Test]
    public void FailedEmployeeDeleteRestoresPreviouslyDeletedLeave()
    {
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Execute(@"CREATE TRIGGER BlockEmployeeDelete ON Calisan AFTER DELETE AS
            BEGIN; THROW 51000, 'Deliberate test deletion failure', 1; END;");
        Assert.Throws<SqlException>(() => Store.Delete(PersonnelGroup.General, Person().Tc));
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(1));
    }

    [Test]
    public void SuccessfulDeleteRemovesOnlySelectedEmployeeAndItsLeaves()
    {
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Store.Add(PersonnelGroup.General, Person(22222222222L), ValidLeave);
        Store.Delete(PersonnelGroup.General, Person().Tc);
        Assert.That(Scalar("SELECT TC FROM Calisan"), Is.EqualTo(22222222222L));
        Assert.That(Scalar("SELECT TC FROM izinler"), Is.EqualTo(22222222222L));
    }

    [Test]
    public void JokerDeleteCannotDeleteGeneralEmployeeOrLeaves()
    {
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Assert.Throws<InvalidOperationException>(() => Store.Delete(PersonnelGroup.Joker, Person().Tc));
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(1));
    }

    [Test]
    public void JokerDeleteStaysInJokerEvenWithALegacyCrossGroupDuplicate()
    {
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Execute("INSERT INTO Joker SELECT TC, AD, SOYAD, IBAN, PARA FROM Calisan");
        Store.Delete(PersonnelGroup.Joker, Person().Tc);
        Assert.That(Scalar("SELECT COUNT(*) FROM Joker"), Is.EqualTo(0));
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(1));
    }

    [Test]
    public void GeneralDeleteCannotDeleteJokerEmployee()
    {
        Store.Add(PersonnelGroup.Joker, Person());
        Assert.Throws<InvalidOperationException>(() => Store.Delete(PersonnelGroup.General, Person().Tc));
        Assert.That(Scalar("SELECT COUNT(*) FROM Joker"), Is.EqualTo(1));
    }

    [Test]
    public void WrongGroupUpdateDoesNotChangeAnotherGroupsEmployee()
    {
        Store.Add(PersonnelGroup.General, Person(), ValidLeave);
        Assert.Throws<InvalidOperationException>(() => Store.Update(PersonnelGroup.Joker,
            Person() with { Amount = 9000m }));
        Assert.That(Scalar("SELECT PARA FROM Calisan"), Is.EqualTo(1500.50m));
    }

    [Test]
    public void CrossGroupDuplicateDoesNotCreateEmployeeOrLeave()
    {
        Store.Add(PersonnelGroup.Joker, Person());
        Assert.Throws<InvalidOperationException>(() => Store.Add(PersonnelGroup.General, Person(), ValidLeave));
        Assert.That(Scalar("SELECT COUNT(*) FROM Calisan"), Is.EqualTo(0));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(0));
        Assert.That(Scalar("SELECT COUNT(*) FROM Joker"), Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentCrossGroupInsertsCannotCreateTwoEmployees()
    {
        bool[] results = await Task.WhenAll(
            Task.Run(() => Attempt(() => Store.Add(PersonnelGroup.General, Person(), ValidLeave))),
            Task.Run(() => Attempt(() => Store.Add(PersonnelGroup.Joker, Person()))));
        Assert.That(results.Count(success => success), Is.EqualTo(1));
        Assert.That(Scalar("SELECT (SELECT COUNT(*) FROM Calisan) + (SELECT COUNT(*) FROM Joker)"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM izinler"), Is.EqualTo(Scalar("SELECT COUNT(*) FROM Calisan")));
    }

    [Test]
    public void MissingRecordDeletionIsRejectedWithoutRemovingOtherRecords()
    {
        Store.Add(PersonnelGroup.Joker, Person());
        Assert.Throws<InvalidOperationException>(() => Store.Delete(PersonnelGroup.Joker, 22222222222L));
        Assert.That(Scalar("SELECT TC FROM Joker"), Is.EqualTo(Person().Tc));
    }

    [Test]
    public void LastAdminCannotBeDeleted()
    {
        Admins.Add("alpha", "secret");
        Assert.Throws<InvalidOperationException>(() => Admins.Delete("alpha"));
        Assert.That(Scalar("SELECT username FROM admin"), Is.EqualTo("alpha"));
    }

    [Test]
    public async Task ConcurrentAdminDeletionKeepsOneAccount()
    {
        Admins.Add("alpha", "secret");
        Admins.Add("beta", "secret");
        bool[] results = await Task.WhenAll(
            Task.Run(() => Attempt(() => Admins.Delete("alpha"))),
            Task.Run(() => Attempt(() => Admins.Delete("beta"))));
        Assert.That(results.Count(success => success), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM admin"), Is.EqualTo(1));
    }

    [Test]
    public void NewAdminStoresOnlyHashAndVerifiesCaseSensitivePassword()
    {
        const string password = "Strong test password 123!";
        Admins.Add("alpha", password);
        Assert.That(Scalar("SELECT password FROM admin"), Is.EqualTo(""));
        string hash = (string)Scalar("SELECT password_hash FROM admin")!;
        Assert.That(hash, Is.Not.EqualTo(password));
        Assert.That(PasswordHasher.Verify(password, hash), Is.True);
        Assert.That(Admins.Authenticate("alpha", password), Is.True);
        Assert.That(Admins.Authenticate("alpha", password.ToUpperInvariant()), Is.False);
        Assert.That(Admins.Authenticate("' OR 1=1 --", password), Is.False);
    }

    [Test]
    public void NewPasswordRetainsLeadingAndTrailingSpaces()
    {
        Admins.Add("alpha", "  secret  ");
        Assert.That(Admins.Authenticate("alpha", "  secret  "), Is.True);
        Assert.That(Admins.Authenticate("alpha", "secret"), Is.False);
    }

    [Test]
    public void TwoAccountsWithTheSamePasswordStoreDifferentHashes()
    {
        Admins.Add("alpha", "secret");
        Admins.Add("beta", "secret");
        Assert.That(Scalar("SELECT COUNT(DISTINCT password_hash) FROM admin"), Is.EqualTo(2));
    }

    [Test]
    public void LegacyAccountsMigrateTogetherAndCanStillSignIn()
    {
        UseLegacyPasswordSchema();
        InsertLegacyAccount("alpha", "Legacy password 123");
        InsertLegacyAccount("beta", "Türkçe şifre 456");
        Admins.MigrateLegacyPasswords();
        Assert.That(Scalar("SELECT COUNT(*) FROM admin WHERE password <> N''"), Is.EqualTo(0));
        Assert.That(Scalar("SELECT COUNT(*) FROM admin WHERE password_hash IS NOT NULL"), Is.EqualTo(2));
        Assert.That(Admins.Authenticate("alpha", "Legacy password 123"), Is.True);
        Assert.That(Admins.Authenticate("beta", "Türkçe şifre 456"), Is.True);
        string first = (string)Scalar("SELECT password_hash FROM admin WHERE username=N'alpha'")!;
        Admins.MigrateLegacyPasswords();
        Assert.That(Scalar("SELECT password_hash FROM admin WHERE username=N'alpha'"), Is.EqualTo(first));
    }

    [Test]
    public void LegacyPasswordThatLooksLikeAnEncodedHashIsTreatedAsLiteralPassword()
    {
        UseLegacyPasswordSchema();
        const string oldPassword = "$pbkdf2-sha256$v=1$i=600000$AAECAwQFBgcICQoLDA0ODw==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=";
        InsertLegacyAccount("alpha", oldPassword);
        Assert.That(Admins.Authenticate("alpha", oldPassword), Is.True);
        Assert.That(Admins.Authenticate("alpha", "known test password"), Is.False);
    }

    [Test]
    public void DamagedHashDoesNotFallBackToLegacyPlaintext()
    {
        Admins.Add("alpha", "secret");
        Execute("UPDATE admin SET password=N'backdoor', password_hash=N'invalid-hash'");
        Assert.That(Admins.Authenticate("alpha", "secret"), Is.False);
        Assert.That(Admins.Authenticate("alpha", "backdoor"), Is.False);
    }

    [Test]
    public void ListingWorksWithoutEitherPasswordColumn()
    {
        Admins.Add("alpha", "secret");
        Execute("ALTER TABLE admin DROP COLUMN password, password_hash;");
        try
        {
            var users = Admins.GetUsers();
            Assert.That(users.Columns.Count, Is.EqualTo(1));
            Assert.That(users.Columns[0].ColumnName, Is.EqualTo("username"));
            Assert.That(users.Rows[0]["username"], Is.EqualTo("alpha"));
        }
        finally
        {
            Execute(@"ALTER TABLE admin ADD password nvarchar(100) NOT NULL DEFAULT N'',
                password_hash nvarchar(256) NULL;");
        }
    }

    [Test]
    public void FailedMigrationRestoresEveryLegacyPasswordAndRollsBackSchema()
    {
        UseLegacyPasswordSchema();
        InsertLegacyAccount("alpha", "first secret");
        InsertLegacyAccount("beta", "second secret");
        Execute(@"CREATE TRIGGER BlockPasswordMigration ON admin AFTER UPDATE AS
            BEGIN; IF EXISTS(SELECT 1 FROM inserted WHERE username=N'beta')
                THROW 51002, 'Deliberate credential migration failure', 1; END;");
        Assert.Throws<SqlException>(() => Admins.MigrateLegacyPasswords());
        Assert.That(Scalar("SELECT password FROM admin WHERE username=N'alpha'"), Is.EqualTo("first secret"));
        Assert.That(Scalar("SELECT password FROM admin WHERE username=N'beta'"), Is.EqualTo("second secret"));
        Assert.That(Scalar("SELECT COL_LENGTH(N'dbo.admin',N'password_hash')"), Is.EqualTo(DBNull.Value));
    }

    [Test]
    public async Task ConcurrentColdMigrationsBothCompleteWithoutDoubleHashing()
    {
        UseLegacyPasswordSchema();
        InsertLegacyAccount("alpha", "legacy secret");
        await Task.WhenAll(Task.Run(() => Admins.MigrateLegacyPasswords()),
            Task.Run(() => Admins.MigrateLegacyPasswords()));
        Assert.That(Scalar("SELECT password FROM admin"), Is.EqualTo(""));
        Assert.That(Admins.Authenticate("alpha", "legacy secret"), Is.True);
    }

    [Test]
    public void NewAdminEnforcesLengthsAtStorageBoundary()
    {
        Assert.Throws<ArgumentException>(() => Admins.Add("alpha", "short"));
        Assert.Throws<ArgumentException>(() => Admins.Add(new string('u', 51), "secret"));
        Assert.Throws<ArgumentException>(() => Admins.Add("alpha", new string('p', 1025)));
        string username = new string('u', 50);
        Admins.Add(username, "secret");
        Assert.That(Admins.Authenticate(username, "secret"), Is.True);
    }

    private void UseLegacyPasswordSchema() => Execute(@"
        IF COL_LENGTH(N'dbo.admin',N'password_hash') IS NOT NULL
            ALTER TABLE dbo.admin DROP COLUMN password_hash;");

    private void InsertLegacyAccount(string username, string password)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand("INSERT INTO admin (username,password) VALUES (@username,@password)", connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@password", password);
        command.ExecuteNonQuery();
    }

    private static bool Attempt(Action change)
    {
        try { change(); return true; }
        catch (InvalidOperationException) { return false; }
        catch (SqlException) { return false; }
    }

    private void Execute(string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        return command.ExecuteScalar();
    }
}
