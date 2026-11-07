# 1.3.7-dev ve 118 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci/API **1.3.7-dev**, dosya sürümü **1.3.7.0**, şema **118**, API **1.7**. [Ücret ve IBAN görüntüleme yetkileri](UCRET_VE_IBAN_GORUNTULEME_YETKILERI.md) bu sürümdedir.

## Yedek ve şema

Açık istemci ve bekleyen ödeme bulunmadı. Eski API görevi devre dışı bırakılıp API durduruldu. Aynı kullanıcının geçici Windows görevinde çalışan bakım aracı mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeğini aldı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 118'in beklediği kontrol edildi. Ayrıca güvenlik kopyası oluşturularak 118 transaction içinde uygulandı. Paket veya betik düzeltmesi gerekmedi.

Personel, izin, defter, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemi/bağlantı, grup geçişleri ve proje yetkilerinin eski içeriklerinin önce/sonra özetleri aynı kaldı. Hesapların oturum nesilleri ve buna bağlı hesap rowversion değerleri beklenen şekilde yenilendi; eski diğer satırların rowversion değerleri de karşılaştırmaya dahil edildi. Parola hash'leri, roller ve eski ödeme/export bitleri korundu.

İki yeni bit alanının bit/not-null/default tanımı, tüm hesaplarda fiziksel değerlerin kapalı başladığı ve audit trigger/fonksiyonunun iki alanı kapsadığı metadata üzerinden doğrulandı. Yönetici rolü uygulamada etkin tam erişimini sürdürür. Yönetici dışındaki hesapların ücret/IBAN okuması yönetici tarafından açıkça verilmelidir. Eski ödeme biti tek başına ücret görüntüleme açmaz; ücret kapalıyken ödeme etkisizdir.

Ön yedek ve güvenlik kopyası artifacts/analysis/live-upgrade-118/backups/ altındadır. database-preparation.json doğrulanan yedeği, baseline.json ve result.json veri özetlerini içerir. Bu özel kayıtlar Git dışındadır.

## İstemci ve API

Paket kaynağı artifacts/release-bundles/field-visibility-118-final/ klasörüdür. ZIP SHA-256 ve manifestteki tüm dosya özetleri doğrulandı. Paketler %LOCALAPPDATA%\Programs\AccountingApp\1.3.7-dev-20261031161715974\ altına ayrı kuruldu; istemci desktop/payload/, API server/ altındadır. Eski sürüm dosyaları silinmedi.

Sunucu/istemci bağlantı profilleri, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance118 görevi tamamlanıp kaldırıldı. MSSQLLocalDB zorlamadan durdurulup kalıcı AccountingApp Local API görevi yeni sürüme geçirildi. API https://localhost:7443/ adresinde yalnız loopback erişimiyle çalışır; Running görev durumu kalıcı başlatıcı için normaldir.

Muhasebe - Güncel kısayolu yeni istemciyi gösterir. 118 eski oturumları geçersizleştirdiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir. İzinler Yönetici hesapları > Ek hesap yetkileri bölümünden yönetilir. Genel Excel/CSV/pano aktarımı ayrı export hakkına ek olarak iki görüntüleme hakkını da ister; ayrıntılar ilgili özellik rehberindedir.

Kurulum kayıtları ve eski görev/kısayol/profil kopyaları artifacts/analysis/install-1.3.7-dev/ altındadır. Son durum deployment.json ve readback.json, kurulu klasörde installation.json dosyasındadır. Masaüstü açılmadı; farklı yetkiler, giriş, ödeme, export, taslak ve UI işlevleri denenmedi. Yalnız yedek/veri koruma, şema nesneleri, paket bütünlüğü, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. GitHub push veya Release yapılmadı.

Eski 117 bekleyen API yeni şemaya karşı başlatılmamalıdır. Önceki dosyaların korunması otomatik veritabanı geri dönüşü anlamına gelmez.
