# Yeniden kullanım rehberi uygulama takibi

Kullanıcı kapsam kararı (24 Ekim 2026): **Mevcut işlevlere uyarla.** Kaynak rehber `yeniden-kullanim-kaynak-rehberi.txt` olarak korunmuştur. Stok/POS/müşteri/tedarikçi modüllerinin teknik yöntemleri personel, izin ve ödeme alanına uygulanmıştır.

.NET 10, güncel paketler ve mevcut PBKDF2-HMAC-SHA256/600.000 yineleme korunur. Rehberdeki SHA-1 varsayılanlı eski kurucu, açık metin parola fallback'i, UI await'inde ConfigureAwait(false), eski .NET/test sürümleri ve antivirüs istisnaları kopyalanmaz. Eşdeğer işlevler güvenli ve mevcut platforma uygun yöntemlerle uygulanır.

| Madde | Uygulama / kanıt | Durum |
|---|---|---|
| 1.1 | PasswordHasher, AdminStore kontrollü göç; PasswordHasherTests ve TransactionTests | Mevcut güçlü uygulama korundu |
| 1.2 | Parametrik SQL; SqlValues, SqlUnitOfWork, UPDLOCK/HOLDLOCK/ROWLOCK; audit/izin/personel rollback testleri | Uygulandı |
| 1.3 | Models/UserSession, AccountService, context ve ekran kontrolleri; rol ve son yönetici testleri | Uygulandı |
| 2.1 | SchemaUpgrade + gömülü database/upgrades; DbUp, SHA256 journal, transaction ve önce doğrulanmış yedek | Uygulandı |
| 2.2 | DatabaseSettings, SettingsForm; LocalDB/yerel SQL Express JSON; DataDirectory; doğrulama ve çıkışla profil geçişi | Uygulandı; Express canlı deneme TLS hatasıyla sınırlı |
| 3.1 | Models, Data/PersonnelRepository, IUnitOfWork/SqlUnitOfWork, Services/PersonnelService | Uygulandı |
| 3.2 | OpenAsync/ExecuteReaderAsync/ReadAsync/ExecuteNonQueryAsync/CommitAsync; token, DisposeAsync, UI busy/close koruması | Uygulandı |
| 3.3 | PayAsync: koşullu bakiye azaltımı, etkilenen satır, kilit, benzersiz request ID; yarış ve tekrar testleri | Uygulandı |
| 4.1 | AppTheme + SessionForm: çift tampon grid, 36px başlık, zebra, standart sürüklenebilir başlık, üç durum alanı, klavye | Uygulandı |
| 4.2 | UserPreferences: kullanıcı JSON ve HKCU, açık/koyu tema; kalıcılık/fallback testi | Uygulandı |
| 4.3 | GridPreferences: genişlik/sıra/görünürlük JSON, Kolonlar menüsü; düzen round-trip testi | Uygulandı |
| 4.4 | TextBox.PlaceholderText; P/Invoke gerektirmeyen .NET eşdeğeri | Uygulandı |
| 5.1 | ReportExport.Excel, ClosedXML; başlık/zebra/kenarlık/TL/SUM; workbook doğrulaması | Uygulandı |
| 5.2 | ReportExport.Csv; UTF-8 BOM, noktalı virgül, quote/newline kaçışı ve formül koruması | Uygulandı |
| 5.3 | PaymentPrinting, A4/80mm PrintDocument/Preview; PNG logo, QR referansı, maskeli TC/IBAN, bakiye ve izin | Uygulandı; fiziksel yazıcı testi manuel |
| 5.4 | Günlük ödeme kapanışı: beyan/kayıt farkı, renk ve çift imza; POS yerine personel ödeme raporu | Uygulandı |
| 6.1 | Calculations.WeightedAverage decimal servisi; özet ekranında personel başına ortalama | Uyarlanarak uygulandı |
| 6.2 | Aynı transaction içinde AuditLog; NLog yalnızca sabit olay/tür/kod; audit-failure rollback ve gizlilik testi | Uygulandı |
| 6.3 | BackupService, yönetici oturumunda 24 saat kontrolü, son 14, aynalama, JSON/HKCU, VERIFYONLY, güvenlik yedeği ve restore | Uygulandı; izole gerçek restore sınaması |
| 6.4 | Sadakat puanı yerine Calculations.LeaveDays, sınanmış takvim günü ve decimal tutar hesabı | Uyarlanarak uygulandı; hayali hakediş kuralı eklenmedi |
| 6.5 | PaymentLedger: açılış/tutar farkı/ödeme/iptal/kapanış, yürüyen bakiye, silmeden bağımsız tarihçe | Uyarlanarak uygulandı |
| 6.6 | Summary tarih sıralı 30 günlük kırmızı/sarı izin uyarısı; LeavePlanForm atomik plan onayı | Uyarlanarak uygulandı |
| 6.7 | F6/F7 DPAPI taslak; kullanıcı/DB/ekran kapsamı, kimliği yeniden bulma, değişmiş bakiye uyarısı ve güncel tutarı koruma | Uyarlanarak uygulandı |
| 7.1 | NUnit 5, güncel adapter/TestSdk; LocalDb kategori ve DB'siz filtre; eski regresyon + Guide testleri | Uygulandı |
| 7.2 | .github/workflows/ci.yml: Windows, SDK pini, kilitli restore, tam test, TRX/rapor ve Release artifact | Hazır; uzak GitHub koşusu yapılmadı |
| 7.3 | automation/otomatik_test_master.ps1: 15 alan gerçek TRX eşlemesi, JSON/MD, -NoDatabase ve -Full | Uygulandı; 15 alan başarılı |

