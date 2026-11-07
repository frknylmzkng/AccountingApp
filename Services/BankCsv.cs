using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

// Parse exactly the operator's declared format. Never guess the sign, culture or date.
internal static class BankCsv
{
    internal const int MaxBytes=24000,MaxRows=200;
    internal sealed record Row(int Line,string ExternalId,DateTime Day,decimal Amount,string Iban,string Description,Guid? PaymentReference,byte[] Hash);
    internal static byte[] Hash(object value)=>SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,value.GetType()));
    internal static string Iban(string value,bool optional=false)
    {
        string s=value.Replace(" ","").ToUpperInvariant();if(optional&&s.Length==0)return s;
        if(!Regex.IsMatch(s,"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",RegexOptions.CultureInvariant))throw new ArgumentException("Geçerli IBAN gerekiyor.");
        int rem=0;foreach(char c in s[4..]+s[..4]){if(char.IsAsciiDigit(c))rem=(rem*10+c-'0')%97;else rem=(rem*100+c-'A'+10)%97;}
        if(rem!=1||(s.StartsWith("TR",StringComparison.Ordinal)&&s.Length!=26))throw new ArgumentException("IBAN kontrol basamakları geçersiz.");return s;
    }
    internal static List<string[]> Cells(string csv,string separator)
    {
        if(csv.Length==0||Encoding.UTF8.GetByteCount(csv)>MaxBytes||separator is not(";" or "," or "\t"))throw new ArgumentException("UTF-8 CSV en fazla 24.000 bayt; ayırıcı noktalı virgül, virgül veya sekme olmalı.");
        var result=new List<string[]>();var fields=new List<string>();var cell=new StringBuilder();bool quoted=false,closed=false;
        void Field(){fields.Add(cell.ToString());cell.Clear();closed=false;if(fields.Count>40)throw new ArgumentException("CSV en fazla 40 sütun içerebilir.");}
        void Line(){Field();result.Add(fields.ToArray());fields.Clear();if(result.Count>MaxRows+1)throw new ArgumentException("Tek dosyada en fazla 200 hareket aktarılabilir.");}
        for(int i=0;i<csv.Length;i++)
        {
            char c=csv[i];if(i==0&&c=='\uFEFF')continue;
            if(quoted){if(c=='"'){if(i+1<csv.Length&&csv[i+1]=='"'){cell.Append('"');i++;}else{quoted=false;closed=true;}}else{if(char.IsControl(c))throw new ArgumentException("CSV hücreleri satır sonu veya kontrol karakteri içeremez.");cell.Append(c);}continue;}
            if(c==separator[0]){Field();continue;}
            if(c is '\r' or '\n'){if(c=='\r'&&i+1<csv.Length&&csv[i+1]=='\n')i++;Line();continue;}
            if(c=='"'&&cell.Length==0&&!closed){quoted=true;continue;}
            if(c=='"'||closed||char.IsControl(c))throw new ArgumentException("CSV tırnak/ayırıcı düzeni geçersiz.");cell.Append(c);
        }
        if(quoted)throw new ArgumentException("CSV içinde kapanmamış tırnak var.");if(cell.Length>0||fields.Count>0||closed)Line();
        if(result.Count==0||result[0].Length<5)throw new ArgumentException("CSV başlık satırı gerekiyor.");
        var headers=result[0].Select(x=>x.Trim()).ToArray();if(headers.Any(x=>x.Length is <1 or >100)||headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=headers.Length)throw new ArgumentException("CSV sütun başlıkları dolu ve benzersiz olmalı.");result[0]=headers;
        if(result.Skip(1).Any(x=>x.Length!=headers.Length))throw new ArgumentException("Tüm CSV satırları başlıkla aynı sütun sayısında olmalı.");return result;
    }
    internal static Row[] Parse(Wire.BankCsvSource source,string accountIban)
    {
        if(source.DateFormat is not("yyyy-MM-dd" or "dd.MM.yyyy")||source.DecimalSeparator is not("." or ","))throw new ArgumentException("Tarih ve ondalık biçimini açık seçin.");
        var lines=Cells(source.Csv,source.Separator);if(lines.Count<2)throw new ArgumentException("CSV veri satırı içermiyor.");var map=source.Columns;
        if(map==null||map.Length!=8||map.Take(5).Any(x=>x<0)||map.Any(x=>x< -1||x>=lines[0].Length)||map.Where(x=>x>=0).Distinct().Count()!=map.Count(x=>x>=0))throw new ArgumentException("Hesap IBAN, işlem kimliği, tarih, işaretli tutar ve para birimi için ayrı sütunlar seçin.");
        var rows=new List<Row>();var ids=new HashSet<string>(StringComparer.Ordinal);string pattern=source.DecimalSeparator=="."?"^[+-]?[0-9]{1,16}(\\.[0-9]{1,2})?$":"^[+-]?[0-9]{1,16}(,[0-9]{1,2})?$";
        for(int n=1;n<lines.Count;n++)
        {
            string Get(int col)=>map[col]<0?"":lines[n][map[col]].Trim().Normalize(NormalizationForm.FormC);
            try
            {
                if(Iban(Get(0))!=accountIban)throw new ArgumentException("Kaynak hesap IBAN seçilen hesapla aynı değil.");string id=Get(1);if(id.Length is <1 or >100||!ids.Add(id))throw new ArgumentException("Banka işlem kimliği dolu, benzersiz ve en fazla 100 karakter olmalı.");
                if(!DateTime.TryParseExact(Get(2),source.DateFormat,CultureInfo.InvariantCulture,DateTimeStyles.None,out var day)||day.Year is <1900 or >2199)throw new ArgumentException("Tarih seçilen biçimle eşleşmiyor (1900–2199).");
                string amount=Get(3);if(!Regex.IsMatch(amount,pattern,RegexOptions.CultureInvariant)||!decimal.TryParse(amount.Replace(',','.'),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal value)||value==0||Math.Abs(value)>9999999999999999.99m)throw new ArgumentException("Tutar sıfır dışı, en fazla iki ondalıklı olmalı; binlik ayırıcı kullanmayın. Çıkış eksi, giriş artıdır.");
                if(Get(4)!="TRY")throw new ArgumentException("Bu aktarım yalnız TRY para birimini kabul eder.");string iban=Iban(Get(5),true),description=Get(6);if(description.Length>500)throw new ArgumentException("Açıklama en fazla 500 karakter olmalı.");
                Guid? reference=null;if(Get(7).Length>0){if(!Guid.TryParseExact(Get(7),"D",out var guid)||guid==Guid.Empty)throw new ArgumentException("Ödeme referansı uygulamanın GUID işlem kimliği olmalı; bankanın açıklama metni bu sütuna eşlenmemeli.");reference=guid;}
                rows.Add(new(n+1,id,day.Date,value,iban,description,reference,Hash(new{id,Day=day.ToString("yyyy-MM-dd"),Amount=value.ToString("F2",CultureInfo.InvariantCulture),Iban=iban,Description=description,Reference=reference})));
            }
            catch(ArgumentException ex){throw new ArgumentException($"CSV satır {n+1}: {ex.Message}");}
        }
        return rows.ToArray();
    }
}
