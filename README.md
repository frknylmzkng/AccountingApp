# AccountingApp

C# Windows Forms ve SQL Server LocalDB ile personel, ödeme tutarı ve izin takibi.

Kaynak kodu Windows üzerinde .NET 10 SDK ve SQL Server Express LocalDB ile derlenir. `global.json`, SDK 10.0.402 ve aynı banttaki sonraki yamaları kullanır. Bu bilgisayarda SDK, `artifacts\tools\dotnet` klasörüne yerel olarak kuruldu; mevcut sistem SDK'sı değiştirilmedi. `dotnet.cmd` yerel SDK varsa onu, yoksa sistemdeki dotnet.exe'yi çalıştırır. Visual Studio ile geliştirmek için .NET 10 destekleyen bir sürüm gerekir.

Güncel Release paketi Windows 64 bit içindir; .NET 10.0.13 çalışma bileşenlerini içerir. Paketi çalıştırmak için ayrıca .NET SDK gerekmez; LocalDB yine gereklidir. Bütün ekranların bağlantısı EXE yanındaki appsettings.json dosyasından tek noktada okunur. Varsayılan ayar mevcut kullanıcının Documents/Muhasebe2.mdf dosyasını açar; kaynak kodda kişisel veritabanı yolu bulunmaz.

Yeni kurulum için sürümlü SQL betikleri, ayrı kurulum aracı ve PowerShell çalıştırıcısı hazırdır. İlk yönetici parola hash'iyle oluşturulur; sabit başlangıç parolası bulunmaz. Mevcut dosyalar/hesaplar üzerine yazılmaz. Ayrıntılar ve yeni bilgisayarda kurulum adımları [veritabanı kılavuzundadır](<C:/Users/FURKAN YILMAZ/Desktop/AccountingApp-master/AccountingApp-master/database/README.md>).

Derleme:

```powershell
.\dotnet.cmd restore WinFormsApp2.sln --locked-mode
.\dotnet.cmd build WinFormsApp2.sln --configuration Release --no-restore
```

Para girişi, bilgisayarın bölge ayarından bağımsızdır. `1500,50`, `1500.50`, `1.500,50` ve `1,500.50` aynı tutardır. En fazla iki ondalık basamak kabul edilir. `1.500` ve `1,500` belirsiz olduğundan reddedilir; bin beş yüz için `1500` veya `1.500,00` yazın. Negatif tutarlara ilişkin önceki davranış korunmuştur.

Personel ve yönetici tabloları salt okunur ve tek satır seçimiyle çalışır. Düzenleme için önce listeden kayıt seçilir; TC değiştirilerek başka kayda güncelleme yapılamaz. Silme, seçilen kaydın veritabanından yüklenen özgün anahtarını kullanır. Joker işlemleri yalnızca Joker tablosunu hedefler.

Genel ekranında EKLE yalnızca personel kaydeder; otomatik izin oluşturmaz. DÜZENLE personel bilgilerini günceller ve izinlere dokunmaz. İzin tanımlamak veya mevcut tek izin dönemini değiştirmek için önce listeden personeli seçin, tarihleri girin ve ayrı İZNİ KAYDET düğmesini kullanın. Bu düğme personel bilgilerini değiştirmez. Seçim yokken izin alanları kapalıdır. TEMİZLE seçimi ve bütün girişleri, izin tarihleri dahil, sıfırlar.

İzin bitişi başlangıçtan önce olamaz; aynı gün başlayıp biten izin geçerlidir. Karşılaştırma saatten bağımsız, takvim günü üzerinden yapılır. Mevcut tek dönem modeli korunur; aynı kişide birden fazla eski izin kaydı varsa toplu tarih güncellemesi reddedilir. İzin türleri ve ayrı dönemler halinde izin geçmişi bu aşamanın kapsamı dışındadır. Eski otomatik izin kayıtları gerçek izinlerden güvenilir biçimde ayırt edilemeyeceği için silinmez.

