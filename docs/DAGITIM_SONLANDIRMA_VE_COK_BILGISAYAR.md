# Dağıtım ve çok bilgisayarlı kurulum

6 Kasım 2026 · **1.3.32-dev / şema 137 / API 1.32**. Yeni SQL yükseltmesi yoktur. İlk kaynak tesliminde kurulu sürüm değiştirilmemişti; ardından [7 Kasım yerel kurulumu](1_3_32_DEV_KURULUMU.md) doğrulanmış yedek sonrası tamamlandı. Şema 137 değişmedi; denemeler yine atlandı. İşlev/kurulum/SQL/geri dönüş denemeleri, imzalama ve GitHub Actions çalıştırılmadı; mevcut CI tam test kapısı korunur. Yerel derleme ve dosya bütünlüğü kontrolleri kabul/prova değildir.

## Paket üretimi ve doğrulama

PowerShell 7.4 veya üstü ve projenin sabitlenmiş SDK'sı kullanılır:

```powershell
.\automation\Build-WindowsRelease.ps1
.\automation\Verify-WindowsBundle.ps1 -BundleDirectory artifacts\release-bundles\<paket> -ExpectedVersion 1.3.32-dev
```

İstemci/API/kurulum/veritabanı/kurtarma araçları yeni bir artifacts klasöründe yayımlanır. Çalışan kurulumun üstüne yazılmaz. İki paketin iş DLL'i aynı olmalıdır. Manifest/provenance; sürüm, commit, kaynak durumu, şema/API/runtime ve imza modunu taşır. ZIP yanında SHA listesi, manifestler, provenance, açıklama, `verification.json` ve Verify/Get-DeploymentPlan betikleri bulunur. Geliştirme paketi kararlı sürüm değildir.

Her iki payload'daki `dependency-inventory.json` yayımlanan `.deps.json` dosyalarından NuGet ad/sürüm envanteridir. Lisans/CVE/standart SBOM uyumluluk denetimi yapıldığını söylemez; bunlar açıkça false kaydedilir. Özel profil, sertifika anahtarı, DB, yedek, kasa anahtarı/politikası/aynalama durumu ve kullanıcı tercihleri paketlenemez.

Doğrulayıcı tüm dış SHA listesini, iç/dış manifest eşitliğini, bütün ZIP dosyalarının uzunluk/SHA-256 değerlerini, gerekli araçları, ortak iş DLL'ini ve kimlik alanlarını kontrol eder. Mutlak/üst dizin/ADS/Windows aygıt adları, bağlantı, tekrar eden yol ve manifest dışı dosya reddedilir. Dosya başına 512 MiB, paket başına 2 GiB ve 10.000 dosya sınırı vardır. ZIP/EXE çalıştırılmaz, DB'ye bağlanılmaz. Araç bu sürümün envanter/imza sözleşmesi içindir; eski paketlerde gereken alanlar bulunmayabilir.

Paketi ve doğrulama betiğini güvenilir proje kaynağından alın. SHA listesi ile aynı yerden değiştirilebilen manifest tek başına yayıncı kimliğini kanıtlamaz. İmzalı politikada güvenilir sertifika ve beklenen yayıncı parmak izi bağımsız kaynaktan doğrulanmalıdır. Betik kendisini veya kaynağın güvenilirliğini kanıtlamaz.

## Salt okunur güncelleme planı

```powershell
.\automation\Get-DeploymentPlan.ps1 -BundleDirectory artifacts\release-bundles\<paket> -ActiveInstallationPath artifacts\deployments\local-api\active-installation.json
```

Araç sürüm/API/şema ve iki sunucu manifestindeki SQL betik özetlerini karşılaştırır. Şema inceleme ve bütün istemcilerle API eşgüdümü ihtiyacını bildirir. Aynı betik/şemada `codeOnlyRollbackCandidate=true` çıkması, geri dönüş izni veya uygulaması değildir: DB, bekleyen ödeme, yeni veri anlamı ve protokol ayrıca incelenir. Aktif kurulum bilgisi yoksa değerlendirme bilinmiyor kabul edilir. Araç süreç, dosya kurulumu veya DB değiştirmez; profil/token/parola içeriği çıktı olarak verilmez.

