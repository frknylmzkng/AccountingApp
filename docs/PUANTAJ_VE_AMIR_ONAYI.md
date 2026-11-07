# Puantaj, istisnalar ve amir onayı

1 Kasım 2026 kaynak güncellemesi: [vardiyasız puantaj](VARDIYASIZ_PUANTAJ.md), 1.3.11-dev / hazırlanmış 122 / API 1.11 ile eklendi. Canlı 1.3.10-dev/121 korunuyor, denemeler atlandı. Aşağıdaki vardiya zorunluluğu ve açık işler listesi ilk 1.3.10-dev tesliminin tarihsel kapsamıdır; yeni vardiyasız yol ayrı rehberde açıklanır.

31 Ekim 2026 · Kaynak/paket **1.3.10-dev** · Şema **121** · API **1.10**.

Kurulum güncellemesi: mevcut veritabanı yedeklenip 121 uygulandı; istemci/API kuruldu ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Veriler ve ayarlar korundu, denemeler atlandı. [Kurulum kaydı](1_3_10_DEV_KURULUMU.md). Aşağıdaki kaynak teslimi notu kurulum öncesini anlatır.

Kullanıcı 1.3.9-dev sürümünün çalıştığını doğruladı. Yol haritası Aşama 4'ün manuel ve kontrollü CSV puantaj dilimi eklendi. Canlı veritabanına 121 uygulanmadı; kurulu istemci/API 1.3.9-dev, şema 120 ve masaüstü kısayolu korundu. İşlev, SQL, UI ve aktarım denemeleri kullanıcı isteğiyle atlandı. Derleme ve paket bütünlüğü kontrolleri kullanıcı senaryosu doğrulaması değildir.

## Açma ve yetkiler

Yönetici **Projeler / ekipler / tarihli atamalar**, proje kullanıcısı **Projelerim** ekranından projeyi açıp **Puantaj ve amir onayı…** düğmesini kullanır. Haftalık görünüm, yayınlanmış vardiya atamaları, eksik puantaj sayısı ve onaylı süre özetleri bulunur. Pencere boyutuna göre listeler büyür, düğmeler satıra geçer. Seçilen hafta değiştirilip yüklenmedikçe eski listedeki işlem düğmeleri devre dışıdır.

- **CanEnterTimesheets:** manuel / CSV taslak kaydetme, düzenleme, onaya gönderme ve taslak/iade kaydını iptal etme.
- **CanApproveTimesheets:** gönderilmiş kaydı onaylama/iade, onaylı kaydı gerekçeli yeniden açma.
- İki hak birbirinden, takvim planlama, izin onayı, personel düzenleme, ödeme ve ücret/IBAN okuma haklarından ayrıdır. Eski proje üyelerine otomatik verilmez. Yönetici etkin tam erişimini sürdürür.
- Yönetici dahil hazırlayan, son düzenleyen veya gönderen **hesap kimliği** kendi kaydını onaylayamaz. İki ayrı yetkili hesap gerekir; karşılaştırma kullanıcı adıyla yapılmaz. Yeniden oluşturulan hesap ayrı kimliktir.
- Proje okuma mevcut üyelik kapsamını kullanır. TC, IBAN ve parasal tutar sorgulanmaz. Proje dışı çakışmalarda kayıt ayrıntısı gösterilmez. CSV şablonu dosyası için mevcut proje dışa aktarma yetkisi ayrıca gerekir. Tablo/pano kopyası mevcut canlı dışa aktarma kontrollerinden geçer.

Bu, hesaplar arasında görev ayrımıdır; çalışan hesabı–PersonId eşlemesi veya farklı hesapların aynı gerçek kişiye ait olduğunu tespit etme özelliği değildir.

## Manuel çalışma kaydı