## Bölüm 8: 18 adımın tek tek karşılığı

| Adım | Karşılık |
|---|---|
| 8.1 Proje | Mevcut .NET 10 WinForms, global.json ve dotnet.cmd korundu |
| 8.2 DB helper | DatabaseSettings/AppDatabase, SqlValues, güçlü PasswordHasher |
| 8.3 Oturum | AccountingApplicationContext + değişmez UserSession ve rol |
| 8.4 Ayarlar | Kullanıcı JSON + HKCU, bağlantı profili, tema, yedek ve logo |
| 8.5 Hata yakalama | Program global handler'ları, LoginDiagnostics ve NLog |
| 8.6 Tema | AppTheme + SessionForm ortak araç/durum çubuğu |
| 8.7 Modeller | Models/UserSession.cs ve Models/Personnel.cs |
| 8.8 Repository/UoW | Data/PersonnelRepository.cs ve Data/SqlUnitOfWork.cs |
| 8.9 Servisler | PersonnelService, AccountService, Calculations, BackupService |
| 8.10 Migration | DbUp + database/upgrades ve ayrı temel kurulum journal'ı |
| 8.11 POS/sepet/kilit | Personel ödeme kaydı, işlem kimliği ve atomik bakiye düşümü |
| 8.12 Cari/tahsilat | Personel PaymentLedger ve yönetici ödeme iptali |
| 8.13 Mal kabul/WAC | Planlanan tutar farkı, açılış bakiyesi ve decimal ağırlıklı ortalama |
| 8.14 Audit/yedek | Transaction audit + NLog + yedek/aynalama/restore |
| 8.15 Grid/export | Kolon ayarı, CSV ve ClosedXML |
| 8.16 Yazdırma | A4/80mm ödeme fişi ve günlük ödeme kapanışı |
| 8.17 Test/CI | NUnit regresyonları ve Windows GitHub Actions |
| 8.18 Otomasyon/kılavuz | Master PS betiği ve KULLANIM_VE_TEST.md |

Bölüm 9 sırası: güvenlik/tema temeli → repository/UoW/migration/servisler → rapor/ödeme/izin/yedek → test/CI/otomasyon. Başlangıç kaynak kaydı yerel Git'e alındı; uygulama ve son kanıtlar ayrı yerel kayıtlara alınır. Uzak depoya push yapılmaz.

## Bölüm 10: tuzakların tek tek karşılığı

