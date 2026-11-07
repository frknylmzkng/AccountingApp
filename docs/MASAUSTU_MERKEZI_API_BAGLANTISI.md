# Masaüstü uygulamasının merkezi API bağlantısı

**31 Ekim 2026 izleyen kaynak güncellemesi:** [Çalışma dönemleri](CALISMA_DONEMLERI.md), 1.3.2-dev / şema 114 / API 1.2 olarak hazırlandı. Aşağıdaki önceki sürüm/kapsam ifadeleri tarihsel teslimi anlatır; yeni sürüm henüz kurulmadı.

**İzleyen kurulum:** 30 Ekim 2026'da kullanıcı isteğiyle bu bilgisayarda [localhost API kuruldu](YEREL_API_KURULUMU.md), mevcut tercih API'ye geçirildi ve Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi. Aşağıdaki 'bu geliştirme sırasında değiştirilmedi' kayıtları kaynak geliştirme aşamasına aittir; kurulum doğrulaması ve atlanan denemeler ayrı belgede açıklanır.

30 Ekim 2026 · F13 masaüstü adaptörleri · API sözleşmesi 1.1, yol öneki `/api/v1`.

## Kullanım

Giriş ekranındaki **Bağlantı seçimi…** bağlantısından **Merkezi API kullan** işaretlenir ve güvenilir HTTPS sunucu adresi girilir; örnek biçim `https://muhasebe-sunucu:7443/`. Sunucu, bu kaynakla birlikte üretilen API sürümüyle ayrıca kurulmuş ve çalışıyor olmalıdır. [Sunucu kurulumu](MERKEZI_API_ALTYAPISI.md) Windows/SQL Express, şema ve sertifika gereksinimlerini açıklar. Sunucunun TLS sertifikası istemcide güvenilir ve adresle eşleşen bir sertifika olmalıdır; sertifika doğrulaması kapatılmadı.

Adres yalnızca HTTPS origin olabilir; kullanıcı bilgisi, yol, query veya fragment kabul edilmez. Yönlendirmeler takip edilmez. Sunucu sürümü giriş hazırlığında kontrol edilir. Bağlantı ayarı kullanıcının mevcut tercihlerinde tutulur; token ve parola bu dosyaya/registry'ye kaydedilmez. API seçimi yoksa mevcut yerel bağlantı çalışır. API bağlantısı hata verirse uygulama **yerel SQL'e otomatik geçmez**. Mevcut veritabanı profili silinmez veya API ayarıyla değiştirilmez.

Yeni paket ilk açılışta mevcut kullanıcı tercihini kullanır; bu geliştirme sırasında gerçek kullanıcının tercihi değiştirilmedi. Kaydedilmemiş değişiklik uyarıları ve ekran geçişleri aynı UI akışlarını kullanır. Sunucu veya istemci bu çalışma sırasında başlatılmadı.

## Bağlanan işlevler

Personel listeleri ve SQL özeti, personel ekleme/düzenleme/arşiv/geri alma/geçmiş, izin planı, ödeme/avans ve defter, fiş/önizleme, proje oluşturma/hakediş/gider/iptal, kalıcı kapanış liste/detay/rapor, hesap yönetimi/yetkileri, parola değiştirme/sıfırlama ve işlem geçmişi API üzerinden çalışacak şekilde yönlendirilir. Masaüstü satır sürümlerini base64 olarak, kalıcı personel ve hesap hedeflerini değiştirmeden iletir. SQL transaction'ları sunucudaki mevcut servislerde kalır.

Tablo yanıtı yeniden typed DataTable'a çevrilir. Int64 metinden kayıpsız ayrıştırılır; decimal, GUID, tarih, binary rowversion, DBNull ve salt okunur sütun özellikleri korunur. Ekranların mevcut seçim/sürüm korumaları bu tabloyu kullanmaya devam eder. Kapanış detay sözleşmesi mevcut ClosingSnapshot modeliyle paylaşılır; bu ilk geçişte istemci ve Windows sunucu aynı kaynak sürümünden dağıtılır.

## Oturum ve yetki

