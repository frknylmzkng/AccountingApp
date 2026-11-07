# 1.3.5-dev ve 116 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci ve API **1.3.5-dev**, dosya sürümü **1.3.5.0**, API sözleşmesi **1.5**, şema **116**. [Audit minimizasyonu](ISLEM_GECMISI_VERI_MINIMIZASYONU.md) bu sürümde bulunur.

## Yedek ve yükseltme

Açık istemci ve bekleyen ödeme bulunmadı. Eski API görevi devre dışı bırakılıp API durduruldu. Mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alındı, RESTORE VERIFYONLY WITH CHECKSUM ve kapsam manifesti doğrulandı. Yalnız 116 betiğinin beklediği kontrol edildi. Yükseltici ayrıca güvenlik kopyası oluşturdu.

Bakım aracını doğrudan başlatırken LocalDB başlangıç hatası görüldü; yedek/yükseltme aynı kullanıcının geçici Windows görevi bağlamında çalıştırıldı. İlk 116 yürütmesi, yeni ChangedFields sütununu kullanan CHECK için ayrı GO bloğu eksik olduğundan geri alındı. Henüz uygulanmamış betik düzeltildi; istemci/API ve bakım aracı yeniden derlenip paketlendi. Eski paket artifacts/release-bundles/audit-privacy-116-final/SUPERSEDED.json ile geçersiz olarak işaretlendi. Kullanılan düzeltilmiş paket artifacts/release-bundles/audit-privacy-116-fixed/ altındadır. Önceden uygulanmış 100–115 betikleri değiştirilmedi.

Yeni doğrulanmış yedek sonrası 116 transaction içinde başarıyla uygulandı. Personel, izin, defter, finans, kapanış, proje/ekip/atama, çalışma dönemi/bağlantı, proje yetkileri ve eski audit içeriklerinin önce/sonra özetleri aynı kaldı. Hesapların oturum nesli ve ona bağlı hesap rowversion değerleri beklenen şekilde değişti; diğer eski satırların rowversion değerleri de koruma karşılaştırmasına dahil edildi. Parola hash'leri, roller ve kullanıcı ayarları korundu.

İki etkin audit trigger'ı, iki SQL fonksiyonu, güvenli audit görünümü ve bir güvenilir JSON kısıtı metadata üzerinden doğrulandı. Kullanıcı kayıtlarıyla maskeleme/ödeme/izin senaryosu denenmedi.

Özel yedekler artifacts/analysis/live-upgrade-116/backups/ altındadır; database-preparation.json doğrulanan yedeği, baseline.json ve result.json veri özetlerini içerir. İlk başarısız uygulama kayıtları first-attempt/ altında tutulur. Bu özel dosyalar Git dışındadır.

## Paket ve kullanım

ZIP ve manifestteki dosya özetleri doğrulandı. İstemci/API %LOCALAPPDATA%\Programs\AccountingApp\1.3.5-dev-20261031104505910\ altına ayrı kuruldu; önceki sürüm dosyaları korunur. Sunucu/istemci bağlantı profilleri, veritabanı kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korunur. AccountingApp Local API görevi aktif sürüm kaydından API 1.5'i başlatır. Adres https://localhost:7443/ ve yalnız loopback erişimi korunur.

Muhasebe - Güncel kısayolu yeni istemciyi gösterir. 116 oturumları geçersizleştirdiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir. Eski 115 bekleyen API yeni şemaya karşı başlatılmamalıdır; önceki klasörlerin korunması otomatik geri dönüş sağlamaz.

Kurulum kayıtları artifacts/analysis/install-1.3.5-dev/ altındadır. Sonuç deployment.json ve readback.json dosyalarında, kurulu klasörde ayrıca installation.json dosyasındadır. API başlangıcı ve kısayol hedefi doğrulandı; masaüstü açılmadı, giriş/işlev/UI denemeleri atlanır. GitHub push veya Release bu kurulumun kapsamında değildir.
