using System.Security.Cryptography;
using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal async Task SaveManualBatchAsync(Wire.ManualTimesheetBatch input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("timesheets/manual-batch",input,ct);return;}
        if(input.PlannedRows==null||input.UnplannedRows==null||input.PlannedRows.Length+input.UnplannedRows.Length is <1 or >200||input.PlannedRows.Any(x=>x==null)||input.UnplannedRows.Any(x=>x==null))throw new ArgumentException("Toplam 1–200 manuel puantaj satırı gerekiyor.");
        var ids=input.PlannedRows.Select(x=>x.TimesheetId).Concat(input.UnplannedRows.Select(x=>x.TimesheetId)).ToArray();
        if(ids.Any(x=>x==Guid.Empty)||ids.Distinct().Count()!=ids.Length||input.PlannedRows.Any(x=>x.Version!=null)||input.UnplannedRows.Any(x=>x.Version!=null||x.ProjectId!=input.ProjectId||x.CommandId!=input.CommandId||x.Reason!=input.Reason))throw new ArgumentException("Toplu manuel giriş yalnız farklı kimlikli yeni kayıtlar içindir; mevcut kayıtları listeden sürümlü düzenleyin.");
        if(input.PlannedRows.Select(x=>x.ShiftAssignmentId).Distinct().Count()!=input.PlannedRows.Length||input.UnplannedRows.Select(x=>(x.PersonId,x.WorkDate)).Distinct().Count()!=input.UnplannedRows.Length)throw new ArgumentException("Aynı vardiya ataması veya vardiyasız kişi/iş günü pakette tekrarlanamaz.");
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await AllocationGateAsync(w,ct);await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);
        Guid actor=await ActorAsync(w,ct);byte[] hash=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        foreach(var row in input.PlannedRows.OrderBy(x=>x.ShiftAssignmentId))
        {try{await SaveRowAsync(w,input.ProjectId,actor,row,ct);}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){throw new InvalidOperationException($"Vardiyalı #{Array.IndexOf(input.PlannedRows,row)+1}: {ex.Message} Paketin tamamı geri alındı.",ex);}}
        foreach(var row in input.UnplannedRows.OrderBy(x=>x.PersonId).ThenBy(x=>x.WorkDate))
        {try{await SaveUnplannedRowAsync(w,row,actor,ct);}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){throw new InvalidOperationException($"Vardiyasız #{Array.IndexOf(input.UnplannedRows,row)+1}: {ex.Message} Paketin tamamı geri alındı.",ex);}}
        await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
}