Personel ve izin işlemleri transaction içinde yürür. Personel silme ve ilişkili izinlerin silinmesi atomiktir. İzin kaydında personelin varlığı ve mevcut izin sayısı aynı transaction içinde kontrol edilir; eşzamanlı ilk kayıt iki izin satırı oluşturmaz. Bir adım hata verirse işlem geri alınır. Başarı yalnızca commit sonrasında gösterilir; başarısız işlem formu temizlemez. Yönetici sayımı ve silme de aynı transaction içindedir.

Oturum ve ekran geçişleri tek bir ApplicationContext tarafından yönetilir. Başarılı girişte giriş penceresi kapatılır; her ekran geçişinde önceki form kapatılıp dispose edilir. Geri düğmesi ana menüye döner. Ana menüde aktif kullanıcı adı ve ÇIKIŞ YAP düğmesi bulunur. Çıkışta kullanıcı bilgisi temizlenir, açık ekran ve ona ait alt pencereler kapatılır; boş bir giriş ekranı açılır. Yeniden giriş yapılmadan personel/yönetici ekranlarına geçilemez. Geçerli pencerenin X düğmesiyle kapatılması uygulamayı sonlandırır. Kapatılmış bir giriş ekranında bekleyen doğrulama sonucu yeni pencere açamaz.

Parolalar PBKDF2-HMAC-SHA256 ile 600.000 yineleme, her parola için rastgele 16 bayt salt ve 32 bayt hash kullanılarak `admin.password_hash` alanında saklanır. Yönetici listesi yalnızca kullanıcı adlarını sorgular; parola veya hash alanını okumaz. Yönetici ekleme ve giriş parola alanları maskelidir. Parolaların başındaki/sonundaki boşluklar korunur ve büyük/küçük harfler önemlidir. Yeni parolalar için mevcut altı karakter alt sınırı korunmuştur; üst sınır 1024 karakterdir.

Uygulama ilk açıldığında `password_hash nvarchar(256)` kolonu gerektiğinde eklenir, eski açık metin parolalar aynı transaction içinde hash'e dönüştürülür ve eski `password` alanı boşaltılır. Kullanıcılar mevcut parolalarıyla giriş yapabilir. Geçiş yeniden çalıştırılabilir; daha önce hash'e çevrilen parolalar tekrar hash'lenmez. Şema değişikliği için bağlantıyı açan Windows kullanıcısının admin tablosunda ALTER yetkisi gerekir. Herhangi bir adım başarısızsa şema ve parola değişiklikleri geri alınır. Bozuk veya desteklenmeyen hash değerleri açık metin parolaya dönülmeden reddedilir.

Testler çözümdeki `WinFormsApp2.RegressionTests` projesindedir. Veritabanı gerektirmeyen para, parola hash'i, kayıt seçimi ve form bağlantısı testleri:

```powershell
.\dotnet.cmd test tests/WinFormsApp2.RegressionTests/WinFormsApp2.RegressionTests.csproj --filter "TestCategory!=LocalDb"
```

Tüm testler, LocalDB kurulu ve erişilebilir bir Windows kullanıcı oturumunda çalıştırılır:

```powershell
.\dotnet.cmd test tests/WinFormsApp2.RegressionTests/WinFormsApp2.RegressionTests.csproj
```

Yalnızca oturum ve pencere yönetimi testleri:

```powershell
.\dotnet.cmd test WinFormsApp2.sln --configuration Release --filter "FullyQualifiedName~SessionManagementTests"
```

Uygulama açıkken mevcut derleme dosyası kilitliyse ayrı Release çıktısı hazırlanabilir:

```powershell
.\dotnet.cmd build WinFormsApp2.sln --configuration Release
.\dotnet.cmd run --project WinFormsApp2.csproj --configuration Release --no-build
```

Yeni oturum davranışı güncellenmiş uygulama yeniden açıldığında kullanılır. Açık eski sürüm bu değişiklikleri kendiliğinden yüklemez.