Her UserSession yalnızca kendisine ait bellek içi HTTP oturumunu taşır. Token belirli bir HTTPS origin'e bağlıdır; sonraki girişin token'ı önceki ekran tarafından kullanılamaz. Kullanıcının rolü ve izinleri sunucu giriş yanıtından alınır; gerçek işlem yetkileri sunucuda canlı hesap sürümü/nesli ve transaction içinde tekrar doğrulanır.

Mevcut 30 saniyelik oturum kontrolü sunucuda hesabı doğrular, API boşta süresini uzatmaz. Bu kontrol kilitliyken de çalışır. Hesap pasife alınması, parola/yetki değişimi veya silinmesi 401 ile eski oturumu kapatır; ağ hatası hesabı geçerli sayıp yazmayı yerel SQL'e yönlendirmez. Sunucunun boşta veya mutlak süresi dolduğunda yeniden giriş gerekir. Masaüstünün 10 dakikalık fiziksel etkinlik kilidi korunur; API varsayılan boşta süresi 15 dakika, mutlak süresi 8 saattir. Uzun süre kilitli kalındığında parola açma yerine yeniden giriş gerekebilir.

Manuel/otomatik kilit önce yerel özel pencereleri gizler, ayrıca sunucuya kilit gönderir. Kilit açma bu isteğin tamamlanmasını bekler ve aynı hesap için sunucuda parola doğrular; ağ hatasında özel pencereler açılmaz. Hatalı giriş/parola sınırlaması sunucuda mevcut kalıcı sayacı kullanır. Çıkışta token yerelde hemen kullanımdan kaldırılır, sunucuya en fazla 5 saniyelik çıkış isteği gönderilir. Ağ yoksa sunucu tarafındaki oturumun iptali garanti edilemez; token sunucunun süre sınırında sona erer. Yerel uygulama eski token'ı yeniden kullanmaz.

Parola değişikliği gerekçesi audit'e aktarılır ve yeni giriş istenir. Yönetici sıfırlamasında mevcut yönetici parolası ile hedef hesap sürümü/nesli sunucuda doğrulanır; geçici parola yalnızca başarılı yanıtta gösterilir. Parola/token taşıyan sözleşmelerin ToString çıktıları gizlenmiştir; gövdeler loglanmaz.

## Ödeme tekrar koruması ve geçiş

Mevcut PaymentWorkflow değiştirilmeden aynı sırayı sürdürür: güncel seçimle sunucudan ödeme niyeti hazırla → isteği DPAPI korumalı, kalıcı yerel günlüğe yaz → aynı RequestId ve tüm payload ile gönder. Hazırlık bakiye değiştirmez. Format 3, PersonId, özgün rowversion, ClosureId, tutar, tür/proje ve gerekçe korunur. Belirsiz yanıt veya timeout günlüğü silmez; otomatik yeni RequestId üretilmez ve HTTP katmanı yazmayı kendiliğinden tekrar etmez.

Tekrar düğmesi önce kayıtlı sonucu sunucudan sorgular. `Recorded`/`Reversed` kayıtlı sonuç olarak değerlendirilir; yalnızca `Unconfirmed` aynı istekle yeniden gönderilebilir. Reddedilen veya başarılı kayıt da mevcut kullanıcı onayına kadar günlükte kalır. Oturum sona ermesi günlüğü temizlemez; yeniden girişten sonra aynı kapsamda kontrol edilir.

API günlüğü URL yerine sunucunun doğruladığı veritabanı kapsam anahtarına bağlıdır. Anahtar SQL FamilyGUID, katalog, sunucu ve fiziksel dosyadan üretilir; isim benzerliğiyle farklı veritabanları karışmaz. Aynı sunucuya farklı adresle ulaşılması aynı kapsam günlüğünü kullanır. İlk başarılı girişte adres/kapsam bağı tercihlere kaydedilir. Aynı adreste kapsam değişirse giriş durur; eski ödemenin başka veritabanında sorgulanmasına izin verilmez. Sunucu süreci de şema kontrolünde kendi başlangıç kapsamını doğrular.