## Gerçek bakım sırası

1. Paketi/özetleri/yayıncıyı doğrulayın; önceki ve yeni sürüm yollarını kaydedin.
2. Belirsiz ödemeleri eski bağlantıda çözün. Kapsamlı SQL yedeğini doğrulayın; şifreli yedekte ayrı kurtarma anahtarı ve parolasını da kontrol edin.
3. Açık düzenlemeleri tamamlayıp bütün istemcileri ve API'yi kontrollü kapatın. Yeni ve eski API bekleyen istemcileri karıştırmayın.
4. Şema farklıysa ayrı, yedekli migration bakımı yapın. Aynı şema numarası tek başına yetmez; journal/hash eşleşmesi gerekir. Sihirbaz mevcut DB'yi otomatik yükseltmez.
5. API'yi yeni klasöre çıkarın; özel sunucu/DB profillerini, sertifika erişimini ve Windows çalışma hesabını koruyun. DPAPI anahtarı farklı hesaba yalnız dosya kopyalamayla taşınmaz.
6. Gizli görev/hizmet başlatıcısının yeni hedefini ve beklenen API sürümünü güncelleyin. HTTPS/DNS/sertifika, süreç yolu, API ve şema başlangıcını kontrol edin.
7. Bütün istemcileri aynı paketle kurup kısayolları güncelleyin. Önceki sürümü/yedekleri koruyup gerçek kurulum özetini kaydedin. Ödeme/taslak günlüklerini elle silmeyin. Uygulama kendiliğinden açılmaz.

Şema uyuşmazlığında eski tokenları yeniden geçerli yapan veya migration hash'ini değiştiren kestirme kullanılmaz. Yeni DB seçeneği yalnız benzersiz LocalDB oluşturur; var olan MDF yeniden kurulmaz. Kurulum sonrası işlev kabulü bu teslimde yapılmadı.

## Topolojiler

| Kullanım | Sunucu / DB | Sınır |
|---|---|---|
| Mevcut tek bilgisayar | Aynı Windows hesabı, LocalDB, AllowUserLocalDb=true, HTTPS localhost | LAN dinleyicisi kodda engellenir; port açmak çok bilgisayarlı kurulum sağlamaz. |
| Birden fazla bilgisayar | Windows API sunucusu, SQL Express/Server, AllowUserLocalDb=false | Ayrı SQL hesabı, geçerli DNS/TLS ve servis hesabı profili kurulmalıdır. Bu teslim LAN/hizmet/firewall kurulumu yapmaz. |

SQL/MDF dosyasını SMB/OneDrive üzerinden istemcilere paylaştırmayın; DB'ye yalnız API erişir. Sunucuda `server.example.json` ve `database-profile.example.json` özel dosyalara uyarlanır; Git/ZIP'e girmez. Windows bütünleşik SQL hesabı, DNS adına uygun güvenilir TLS, sertifika özel anahtar izni ve yalnız gerekli ağ erişimi operatörce hazırlanır. Sertifika doğrulaması kapatılmaz; istemciye SQL parolası/özel TLS anahtarı dağıtılmaz.

LocalDB için şifreli uygulama yedeği ayrı SQL Server servis hesabında otomatik çalışmaz. Bu topolojide DBA'nın yetkili, şifreli ve denenmiş SQL yedek/anahtar/kurtarma süreci gerekir. Engel kaldırılarak açık yedeğe sessiz dönüş yapılmaz. Host/katalog/dosya değişimi eski kapsam sabitlemesine otomatik eşitlenmez; doğru kaynak ve yeni bağlantı kapsamı/pinleri ayrıca incelenir. [API topolojisi](MERKEZI_API_ALTYAPISI.md), [yedek ve kurtarma sınırları](YEDEK_VE_DESTEK_GUVENLIGI.md).

