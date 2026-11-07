# Proje rapor merkezi

6 Kasım 2026 · Kaynak/paket: **1.3.30-dev / hazırlanmış şema 137 / API 1.30**. [Yerel kurulum](1_3_30_DEV_KURULUMU.md) doğrulanmış yedek sonrası 1.3.30-dev / 137 / API 1.30 olarak tamamlandı. 137 yalnız kişisel rapor görünümleri tablosunu ekler; mali kayıt veya bakiye üretmez. Başka kurulumlarda şema kapsam kimlikli yedek sonrası ayrıca uygulanmalıdır.

## Kullanım

Proje maliyetleri veya Projelerim ekranında bir proje seçip **Rapor merkezi** düğmesini açın. Ekran pencere genişliğine göre satıra geçen araç çubukları, büyüyen sonuç listesi ve SQL sayfalaması kullanır. Sayfa boyutu 25/50/100/200 olabilir. Filtre değişince eski sonuç ve dışa aktarma seçimi temizlenir; raporu yeniden oluşturun.

- **Personel / ekip atamaları:** asıl ve ek atama, çalışma dönemi, iptal durumu; ad güncel personel kartındandır, tarihsel ad fotoğrafı olduğu iddia edilmez.
- **Ücret / gider kural sürümleri:** tarihli sürüm, tür, birim tutar, katsayı, bölen ve karar. Yalnız bekleyen filtresi kararı verilmemiş sürümleri seçer. Proje kuralı kişi veya ekibe ait olmadığından bu iki filtreyle sorgulanamaz.
- **Ücret / gider talepleri:** donmuş kaynak adı, çalışma dönemi, kural sürümü, hizmet dönemi, belge, birimler, tutar ve güncel iş akışı durumu.
- **Ücret / gider defter aktarımları:** bağlantılı mali belge/defter kimliği, imzalı aktarım veya ters aktarım tutarı, aktör ve gerekçe. Kişinin proje dışı önceki/sonraki genel bakiyesi, IBAN veya banka ekstresi verilmez. Proje gideri bir banka ödemesi değildir.
- **Bekleyen ücret / gider işleri:** hazırlanmış, onaylı fakat aktarılmamış, ters onay veya ters aktarım bekleyen talepler. Liste iş akışı görünümüdür; kişisel görev ataması veya onaylama hakkı vermez. Kural onayı kendi raporundadır. Genel puantaj/izin/banka görev motoru eklenmedi; mevcut ekranlardaki istisnalar sürer.

Kişi-saat, bütçe/maliyet, ödeme/tahsis mutabakatı ve değişmez mali dönem ekranları aynı merkezden açılır. Bu bağlantılar mevcut yetki/sayfalama/dışa aktarma kurallarını korur. Merkezdeki filtreler bu ayrı ekranlara otomatik taşınmaz; her ekran kendi kapsamını belirtir. Değişmez dönem raporu canlı veriden yeniden oluşturulmaz.

## Filtrelerin anlamı

Tarih aralığı İstanbul gün sınırlarıyla ilgili **kayıt zamanını** süzer: atama/kural/talep oluşturulması veya aktarım zamanı. Hizmet dönemiyle aynı değildir. Kişi araması kalıcı kişi kimliğini ya da raporda gösterilen kaynak adı metnini kullanır; SQL joker karakterleri kaçırılır.

Ekip seçimi, seçili projenin iptal edilmemiş atamalarına dayanır. Atamalarda doğrudan ekip kimliği; talepler ve aktarımlarda **aynı çalışma döneminin talep hizmet aralığıyla kesişen** ekip ataması aranır. Sonradan değişen/iptal edilen atama, gelecekte yeniden oluşturulan rapordaki üyeliği etkileyebilir: eski talep anındaki ekip fotoğrafı saklanmıyor. Silinip yeniden açılmış personel/çalışma dönemi eşleştirilmez. `EXISTS` kullanımı, çoklu/ek atamaların bir mali satırı çoğaltmasını önler. Aynı talep birden fazla ekip filtresine uyabilir; farklı ekip çıktıları toplanarak proje toplamı bulunamaz. Bu bir ekip maliyet tahsis raporu değildir. Kişisiz proje gideri, kişi/ekip seçildiğinde kapsam dışıdır.

Kapsam ve UTC veri zamanı sonuç üzerinde ve ZIP manifestinde yer alır. Sonuç listesi ile daha sonra alınan ZIP iki ayrı güncel okumadır; arada iş akışı değişirse sonuç değişebilir. ZIP'in kendi parçaları tek serializable transaction içindeki aynı okumadır.

