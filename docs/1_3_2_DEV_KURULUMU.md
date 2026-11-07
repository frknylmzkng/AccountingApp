# 1.3.2-dev ve 114 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla kuruldu; işlev denemeleri atlandı.

İstemci ve merkezi API **1.3.2-dev**, dosya sürümü **1.3.2.0**, veritabanı şeması **114**, API sözleşmesi **1.2**. **Muhasebe - Güncel** kısayolu yeni istemciyi açar. API `https://localhost:7443/` adresinde yalnız IPv4/IPv6 loopback üzerinden çalışır.

Paketler ZIP SHA-256 ve dosya manifestleriyle doğrulanıp `%LOCALAPPDATA%\Programs\AccountingApp\1.3.2-dev-20261031093615949\` altına ayrı kuruldu. İstemci `desktop/payload/`, API `server/` klasöründedir. Önceki sürüm dosyaları silinmedi. Yeni sürüm geliştirme paketidir; GitHub Release yayımlanmadı. [Çalışma dönemleri rehberi](CALISMA_DONEMLERI.md).

## Yedek ve veri koruma

Açık masaüstü veya bekleyen ödeme bulunmadı. API görevi bakım için devre dışı bırakılıp eski API durduruldu. Mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alındı; VERIFYONLY, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 114'ün beklediği ve yedekten sonra verilerin değişmediği kontrol edildi. Şema aracı ayrıca güvenlik kopyası oluşturdu; 114 transaction içinde uygulandı.

Personel, izin, ödeme, finans, kapanış, proje/ekip/atama, kimlik ve eski audit içeriklerinin önce/sonra özetleri korundu. Yeni dönem sütunları, kart/izin/hesap rowversion değerleri ve beklendiği gibi yenilenen hesap oturum nesilleri ayrı ele alındı. Eski audit satırları değiştirilmedi; bakımın ürettiği yeni audit kayıtları korunur.

Mevcut bir personel kartı için bir eski aktarım dönemi ve kimliği belirlenebilen bir geçmiş bağlantısı oluştu. İşe giriş/ayrılış, kayıt zamanı ve sahibi uydurulmadı; bilinmeyen değerler boş bırakıldı. On etkin kimlik/koruma/bağlantı/audit trigger'ı, üç güvenilir check, dört açıkça adlandırılmış güvenilir FK ve dört dönem indeksi metadata üzerinden doğrulandı. Bu kontroller iş senaryosu denemesi değildir.

Ön yedek ve güvenlik kopyası `artifacts/analysis/live-upgrade-114/backups/` altındadır. Veri özetleri, yedek kimliği ve şema sonucu aynı üst klasördeki `baseline.json`, `database-preparation.json`, `result.json` dosyalarındadır. Özel veri içerebilen bu kayıtlar Git dışındadır.

## API ve kısayol

Windows oturum açılışındaki **AccountingApp Local API** görevi yeni sürümü aktif kurulum kaydından seçer. Başlatıcı protokol beklentisini bu kaydın `ApiVersion` alanından alacak şekilde güncellendi; yeni beklenti **1.2**. Başlatıcı API süreci bitene kadar açık kalır; API çalışırken görevin Running görünmesi normaldir. Mevcut sınırlı yeniden deneme politikası korundu.

İlk başlangıçta LocalDB 575 / SQL bağlantı hatası alındı. API görevi durdurulup yalnız bu kullanıcıya ait MSSQLLocalDB örneği zorlamadan kapatıldı; yeni API görevi üzerinden yeniden başlatma sonrasında HTTPS başlangıcı başarılı oldu. Veritabanı yeniden oluşturulmadı veya geri yüklenmedi. İlk hata kayıtları tarihsel kanıt olarak saklandı; son başarılı durum `deployment.json` ve `readback.json` dosyalarındadır.

Mevcut Windows hesabı, HTTPS sertifikası, veritabanı profili/kapsamı, API sunucu profili ve kullanıcı tercihleri korundu. Kısayol hedefi ve çalışma klasörü yeni istemciyi gösterir. Eski oturumlar geçersizleşti; mevcut kullanıcı adı/parolayla yeniden giriş gerekir. Yeni özellik için personel arşivinde bir kart seçip **Çalışma dönemleri…** düğmesini kullanın.

114 sonrasında 113 bekleyen eski API başlatılmamalıdır. Önceki dosyaların saklanması otomatik veritabanı geri dönüşü anlamına gelmez. Geri dönüş, yedek/uyumluluk ve sonradan oluşan veri değerlendirmesi gerektirir.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları `artifacts/analysis/install-1.3.2-dev/` altındadır. Kurulu sürüm kökünde `installation.json` bulunur. Masaüstü başlatılmadı; giriş, dönem/izin/ödeme işlemleri, diğer ekranlar, eşzamanlılık ve kurulum sihirbazı denenmedi. Yalnız yedek/şema/veri koruma, paket bütünlüğü ve API başlangıç kontrolleri yapıldı.