Güncel .NET 10 Release çıktısı `artifacts\releases\database-config\WinFormsApp2.exe` konumundadır. Masaüstündeki Muhasebe - Güncel kısayolu bu sürümü açar. Açık eski uygulama kendiliğinden yeni sürüme geçmez; kapatıp kısayoldan yeniden açın. Aynı paketi yeniden üretmek için uygulama ve kurulum aracını aynı klasöre yayımlayın:

```powershell
.\dotnet.cmd publish WinFormsApp2.csproj --configuration Release --runtime win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true --output artifacts\releases\database-config
.\dotnet.cmd publish tools\DatabaseSetup\AccountingApp.DatabaseSetup.csproj --configuration Release --runtime win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true --output artifacts\releases\database-config
```

Bu çıktı klasörünün tamamı dağıtım paketidir. Çalışma bileşenleri EXE'nin yanındadır. .NET sürümünün sonraki güvenlik yamalarında SDK, paketler ve dağıtım çıktısı birlikte güncellenmelidir.

SQL sürücüsü Microsoft.Data.SqlClient 7.1.1; test altyapısı Microsoft.NET.Test.Sdk 18.11.0, NUnit 5.0.0 ve NUnit3TestAdapter 6.3.1 oldu. Uygulama, test ve kurulum projelerindeki `packages.lock.json` dosyaları doğrudan ve geçişli bağımlılıkların sürüm ve içerik özetlerini kaydeder. 24 Ekim 2026 taramasında üç proje için NuGet tarafından bilinen bir paket açığı bildirilmedi. Bu, uygulamanın bütün güvenlik özelliklerinin denetlendiği anlamına gelmez. Tarama:

```powershell
.\dotnet.cmd list WinFormsApp2.sln package --vulnerable --include-transitive
```

Son doğrulama: .NET 10 üzerinde **212 test başarılı, 0 başarısız, 0 atlanan**. Uyarıları hata sayan derleme/test kontrolü ve yerel .NET kurulumuna bağımlı olmayan dağıtım paketinin açılış kontrolü başarılıdır. Merkezi ayar doğrulaması, temiz kurulum, tekrar/eşzamanlı kurulum, sürüm/özet uyumu, rollback, ilk yöneticiyle giriş ve konsol/PowerShell kurulum akışı da sınanır.

Giriş hazırlanırken ekranda durum açıklaması ve BEKLEYİN düğmesi gösterilir. Hazırlık başarısız olursa düğme YENİDEN DENE olur; girilen bilgiler korunur. Bağlantı/altyapı hataları yanlış parola mesajıyla karıştırılmaz. Hata ayrıntıları yerine ekranda bir G- kodu gösterilir. Uygulama klasöründeki `logs\login-errors.log` dosyası yalnızca hata kodu, işlem aşaması, istisna tipi, sayısal SQL/Windows hata kodları ve metot adlarını kaydeder; istisna mesajları, kullanıcı adı, parola, hash ve personel değerleri yazılmaz. `logs\login-startup.log` hazırlığın başladığını ve hazır/hatalı sonucunu işlem kimliğiyle kaydeder. Günlük yazılamasa da giriş yeniden denenebilir.

LocalDB testleri `AccountingAppRegression_<GUID>` ve `AccountingAppSetupRegression_<GUID>` isimli ayrı veritabanları oluşturur; dosyaları test çıktısındaki TestDatabases/SetupDatabases klasörlerinde tutar ve bitince kaldırır. Muhasebe2.mdf dosyasına bağlanmaz. Önceki transaction/parola testleri eski şema uyumluluğunu; kurulum testleri gerçek yeni kurulum betiğini kullanır. Testlerde SQL adımının başarısız olması, geri alma, grup dışı silmeyi engelleme, parola geçişi, hash ile giriş, ayar doğrulama ve eşzamanlı işlemler kontrol edilir.

Para girişindeki düzeltme daha önce yanlış kaydedilmiş tutarları dönüştürmez; bunlar ayrıca incelenmelidir. İşletmeye göre tutar sınırları, izin geçmişi ve diğer açık analiz maddeleri sonraki işlerdir. Yeni kurulum betikleri mevcut eski veritabanına otomatik şema taşıması uygulamaz.

