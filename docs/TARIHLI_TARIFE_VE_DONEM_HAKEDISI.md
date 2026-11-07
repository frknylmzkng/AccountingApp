# Tarihli saatlik tarife ve dönem hakedişi temeli

2 Kasım 2026 · Kaynak/paket **1.3.18-dev** · Hazırlanan şema **128** · API **1.18**.

Kullanıcı 1.3.17-dev sürümünü doğruladı. Bu dilim tarihli ücret tarifesini ve onaylı çalışmadan değişmez dönem hesap dökümünü ekler. **Bakiye artırmaz, FinanceEntries/maliyet defterine işlemez, ödeme veya bordro üretmez.** Ücret hesabını mevcut manuel hakediş/avans/ödeme kayıtlarıyla otomatik birleştirmez. Bu, sonraki mali onay/deftere aktarma işinin hesap temelidir.

Canlı veritabanı, kurulu 1.3.17-dev istemci/API, şema 127 ve masaüstü kısayolu değiştirilmedi. 128 yalnız kaynakta hazırlandı; denemeler kullanıcı isteğiyle atlandı.

## Giriş ve yetkiler

**Hakediş, avans ve proje maliyeti → proje seç → Tarihli tarife / dönem hesabı…** veya **Projelerim → Maliyetler → Tarihli tarife / dönem hesabı…** kullanılır.

Tarife, önizleme, hesap geçmişi ve satır okuması güncel **ücret görüntüleme + proje maliyet okuma** yetkisi ister. Tarife oluşturma/kaldırma ve değişmez dönem hesabını kaydetme bu temel sürümde **yalnız yönetici** içindir. Yeni yetki otomatik verilmez. Başka projenin tarifesi veya kaynak ayrıntıları aynı isteğe alınmaz. Pano/dışa aktarma için mevcut korumalar sürer.

## Tarihli saatlik TL tarifesi

1. **Saatlik TL tarifeleri → Yeni saatlik tarife…** açılır. Seçilen projeyle tarihli atama ilişkisi olan personelin **çalışma dönemi** seçilir. Eski/ayrılmış dönemler tarihsel ücret hesabı için listelenebilir; yeniden işe giriş yeni EmploymentId ile ayrı tarifedir. Ad/TC üzerinden otomatik eşleme yapılmaz.
2. Başlangıç ve isteğe bağlı bitiş günü **dahildir**. Bilinen işe giriş/ayrılış tarihleri dışına çıkılamaz. Başlangıç tarihi bilinmeyen eski dönemde tarih uydurulmaz; kullanıcı tarifenin gerçek geçerlilik tarihini açıkça belirtir.
3. Pozitif, en fazla 1.000.000 TL/saat ve iki ondalıklı tutar açıkça girilir. Tek desteklenen birim **saatlik TRY**'dır. Günlük/aylık ücret, fazla mesai katsayısı, prim, kesinti veya mevzuat hesabı kendiliğinden dönüştürülmez.
4. Aynı proje/çalışma döneminde etkin tarih aralıkları çakışamaz. Örneğin ilk tarife 15'inde bitiyorsa sonraki 16'sında başlayabilir. Aynı gün iki etkin oran seçilemez.

Tarifenin oranı/tarihleri yerinde düzenlenmez. Yanlış tarife özgün satır sürümü ve gerekçeyle kaldırılır; gerekiyorsa yeni kimlikle ve doğru tarihlerle yeniden oluşturulur. Kaldırılmış tarife geçmişte kalır; önceki dönem hesaplarının oran/tutarı değiştirilmez. Bir tarife gelecekteki ücret değişimini temsil edecekse tarih aralıkları baştan çakışmayacak şekilde açıkça hazırlanmalıdır; bu dilimde eski tarifeyi sessizce kısaltma yoktur.

## Dönem önizlemesi ve hesap politikası

Başlangıç/bitiş seçilip **Önizlemeyi hesapla** kullanılır. Dönem en fazla 366 gün, hesap en fazla 2000 kaynak satırıdır.

- Dağıtılmamış puantajda bir kaynak satırı; dağıtılmış puantajda yalnız seçilen projeye ait değişmez parçalar alınır. Ana süre tekrar eklenmez.
- Kaynak ana puantaj **Approved** olmalıdır. Bağımsız parçası onaylanmış fakat diğer projeleri beklediği için ana kaydı Submitted kalan parçalar bu temel sürümde mali hesaba alınmaz. Önce tüm parça onayları tamamlanır.
- Eski toplu onaylı dağılımda mevcut onaylı ana kayıt ve parça sürümü esas alınır; bağımsız karar varmış gibi üretilmez.
- Tarihli oran ana kaydın sabit **WorkDate / iş günü** ile seçilir. Gece çalışmasında tarife gece yarısında otomatik bölünmez. Dönem ve tarife sınırında da bütün kaynağın iş günü esas alınır.
- Tutar = **onaylı ücretlendirilecek dakika × saatlik TL / 60**. Her kaynak satırı iki ondalığa, yarım değer sıfırdan uzağa yuvarlanır; dönem toplamı bu satırların toplamıdır. Gerçekleşen net dakika yerine açıkça onaylanmış ücret dakikası kullanılır.
- Sıfır ücret dakikası da sıfır tutarlı kaynak satırı olarak korunur ve kullanılmış sayılır; sonradan aynı kaynaktan pozitif tutar üretme yolu değildir. Bu satırlar için de açık tarife gerekir.