## Kişisel görünümler

Görünüm adı yazıp **Yeni görünüm kaydet** ile mevcut proje/rapor/tarih/kişi/ekip/bekleyen seçimini saklayın. Açılır listeden seçip **Görünümü uygula** ile yeniden sorgulayın. Görünümü silme yalnız bu hesap kimliğine ve bu projeye ait değişmez görünüm kimliğine uygulanır; eski seçim başka görünümü silemez. Görünüm düzenleme yerine yeni kimlikle yeni kayıt oluşturulur. Aynı kimlik ve aynı içerikle tekrar kaydetme idempotenttir; farklı içerik reddedilir. Proje başına hesap başına 50 görünüm sınırı vardır. Göreli tarih değil, seçilmiş sabit günler saklanır.

Görünümler veritabanında tutulduğundan aynı adlı farklı veritabanları karışmaz; sahiplik sunucunun doğruladığı kalıcı hesap kimliğidir. Yeniden oluşturulan aynı kullanıcı adına devredilmez. Kayıt/silme gerekçeli audit ile aynı transaction içindedir. Görünüm yetki kazandırmaz; çalıştırma ve indirme her seferinde canlı oturum ve proje yetkisini doğrular. Ücret yetkisi kaldırılmış görünüm çalıştırılamaz. Görünümler kişi araması içerebilir, veritabanı yedeklerinin veri koruma kapsamındadır.

## Büyük dışa aktarma

**Tüm sonucu parçalı CSV / ZIP kaydet** seçili sayfayı değil filtreye uyan bütün merkez raporunu sunucuda üretir. Ayrıca güncel proje dışa aktarma yetkisi gerekir; finans raporları ücret ve proje maliyeti hakkını da ister. İstemciden satır tablosu veya SQL kabul edilmez. Yanıt tamamlanana kadar hesap/proje yetki kilitleri korunur; eski rapor önizlemesi erişim bileti sayılmaz.

Arşiv, UTF-8 BOM ve noktalı virgüllü, başlığı tekrarlanan en fazla 10.000 satırlık CSV parçaları ile `manifest.json` içerir. Manifest proje/filtre/veri zamanı/oluşturan/satır sayısı ve her parçanın SHA-256 değerini taşır; dijital imza veya kesin dönem belgesi değildir. Makine sütun adları ve kalıcı kaynak kimlikleri izlenebilirlik için CSV'de korunur. UTC alanlar ISO tarih-saat olarak aktarılır; formül benzeri metinler Excel formülüne dönüşmeyecek şekilde kaçırılır. Dört ondalıklı birimler korunur; talep tutarı ile imzalı aktarım tutarı ayrı raporlardır, birlikte toplanmaz.

Sınırlar: 200.000 veri satırı, yaklaşık 128 MiB toplam CSV metni, parça başına yaklaşık 16 MiB ve ZIP için 32 MiB. Aşımda eksik dosya yayımlanmaz; filtre daraltılır. Mevcut HTTP istek sınırı 128 KiB ve SQL komut/istemci zaman aşımı politikası korunur; çok büyük/yavaş kapsam zaman aşımına uğrayabilir. Üretim isteğe bağlı ve sınırlı bellektedir; kalıcı arka plan iş kuyruğu veya sunucuda kişisel rapor dosyası bırakılmaz. Pencerenin kapanması/oturum iptali isteği iptal eder. İstemci dosyayı önce rastgele geçici ada yazar, tam yanıt sonrası hedefe atomik geçirir; hata/iptalde geçici dosyayı temizler. Kayıt/indirme denemelerini kullanıcı yeniden başlatır; otomatik mali işlem tekrarı yoktur.

## Teslim ve açık doğrulama

İstemci/API derlemesi, paket ve statik bütünlük kontrolleri yapılır. Kullanıcının isteğiyle işlevsel, SQL uygulama, UI/DPI, yük, erişilebilirlik ve eşzamanlılık denemeleri yapılmadı. Özellikle büyük veri süresi/bellek, yetki iptali sırasında aktarım ve ekip kapsamları pilotta doğrulanmalıdır. İlk kaynak tesliminde canlı kurulum yapılmamıştı. Ardından 137 ve istemci/API/kısayol yerel kurulumu tamamlandı; işlev denemeleri yine atlandı. GitHub'a gönderilmedi.

Kalan **4 ana iş paketi**: yedek/destek güvenliği; kurulum/dağıtım sonlandırması; performans/erişilebilirlik doğrulaması; gerçek kullanım pilotu/teslim. Yeni kapsam bu sayıya ayrıca eklenir.
