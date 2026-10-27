using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Leaves")]
public class LeaveVersionTests
{
    private static byte[] Version(byte value) => [0,0,0,0,0,0,0,value];
    private static DataTable Rows(bool hasLeave)
    {
        var t=new DataTable();t.Columns.Add("TC",typeof(long));t.Columns.Add("GRUP");t.Columns.Add(PersonnelConcurrency.ColumnName,typeof(byte[]));t.Columns.Add(LeaveTarget.ColumnName,typeof(byte[]));t.Columns.Add("IZINBASLANGIC",typeof(DateTime));t.Columns.Add("IZINBITIS",typeof(DateTime));
        t.Rows.Add(11111111111L,"Genel",Version(1),hasLeave?Version(2):DBNull.Value,hasLeave?new DateTime(2026,1,1):DBNull.Value,hasLeave?new DateTime(2026,1,2):DBNull.Value);t.AcceptChanges();return t;
    }
    [TestCase(-1),TestCase(0),TestCase(7),TestCase(9)]
    public async Task MissingPersonVersionCannotReachDatabase(int length)
    {
        var service=new PersonnelService("invalid connection string",new("operator",UserRole.PersonnelManager));
        var target=new LeaveTarget(11111111111L,length<0?null!:new byte[length],null);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveLeavesAsync([(target,new(DateTime.Today,DateTime.Today))]));
        Assert.That(error!.Message,Is.EqualTo(LeaveTarget.SelectionMessage));
    }
    [TestCase(0),TestCase(7),TestCase(9)]
    public async Task MalformedExistingLeaveVersionCannotReachDatabase(int length)
    {
        var service=new PersonnelService("invalid connection string",new("operator",UserRole.PersonnelManager));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveLeavesAsync([(new(11111111111L,Version(1),new byte[length]),new(DateTime.Today,DateTime.Today))]));
    }
    [TestCase(false),TestCase(true)]
    public void SelectionRetainsOriginalPersonAndLeaveState(bool hasLeave)
    {
        var t=Rows(hasLeave);t.Rows[0]["TC"]=22222222222L;t.Rows[0][PersonnelConcurrency.ColumnName]=Version(3);t.Rows[0][LeaveTarget.ColumnName]=Version(4);
        var target=LeaveTarget.FromRow(t.Rows[0]);Assert.That(target.Tc,Is.EqualTo(11111111111L));Assert.That(target.PersonVersion,Is.EqualTo(Version(1)));
        Assert.That(target.LeaveVersion,Is.EqualTo(hasLeave?Version(2):null));target.PersonVersion[0]=99;if(target.LeaveVersion!=null)target.LeaveVersion[0]=99;
        var again=LeaveTarget.FromRow(t.Rows[0]);Assert.That(again.PersonVersion,Is.EqualTo(Version(1)));Assert.That(again.LeaveVersion,Is.EqualTo(hasLeave?Version(2):null));
    }
    [TestCase(false),TestCase(true)]
    public void BulkSelectionDeduplicatesOnlyTheSameOriginalSnapshot(bool hasLeave)
    {
        var t=Rows(hasLeave);t.ImportRow(t.Rows[0]);
        using var grid=new DataGridView{BindingContext=new BindingContext()};RecordSelection.Configure(grid);grid.MultiSelect=true;grid.DataSource=t;
        grid.ClearSelection();grid.Rows[0].Selected=grid.Rows[1].Selected=true;
        Assert.That(LeaveTarget.FromSelection(grid),Has.Length.EqualTo(1));
        t.Rows[1][LeaveTarget.ColumnName]=Version(9);t.Rows[1]["IZINBASLANGIC"]=new DateTime(2026,2,1);t.Rows[1]["IZINBITIS"]=new DateTime(2026,2,3);t.Rows[1].AcceptChanges();
        grid.ClearSelection();grid.Rows[0].Selected=grid.Rows[1].Selected=true;
        Assert.Throws<InvalidOperationException>(()=>LeaveTarget.FromSelection(grid));
    }
    [Test]
    public void MissingMetadataCannotBeTreatedAsAnAbsentLeave()
    {
        var t=Rows(false);t.Columns.Remove(LeaveTarget.ColumnName);Assert.Throws<InvalidOperationException>(()=>LeaveTarget.FromRow(t.Rows[0]));
        t=Rows(true);t.Rows[0][LeaveTarget.ColumnName]=DBNull.Value;t.AcceptChanges();Assert.Throws<InvalidOperationException>(()=>LeaveTarget.FromRow(t.Rows[0]));
        t=Rows(false);t.Rows.Add(22222222222L,"Genel",Version(3),DBNull.Value,DBNull.Value,DBNull.Value);Assert.Throws<InvalidOperationException>(()=>LeaveTarget.FromRow(t.Rows[1]));
    }
    [Test]
    public async Task DuplicatePeopleAreRejectedBeforeDatabaseAccess()
    {
        var target=LeaveTarget.FromRow(Rows(false).Rows[0]);var service=new PersonnelService("invalid connection string",new("operator",UserRole.PersonnelManager));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveLeavesAsync([(target,new(DateTime.Today,DateTime.Today)),(target,new(DateTime.Today,DateTime.Today))]));
    }
    [Test]
    public void LeaveVersionIsHiddenAndCannotBeExportedEvenWhenForcedVisible()
    {
        var t=Rows(true);using var form=new Form();using var grid=new DataGridView{BindingContext=new BindingContext()};form.Controls.Add(grid);RecordSelection.Configure(grid);grid.DataSource=t;
        RecordSelection.HideInternalColumns(grid);Assert.That(grid.Columns[LeaveTarget.ColumnName]!.Visible,Is.False);
        grid.Columns[LeaveTarget.ColumnName]!.Visible=true;var report=ReportExport.Snapshot(grid);Assert.That(report.Columns.Contains(LeaveTarget.ColumnName),Is.False);
        string path=Path.Combine(TestContext.CurrentContext.WorkDirectory,Guid.NewGuid().ToString("N")+".csv");
        try{ReportExport.Csv(t,path);Assert.That(File.ReadAllText(path),Does.Not.Contain(LeaveTarget.ColumnName));}finally{File.Delete(path);}
    }
}

