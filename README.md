# AccountingApp

.NET 10 Windows Forms uygulaması: personel, izin ve kalan ödeme tutarı takibi. Rehberdeki yöntemler mevcut işlevlere uyarlandı. Ayrıntılı madde eşlemesi `docs/REHBER_UYGULAMA_TAKIBI.md`, kullanım ve sınama adımları `docs/KULLANIM_VE_TEST.md` içindedir.

**Neler, ne zaman değişti?** [Tarihli değişiklik günlüğü](CHANGELOG.md) geliştirmeleri, şema yükseltmelerini ve test sonuçlarını listeler.

Tam doğrulama (26 Ekim 2026): **392 test başarılı, 0 başarısız, 0 atlanan**. Release derlemesi 0 uyarı / 0 hata ile tamamlandı; entegrasyon ve geri yükleme testleri ayrı, geçici LocalDB motorunda çalıştı.

Arayüz güncellemesi (26 Ekim 2026): [Windows 2000 görünümü, yenilenen ekranlar ve görseller](docs/WINDOWS_2000_DENEME.md). Giriş, ana ekran, personel, arama, özet, hesap yönetimi, ödeme, ayarlar, izin ve parola pencereleri düzenlendi. Bu değişikliklerin tamamı aşağıdaki **v1.1.0 Windows paketine** dahildir.

Responsive arayüz güncellemesi (27 Ekim 2026): ana ekran ve diğer 11 ekran/akış pencere boyutuna uyarlandı; sütunlar, bölümler, listeler ve işlem düğmeleri kullanılabilir alana göre yerleşir. Girilmiş bilgiler, seçimler ve güvenlik kontrolleri korunur. [Ekranlar, önizlemeler ve deneme adımları](docs/RESPONSIVE_ARAYUZLER.md). Bu güncelleme kaynak kodundadır; aşağıdaki v1.1.0 indirme paketi 26 Ekim arayüzünü içerir. Responsive arayüz için henüz yeni bir GitHub Release paketi yayımlanmadı.

Güncel kaynak doğrulaması (27 Ekim 2026): **610 test başarılı, 0 başarısız, 0 atlanan**; Release derlemesi 0 uyarı / 0 hata. Entegrasyon ve geri yükleme testleri gerçek kullanıcı veritabanından ayrı, testin oluşturduğu geçici LocalDB motorunda tamamlandı; motor temizlendi. [CI ortamındaki ekran boyutu sınırlamasının düzeltmesi](docs/CI_RESPONSIVE_TESTLER.md), geniş yerleşimlerin küçük sanal ekranlarda da gerçekten sınanmasını sağlar.

## Windows paketini indir ve kur