1. Önce tarihli proje/ekip ataması, yayınlanmış vardiya ve etkin kişi ataması bulunmalı. Yayınlanmamış plan veya atamasız kişi için puantaj oluşturulmaz.
2. Haftayı yükleyip **Yayınlanmış vardiya atamaları** sekmesinde kişiyi seçin; **Seçili atamaya puantaj…** kullanın. Bir vardiya kişi atamasının tek puantajı vardır. Personel, çalışma dönemi, proje ve atama kimliği sunucuda kaynaktan alınır; serbest TC eşlemesi yapılmaz.
3. Gerçek giriş ve çıkışı İstanbul saatiyle tam dakika girin. Bilinmeyen saatin işaretini kaldırın. Plan saatleri başlangıç değerleri olarak görünse de işaretli değildir; gerçekleşen kayıt sayılmaz. Gece çalışmasında çıkışın ertesi gününü açıkça seçin; giriş/çıkış türü sırayla tahmin edilmez.
4. Gerçek molayı girin. Brüt süre UTC çıkış–giriş farkı, net süre bu farktan mola düşülerek hesaplanır. Politika `UTCElapsedMinutes.v1`, saat dilimi ve kaynak iş günü saklanır. Belirsiz/geçersiz saat dilimi anları reddedilir. Başlangıç dahil, bitiş hariçtir.
5. Ücretlendirilecek dakikayı açıkça belirtin; sıfır geçerlidir, net süreden fazla olamaz. Sistem mola, fazla mesai veya ücret katsayısı uydurmaz.
6. Gerekçeyle taslak kaydedin. **Puantaj / istisnalar** sekmesinden sürümlü düzenleyip onaya gönderin. Onay öncesi güncel dönem, arşiv, proje/ekip, tam aralığı kapsayan asıl atama, yayın durumu ve çakışmalar yeniden denetlenir.
7. Başka yetkili hesap **Onayla…** veya **İade et / yeniden aç…** kullanır. Onaylı kayıt yerinde düzenlenmez; yeniden açma onu onaylı özetten çıkarır. Düzeltilen kayıt yeniden gönderilip yeniden onaylanır. Eski değerler ve kararlar değişmez audit geçmişinde kalır; yönetici işlem geçmişinden okuyabilir.

İş günü vardiyanın başlangıç günüdür; gece kaydı iki güne bölünüp toplamı iki kez sayılmaz. Net dakika ile ücretlendirilecek dakika ayrı gösterilir. SQL özetine yalnız **Approved** satırlar girer; taslak/gönderilmiş/iade/iptal ücretlendirilebilir onaylı süreye katılmaz. Bu sürüm bakiye, hakediş, ödeme veya proje maliyet tutarı üretmez.

## İstisnalar ve geçişler

`Draft → Submitted → Approved / Returned`; `Approved → Returned`; `Returned → Submitted`; `Draft / Returned → Cancelled`. İptal terminaldir: kaynak kimlikleri/atama tekilliği korunur; aynı atamaya yeni satır açılmaz. Hatalı taslağı iptal etmek yerine düzeltmek, onaylı kaydı düzeltmek için yeniden açmak gerekir.

Eksik giriş/çıkış, ters zaman, 24 saatten uzun brüt süre, molanın net süreyi tüketmesi, eksik/fazla ücret dakikası, artık geçerli olmayan vardiya/dönem/atama, izin ve gönderilmiş/onaylı başka çalışma çakışmaları listede istisna olarak gösterilir. **Yalnız istisnalar** filtresi bunları ayırır. Eksik veya hatalı saatler taslakta tutulabilir; gönderim/onay engellenir. Puantajı olmayan yayınlanmış atamalar ayrı sekmede ve sayaçta görünür.

Aynı personelin bütün projelerde gönderilmiş/onaylı zaman aralıkları ortak kişi transaction kilidiyle korunur; uç uca aralıklar çakışmaz. Taslak çakışmaların ikisinin birden gönderilmesine izin verilmez; ilk başarılı gönderimden sonra diğeri istisna olur. Mola yalnız bir kaydın netinden düşülür. Bir vardiya içindeki birden fazla proje/parça dağıtımı bu dilimde yoktur; mevcut tarihli asıl proje ataması modeli sürer.

