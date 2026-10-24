# Bağlantı ayarı ve veritabanı kurulumu

Uygulama ve kurulum aracı aynı `appsettings.json` biçimini kullanır. Varsayılan dosya EXE yanındadır; kısayolun çalışma klasörü ayarı değiştirmez. Yönetici, Ayarlar ekranında başka bir JSON profilini doğrulayıp seçebilir; profil değişince oturum kapanır ve yeniden giriş gerekir. Transaction ortasında bağlantı değiştirilmez. Ayar okunamazsa veya LocalDB MDF'si bulunamazsa başka bir DB'ye sessizce geçilmez.

## Mevcut veritabanıyla kullanım

Dağıtılan varsayılan ayar, önceki sürümlerde kullanılan Documents/Muhasebe2.mdf dosyasını açar. Kullanıcı adı kaynak kodunda sabit değildir:

```json
{
  "Database": {
    "Server": "(LocalDB)\\MSSQLLocalDB",
    "Name": null,
    "FilePath": "%USERPROFILE%\\Documents\\Muhasebe2.mdf",
    "ConnectTimeoutSeconds": 30
  }
}
```

| Alan | Anlamı |
|---|---|
| Server | Yerel LocalDB veya bu bilgisayardaki SQLEXPRESS örneği. |
| Name | Yeni kurulumda seçilen veritabanı adı. Eski dosya adıyla otomatik attach edilen veritabanlarında null bırakılır. |
| FilePath | LocalDB MDF yolu: ortam değişkenleri açılır, göreli yol profil klasöründen çözülür. DataDirectory Documents klasörüdür. SQL Express'te boş bırakılır; Name zorunludur. |
| ConnectTimeoutSeconds | 1–120 saniye; belirtilmezse 30. |

Windows kimliğiyle bağlantı ve zorunlu şifreleme kullanılır; sertifika doğrulaması açıktır. JSON dosyasına SQL kullanıcı adı/parolası veya ek bağlantı anahtarı konmaz. Alan isimleri örnektekiyle aynı olmalıdır.

Mevcut MDF için yeni temel kurulum çalıştırılmaz. Giriş hazırlığı önce doğrulanmış yedek alır; ardından `upgrades/100-roles-audit-ledger.sql` gibi ek DbUp yükseltmelerini uygular. Var olan personel, izin, tutar ve hesaplar korunur. Mevcut tutarlar ödeme defterinin açılış bakiyesidir; mevcut hesaplar Yönetici olur. Eski DB'ye gerçekte uygulanmamış 001 baseline kaydı yazılmaz.

SQL Express örneği `appsettings.sqlexpress.example.json` içindedir. SQL Server'ın güvenilir TLS sertifikası ve Windows hesabının DB/kurulum/yedek klasörü yetkileri gerekir. Bu bilgisayardaki Express denemesinde -2146893019 sertifika güven hatası görüldü; profil kodu hazırdır, sunucudaki TLS kurulumu ayrıca tamamlanmalıdır. Mevcut LocalDB çalışır. Sertifika denetimi kapatılmaz.

## Yeni bilgisayarda veya ayrı boş veritabanında ilk kurulum

Dağıtım klasörünün tamamını kullanın. .NET çalışma bileşenleri ve kurulum EXE'si pakette bulunur; SDK veya sqlcmd kurulması gerekmez. LocalDB veya yapılandırılmış yerel SQL Express ayrıca gerekir. Express'te CREATE DATABASE sunucunun varsayılan veri dizinini kullanır; MDF attach yapılmaz.

1. Dağıtım klasöründe PowerShell açın. `database/appsettings.new-install.example.json` dosyasını `appsettings.json` olarak kopyalayın. Bu adım yeni kurulum içindir; mevcut kullanım ayarını değiştirmek istemiyorsanız önceki bölümdeki dosyayı kullanın.
2. `Database.Name` ve `Database.FilePath` değerlerini seçin. Örnek yeni veritabanını `%LOCALAPPDATA%\AccountingApp\AccountingApp.mdf` konumunda, `AccountingApp` adıyla oluşturur.
3. Aşağıdaki komutu çalıştırın:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\database\Install-Database.ps1
```

4. İlk yönetici kullanıcı adını, parolayı ve parola tekrarını girin. Terminalde parola yıldızlarla gösterilir; komut satırı argümanına veya PowerShell geçmişine yazılmaz. Kullanıcı adı 1–50, yeni parola 6–1024 karakter olmalıdır. Başta/sonda bulunan parola boşlukları korunur.
5. Kurulum tamamlandıktan sonra `WinFormsApp2.exe` dosyasını açın ve oluşturduğunuz hesapla giriş yapın.

Başka bir ayar dosyası kullanmak için:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\database\Install-Database.ps1 -ConfigurationPath 'C:\Kurulum Ayarları\appsettings.json'
```

