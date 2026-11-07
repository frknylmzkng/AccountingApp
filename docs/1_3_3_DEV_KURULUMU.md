# 1.3.3-dev ve 115 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kuruldu; işlev denemeleri atlandı.

İstemci ve merkezi API **1.3.3-dev**, dosya sürümü **1.3.3.0**, veritabanı şeması **115**, API sözleşmesi **1.3**. **Muhasebe - Güncel** kısayolu yeni istemciyi açar. API `https://localhost:7443/` adresinde yalnız IPv4/IPv6 loopback üzerinden çalışır.

Paketler ZIP SHA-256 ve dosya manifestleriyle doğrulanıp `%LOCALAPPDATA%\Programs\AccountingApp\1.3.3-dev-20261031100628950\` altına ayrı kuruldu. İstemci `desktop/payload/`, API `server/` klasöründedir. Önceki sürüm dosyaları silinmedi. Bu bir geliştirme kurulumu; GitHub Release yayımlanmadı. [Proje sorumlusu ve yetkiler rehberi](PROJE_SORUMLUSU_VE_YETKILER.md).

## Yedek ve şema

Açık masaüstü veya bekleyen ödeme bulunmadı. Eski API görevi devre dışı bırakılıp API durduruldu. Mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alındı; VERIFYONLY, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 115'in beklediği ve yedekten sonra verilerin değişmediği kontrol edildi. Şema aracı ayrıca güvenlik kopyası oluşturdu; 115 transaction içinde uygulandı.

Personel, izin, ödeme, finans, kapanış, proje/ekip/atama, çalışma dönemi/bağlantı, kimlik ve eski audit içeriklerinin önce/sonra özetleri aynı kaldı. Hesapların oturum nesilleri ve buna bağlı hesap rowversion değerleri beklendiği gibi yenilendi; diğer eski satırların rowversion değerleri de karşılaştırmaya dahil edildi. Kullanıcı adı, parola hash'i, rol ve mevcut ek izinler korundu.

Yeni ProjectAccountAccess tablosu boş başladı; proje sorumlusu/üyelik veya yeni kullanıcı oluşturulmadı. İki etkin koruma/audit trigger'ı, üç güvenilir check, bir güvenilir proje foreign key'i ve üç yetki indeksi metadata üzerinden doğrulandı. Bunların ikisi etkin üyelik/tek sorumlu ayrımı için filtreli unique indekstir. Bu kontroller iş senaryosu veya yetki sızması denemesi değildir.

Ön yedek ve güvenlik kopyası `artifacts/analysis/live-upgrade-115/backups/` altındadır. Yedek kimliği, veri özetleri ve şema sonucu aynı üst klasördeki `database-preparation.json`, `baseline.json`, `result.json` dosyalarındadır. Bu özel bakım kayıtları Git dışındadır.

## API ve kullanım

**AccountingApp Local API** Windows görevi aktif kurulum kaydından 1.3.3-dev sunucusunu ve **1.3** protokol beklentisini seçer. Önceki kurulumda görülen LocalDB başlangıç bağlamı sorununu önlemek için bakım aracı bittikten sonra bu kullanıcıya ait MSSQLLocalDB örneği zorlamadan durdurulup kalıcı API görevi bağlamında başlatıldı. Yeni API ilk etkinleştirmede HTTPS başlangıç kontrolünü geçti. Başlatıcı API ömrü boyunca açık kalır; görevin Running görünmesi normaldir. Sınırlı yeniden deneme politikası korundu.

HTTPS sertifikası, Windows hesabı, API sunucu profili, veritabanı profili/kapsam sabitlemesi ve kullanıcı tercihleri korundu. Kısayol hedefi ve çalışma klasörü yeni istemciyi gösterir. Eski oturumlar geçersizleşti; mevcut kullanıcı adı/parolayla yeniden giriş gerekir.

Proje yetkisi tanımlamak için yönetici hesabıyla **Yönetici hesapları** ekranında **Proje kullanıcısı (kısıtlı)** rolünde hesap açın. Ardından **İşlemler → Projeler, ekipler ve tarihli atamalar** ekranında projeyi açıp **Proje sorumlusu ve yetkiler…** düğmesini kullanın. Mevcut genel merkez rolleri genel erişimlerini sürdürür; kısıtlı kullanıcı girişte **Projelerim** ekranına yönlendirilir. Bu işlemler kurulum sırasında denenmedi.

115 sonrasında eski 114 bekleyen API başlatılmamalıdır. Önceki dosyaların saklanması otomatik veritabanı geri dönüşü anlamına gelmez. Geri dönüş, yedek/uyumluluk ve sonradan oluşan veri değerlendirmesi gerektirir.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları `artifacts/analysis/install-1.3.3-dev/` altındadır. Son durum `deployment.json` ve `readback.json`; kurulu sürüm kökünde ayrıca `installation.json` bulunur. Masaüstü başlatılmadı; giriş, proje sorumlusu/yetki verme-geri alma, kapsam izolasyonu, rapor, ödeme, diğer ekranlar, eşzamanlılık ve kurulum sihirbazı denemeleri atlandı. Yalnız yedek/şema/veri koruma, paket bütünlüğü ve API başlangıç kontrolleri yapıldı.