public partial class GuideDatabaseTests
{
    private static LeavePeriod Period(int month)=>new(new(2026,month,1),new(2026,month,3));
    private async Task SeedLeavePerson(long tc=11111111111L,bool hasLeave=true)
    {
        await Service.SaveAsync(PersonnelGroup.General,Person(tc),true);
        if(hasLeave)await Service.SaveLeavesAsync([(await LeaveSelectionAsync(tc),Period(1))]);
    }
    private async Task AssertLeaveUnchanged(LeaveTarget snapshot,int audits,int ledger)
    {
        var actual=await LeaveSelectionAsync(snapshot.Tc);Assert.That(actual.PersonVersion,Is.EqualTo(snapshot.PersonVersion));Assert.That(actual.LeaveVersion,Is.EqualTo(snapshot.LeaveVersion));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"),Is.EqualTo(audits));Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"),Is.EqualTo(ledger));
    }
    [TestCase(false),TestCase(true),Category("Leaves")]
    public async Task OldLeaveSelectionCannotWriteToRecreatedPersonnel(bool hasLeave)
    {
        await SeedLeavePerson(hasLeave:hasLeave);var stale=await LeaveSelectionAsync();
        await Service.DeleteAsync(PersonnelGroup.General,Person().Tc,await VersionAsync());await Service.SaveAsync(PersonnelGroup.General,Person() with{FirstName="Replacement",Amount=250m},true);
        if(hasLeave)await Service.SaveLeavesAsync([(await LeaveSelectionAsync(),Period(2))]);
        var current=await LeaveSelectionAsync();int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog")),ledger=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.SaveLeavesAsync([(stale,Period(3))]));
        await AssertLeaveUnchanged(current,audits,ledger);Assert.That(Scalar("SELECT AD FROM dbo.Calisan"),Is.EqualTo("Replacement"));Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"),Is.EqualTo(250m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"),Is.EqualTo(hasLeave?1:0));if(hasLeave)Assert.That(Scalar("SELECT IZINBASLANGIC FROM dbo.izinler"),Is.EqualTo(Period(2).Start));
    }
    [TestCase(false),TestCase(true),Category("Leaves")]
    public async Task StaleLeaveStateCannotOverwriteAnInterveningSave(bool initiallyPresent)
    {
        await SeedLeavePerson(hasLeave:initiallyPresent);var stale=await LeaveSelectionAsync();var other=new PersonnelService(connection,CreateActor("leave-operator",UserRole.PersonnelManager));
        await other.SaveLeavesAsync([(stale,Period(2))]);var current=await LeaveSelectionAsync();Assert.That(current.PersonVersion,Is.EqualTo(stale.PersonVersion));
        int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog")),ledger=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.SaveLeavesAsync([(stale,Period(3))]));Assert.That(error!.Message,Is.EqualTo(LeaveTarget.ConflictMessage));
        await AssertLeaveUnchanged(current,audits,ledger);Assert.That(Scalar("SELECT IZINBASLANGIC FROM dbo.izinler"),Is.EqualTo(Period(2).Start));Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"),Is.EqualTo(1));
    }
    [TestCase("Reinsert"),TestCase("SameDates"),TestCase("RevertDates"),TestCase("Delete"),Category("Leaves")]
    public async Task LeaveVersionRejectsChangedRowsEvenIfDatesLookUnchanged(string change)
    {
        await SeedLeavePerson();var stale=await LeaveSelectionAsync();
        if(change=="Reinsert")Execute("DELETE FROM dbo.izinler; INSERT dbo.izinler(TC,IZINBASLANGIC,IZINBITIS) VALUES(11111111111,'20260101','20260103')");
        else if(change=="SameDates")await Service.SaveLeavesAsync([(stale,Period(1))]);
        else if(change=="RevertDates")Execute("UPDATE dbo.izinler SET IZINBASLANGIC='20260201',IZINBITIS='20260203'; UPDATE dbo.izinler SET IZINBASLANGIC='20260101',IZINBITIS='20260103'");
        else Execute("DELETE FROM dbo.izinler");
        var current=await LeaveSelectionAsync();int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog")),ledger=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.SaveLeavesAsync([(stale,Period(3))]));await AssertLeaveUnchanged(current,audits,ledger);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"),Is.EqualTo(change=="Delete"?0:1));
    }
    [TestCase("Name"),TestCase("Iban"),TestCase("Payment"),Category("Leaves")]
    public async Task ChangedPersonnelContextRequiresANewLeaveSelection(string change)
    {
        await SeedLeavePerson();var stale=await LeaveSelectionAsync();
        if(change=="Payment")await Service.PayAsync(PersonnelGroup.General,Person().Tc,30m,Guid.NewGuid());
        else await Service.SaveAsync(PersonnelGroup.General,change=="Name"?Person() with{FirstName="New"}:Person() with{Iban="TR111111111111111111111111"},false,expectedVersion:stale.PersonVersion);
        var current=await LeaveSelectionAsync();int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog")),ledger=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.SaveLeavesAsync([(stale,Period(2))]));await AssertLeaveUnchanged(current,audits,ledger);
    }
    [TestCase(false),TestCase(true),Category("Leaves")]
    public async Task ConcurrentLeaveSavesWithTheSameSnapshotHaveOneWinner(bool existing)
    {
        await SeedLeavePerson(hasLeave:existing);var target=await LeaveSelectionAsync();var other=new PersonnelService(connection,CreateActor("second-user",UserRole.PersonnelManager));
        int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        static async Task<bool> Try(Task action){try{await action;return true;}catch(InvalidOperationException){return false;}}
        bool[] results=await Task.WhenAll(Try(Service.SaveLeavesAsync([(target,Period(2))])),Try(other.SaveLeavesAsync([(target,Period(3))])));
        Assert.That(results.Count(won=>won),Is.EqualTo(1));Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"),Is.EqualTo(1));Assert.That(Scalar("SELECT IZINBASLANGIC FROM dbo.izinler"),Is.EqualTo(Period(results[0]?2:3).Start));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"),Is.EqualTo(audits+1));
    }
    [TestCase(false),TestCase(true),Category("Leaves")]
    public async Task BulkLeaveConflictRollsBackEarlierPersonsInsertOrUpdate(bool existing)
    {
        await SeedLeavePerson(hasLeave:existing);await SeedLeavePerson(22222222222L,existing);var first=await LeaveSelectionAsync();var second=await LeaveSelectionAsync(22222222222L);
        await Service.SaveLeavesAsync([(second,Period(2))]);var changed=await LeaveSelectionAsync(second.Tc);
        int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog")),ledger=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.SaveLeavesAsync([(first,Period(3)),(second,Period(3))]));
        await AssertLeaveUnchanged(first,audits,ledger);await AssertLeaveUnchanged(changed,audits,ledger);Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"),Is.EqualTo(existing?2:1));
    }
    [Test,Category("Leaves")]
    public async Task BulkLeaveCannotUseARecreatedSecondPersonAndRollsBackTheFirst()
    {
        await SeedLeavePerson();await SeedLeavePerson(22222222222L);var first=await LeaveSelectionAsync();var staleSecond=await LeaveSelectionAsync(22222222222L);
        await Service.DeleteAsync(PersonnelGroup.General,staleSecond.Tc,staleSecond.PersonVersion);await Service.SaveAsync(PersonnelGroup.General,Person(staleSecond.Tc) with{FirstName="Replacement"},true);
        var second=await LeaveSelectionAsync(staleSecond.Tc);int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog")),ledger=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.SaveLeavesAsync([(first,Period(2)),(staleSecond,Period(2))]));await AssertLeaveUnchanged(first,audits,ledger);await AssertLeaveUnchanged(second,audits,ledger);
    }
    [TestCase(false),TestCase(true),Category("Leaves")]
    public async Task BulkLeaveAuditFailureRestoresEveryLeaveVersion(bool existing)
    {
        await SeedLeavePerson(hasLeave:existing);await SeedLeavePerson(22222222222L,existing);var first=await LeaveSelectionAsync();var second=await LeaveSelectionAsync(22222222222L);
        int audits=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog")),ledger=Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Leave.SavePlan') THROW 51099,N'Test leave audit failure',1;");
        await Assert.ThrowsAsync<SqlException>(()=>Service.SaveLeavesAsync([(first,Period(2)),(second,Period(2))]));await AssertLeaveUnchanged(first,audits,ledger);await AssertLeaveUnchanged(second,audits,ledger);
    }
    [Test,Category("Migrations")]
    public async Task LeaveVersionUpgradeIsRepeatableAndPreservesLeavesAndSessions()
    {
        await SeedLeavePerson();var previous=admin;Execute("ALTER TABLE dbo.izinler DROP COLUMN LeaveVersion; DELETE dbo.__MigrationJournal WHERE ScriptName LIKE N'%105-leave-rowversion.sql:%'");
        string folder=Path.Combine(directory,"leave-upgrade");await SchemaUpgrade.PrepareAsync(connection,safetyFolder:folder);await SchemaUpgrade.PrepareAsync(connection,safetyFolder:folder);
        Assert.That((await LeaveSelectionAsync()).LeaveVersion,Has.Length.EqualTo(8));Assert.That(Scalar("SELECT IZINBASLANGIC FROM dbo.izinler"),Is.EqualTo(Period(1).Start));Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal"),Is.EqualTo(6));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection,previous,default),Is.True);Assert.That(Directory.GetFiles(folder,"*.bak"),Has.Length.EqualTo(1));
    }
    [Test,Category("Migrations")]
    public async Task IncompatibleLeaveVersionStopsUpgradeWithoutRewritingLeaveData()
    {
        await SeedLeavePerson();Execute("ALTER TABLE dbo.izinler DROP COLUMN LeaveVersion; ALTER TABLE dbo.izinler ADD LeaveVersion binary(8) NULL; DELETE dbo.__MigrationJournal WHERE ScriptName LIKE N'%105-leave-rowversion.sql:%'");
        string folder=Path.Combine(directory,"leave-upgrade-incompatible");
        try{await Assert.ThrowsAsync<InvalidOperationException>(()=>SchemaUpgrade.PrepareAsync(connection,safetyFolder:folder));Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal WHERE ScriptName LIKE N'%105-leave-rowversion.sql:%'"),Is.Zero);Assert.That(Scalar("SELECT IZINBASLANGIC FROM dbo.izinler"),Is.EqualTo(Period(1).Start));}
        finally{Execute("ALTER TABLE dbo.izinler DROP COLUMN LeaveVersion");await SchemaUpgrade.PrepareAsync(connection,safetyFolder:folder);}
    }
}