**Yerel/API geçişinden önce** eski uygulama örnekleri kapatılır, eski bağlantıdaki bekleyen ödemeler sonuçlandırılıp onaylanır. Bağlantı seçimi diğer aynı uygulama süreçleri veya `.pending` günlükleri varken değişikliği engeller. Eski sürüm/doğrudan SQL kullanan başka bilgisayarlar da yönetici tarafından geçişte durdurulmalıdır. Yerel ödeme günlüğü API günlüğüne sessizce dönüştürülmez, silinmez veya gönderilmez. İlk geçiş tamamlanmadan iki bağlantı türüyle paralel ödeme yapılmaz.

Meşru veritabanı taşınmasında kapsam değişmesi beklenebilir: önce eski kapsamın bekleyen işlemleri çözülür, yeni sunucu/veritabanı kimliği yönetici tarafından doğrulanır; daha sonra kullanıcı tercihlerindeki ilgili CentralApiScopes bağı kontrollü olarak yenilenir. Bu işlem için otomatik 'korumayı atla' düğmesi eklenmedi. Eski `.pending` dosyalarını silmek geçiş yöntemi değildir.

## Raporlar ve bakım

Excel/CSV seçimi aynı kalır. Seçilmiş rapor görüntüsü ve rapor/kapsam bilgisi API'ye gönderilir; sunucu canlı dışa aktarma izniyle dosyayı üretir, audit kaydeder ve yetki read lock'unu veri aktarımı boyunca tutar. İstemci tam yanıt alındıktan sonra dosyayı geçici dosyadan atomik olarak hedefe taşır. Sunucuya istemci dosya yolu verilmez. Bu, seçili ekran görüntüsünün raporudur; sunucu gelen rapor hücrelerini yeniden canlı sorgu sonucu gibi sunmaz.

Panoya kopyalamada canlı izin ve audit sunucuda doğrulanır. Yerel yazdırmanın her BeginPrint aşamasında tekrar sunucu dışa aktarma izni aranır; ağ/yetki hatasında yazdırma iptal edilir. Ağlı modelde yetki sunucudan veri teslim edilirken uygulanır: teslim edilmiş içeriğin yerel dosya/pano/yazıcıya çıkışı sonradan geri çağrılamaz. Yetkili olarak başlamış dosya aktarımı tamamlanabilir; yeni işlemler güncel izinle kontrol edilir.

Dosya/pano gövdesi en fazla 8 MiB'dir; dosya raporu ayrıca 10.000 satır, 100 sütun ve toplam 200.000 hücreyle sınırlıdır. Daha büyük görüntü reddedilir; kullanıcı kapsam/sütun seçimini küçültür. Diğer API gövdeleri 128 KiB sınırını korur. Yerel yazıcı ve dosya hedefi masaüstündedir.

API modunda Ayarlar'ın yerel SQL profili/yedekleme/aynalama/restore işlemleri kapalıdır; görünüm ve logo tercihleri kullanılabilir. İstemci otomatik SQL yedeği çalıştırmaz. Sunucu yedekleme ve bakımı ayrı sorumlu süreçte yürütülür. Geri yükleme/şema değişiminde API ve doğrudan SQL istemcileri durdurulur; sunucu yeniden başlatılır, yeni giriş yapılır.

## Paketler ve doğrulama

Bu çalışma için ayrı Windows x64 self-contained paket yolları:

- `artifacts/releases/desktop-central-api`: masaüstü istemci.
- `artifacts/releases/server-desktop-api`: API sunucusu, sözleşme 1.1.

Mevcut masaüstü kısayolu ve çalışan paket değiştirilmedi. Canlı veritabanına bağlanılmadı; SQL yükseltmesi gerekmedi. İki paketin dağıtımı/sertifika/servis kurulumu ayrıca yapılır. İstek üzerine testler, UI/API denemeleri, ağ kesintisi ve çok bilgisayar denemeleri atlandı. Release derlemesi ve yayın çıktıları çalışma zamanı doğrulaması yerine geçmez. Dağıtık oturum deposu, çok worker, proxy kurulumu ve proje bazlı erişim sonraki kapsamdadır.
