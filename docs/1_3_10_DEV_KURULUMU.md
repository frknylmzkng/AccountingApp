# 1.3.10-dev ve 121 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci/API **1.3.10-dev**, dosya sürümü **1.3.10.0**, şema **121**, API **1.10**. [Puantaj ve amir onayı kullanım rehberi](PUANTAJ_VE_AMIR_ONAYI.md).

## Yedek ve yükseltme

Açık masaüstü istemcisi ve bekleyen ödeme bulunmadı. Eski API görevi bakım için devre dışı bırakılıp API durduruldu. Aynı Windows kullanıcısının geçici bakım görevinde mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alındı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 121'in beklediği ve uygulanmış betiklerin değişmediği kontrol edildi. İlave güvenlik kopyası oluşturularak 121 transaction içinde uygulandı. Betik/paket düzeltmesi gerekmedi.

Personel, izin, ödeme defteri, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemi/bağlantı, grup geçişleri, proje ödeme talepleri, takvim şablonları/vardiyalar/kişi atamaları/izin talepleri/hafta kopyaları ve önceki proje haklarının içerik özetleri önce/sonra karşılaştırıldı; aynı kaldı. Hesap oturum nesilleri ve ilgili hesap rowversion değerleri beklendiği gibi yenilendi. Diğer eski satırların rowversion değerleri karşılaştırmaya dahil edildi. Parola hash'leri, roller ve mevcut yetkiler korundu.

İki yeni puantaj proje biti kapalı başladı. Timesheets ve TimesheetCommands tabloları boş oluşturuldu; plandan gerçekleşen çalışma veya onay uydurulmadı. Üç etkin koruma/audit trigger'ı, dört adlandırılmış güvenilir CHECK, altı güvenilir foreign key, altı indeks ve genişletilmiş audit gizlilik fonksiyonu metadata üzerinden doğrulandı. Yeni tablolarda devre dışı veya güvenilmeyen CHECK/FK bulunmadı. Bunlar kullanıcı işlev senaryosu denemeleri değildir.

Doğrulanmış ön yedek `artifacts/analysis/live-upgrade-121/backups/AccountingApp-v2-<kapsam-kimliği>-20261031T1555563912460-pre121.bak` dosyasıdır. Aynı klasörde ilave güvenlik kopyası bulunur. `database-preparation.json` yedeği; `baseline.json` ve `result.json` veri özetlerini kaydeder. Bu özel kayıtlar Git dışındadır.

## İstemci/API ve kısayol

Paket kaynağı `artifacts/release-bundles/timesheets-121-delivery/` klasörüdür. ZIP SHA-256 ve manifestteki dosya özetleri doğrulandı. İstemci/API `%LOCALAPPDATA%\Programs\AccountingApp\1.3.10-dev-20261031185613723` altında ayrı klasörlere kuruldu; önceki sürüm dosyaları korundu.

Sunucu/istemci bağlantı profilleri, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance121 görevi kaldırıldı. MSSQLLocalDB zorlamadan durdurulup kalıcı AccountingApp Local API görevi yeni sürüme geçirildi. API 1.10 HTTPS başlangıcı ve beklenen süreç doğrulandı; adres https://localhost:7443/ ve yalnız IPv4/IPv6 loopback erişimi korundu. Görev Running durumunda.

**Muhasebe - Güncel** kısayolu yeni `desktop/payload/WinFormsApp2.exe` dosyasına yönlendirildi; hedef, çalışma klasörü ve simge ayarlandı. Hedef/çalışma klasörü tekrar okunarak doğrulandı. 121 eski oturumları geçersizleştirdiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir.

Yönetici proje ekranından, proje kullanıcısı Projelerim üzerinden **Puantaj ve amir onayı…** düğmesini kullanır. Proje kullanıcısına ilgili projede **CanEnterTimesheets** ve/veya **CanApproveTimesheets** hakkı yönetici tarafından açıkça verilmelidir. Yönetici dahil hazırlayan, son düzenleyen veya gönderen hesap kendi puantajını onaylayamaz; ayrı yetkili hesap gerekir. Yeni haklar takvim, ödeme veya ücret/IBAN haklarını açmaz. Puantaj kayıtları bu sürümde ücret/hakediş üretmez.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları `artifacts/analysis/install-1.3.10-dev/` altında; son durum `deployment.json` / `readback.json`, kurulu klasörde `installation.json` dosyasındadır. Masaüstü açılmadı. Manuel/CSV girişi, tekrar aktarım, çakışma, onay/iade/yeniden açma, farklı yetkiler ve UI denemeleri kullanıcı isteğiyle atlandı. Yalnız yedek/veri koruma, şema nesneleri, paket bütünlüğü, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. GitHub push veya Release yapılmadı.

Eski 120 bekleyen API yeni şemaya karşı çalıştırılmamalıdır. Eski dosyaların saklanması otomatik veritabanı geri dönüşü sağlamaz.