Her istemcide ZIP'in tamamını **yeni ve boş** klasöre çıkarıp `AccountingApp.Setup.exe` açın. HTTPS API'yi seçin veya mevcut bağlantıyı koruyun; o Windows hesabının sertifika güvenini ve adresi kontrol edin. Sihirbaz artık commit/sourceState/fileVersion/şema/API/runtime ile paket kökündeki tüm dosyaları doğrular. Pakete eski dosya veya özel profil eklemeyin. Mevcut SQL profilinde journal tam uyumlu olmalıdır.

## Geri dönüş

Aynı SQL betikleri/şemada eski istemci/API çifti kod geri dönüşü için aday olabilir; bekleyen ödeme ve yeni işlevlerin veri anlamı yine incelenir. Şema değiştiyse yalnız eski EXE'yi açmak geri dönüş değildir. Ayrı bakımda doğru yedek ve uyumlu paket seçilir; yedekten sonra oluşan kayıtların mutabakatı planlanır. Otomatik RESTORE veya veri silme yapılmaz. Hedef ve kurtarma kaynağı ayrı tutulur. Windows hesabı değişiminde parolalı anahtar içe aktarımı ve ayrı hedefte gerçek prova gereklidir.

## İsteğe bağlı Authenticode

```powershell
.\automation\Build-WindowsRelease.ps1 -SigningCertificateThumbprint '<40-hex-public-thumbprint>' -TimestampUrl 'https://kurumun-rfc3161-sunucusu.example/'
.\automation\Verify-WindowsBundle.ps1 -BundleDirectory artifacts\release-bundles\<imzali-paket> -RequireSigned
```

Örnek URL gerçek hizmet değildir. Geçerli Code Signing EKU'lu sertifika ve özel anahtar CurrentUser/My deposunda, Windows SDK x64 SignTool erişilebilir olmalıdır. PFX/parola komut satırına veya Git kaynaklarına yazılmaz. Proje EXE/DLL'leri SHA-256/RFC3161 ile imzalanır; aynı DLL bir kez imzalanıp iki pakete aynı baytlarla taşınır. Üçüncü taraf runtime dosyaları yeniden imzalanmaz. Hata halinde imzasız pakete sessiz geçiş olmaz. Manifest/hash imzadan sonra üretilir.

RequireSigned yalnız kendi rastgele geçici ürün PE dosyalarında Windows Authenticode durumunu ve manifest yayıncı parmak izini denetler, sonra onları kaldırır. Beklenen yayıncı kimliği yine bağımsız doğrulanır. Varsayılan paket **signatureMode=unsigned** olarak açıktır. Bu teslimde sertifika, imza/zaman damgası/güven zinciri kullanılmadı; self-signed sertifika üretilmedi veya Windows güven deposu değiştirilmedi.

## CI / Release ve açık doğrulama

Normal push/PR/manuel koşularda mevcut tam test kapısı korunur; ardından paket/ikinci dosya doğrulaması ve artifact üretimi vardır. Branch koşuları birbirini iptal edebilir, etiket Release koşuları bu yolla iptal edilmez. Derleme/PR işi yalnız contents:read alır. Kararlı etiket VersionPrefix ile eşleşmeli ve aynı temiz commit'i göstermelidir.

Yalnız etiket işi contents:write ile doğrulanmış artifact'ı tekrar kontrol eder; v etiketi ile çıplak manifest sürümü eşleştirilir. Mevcut aynı etiketin varlıkları değiştirilmeden **taslak Release** oluşturulur; yayımlama ayrı işlemdir. Standart GitHub koşucusuna sertifika kurulmadı, CI varsayılanı imzasızdır. Kurumsal imzalama ayrı kontrollü ortam/sertifika erişimi gerektirir.

Bu çalışmada commit/push/Actions/etiket/Release yapılmadı. Denemeler geçirilmiş gösterilmez. Temiz Windows, LAN/TLS, imza, kesinti/geri dönüş ve büyük veri kabulü açık iştir. İki kalan ana paket: **performans/erişilebilirlik ve güvenlik doğrulaması**, **gerçek kullanım pilotu/kurtarma/teslim**. Yerel paket hazırlığı bunların yerine geçmez.

Dayanaklar: [Microsoft SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool), [GitHub Release bütünlüğü](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/verify-release-integrity).
