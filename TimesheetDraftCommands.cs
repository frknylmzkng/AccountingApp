using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal static class TimesheetDraftCommands
{
    internal static object Parse(string kind,JsonElement payload)
    {
        if(payload.ValueKind!=JsonValueKind.Object||payload.GetRawText().Length>2*1024*1024)throw new ArgumentException("Taslak istek biçimi/boyutu geçersiz.");
        return kind switch
        {
            "ManualBatch"=>payload.Deserialize<Wire.ManualTimesheetBatch>()??throw new ArgumentException("Toplu manuel taslak boş."),
            "Planned" or "Csv"=>payload.Deserialize<Wire.TimesheetBatch>()??throw new ArgumentException("Puantaj taslağı boş."),
            "Unplanned"=>payload.Deserialize<Wire.UnplannedTimesheetWrite>()??throw new ArgumentException("Vardiyasız taslak boş."),
            "Allocation"=>payload.Deserialize<Wire.WorkAllocation>()??throw new ArgumentException("Dağılım taslağı boş."),
            _=>throw new ArgumentException("Desteklenmeyen taslak türü.")
        };
    }
    internal static (Guid Project,Guid Command,string Reason) Identity(object request)=>request switch
    {
        Wire.ManualTimesheetBatch x=>(x.ProjectId,x.CommandId,x.Reason),
        Wire.TimesheetBatch x=>(x.ProjectId,x.CommandId,x.Reason),
        Wire.UnplannedTimesheetWrite x=>(x.ProjectId,x.CommandId,x.Reason),
        Wire.WorkAllocation x=>(x.ProjectId,x.CommandId,x.Reason),
        _=>throw new ArgumentException("Taslak komutu geçersiz.")
    };
}
