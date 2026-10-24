# AccountingApp

.NET 10 Windows Forms uygulaması: personel, izin ve kalan ödeme tutarı takibi. Rehberdeki yöntemler mevcut işlevlere uyarlandı. Ayrıntılı madde eşlemesi `docs/REHBER_UYGULAMA_TAKIBI.md`, kullanım ve sınama adımları `docs/KULLANIM_VE_TEST.md` içindedir.

Güncel paket: `artifacts\releases\guide\WinFormsApp2.exe`. Masaüstündeki **Muhasebe - Güncel** kısayolu bu paketi açar. Dağıtım klasörünün tamamı birlikte taşınır; paket .NET 10.0.13 çalışma bileşenlerini içerir. LocalDB veya güvenilir TLS sertifikası yapılandırılmış yerel SQL Express gerekir.

Uygulama EXE yanındaki `appsettings.json` dosyasını kullanır. Varsayılan bağlantı, kullanıcının Documents/Muhasebe2.mdf dosyasıdır. Yönetici, Ayarlar ekranından başka JSON profilini doğrulayarak seçebilir; değişiklik yeniden girişte uygulanır. SQL Express örneği `database/appsettings.sqlexpress.example.json` içindedir. İlk kurulum ve mevcut veritabanı yükseltmesi farklı işlemlerdir; `database/README.md` ayrıntıları açıklar.

Yeni özellikler:

- Yönetici, personel sorumlusu ve görüntüleyici rolleri; servislerde yetki denetimi ve son yönetici koruması.
- Gerçek async veri erişimi, cancellation, repository ve bir işlem için UnitOfWork; commit yoksa rollback.
- Transaction'a bağlı audit, ödeme defteri ve atomik bakiye düşümü; tekrarlanan işlem kimliği ikinci ödeme oluşturmaz. Ödeme iptalini yönetici yapabilir.
- Açık/koyu tema, kolon düzeni, üç bölümlü durum çubuğu ve klavye kısayolları; şifrelenmiş personel/izin taslakları.
- Excel/CSV, A4 ve 80 mm ödeme fişi, QR referansı, günlük ödeme kapanışı, tarih sıralı ve renkli izin uyarıları, toplu izin planı.
- Yükseltmeden önce doğrulanmış yedek; yönetici oturumunda günlük yedek, son 14 kayıt, isteğe bağlı aynalama ve onaylı geri yükleme.

PBKDF2-HMAC-SHA256, 600.000 yineleme, rastgele 16 bayt salt ve 32 bayt hash korunur. Eski parolalar kontrollü transaction ile dönüştürülür; bozuk hash açık metne geri düşmez. Kullanıcı listesi yalnızca kullanıcı adı ve rol okur. Giriş ve hesap ekleme parola alanları maskelidir. Parola boşlukları korunur; yeni parola sınırı 6–1024 karakterdir.

Para girişinde `1500,50`, `1500.50`, `1.500,50` ve `1,500.50` aynı tutardır. En fazla iki ondalık kabul edilir; `1.500`/`1,500` belirsiz olduğu için reddedilir. Önceki negatif tutar davranışı korunur; pozitif olmayan ödeme yapılamaz. Önceden yanlış kaydedilmiş tutarlar otomatik değiştirilmez.

Personel EKLE otomatik izin oluşturmaz. DÜZENLE izin tarihlerini değiştirmez. İZNİ KAYDET yalnızca seçili personelin izin dönemini kaydeder. Birden fazla eski izin dönemi varsa topluca değiştirme reddedilir. İzin hesabı başlangıç ve bitiş dahil takvim günüdür. Hayali izin hakedişi veya yasal izin kuralı üretilmez.

Personel tutarı **kalan/verilecek tutardır**. Yeni şema mevcut tutarları açılış bakiyesine aktarır. Düzenleme farkı deftere eklenir; ödeme tutarı atomik azaltır; iptal geri artırır. Arada ödeme yapıldıysa eski ekrandan kalan tutarı ezerek düzenleme engellenir. Personel silinse bile ödeme defteri korunur.

Ekranlar tek ApplicationContext ile yönetilir. Geçişte önceki pencere dispose edilir; çıkış kullanıcı ve rolü temizler. Bekleyen iş iptal edilir; kapalı ekrana gelen giriş sonucu yeni oturum açamaz.

Derleme ve test:

```powershell
.\dotnet.cmd restore WinFormsApp2.sln --locked-mode
.\dotnet.cmd build WinFormsApp2.sln -c Release --no-restore -p:TreatWarningsAsErrors=true
powershell.exe -NoProfile -File .\automation\otomatik_test_master.ps1 -Full
```

`-NoDatabase` yalnızca DB'siz testleri çalıştırır. Varsayılan master çalıştırma restore testini dışarıda bırakır; `-Full` yalnızca testin oluşturduğu GUID adlı veritabanında restore yapar. Gerçek Muhasebe2.mdf test hedefi olmaz. Raporlar `artifacts/analysis/master-*/` altında JSON, Markdown ve TRX olarak üretilir. Testler ayrı geçici klasör kullanır; antivirüs ayarlarına dokunulmaz.

SDK `global.json` ile 10.0.402 sürümüne sabitlenmiştir. `dotnet.cmd` varsa `artifacts/tools/dotnet` içindeki SDK'yı, yoksa sistemdeki dotnet.exe'yi kullanır. Paket kilitleri kaynak denetimindedir. `.github/workflows/ci.yml`, Windows üzerinde aynı tam testleri ve self-contained paketlemeyi tanımlar; uzak GitHub çalıştırması yerel testten ayrı bir işlemdir.

Paket üretimi:

```powershell
.\dotnet.cmd publish WinFormsApp2.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -p:TreatWarningsAsErrors=true -o artifacts\releases\guide
.\dotnet.cmd publish tools\DatabaseSetup\AccountingApp.DatabaseSetup.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -p:TreatWarningsAsErrors=true -o artifacts\releases\guide
```
