using System.Text.Json;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [Test, Category("Audit")]
    public async Task DetailedAuditRecordsChangedFieldsAndMasksPersonalValuesInSameTransaction()
    {
        using var scope = AuditContext.UseReason("Sentetik kayıt düzeltmesi");
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        var version = await VersionAsync();
        await Service.SaveAsync(PersonnelGroup.General, Person() with { FirstName = "Yeni" }, false, expectedVersion: version);
        using var c = new SqlConnection(connection); c.Open();
        using var cmd = new SqlCommand("SELECT TOP(1) Actor,Reason,OldValues,NewValues,CorrelationId,ActorAccountId,CreatedUtc,RecordKey,ChangedFields FROM dbo.AuditLog WHERE EntityType=N'Personnel.General' AND Operation=N'Update' ORDER BY AuditId DESC", c);
        using var row = cmd.ExecuteReader(); Assert.That(row.Read(), Is.True);
        Assert.That(row.GetString(0), Is.EqualTo(admin.Username)); Assert.That(row.GetString(1), Is.EqualTo("Sentetik kayıt düzeltmesi"));
        using var before = JsonDocument.Parse(row.GetString(2)); using var after = JsonDocument.Parse(row.GetString(3));
        Assert.That(before.RootElement.GetProperty("AD").GetString(), Is.EqualTo("[MASKELENDİ]"));
        Assert.That(after.RootElement.GetProperty("AD").GetString(), Is.EqualTo("[MASKELENDİ]"));
        using var fields = JsonDocument.Parse(row.GetString(8));
        Assert.That(fields.RootElement.EnumerateArray().Select(field => field.GetString()), Does.Contain("AD"));
        Assert.That(row.GetGuid(4), Is.Not.EqualTo(Guid.Empty)); Assert.That(row.GetGuid(5), Is.Not.EqualTo(Guid.Empty));
        Assert.That(row.GetDateTime(6), Is.GreaterThan(DateTime.UtcNow.AddMinutes(-5)));
        Assert.That(Guid.TryParse(row.GetString(7), out _), Is.True);
    }

    [Test, Category("Audit")]
    public async Task DetailedAuditFailureRollsBackPersonnelAndLedgerAndHistory()
    {
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Record.Change') THROW 51099,N'Synthetic detail failure',1;");
        using var scope = AuditContext.UseReason("Geri alım senaryosu");
        await Assert.ThrowsAsync<SqlException>(() => Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1)));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.Calisan"), Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityType=N'Personnel.General'"), Is.Zero);
        await Task.CompletedTask;
    }

    [Test, Category("Audit")]
    public async Task DetailedAccountAuditDoesNotSerializeSecretsAndCannotBeRewritten()
    {
        using var scope = AuditContext.UseReason("Geçici test hesabı açma");
        await new AccountService(connection).AddAsync(admin, "audit-target", "Synthetic-only-password-123", UserRole.Viewer, default);
        string json = (string)Scalar("SELECT TOP(1) NewValues FROM dbo.AuditLog WHERE EntityType=N'Account' AND Operation=N'Insert' ORDER BY AuditId DESC");
        Assert.That(json, Does.Not.Contain("Synthetic-only-password-123").And.Not.Contain("password_hash").And.Not.Contain("SessionEpoch"));
        Assert.Throws<SqlException>(() => Execute("UPDATE dbo.AuditLog SET Reason=N'changed' WHERE EntityType=N'Account'"));
        Assert.Throws<SqlException>(() => Execute("DELETE dbo.AuditLog WHERE EntityType=N'Account'"));
        Execute("UPDATE dbo.admin SET IsActive=0 WHERE username=N'audit-target'");
        Assert.That(Scalar("SELECT TOP(1) Source FROM dbo.AuditLog WHERE EntityType=N'Account' AND Operation=N'Update' ORDER BY AuditId DESC"), Is.EqualTo("DirectSql"));
        Assert.That(Scalar("SELECT TOP(1) ReasonSource FROM dbo.AuditLog WHERE EntityType=N'Account' AND Operation=N'Update' ORDER BY AuditId DESC"), Is.EqualTo("NotProvided"));
    }

    [Test, Category("Audit")]
    public async Task DetailedPaymentRetryDoesNotDuplicateMutationAuditAndKeepsOriginalReason()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        PaymentRequest request;
        using (AuditContext.UseReason("Vardiya ödemesi gerekçesi"))
            request = await Service.PreparePaymentAsync(PersonnelGroup.General, Person().Tc, 10m, await VersionAsync(), default);
        await Service.SubmitPaymentAsync(request, default);
        int count = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Record.Change'"));
        using (AuditContext.UseReason("Yeniden deneme gerekçesi"))
            await Service.SubmitPaymentAsync(request, default);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Record.Change'"), Is.EqualTo(count));
        Assert.That(Scalar("SELECT TOP(1) Reason FROM dbo.AuditLog WHERE EntityType=N'Payment' ORDER BY AuditId DESC"), Is.EqualTo("Vardiya ödemesi gerekçesi"));
    }
}