**[Windows x64 paketini indir (v1.1.0)](https://github.com/frknylmzkng/AccountingApp/releases/download/v1.1.0/AccountingApp-v1.1.0-win-x64.zip)** · [Sürüm notları](https://github.com/frknylmzkng/AccountingApp/releases/tag/v1.1.0) · [SHA256 doğrulama dosyası](https://github.com/frknylmzkng/AccountingApp/releases/download/v1.1.0/SHA256SUMS.txt)

1. Windows 64 bit bilgisayarda SQL Server Express LocalDB'nin kurulu olduğundan emin olun. .NET 10.0.13 çalışma bileşenleri ZIP'e dahildir; ayrıca .NET veya SDK kurmanız gerekmez.
2. ZIP'in **tamamını yeni bir klasöre** çıkarın; yalnızca EXE dosyasını taşımayın. Güncelleme yapıyorsanız önce açık uygulamayı kapatın.
3. Veritabanınızı aşağıdaki uygun yöntemle hazırlayın, ardından `WinFormsApp2.exe` dosyasını açıp hesabınızla giriş yapın.
4. Kısayol kullanıyorsanız hedefini yeni klasördeki `WinFormsApp2.exe` dosyasına yönlendirin; çalışma klasörünü de aynı klasör olarak ayarlayın.

**Mevcut veritabanını kullanıyorsanız:** Önceki `appsettings.json` bağlantı ayarınızı yeni klasöre taşıyın veya mevcut MDF yolunu bu dosyada belirtin. Varsayılan yol `%USERPROFILE%\Documents\Muhasebe2.mdf` dosyasıdır. Mevcut kullanıcı adı ve parolanızla giriş yapabilirsiniz; ilk kurulum komutlarını çalıştırmayın.

**Yalnızca ilk kurulumda:** Paketi çıkardığınız klasörde PowerShell açıp şu komutları çalıştırın:

```powershell
Copy-Item .\database\appsettings.new-install.example.json .\appsettings.json
.\AccountingApp.DatabaseSetup.exe --config .\appsettings.json
```

Kurulum aracının istediği ilk yönetici kullanıcı adını, parolayı ve parola tekrarını girin. Sabit başlangıç parolası yoktur. Kurulum tamamlanınca `WinFormsApp2.exe` dosyasını açın. Ayrıntılar: [veritabanı kurulumu](database/README.md) ve [kullanım kılavuzu](docs/KULLANIM_VE_TEST.md).

v1.1.0 paketi uygulamayı, `AccountingApp.DatabaseSetup.exe` kurulum aracını, veritabanı betiklerini ve kullanım belgelerini içerir. Gerçek kullanıcı veritabanı pakete dahil değildir. Başlangıçta kurulu Microsoft SqlLocalDB aracı shell kullanmadan çağrılır; bu işlem veritabanını silmez veya yeniden oluşturmaz. LocalDB yerine güvenilir TLS sertifikası yapılandırılmış yerel SQL Express de kullanılabilir.

Uygulama EXE yanındaki `appsettings.json` dosyasını kullanır. Varsayılan bağlantı, kullanıcının Documents/Muhasebe2.mdf dosyasıdır. Yönetici, Ayarlar ekranından başka JSON profilini doğrulayarak seçebilir; değişiklik yeniden girişte uygulanır. SQL Express örneği `database/appsettings.sqlexpress.example.json` içindedir. İlk kurulum ve mevcut veritabanı yükseltmesi farklı işlemlerdir; `database/README.md` ayrıntıları açıklar.

Yeni özellikler:

- Yönetici, personel sorumlusu ve görüntüleyici rolleri; servislerde yetki denetimi ve son yönetici koruması.
- Gerçek async veri erişimi, cancellation, repository ve bir işlem için UnitOfWork; commit yoksa rollback.
- Transaction'a bağlı audit, ödeme defteri ve atomik bakiye düşümü; tekrarlanan işlem kimliği ikinci ödeme oluşturmaz. Ödeme iptalini yönetici yapabilir.
- Açık/koyu tema, kolon düzeni, üç bölümlü durum çubuğu ve klavye kısayolları; şifrelenmiş personel/izin taslakları.
- Excel/CSV, A4 ve 80 mm ödeme fişi, QR referansı, günlük ödeme kapanışı, tarih sıralı ve renkli izin uyarıları, toplu izin planı.
- Yükseltmeden önce doğrulanmış yedek; yönetici oturumunda günlük yedek, son 14 kayıt, isteğe bağlı aynalama ve onaylı geri yükleme.

PBKDF2-HMAC-SHA256, 600.000 yineleme, rastgele 16 bayt salt ve 32 bayt hash korunur. Eski parolalar kontrollü transaction ile dönüştürülür; bozuk hash açık metne geri düşmez. Kullanıcı listesinde parola veya hash okunmaz. Parola alanları maskelidir. Parola boşlukları korunur; yeni hesaplarda ve parola değişiminde sınır 15–1024 karakterdir. Mevcut kısa parolalar değiştirilmelerine kadar girişte geçerlidir.

Her kullanıcı üst menüdeki **Parolamı değiştir** ile mevcut parolasını doğrulayarak yeni parola belirleyebilir. Yönetici, hesap listesinden başka bir etkin hesabı seçip **Parola sıfırla…** ile kendi parolasını tekrar doğrular. Uygulama 30 dakika geçerli rastgele geçici parola üretir; bu hesap kendi parolasını belirleyene kadar diğer ekranlara erişemez. Parola değişimi ve sıfırlama eski oturumları geçersiz kılar. Ayrıntılar ve deneme adımları: [parola güvenliği kılavuzu](docs/PAROLA_DEGISTIRME_VE_SIFIRLAMA.md). Bu özellikler yukarıdaki v1.1.0 Release paketine dahildir.

Hatalı giriş sınırı: aynı hesapta 15 dakika içinde 5 yanlış parola, 5 dakikalık geçici kilit oluşturur. Sayaç veritabanında tutulur; yeniden başlatma veya farklı pencereler sınırı aşamaz. Açık oturumlar etkilenmez. Mevcut parolayla yeniden doğrulamalar da aynı bütçeyi kullanır. [Kullanım ve deneme kılavuzu](docs/HATALI_GIRIS_SINIRLAMASI.md).

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
.\dotnet.cmd publish WinFormsApp2.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -p:TreatWarningsAsErrors=true -o artifacts\releases\v1.1.0
.\dotnet.cmd publish tools\DatabaseSetup\AccountingApp.DatabaseSetup.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -p:TreatWarningsAsErrors=true -o artifacts\releases\v1.1.0
```
