# 1.3.8-dev ve 119 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci/API **1.3.8-dev**, dosya sürümü **1.3.8.0**, şema **119**, API **1.8**. [Proje ödeme hazırlama ve onay rehberi](PROJE_ODEME_HAZIRLAMA_VE_ONAY.md).

## Yedek ve şema

Açık istemci ve bekleyen ödeme bulunmadı. Eski API görevi devre dışı bırakılıp API durduruldu. Aynı kullanıcının geçici Windows görevinde bakım aracı mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeğini aldı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 119'un beklediği kontrol edildi. Ayrıca güvenlik kopyası oluşturularak 119 transaction içinde uygulandı. Paket/betik düzeltmesi gerekmedi.

Personel, izin, defter, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemi/bağlantı, grup geçişleri ve proje yetkilerinin eski içeriklerinin önce/sonra özetleri aynı kaldı. Hesapların oturum nesilleri ve bunlara bağlı hesap rowversion değerleri beklenen şekilde yenilendi; diğer eski satırların rowversion değerleri de karşılaştırmaya dahil edildi. Parola hash'leri, roller ve önceki ücret/IBAN/ödeme/export hakları korundu.

İki yeni proje ödeme biti kapalı başladı; otomatik hazırlama/onay hakkı verilmedi. ProjectPaymentProposals tablosu boş başladı; ödeme hazırlanmadı, onaylanmadı veya kaydedilmedi. İki etkin talep/audit trigger'ı, üç adlandırılmış güvenilir CHECK, beş güvenilir foreign key, iki talep indeksi ve genişletilen güvenli audit fonksiyonu metadata üzerinden doğrulandı. Bu kontroller işlev senaryosu denemesi değildir.

Yedekler artifacts/analysis/live-upgrade-119/backups/ altındadır. database-preparation.json doğrulanan yedeği, baseline.json ve result.json veri özetlerini içerir. Bu özel kayıtlar Git dışındadır.

## Paket ve kullanım

Kaynak paket artifacts/release-bundles/project-payments-119-final/ klasöründedir. ZIP SHA-256 ve manifestteki tüm dosya özetleri doğrulandı. Paketler %LOCALAPPDATA%\Programs\AccountingApp\1.3.8-dev-20261031164416543\ altına ayrı kuruldu; istemci desktop/payload/, API server/ altındadır. Önceki sürüm dosyaları silinmedi.

Sunucu/istemci bağlantı profilleri, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance119 görevi tamamlanıp kaldırıldı. MSSQLLocalDB zorlamadan durdurulduktan sonra kalıcı AccountingApp Local API görevi yeni sürümü başlattı; HTTPS API 1.8 yanıtı ve doğru süreç/loopback dinleyicileri doğrulandı. Adres https://localhost:7443/ ve yalnız IPv4/IPv6 loopback sınırı korunur; Running görev durumu kalıcı başlatıcı için normaldir.

Muhasebe - Güncel kısayolu yeni istemciyi gösterir. Eski oturumlar 119 ile geçersizleşti; mevcut kullanıcı adı/parolayla yeniden giriş gerekir. Proje kullanıcısı için ücret okuması ile ilgili projenin maliyet+hazırlama veya maliyet+onay hakkını yönetici tanımlar. Yönetici de kendi hazırladığı talebe karar veremez; iki farklı etkin hesap gerekir. Yeni proje etiketli ödemeler hazırlama/onay akışını kullanır; projesiz genel merkez ödemeleri önceki kurallarını sürdürür.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları artifacts/analysis/install-1.3.8-dev/ altındadır. Sonuç deployment.json ve readback.json; kurulu klasörde installation.json bulunur. Yalnız yedek/veri koruma, şema nesneleri, paket bütünlüğü, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. Masaüstü açılmadı; giriş, iki hesapla hazırlama/onay, ödeme/kayıp yanıt, eski journal, proje kapsamı ve UI denemeleri kullanıcı isteğiyle atlandı. GitHub push veya Release yapılmadı.

Eski 118 bekleyen API yeni şemaya karşı başlatılmamalıdır. Önceki dosyaları saklamak otomatik veritabanı geri dönüşü sağlamaz.
