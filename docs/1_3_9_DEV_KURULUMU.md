# 1.3.9-dev ve 120 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci/API **1.3.9-dev**, dosya sürümü **1.3.9.0**, şema **120**, API **1.9**. [Vardiya ve izin takvimi kullanım rehberi](VARDIYA_VE_IZIN_TAKVIMI.md).

## Yedek ve şema

Açık istemci ve bekleyen ödeme bulunmadı. Eski API görevi devre dışı bırakılıp API durduruldu. Aynı kullanıcının geçici Windows görevinde bakım aracı mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeğini aldı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 120'nin beklediği kontrol edildi. Ayrıca güvenlik kopyası oluşturularak 120 transaction içinde uygulandı. Paket/betik düzeltmesi gerekmedi.

Personel, izin, defter, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemi/bağlantı, grup geçişleri, proje ödeme talepleri ve eski proje yetkisi içeriklerinin önce/sonra özetleri aynı kaldı. Hesapların oturum nesilleri ve buna bağlı hesap rowversion değerleri beklendiği gibi yenilendi; diğer eski satırların rowversion değerleri de karşılaştırmaya dahil edildi. Parola hash'leri, roller ve önceki ücret/IBAN/ödeme/export/proje ödeme izinleri korundu.

İki yeni takvim biti kapalı başladı. ShiftTemplates, PlannedShifts, ShiftAssignments, LeaveRequests ve ScheduleCopyBatches tabloları boş oluşturuldu; mevcut izinler bu tablolara sahte taleple/onayla aktarılmadı. On bir etkin koruma/audit trigger'ı, dokuz adlandırılmış güvenilir CHECK, on altı güvenilir foreign key, on bir indeks ve genişleyen audit gizlilik fonksiyonu metadata üzerinden doğrulandı. Yeni tablolarda devre dışı veya güvenilmeyen CHECK/FK bulunmadı. Bunlar kullanıcı işlev senaryosu denemeleri değildir.

Ön yedek ve güvenlik kopyası artifacts/analysis/live-upgrade-120/backups/ altındadır. database-preparation.json doğrulanan yedeği, baseline.json ve result.json veri özetlerini içerir. Bu özel kayıtlar Git dışındadır.

## İstemci/API kurulumu ve kullanım

Paket kaynağı artifacts/release-bundles/shift-calendar-120-final/ klasörüdür. ZIP SHA-256 ve manifestteki dosya özetleri doğrulandı. İstemci/API `%LOCALAPPDATA%\Programs\AccountingApp\1.3.9-dev-20261031172644597` altında ayrı klasörlere kuruldu; eski sürüm dosyaları korundu. Sunucu/istemci bağlantı profilleri, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance120 görevi tamamlandıktan sonra kaldırıldı. MSSQLLocalDB zorlamadan durdurulup kalıcı AccountingApp Local API görevi yeni sürüme geçirildi; API 1.9 başlangıcı doğrulandı, adres https://localhost:7443/ ve yalnız loopback erişimi korundu.

Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi ve hedefi tekrar okundu. 120 eski oturumları geçersizleştirdiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir. Yönetici proje ekranından, proje kullanıcısı Projelerim üzerinden Vardiya ve izin takvimi düğmesini açar. Proje kullanıcısına ilgili projede CanPlanShifts ve/veya CanApproveLeaves hakkı yönetici tarafından açıkça verilir. Bunlar ücret/IBAN veya ödeme haklarını açmaz.

Takvim ilk açıldığında yeni vardiya/şablon/izin talebi bulunmaz. Önce mevcut tarihli proje/ekip atamaları hazırlanır, ardından şablon ve taslak vardiya açılır, kişi atanıp yayınlanır. Eski doğrudan Genel izinleri uygun proje/dönem ilişkisiyle salt okunur görünür. Takvim puantaj, izin hakkı, ücret veya ödeme üretmez.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları artifacts/analysis/install-1.3.9-dev/ altında; son durum deployment.json ve readback.json, kurulu klasörde installation.json dosyasındadır. Masaüstü açılmadı; giriş, gece vardiyası, çakışma, izin onayı, hafta kopyalama/rollback, farklı yetkiler ve UI denemeleri kullanıcı isteğiyle atlandı. Yalnız yedek/veri koruma, şema nesneleri, paket bütünlüğü, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. GitHub push veya Release yapılmadı.

Eski 119 bekleyen API yeni şemaya karşı çalıştırılmamalıdır. Eski dosyaların saklanması otomatik veritabanı geri dönüşü sağlamaz.