Uygulamanın da aynı veritabanını açması için EXE yanındaki appsettings.json dosyası aynı ayarları içermelidir. Kurulum aracını doğrudan çalıştırmak da mümkündür:

```powershell
.\AccountingApp.DatabaseSetup.exe --config .\appsettings.json
```

Sabit bir ilk parola veya örnek personel kaydı oluşturulmaz. İlk yönetici doğrudan uygulamanın PBKDF2 hash biçimiyle saklanır. Eski `password` kolonu uyumluluk için bulunur, fakat yeni şemada yalnızca boş değer kabul eder.

## Yeniden çalıştırma ve sürümleme

Kurulum, aynı veritabanında tekrar çalıştırılabilir. Uygulanmış betikleri atlar ve mevcut yönetici hesaplarını değiştirmez. İki eşzamanlı kurulum aynı veritabanı için kilitlenerek sırayla çalışır; ilk yönetici bir kez oluşturulur.

Var olan MDF/LDF üzerine yazılmaz. Aynı veritabanı adı başka dosyaya bağlıysa veya mevcut şema bu araçla yönetilmiyorsa işlem durdurulur. Eski otomatik attach ayarıyla (`Name: null`) yeni kurulum yapılmaz.

`migrations/001-initial.sql`, çalışan/Joker tablolarını, izin tablosunu, ilişkileri, indeksleri ve hash kullanan yönetici tablosunu tanımlar. İzin süresi başlangıç ve bitiş günleri dahil hesaplanır. Betikler kurulum aracının başvurduğu uygulama derlemesine gömülüdür; pakette ayrıca incelenebilir SQL kopyaları bulunur. Yayımlanan SQL kopyasını değiştirmek çalıştırılan derlemeyi değiştirmez.

Her uygulanan sürüm ve normalize edilmiş SQL metninin SHA256 özeti `dbo.SchemaMigrations` tablosunda saklanır. Satır sonu biçimi değişimi aynı betik kabul edilir; uygulanmış betik içeriğinin değiştirilmesi veya paketin tanımadığı bir şema sürümü işlemi durdurur. İleride değişiklik gerektiğinde mevcut dosyayı düzenlemek yerine `002-aciklama.sql` gibi yeni bir dosya eklenip paket yeniden derlenir.

Şema, sürüm kayıtları ve ilk yönetici aynı transaction içinde kaydedilir; hata olursa geri alınır. SQL Server'da CREATE DATABASE bu transaction'ın dışında çalışır. Sonraki adım hata verirse yeni, boş veritabanı/dosyalar kalabilir; aynı ayarla yeniden çalıştırmak desteklenir. Kurulum aracı mevcut veritabanını silmez veya eski kullanıcı kayıtlarını yeni veritabanına taşımaz.

## Kaynaktan paketleme

Proje kökünden:

```powershell
.\dotnet.cmd restore WinFormsApp2.sln --locked-mode
.\dotnet.cmd publish WinFormsApp2.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -o artifacts\releases\guide
.\dotnet.cmd publish tools\DatabaseSetup\AccountingApp.DatabaseSetup.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -o artifacts\releases\guide
```

İki EXE aynı .NET/SQL bileşenlerini paylaşacak şekilde aynı klasöre yayımlanır. Paket klasörünü bütün olarak kullanın. Sürüm güncellerken mevcut appsettings.json ayarını yeni dağıtıma taşıyın; yeni kurulum örneğiyle değiştirmek veritabanı seçimini değiştirir.

## Otomatik özellik yükseltmeleri ve bakım

Gömülü upgrades betikleri DbUp ve dbo.__MigrationJournal ile izlenir. Journal betik adı SHA256 özeti içerir. Uygulanmış dosyayı değiştirmeyin; yeni numaralı dosya ekleyin. Betikler tek transaction içinde uygulanır ve önce doğrulanmış güvenlik yedeği alınır. Temel kurulum journal'ı dbo.SchemaMigrations bundan ayrıdır.

Konsolda profil doğrulamak için AccountingApp.DatabaseSetup.exe --check --config appsettings.json; aynı profilin mevcut uygulama şemasını yükseltmek için --upgrade --config appsettings.json kullanılabilir. Bu komut parola istemez veya sıfırlamaz.

Yedek, aynalama, geri yükleme ve manuel test adımları docs/KULLANIM_VE_TEST.md dosyasındadır.