Eski doğrudan Genel izin kaydetme ve yeni izin onaylama, gönderilmiş/onaylı puantajla çakışamaz. Puantajı bulunan vardiya/roster iptali önce ilgili puantajın gerekçeli yeniden açılıp iptal edilmesini gerektirir. Çalışma dönemi tarihleri ve eski/yeni dönem sınırları puantajı da dikkate alır. Proje tarihleri gerçekleşen saatleri dışarıda bırakamaz; bekleyen puantajı olan proje/ekip pasife alınamaz.

## CSV aktarımı

**CSV şablonunu kaydet…** yalnız puantajı olmayan yüklenen hafta atamalarını, kaynak/atama kimliği ve boş gerçekleşen alanlarla çıkarır. Personel adı/TC/IBAN/ücret içermez. Planı gerçekleşen sayan hazır saatler yazılmaz.

Dosya UTF-8, ayırıcı noktalı virgül, başlık sırası aşağıdaki gibi olmalı. En çok 1 MB / 200 satır; atamalar yüklenen proje/haftanın yayınlanmış atamaları olmalı. Saat biçimi `yyyy-MM-dd HH:mm`; eksik giriş/çıkış/ücret dakikası boş olabilir, mola tam sayıdır.

```text
SourceKey;ShiftAssignmentId;Start;End;BreakMinutes;PayableMinutes
```

**CSV içe al / önizle…** dosyayı okuyup kişi/vardiya ve girilen değerleri gösterir. Kullanıcı önizlemeyi kontrol edip Kaydet ile gerekçe verir. Kaynak satır kimliği proje içinde sabit ve benzersiz olmalı; aynı dosyada kimlik veya atama tekrarı reddedilir. Sunucu bütün satırları tek transaction içinde kaydeder; biri başarısızsa yeni kayıtların tamamı geri alınır. Aktarım yalnız taslak üretir, kendiliğinden göndermez/onaylamaz.

Kararlı satır kimliğinden türeyen puantaj kimliği, kaynak tekilliği ve kalıcı komut özeti çoğalmayı önler. Aynı içerik yeniden aktarılınca mevcut kayıt değiştirilmez; farklı içerik gelirse otomatik üzerine yazılmaz, listeden sürümlü düzenleme istenir. İptal geçmişi de tekilliği korur. Yanıt kaybında dialog aynı dondurulmuş istek kimliği/içeriği/gerekçeyi yeniden gönderir; başarılı commit tekrar yazılmaz. Doğrulama hatasından sonra içeriği değiştirmek gerekiyorsa dialog kapatılıp yeniden açılır.

## Veri ve dağıtım

121 yalnız `Timesheets`, değişmez `TimesheetCommands`, iki kapalı proje biti, koruma/audit trigger'ları ve indeksleri ekler; audit izin listesi puantaj kimlikleri/süreleri için genişletilir, kaynak serbest metni maskelenir. Eski 100–120 betikleri değiştirilmez. Eski planlardan veya para/izin kayıtlarından gerçekleşen çalışma uydurulmaz. Migration hesap oturumlarını geçersizleştirir; kullanıcı adı/parola korunur.

Kaynak/paket 1.3.10-dev, API 1.10 ve şema 121 birlikte kurulmalı; sunucu kendiliğinden migration uygulamaz. Kurulum öncesi mevcut veritabanının doğrulanmış yedeği gerekir. Bu kaynak tesliminde yedek alma, canlı yükseltme/kurulum veya GitHub yayını yapılmadı.

## Açık kalan işler

Aşama 4'ün tamamı bitmiş sayılmaz: vardiyasız çalışma, ham cihaz olayları ve olay eşleme, çok proje/gün içi zaman parçaları ve mola dağıtımı, çalışan–hesap eşlemesi, güvenli kalıcı taslak kurtarma ve klavye ile çok satırlı manuel giriş sonraki dilimlerdir. Haftalık okuma 2000 satırla sınırlıdır; daha büyük takımlar için sunucu sayfalama/ekip filtreleri gerekir. CSV kaynak sistem entegrasyonu, otomatik mevzuat/izin hakkı, ücret tarifesi, onaylı çalışmadan hakediş ve bordro bu adımda yoktur.