Eksik/çakışan tarife, daha önce bir hesaba alınmış kaynak, aynı proje/tarihli mevcut hesap veya dönemde ilgili taslak/iade/gönderilmiş puantaj varsa kayıt engellenir. Sonuçlandırılmamış kaynaklar bu ilk kapsamda onay veya uygun iptal ile sonuçlandırılmalıdır; sessizce atlanmaz. Geçersiz puantajı iptal etmek yerine otomatik sıfır ücret uygulanmaz.

## Değişmez hesap kaydı

Önizlemedeki kaynaklar ve tutarlar kontrol edildikten sonra yönetici **Hesabı değişmez kaydet…** ekranını açar. Bu ekranda kaynakların kilitleneceği ve mevcut sürümde geri alma/düzeltme bulunmadığı açıkça belirtilir. **Kaydet + gerekçe** ile:

1. Oturum/yetki ve önizleme yeniden doğrulanır. Kaynak, kişi adı, tarife veya sürüm değişmişse güncel önizleme istenir; sessiz yeni oran kabul edilmez.
2. Hesap başlığı ve satırları tek transaction içinde saklanıp mühürlenir. Personel/çalışma dönemi, kayıt anındaki kişi adı, iş günü, kaynak puantaj/parça kimliği, dakika, tarife/uygulanmış oran, kaynak/karar sürümleri ve tutar saklanır. Sonraki ad veya tarife değişimi bu dökümü değiştirmez.
3. Aynı proje/tarih aralığı ikinci hesap başlığı oluşturamaz. Bir kaynak puantaj veya parça, başka bir dönem aralığı seçilse de ikinci hesap satırına yazılamaz. Kısmen daha önce hesaplanmış aralık otomatik eksik satır eklemesi olarak ele alınmaz; engel gösterilir.
4. Ana puantajın tamamı, etkin dağılımı ve bağlı parça kararları kilitlenir. Bir proje parçası hesaba alınmışsa başka projenin amiri de aynı ana kaydı iade ederek mevcut hesabın dayanağını geçersizleştiremez. Diğer projeler kendi henüz kullanılmamış parçalarını ayrıca hesaplayabilir.

Hesaplar **Değişmez hesap geçmişi** ve **Seçili hesabın satırları** sekmelerinde görülür. Tarih filtresiyle çakışan en yeni 1000 hesap, seçili projede en yeni 1000 tarife gösterilir; tarife dönem seçicisi 2000 çalışma dönemiyle sınırlıdır.

Geç yanıt durumunda açık hesap penceresinde aynı CalculationId ve önizleme özeti tekrar kullanılır. Sonuç makbuzu mevcutsa yeni hesap/satır üretilmez. Uygulama kapanmışsa dönem geçmişi kontrol edilmelidir; yeni kimlikle aynı dönem veya kaynaklar tekrar yazılamaz. Tarife oluşturma da açık pencerenin özgün RateId'siyle tekrar edilir; yeni kimlikle çakışan oran oluşamaz. Bu finans formları için yeni bir çevrimdışı kalıcı gönderim kuyruğu eklenmedi.

## Bu dilimin sınırı

Bu hesap dökümü mali dönem onayı veya ödeme belgesi değildir. Finansal hazırlayan/onaylayan ayrımı, onaylı hesabın mevcut bakiye ve FinanceEntries defterine bir kez aktarılması, geç gelen çalışma için ek dönem/düzeltme, hesap iptali/ters hareket ve ödeme/avans tahsisi sonraki işlerdir. **Şimdilik kaynakları değiştirmek veya mühürlü hesabı geri almak mümkün değildir.** Yanlış hesabı düzeltmek için DB'den satır silme veya kaynak kilidini kaldırma yolu sunulmaz.

Mevcut manuel hakediş belgeleri ayrı kalır; otomatik mahsup veya mükerrerlik eşlemesi yapılmaz. Proje maliyeti toplamları hesap dökümünü henüz içermez. Resmi bordro, vergi, SGK, mevzuat/fazla mesai kuralı veya banka aktarımı yoktur. Saatlik politika işletmenin açıkça girdiği oranlara dayanır.

## Şema ve teslim

`128-dated-rates-accrual-foundation.sql` üç yeni boş tablo, tekillik/tarih/kaynak kısıtları, değişmezlik/kaynak kilidi ve audit tetikleyicilerini ekler; eski oturum nesillerini yeniler. 100–127 değiştirilmedi. Eski bakiyeler, tarihsiz oranlar veya geçmiş ücretler dönüştürülmez; otomatik tarife veya hesap üretilmez.

Kaynak **1.3.18-dev / API 1.18** istemci ve sunucu birlikte, şema 128 ile kullanılmalıdır. Bu teslimde canlı yükseltme/kurulum, GitHub push veya Release yapılmadı. SQL/işlev/UI, para hesabı ve eşzamanlılık denemeleri kullanıcı isteğiyle atlandı. Derleme ve paket bütünlüğü kontrolü çalışma zamanı davranışının doğrulanması değildir.
