using System.Security.Cryptography;
using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal async Task<Wire.TimesheetDraftContext> DraftContextAsync(Guid project, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString,user).SendAsync<Wire.TimesheetDraftContext>($"timesheets/{project:D}/draft-context",null,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.EnterTimesheets,ct);
        Guid actor=await ActorAsync(w,ct);
        using var c=w.Command("SELECT SessionEpoch FROM dbo.admin WHERE AccountId=@id");Id(c,"@id",actor);
        Guid epoch=(Guid)(await c.ExecuteScalarAsync(ct)??throw new SessionRevokedException());
        string scope=(await BackupScope.ReadAsync(connectionString,ct)).Key;
        await w.CommitAsync(ct);return new(actor,epoch,scope);
    }
    internal async Task CheckDraftPreviewAsync(Wire.TimesheetDraftPreviewAccess input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("timesheets/drafts/preview-access",input,ct);return;}
        if(input.Projects==null||input.Projects.Length>50)throw new ArgumentException("Taslak proje kapsamı geçersiz.");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);
        foreach(Guid p in input.Projects.Distinct().Order())await ProjectAuthorization.DemandAsync(w,user,p,ProjectAccess.Read,ct);
        await w.CommitAsync(ct);
    }
    internal async Task<Wire.TimesheetDraftResolution> ResolveDraftAsync(Wire.TimesheetDraftResolve input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.TimesheetDraftResolution>("timesheets/drafts/resolve",input,ct);
        var command=TimesheetDraftCommands.Parse(input.Kind,input.Payload);
        var identity=TimesheetDraftCommands.Identity(command);
        if(identity.Project!=input.ProjectId||identity.Command==Guid.Empty)throw new ArgumentException("Taslak proje/işlem kimliği geçersiz.");
        byte[] hash=command is Wire.WorkAllocation?EventHash("Allocation.Save.v1",command):SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(command,command.GetType()));
        using var reason=AuditContext.UseReason(input.Cancel?AuditContext.ValidateReason(input.Reason):"Taslak işlem sonucu sorgusu");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);
        Guid actor=await ActorAsync(w,ct);
        // Same range-lock order as ReplayAsync: cancellation cannot race a commit or late arrival.
        if(await ReplayAsync(w,identity.Command,input.ProjectId,actor,hash,ct,allowCanceled:true)) {await w.CommitAsync(ct);return new("Recorded");}
        using var c=w.Command("SELECT ActorId,PayloadHash FROM dbo.TimesheetCanceledCommands WITH(UPDLOCK,HOLDLOCK) WHERE CommandId=@id");Id(c,"@id",identity.Command);
        using(var r=await c.ExecuteReaderAsync(ct))if(await r.ReadAsync(ct))
        {if(r.GetGuid(0)!=actor||!r.GetFieldValue<byte[]>(1).SequenceEqual(hash))throw new InvalidOperationException("Taslak işlem kimliği farklı içerikle kullanılmış.");await r.DisposeAsync();await w.CommitAsync(ct);return new("Canceled");}
        if(input.Cancel)
        {
            using var cancel=w.Command("INSERT dbo.TimesheetCanceledCommands(CommandId,ProjectId,ActorId,PayloadHash) VALUES(@id,@project,@actor,@hash)");Id(cancel,"@id",identity.Command);Id(cancel,"@project",input.ProjectId);Id(cancel,"@actor",actor);cancel.Parameters.Add("@hash",System.Data.SqlDbType.Binary,32).Value=hash;await cancel.ExecuteNonQueryAsync(ct);
            await w.CommitAsync(ct);return new("Canceled");
        }
        await w.CommitAsync(ct);return new("NotRecorded");
    }
}
