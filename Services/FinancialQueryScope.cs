using System.Data;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class FinancialQueryScope
{
    internal Wire.FinancialFilter Filter{get;}
    private readonly DateTime? start,end;private readonly string pattern;
    internal FinancialQueryScope(Wire.FinancialFilter? filter)
    {
        filter??=new();var person=(filter.Person??"").Trim();if(person.Length>100||person.Any(char.IsControl))throw new ArgumentException("Kişi araması en fazla 100 karakter olmalı.");
        foreach(var day in new[]{filter.From,filter.To})if(day.HasValue&&(day.Value.TimeOfDay!=TimeSpan.Zero||day.Value.Year is <1900 or >2199))throw new ArgumentException("Tarih yalnız gün olmalı (1900–2199).");
        if(filter.From>filter.To)throw new ArgumentException("Başlangıç bitişten sonra olamaz.");Filter=new(filter.From?.Date,filter.To?.Date,person);
        if(filter.From.HasValue)start=PaymentReportService.UtcDayRange(filter.From.Value,DailyClosingService.BusinessZone).Start;
        if(filter.To.HasValue)end=PaymentReportService.UtcDayRange(filter.To.Value,DailyClosingService.BusinessZone).End;
        pattern="%"+person.Replace("~","~~").Replace("%","~%").Replace("_","~_").Replace("[","~[")+"%";
    }
    internal string Predicate(string alias,string? dateAlias=null)=>$@"(@first IS NULL OR {dateAlias??alias}.CreatedUtc>=@first) AND (@last IS NULL OR {dateAlias??alias}.CreatedUtc<@last)
 AND (@person=N'' OR {alias}.PersonId=TRY_CONVERT(uniqueidentifier,@person) OR EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines QL JOIN dbo.AccrualCalculations QH ON QH.CalculationId=QL.CalculationId WHERE QH.ProjectId=@project AND QL.PersonId={alias}.PersonId AND QL.PersonCaption LIKE @pattern ESCAPE N'~'))";
    internal void Bind(SqlCommand c){c.Parameters.Add("@first",SqlDbType.DateTime2).Value=(object?)start??DBNull.Value;c.Parameters.Add("@last",SqlDbType.DateTime2).Value=(object?)end??DBNull.Value;c.Text("@person",Filter.Person,100);c.Text("@pattern",pattern,202);}
    internal string Description=>$"Kaynak kayıt tarihi (İstanbul): {Filter.From?.ToString("dd.MM.yyyy")??"açık"} – {Filter.To?.ToString("dd.MM.yyyy")??"açık"}; kişi no/hesap adı: {(Filter.Person.Length==0?"tümü":Filter.Person)}; kaynakların güncel durumu";
    internal static (long Index,int Size) Page(long index,int size){if(index<0||size is <1 or >200||index>long.MaxValue/size)throw new ArgumentException("Geçersiz sayfa.");return(index,size);}
    // SQL already has a stable outer ORDER BY. Window count describes the entire filtered query.
    internal static async Task<(DataTable Rows,Wire.PageInfo Info)> ReadPageAsync(IUnitOfWork w,string sql,Action<SqlCommand> bind,long index,int size,CancellationToken ct,bool export=false)
    {
        Page(index, size);
        if (export)
        {
            // Export needs only the rows and one overflow sentinel.
            using var command = w.Command(sql.Replace("SELECT TOP(500)", "SELECT TOP(10001)", StringComparison.Ordinal));
            bind(command);
            var exportRows = await command.TableAsync(ct);
            if (exportRows.Rows.Count > 10000)
            {
                exportRows.Dispose();
                throw new InvalidOperationException("Seçili filtrede bölüm 10.000 satırı aşıyor; CSV için filtreyi daraltın.");
            }
            return (exportRows, new(exportRows.Rows.Count, index, size));
        }

        sql=sql.Replace("SELECT TOP(500)","SELECT COUNT_BIG(*) OVER() __Total,",StringComparison.Ordinal).Replace("SELECT TOP(10001)","SELECT COUNT_BIG(*) OVER() __Total,",StringComparison.Ordinal);
        async Task<DataTable> Read(long skip,int take){using var c=w.Command(sql+" OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY");bind(c);c.Parameters.Add("@skip",SqlDbType.BigInt).Value=skip;c.Parameters.Add("@take",SqlDbType.Int).Value=take;return await c.TableAsync(ct);}
        var rows=await Read(index*size,size);long total=rows.Rows.Count==0?0:rows.Rows[0].Field<long>("__Total");
        if (rows.Rows.Count == 0 && index > 0)
        {
            using var head = await Read(0, 1);
            total = head.Rows.Count == 0 ? 0 : head.Rows[0].Field<long>("__Total");
            index = total == 0 ? 0 : (total - 1) / size;
            if (total > 0)
            {
                rows.Dispose();
                rows = await Read(index * size, size);
            }
        }
        rows.Columns.Remove("__Total");
        return (rows, new(total, index, size));
    }
}
internal sealed record ReconciliationQuery(Wire.FinancialFilter Filter,Dictionary<string,Wire.PageRequest>? Pages=null,string? ExportSection=null)
{
    internal Dictionary<string,Wire.PageInfo> Results{get;}=new();
    internal Wire.PageRequest Page(string name)=>Pages?.GetValueOrDefault(name)??new();
}
