# Yedek aynalamasını yeniden deneme

26 Ekim 2026. Güncel analizdeki 5. bulgu giderildi: son 24 saatte doğrulanmış yerel yedek bulunması, başarısız harici kopyanın yeniden denenmesini artık engellemez.

## İşleyiş

- Yönetici oturumu açıkken girişte ve mevcut saatlik kontrolde yerel yedek değerlendirilir. Son 24 saatte doğrulanmış yedek varsa yeni ana yedek alınmaz; en güncel yerel yedeğin seçili aynalama hedefine kopyası kontrol edilir ve eksik/başarısız kopya yeniden denenir.
- Aynalama isteğe bağlıdır. Hedef USB/ağ klasörü kullanılamıyorsa ana yedek ve doğrulama zamanı korunur. Sonraki kontrolde tekrar denenir; uygulama kapalıyken görev çalışmaz. USB takılması olayı ayrıca izlenmez.
- `.bak.mirror.json` yan dosyasında hedef klasör, kaynak SHA256, son deneme, son başarılı aynalama ve sabit hata türü atomik ve kalıcı saklanır. Eksik/bozuk durum dosyası veya yarıda kesilmiş önceki deneme yeniden denemeyi engellemez. Dosya Git kapsamı dışındadır.
- Tamamlandı bilgisi tek başına yeterli değildir: kaynak ve hedefin varlığı/SHA256 değerleri kontrol edilir. Silinen veya bozulan harici kopya onarılır. Hedef klasör değişirse yeni hedef ayrıca tamamlanır.
- Yeniden kopyalanacak yerel yedek SQL `RESTORE VERIFYONLY WITH CHECKSUM` ve tam yedek/veritabanı başlığıyla yeniden doğrulanır. Kaynak bozuksa mevcut harici kopya değiştirilmez; durum ekranı yeni yerel yedek oluşturulmasını ister.
- Kopya benzersiz `.copying` dosyasında hazırlanır, diske yazılır, SHA256 ile karşılaştırılır ve sonra hedefe atomik taşınır. Başarısız değiştirme eski hedefi korur. İptal/hata durumunda yalnızca o denemenin geçici dosyası temizlenir; yerel yedek silinmez.
- Yerel yedek başarısı `Backup.Create`, aynalama başarısı ayrı `Backup.Mirror` audit olayıdır. Başarısız aynalama yüzünden yeni bir `Backup.Create` oluşturulmaz. Aynalama audit'i başarısızsa tamamlandı kaydedilmez; yeniden deneme mümkündür.
- Günlük kontrol, manuel yedek, aynalama ve geri yükleme aynı süreç içi kilidi paylaşır. Birden çok süreç arasındaki kapsamlı bakım kilidi bu değişiklikte eklenmedi.

## Kullanıcı ekranı

Ayarlar'da **Aynalamayı yeniden dene** düğmesi en güncel doğrulanmış yerel yedeği kullanır; yeni ana yedek oluşturmaz. Yerel yedek yoksa önce Şimdi yedekle kullanılması istenir. Yerel yedek 24 saatten eski olsa da manuel aynalama mümkündür; işlem yedeğin doğrulama tarihini yenilemez. Sonraki otomatik günlük kontrol yine yeni ana yedek alır.

Ayarlar açıldığında, F5 ile ve yedek/aynalama işlemlerinden sonra son doğrulanmış yerel yedek, geçerli hedefe son başarılı aynalama veya bekleyen durum/hata nedeni görünür. Durum okuması dosya kopyalamaz ve audit yazmaz. Başarı zamanı son tamamlanmış denemeyi gösterir; sonraki kontrol içerik bütünlüğünü yeniden sınar.

Ana ekranda otomatik kontrol harici kopyayı tamamlayamazsa “Ana yedek mevcut; harici kopya bekliyor” bilgisi gösterilir. Sonraki başarılı kontrolde durum güncellenir. Aynalama hedefi yerel klasörle aynıysa ikinci fiziksel kopya varmış gibi gösterilmez.

## Saklama ve mevcut korumalar

Son 14 yedek saklama kuralı sürer. Yeniden aynalanan eski bir dosya, hedefte daha yeni dosyalar bulunsa da o temizlemede korunur; geçici olarak 15 dosya tutulabilir. Sonraki normal temizleme kendi korunan dosyasıyla devam eder. Silinen uygulama yedeğinin yalnızca ona ait doğrulama/aynalama yan dosyaları da temizlenir. Geri yüklemede seçilen kaynak koruması devam eder.

İşlem yönetici ve güncel oturum gerektirir. İptal edilen oturum, görüntüleyici veya personel sorumlusu aynalama/otomatik yedek/durum okuma yapamaz. Günlüklere ham hata mesajı, bağlantı bilgisi veya personel verisi eklenmez.

## Deneme adımları

Yalnızca deneme veritabanı ve ayrı yedek klasörleriyle:

1. Ana ve harici klasörleri seçin; harici ortamı erişilemez yapın. Şimdi yedekle: yerel `.bak` ve `.verified` oluşmalı, harici kopya bekliyor görünmeli.
2. Harici erişim sağlanınca Aynalamayı yeniden dene: aynı yerel dosya kopyalanmalı; yeni `.bak` oluşmamalı, yerel doğrulama zamanı değişmemeli.
3. Hedefteki deneme kopyasını silin veya bozun; yeniden deneyin. Kaynak/hedef SHA256 eşleşmeli.
4. Harici klasörü değiştirin. Yeni hedef tamamlanmalı; önceki hedefin başarı kaydı yeni hedef için kullanılamamalı.
5. Birkaç kontrol boyunca ortamı erişilemez tutun. Yerel yedek sayısı artmamalı; her kontrolde yeniden deneme mümkün olmalı.
6. Ana klasörle aynı hedefi seçin: ayrı harici kopya oluşturulmadığı belirtilmeli.

