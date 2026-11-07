# 1.3.6-dev ve 117 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kuruldu; işlev denemeleri atlandı.

İstemci/API **1.3.6-dev**, dosya sürümü **1.3.6.0**, şema **117**, API **1.6**. [Tarihli Genel/Joker geçişi](TARIHLI_PERSONEL_GRUBU_GECISI.md) bu sürümdedir.

## Yedek ve yükseltme

Açık istemci ve bekleyen ödeme bulunmadı. Eski API görevi devre dışı bırakılıp API durduruldu. Kullanıcının Windows görev bağlamında çalışan geçici bakım aracı, mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeğini aldı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 117'nin beklediği kontrol edildi. Ayrıca güvenlik kopyası oluşturularak 117 transaction içinde uygulandı. Bu kurulumda SQL betiği düzeltmesi gerekmedi; paket kaynaklarıyla aynı betik uygulandı.

Personel, izin, defter, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemi/bağlantı ve proje yetkisi içeriklerinin önce/sonra özetleri aynı kaldı. Hesap oturum nesilleri ve buna bağlı hesap rowversion değerleri beklendiği gibi yenilendi; diğer eski satırların rowversion değerleri de veri koruma karşılaştırmasına dahil edildi. Kullanıcı adı/parola hash'i/roller korundu.

Yeni PersonnelGroupTransitions tablosu boş başladı; geçmişe geçiş uydurulmadı, hiçbir kişi başka gruba taşınmadı. Dokuz etkin kimlik/bağlantı/geçiş trigger'ı, bir güvenilir grup CHECK, üç güvenilir foreign key, kişi başına tek açık dönem unique filtreli indeksi ve geçiş tablosunun dört indeksi metadata üzerinden doğrulandı. Bunlar kullanıcı işlev denemeleri değildir.

Ön yedek ve güvenlik kopyası artifacts/analysis/live-upgrade-117/backups/ altındadır. database-preparation.json doğrulanan yedeği, baseline.json ve result.json veri özetlerini içerir. Bu özel kayıtlar Git dışındadır.

## İstemci/API kurulumu

Paket kaynağı artifacts/release-bundles/group-transition-117-final/ klasörüdür. ZIP SHA-256 ve manifestteki tüm dosya özetleri doğrulandı. Paketler %LOCALAPPDATA%\Programs\AccountingApp\1.3.6-dev-20261031144248901\ altına ayrı kuruldu; istemci desktop/payload/, sunucu server/ altındadır. Eski sürüm dosyaları silinmedi.

Sunucu profili, istemci appsettings.json, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance117 görevi tamamlanıp kaldırıldı. MSSQLLocalDB zorlamadan durdurulduktan sonra kalıcı AccountingApp Local API görevi yeni paketi başlattı. HTTPS başlangıcında API 1.6, doğru süreç yolu ve yalnız 127.0.0.1/::1 dinleyicileri doğrulandı. Adres https://localhost:7443/ olarak kaldı; görevin Running durumu normaldir.

Muhasebe - Güncel kısayolunun hedefi, çalışma klasörü ve simgesi yeni istemciye geçirildi. Oturumlar yenilendiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir. İlgili etkin personel kartında Tarihli grup geçişi… düğmesi kullanılabilir. Bu işlem kurulum sırasında denenmedi.

Kurulum kanıtları ve önceki görev/kısayol/profil kopyaları artifacts/analysis/install-1.3.6-dev/ altında; son durum deployment.json ve readback.json, kurulu klasörde installation.json dosyasındadır. Masaüstü açılmadı. Giriş, grup geçişi, eski seçim/tekrar, ödeme, izin ve UI denemeleri kullanıcı isteğiyle atlandı. Yalnız yedek/veri koruma, şema nesneleri, paket bütünlüğü, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. GitHub push veya Release yapılmadı.

Eski 116 bekleyen API yeni şemaya karşı çalıştırılmamalıdır. Eski klasörlerin saklanması otomatik veritabanı geri dönüşü anlamına gelmez.