| Tuzak | Uygulanan yöntem |
|---|---|
| 10.1 | Test için ayrı TEMP/AccountingAppTests/GUID alanı; güvenlik yazılımına dokunma yok |
| 10.2 | PowerShell 5.1 dosyaları UTF-8 BOM; Türkçe dosya/içerik round-trip sınaması |
| 10.3 | global.json ve yerel dotnet.cmd; SSMS MSBuild'i kullanılmaz |
| 10.4 | Boşluklu yollar ayrı argüman; NUnit sınırında normalize edilen çalışma yolu |
| 10.5 | Geçerli dotnet test/TRX; desteklenmeyen vstest /nologo yok |
| 10.6 | Program açılışında DataDirectory; ilk DB çağrısından önce kullanıcı profilini yükleme |
| 10.7 | Commit edilmemiş UoW dispose'da rollback; commit sonrası rollback yok; rollback hatası asıl hatayı maskelemez |
| 10.8 | PARA>=amount, affected-row kontrolü, kilit ve idempotent request ID |
| 10.9 | AV kapatma, klasör istisnası veya powershell.exe allowlist ekleme yapılmaz |
| 10.10 | Kaynak taramasında .pfx/büyük JPG bulunmadı; veri/yedek/taslak/derleme çıktıları Git dışında |
| 10.11 | Başlangıç kaydı, uyarıları hata sayan build, özellik ve tam regresyon, ekran render ve yerel sürüm kayıtları |
| 10.12 | async void yalnızca olaylarda, iç API Task; DAL ConfigureAwait(false), UI await bağlamı korunur |

## Doğrulama durumu

Kod, tam test, izole paket ve gerçek veritabanı geçişi tamamlandı. Kullanıcının açık onayıyla 24 Ekim 2026 tarihinde doğrulanmış yedek alınarak yükseltme uygulandı. Veri bütünlüğü, açılış bakiyeleri, roller ve giriş hazırlığı doğrulandı; kısayol yeni sürüme yönlendirildi. Uzak CI ve fiziksel yazıcı kontrolü ayrıca belirtilmiştir.

- Veritabanısız master: 185/185 başarılı; artifacts/analysis/master-20261024-100543-560/. -NoDatabase PowerShell 5.1 altında uçtan uca doğrulandı.
- Yeni rehber regresyonları: 41 test; önceki 212 regresyonla birlikte 253 test başarılı.
- Tam master koşu: 253/253 başarılı, 0 başarısız, 0 atlanan; 15/15 alan. Kanıt: artifacts/analysis/master-20261024-095439-621/master.trx ve RAPOR.md.
- Açık/koyu ekran render: 8 ekran × 2 tema, 16 görüntü; alan görünürlüğü ve sınır testleri başarılı. Personel, ödeme, hesap ve ayar yerleşimleri incelendi. Kanıt: artifacts/analysis/ui-preview/.
- Release: artifacts/releases/guide; .NET 10.0.13 self-contained. GUID adlı izole DB üzerinde kurulum, yükseltme, bağlantı ve giriş hazırlığı başarılı. Kanıt: artifacts/analysis/guide-release-smoke.json. Gerçek DB yükseltmesi ve giriş hazırlığı da kullanıcı onayıyla doğrulandı.
- SQL Express gerçek bağlantı: sunucudaki güvenilmeyen TLS sertifikası nedeniyle başarısız; LocalDB bağlantısı korunur.
- Gerçek DB ve kısayol: açık kullanıcı onayıyla tamamlandı. COPY_ONLY/CHECKSUM yedeği RESTORE VERIFYONLY ile doğrulandı. Mevcut veri özetleri korundu; roller, journal ve açılış bakiyeleri doğrulandı. Kısayol yeni Release sürümünü açar. Kanıt: artifacts/analysis/guide-production-status.json ve guide-approved-backup.json.
- Paket güvenlik taraması: üç projede doğrudan/geçişli bilinen NuGet açığı bildirilmedi; artifacts/analysis/guide-package-audit.json.
- Uzak CI ve fiziksel yazıcı: manuel/harici doğrulama; yapılmadı.

Son giriş düzeltmesi (24 Ekim 2026): LocalDB kapalıyken gerçek Explorer oturumunda giriş hazırlığı başarılı, ardından Dashboard gözlendi. 260/260 test ve 15/15 alan başarılı. Test motoru her koşuda ayrı GUID örneğidir; açık kullanıcı motoru etkilenmez. Güncel kısayol artifacts/releases/localdb-startup-fix/WinFormsApp2.exe dosyasını açar. Ayrıntılar LOCALDB_GIRIS_DUZELTMESI.md dosyasındadır.