Yeni SQL betiği gerekmiyor. Gerçek veritabanı, yedek klasörleri, açık uygulama ve masaüstü kısayolu geliştirme sırasında değiştirilmedi. Yerel paket `artifacts/releases/backup-mirror-retry/` için hazırlanır. GitHub'a push veya yeni Release yapılmadı. Tam test ve paket doğrulama kanıtları aşağıdaki dağıtım kaydındadır.

## Doğrulama ve yerel paket

Tam takım: **532 başarılı, 0 başarısız, 0 atlanan**. Önceki 509 test korundu; aynalama için 23 test durumu eklendi. Release derlemesi 0 uyarı, 0 hata. Kanıt: `artifacts/analysis/master-20261026-195505-188/master.trx` ve `summary.json`; geçici LocalDB motoru temizlendi.

Yeni testler başarısız manuel/otomatik aynalamanın aynı dosyadan onarılması, tekrarlanan başarısızlıklarda ana yedeğin çoğalmaması, tamamlandı kaydına rağmen silinen/bozulan hedefin onarılması, atomik değiştirme hatasında hedef/geçici dosya koruması, hedef değişimi, eksik/bozuk/yarıda kalan durum dosyası, eşzamanlı kontroller, yerel yedek yaşının korunması, bozuk kaynağın yayımlanmaması, audit hatası, iptal ve yetki/oturum denetimini kapsar. Aynı klasör seçimi, salt okunur durum kontrolü, yan dosya saklama temizliği ve eski yedeğin aynalama temizliğinden korunması da doğrulandı. Mevcut geri yükleme/seçili kaynak korumaları tam takımda geçti.

Yerel paket: `artifacts/releases/backup-mirror-retry/`. Paket DLL'i derleme çıktısıyla SHA256 üzerinden eşleştirilir; kurulum aracının `--help` çalışması kontrol edilir. Paket doğrulama kaydı: `artifacts/analysis/backup-mirror-retry/verification.json`. Canlı uygulamada USB takma/çıkarma denemesi veya gerçek veritabanı yedekleme bu geliştirme adımında yapılmadı.

## 26 Ekim 2026 — güncel yerel pakette ekran denemesi

Kullanıcı isteğiyle önceki uygulama kapatıldı; Muhasebe - Güncel kısayolu `artifacts/releases/backup-mirror-retry/WinFormsApp2.exe` paketine yönlendirildi. Bağlantı dosyası korundu ve SHA256 ile eşleştirildi. Giriş hazırlığı başarılı; güncel uygulama gerçek bağlantıyla giriş ekranında açık bırakıldı.

Paketle aynı DLL (`D5C1BCCB8B1BB276A03D918C7B996F01614085EBBEE864555B95EB60A45F8925`) kullanılarak gerçek Ayarlar formu, ayrı LocalDB motoru ve yalnızca test klasörlerinde denendi. **6 kontrol başarılı**:

1. Yerel yedek yokken doğru boş durum bilgisi görüldü.
2. Şimdi yedekle düğmesiyle erişilemeyen hedefte beklenen hata oluştu; ana yedek/VERIFYONLY işareti korundu ve Ayarlar harici kopya bekliyor/hata bilgisini gösterdi.
3. Test hedefi erişilebilir yapılınca Aynalamayı yeniden dene düğmesi aynı dosyayı kopyaladı. Ana yedek sayısı bir kaldı; kaynak SHA256 ve yerel doğrulama zamanı değişmedi. Durum başarı zamanına döndü.
4. Harici test kopyası bozulup kilitlenince otomatik kontrol onarımı tamamlayamadı. F5 sonrasında Ayarlar önceki başarı zamanı ile yeni bekleyen durum/hata bilgisini birlikte gösterdi; geçici kopya artığı kalmadı.
5. Kilit kaldırıldıktan sonra otomatik kontrol aynı yedeği onardı; hedef/kaynak SHA256 eşleşti. Yeni ana yedek oluşmadı; F5 ile güncel başarı zamanı ve tamamlanmış durum görüldü.
6. Sonraki başarılı kontrol yeni ana yedek, yinelenen aynalama audit'i veya gereksiz başarı zamanı değişikliği oluşturmadı.

Otomatik senaryo için saatlik zamanlayıcının kullandığı üretim `EnsureDailyAsync` yolu, test klasörleriyle doğrudan çağrıldı; bir saat beklenmedi. Fiziksel USB çıkarılmadı: erişilemez hedef ve dosya kilidi ayrı test klasörlerinde oluşturuldu. Ekran işlemleri Computer Use ile yapıldı. Ayarları kaydet düğmesi kullanılmadı; gerçek kullanıcı tercihleri/bağlantı ayarları test için değiştirilmedi. Gerçek veritabanı kayıtları kullanılmadı; test personeli, izinleri ve ödeme defterinin özetleri her aşamada aynı kaldı. Yedek/aynalama audit'leri yalnızca geçici veritabanında oluştu.

Test süreci normal kapatıldı; geçici veritabanı ve LocalDB motoru temizlendi. Kanıtlar `artifacts/analysis/mirror-live-ui/ui-results.json`, `verification.json` ve `cleanup.txt` dosyalarında; sentetik yedek/kopya dosyaları bu klasörde kanıt olarak tutuldu. Kaynak kod veya paket DLL'i bu deneme adımında değiştirilmedi. Önceki tam regresyon sonucu 532 başarılı testtir; bu adım paket üzerinde ekran doğrulamasıdır. GitHub'a push veya yeni Release yapılmadı.
