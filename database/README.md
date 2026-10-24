# Bağlantı ayarı ve veritabanı kurulumu

Uygulama ve kurulum aracı aynı `appsettings.json` biçimini kullanır. Uygulama dosyayı EXE'nin yanından okur; kısayolun çalışma klasörü ayarı değiştirmez. Geçerli ayarlar işlem boyunca sabit tutulur. Ayarı değiştirdikten sonra uygulamayı yeniden başlatın. Ayar okunamazsa veya MDF bulunamazsa başka bir veritabanına sessizce geçilmez.

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
| Server | Yerel LocalDB örneği. SQL Server Express LocalDB kurulu olmalıdır. |
| Name | Yeni kurulumda seçilen veritabanı adı. Eski dosya adıyla otomatik attach edilen veritabanlarında null bırakılır. |
| FilePath | MDF dosyasının yolu. `%USERPROFILE%` ve `%LOCALAPPDATA%` gibi Windows ortam değişkenleri açılır. Göreli yollar ayar dosyasının klasöründen çözülür. |
| ConnectTimeoutSeconds | 1–120 saniye; belirtilmezse 30. |

Windows kimliğiyle bağlantı ve zorunlu şifreleme kullanılır; sertifika doğrulaması açıktır. JSON dosyasına SQL kullanıcı adı/parolası veya ek bağlantı anahtarı konmaz. Alan isimleri örnektekiyle aynı olmalıdır.

Mevcut MDF'yi kullanmak için yalnızca bu ayarın doğru dosyayı göstermesi yeterlidir. Yeni kurulum aracı, geçmiş sürümlerden kalan ve SchemaMigrations kaydı olmayan bir şemayı otomatik dönüştürmez.

## Yeni bilgisayarda veya ayrı boş veritabanında ilk kurulum

Dağıtım klasörünün tamamını kullanın. .NET çalışma bileşenleri ve kurulum EXE'si pakette bulunur; SDK veya sqlcmd kurulması gerekmez. LocalDB ayrıca kurulu olmalıdır.

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
.\dotnet.cmd publish WinFormsApp2.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -o artifacts\releases\database-config
.\dotnet.cmd publish tools\DatabaseSetup\AccountingApp.DatabaseSetup.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -o artifacts\releases\database-config
```

İki EXE aynı .NET/SQL bileşenlerini paylaşacak şekilde aynı klasöre yayımlanır. Paket klasörünü bütün olarak kullanın. Sürüm güncellerken mevcut appsettings.json ayarını yeni dağıtıma taşıyın; yeni kurulum örneğiyle değiştirmek veritabanı seçimini değiştirir.
