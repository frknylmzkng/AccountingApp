# Banka CSV aktarımı ve mevcut ödemeyle eşleştirme

6 Kasım 2026 · Kaynak/paket **1.3.27-dev** · Uygulanmış şema **134** · API **1.27**.

Bu dilim ham banka ekstresi hareketlerini değişmez olarak saklar ve mevcut uygulama ödemelerine açık kullanıcı onayıyla bağlar. **Yeni ödeme, banka transferi, avans, hakediş, tahsis veya bakiye hareketi üretmez.** Bankada gerçekleşmiş ama uygulamada kaydedilmemiş bir ödeme, bu ekrandan ikinci kez para çıkışı gibi oluşturulamaz; eşleşmeden kalır. Mevcut ödeme akışının tekrar kontrolü korunur.

## Erişim ve ekran

Ödemeler veya Hakediş/avans/proje maliyeti ekranındaki **Banka CSV / ödeme eşleştirme…** düğmesini kullanın. Bir hesap ekstresi birden fazla proje ve başka alıcılar içerebildiği için bu sürüm yalnız merkez yöneticisine açıktır. Servis/API her istekte canlı oturum, yönetici, tutar ve IBAN yetkisini doğrular; eşleştirme/kaldırma ayrıca ödeme yetkisini doğrular. Bir projeye verilen ödeme hazırlama/onaylama yetkisi tüm banka ekstresini görmeye yetmez.

Kaynak hesap, adı ve kontrol basamakları geçerli IBAN'ı ile bir kez tanımlanır. Para birimi TRY'dir. Aynı IBAN ikinci hesap olarak açılamaz. Hesap kimliği/IBAN ve aktarılmış ham hareketler düzenlenemez veya silinemez. Hesap tanımı bankaya bağlantı kurmaz.

Ekranda banka hareketleri, seçili hareketin ödeme adayları, eşleştirme/kaldırma geçmişi ve dosya aktarım geçmişi bulunur. Hareket, aday ve geçmiş listeleri bağımsız SQL sayfalaması kullanır. Sayfa değişiminde eski seçimler temizlenir. Banka hareketinin durumu **eşleştirildi**, **eşleştirme bekliyor** veya **giriş; ödeme çıkışıyla eşleşmez** olarak görünür.

## CSV dosyasını hazırlama

- Dosya UTF-8 olmalı; BOM desteklenir. Tek hesap için en fazla **200 hareket / 24.000 UTF-8 bayt**; 40 sütun sınırı vardır. Daha büyük ekstreden kopyalar hazırlayın; sabit banka işlem kimliklerini değiştirmeyin.
- İlk satır dolu, benzersiz başlıklardan oluşur. Her satır aynı sayıda sütun içermeli. Hücre içinde satır sonu veya kontrol karakteri kabul edilmez. Tırnak içindeki ayırıcı ve `""` ile kaçırılmış tırnak desteklenir.
- Ayırıcı noktalı virgül, virgül veya sekme olarak açıkça seçilir; tahmin edilmez.
- Tarih `yyyy-MM-dd` veya `dd.MM.yyyy` olarak seçilir; 1900–2199 aralığındadır. Yalnız gün saklanır; banka günü bir UTC saat gibi yorumlanmaz.
- Ondalık nokta veya virgül açık seçilir. Binlik ayırıcı yoktur; en fazla iki ondalık basamak. **Çıkış negatif, giriş pozitiftir**, sıfır kabul edilmez. Borç/alacak ayrı sütunlu dosyanın kopyasında önce tek işaretli tutar sütunu hazırlanmalıdır; uygulama işareti tahmin etmez.
- Para birimi tam olarak `TRY` olmalıdır. TL metni veya başka döviz bu dilimde dönüştürülmez.

Sütun eşleme:

| Alan | Gereklilik | Anlam |
|---|---|---|
| Kaynak hesap IBAN | Zorunlu | Her satır seçili banka hesabının IBAN'ını içermeli |
| Banka işlem kimliği | Zorunlu | Bankanın bu hesapta benzersiz ve tekrar indirmelerde sabit işlem kimliği; 1–100 karakter |
| Banka günü | Zorunlu | Seçilen biçimde kayıt günü |
| İşaretli tutar | Zorunlu | Eksi çıkış / artı giriş |
| Para birimi | Zorunlu | TRY |
| Karşı taraf IBAN | İsteğe bağlı | Boş olabilir; doluysa IBAN kontrol basamakları doğrulanır |
| Açıklama | İsteğe bağlı | En fazla 500 karakter |
| Uygulama ödeme GUID | İsteğe bağlı | Uygulamadaki ödeme işlem kimliği; doluysa tam GUID olmalı |

Bankanın serbest açıklamasını veya kendi referans numarasını **uygulama ödeme GUID** alanına eşlemeyin. Bunlar banka işlem kimliği/açıklama alanlarında tutulur. Satır numarasını veya rastgele yeni bir değeri sabit banka kimliği yerine kullanmak tekrar tespitini güvenilmez hale getirir.

Varsayılan otomatik başlık seçimi `AccountIban;TransactionId;Date;Amount;Currency;CounterpartyIban;Description;PaymentReference` içindir; diğer başlıklar açılır listelerden elle eşlenir. Zorunlu alanlar aynı sütunu paylaşamaz. Herhangi bir biçim veya eşleme değişikliği önizlemeyi geçersizleştirir.

**Sunucuda önizlemeyi oluştur / tekrarları kontrol et** ile tüm satırları gözden geçirin. Önizleme yazmaz; yeni/önceden var satırlar ve adetler görünür. Gerekçeli Kaydet, aynı transaction içinde aktarım makbuzu ile yalnız yeni ham hareketleri kaydeder.

## Tekrar ve çelişki koruması

1. Her aktarım isteğinin kalıcı BatchId'si ve içerik özeti vardır. Yanıt belirsizse açık pencerede **Aynı isteği yeniden gönder** kullanılır; ikinci aktarım üretmez. Pencere kapandıysa önce aktarım geçmişine bakın.
2. Hesap + ham dosya içeriği + biçim/eşleme özeti aynıysa aynı dosya/eşleme yeniden aktarılmaz. Dosyayı yeniden adlandırmak bunu aşmaz.
3. Dosyalar örtüşse, satırlar sıralansa veya biçim değişse bile hesap + sabit banka işlem kimliği aynı olan ve normalize alanları eşit satır atlanır. Örneğin `10.0` ve `10.00` aynı tutardır.
4. Aynı hesap/işlem kimliği başka gün, tutar, IBAN, açıklama veya ödeme referansıyla gelirse **aktarımı bütünüyle reddeder**; önceki kayıt güncellenmez. Dosya içindeki yinelenen kimlikler de reddedilir.
5. Önizleme ile kayıt arasında yeni veri gelirse özet uyuşmazlığı kaydı durdurur; yeni önizleme gerekir. Hatalı satırda kısmi aktarım yapılmaz.

Kaydedilen kanıt: dosya SHA-256 özeti, biçim/sütun eşlemesi, yeni/atlanan adetleri, aktör/zaman/gerekçe, her yeni hareketin kaynak satırı ve normalize satır özeti. Tam ham CSV ve kullanılmayan sütunlar saklanmaz; özgün dosya kendi kontrollü arşivinizde tutulmalıdır. Özet, dosyanın banka tarafından imzalandığını veya gerçek transfer olduğunu kanıtlamaz.

## Mevcut ödemeyle eşleştirme

Bir negatif banka hareketi seçin. Adaylar yalnız iptal edilmemiş, kalıcı personel kimliği ve işlem GUID'si olan, aynı tam tutarlı mevcut ödemelerdir:

- Banka satırında uygulama ödeme referansı varsa yalnız bu GUID'ye ait ödeme aday olabilir.
- Referans yoksa eşit tutarlı ve İstanbul ödeme kayıt günü banka gününün ±7 günü içindeki kayıtlar önerilir. Adaylık alıcı kimlik kanıtı değildir; birden fazla aday varsa hiçbiri otomatik seçilmez.
- Daha eski bir kayıt için **ödeme defter no** ile açık arama yapılabilir. Bu arama tarih penceresini genişletir; tutar, iptal, referans ve aktif bağlantı kurallarını aşmaz. Alanı değiştirince yeni arama yapılmadan eşleştirme açılmaz.
- Aktif olarak eşleşmiş banka hareketi veya ödeme yeniden aday olamaz. Aynı ödeme başka bir banka hesabına da ikinci kez aktif bağlanamaz.

**Seçili hareket ve ödemeyi kontrol et…** ekranı banka satırını, dekont IBAN/açıklamasını, banka gününü, uygulama ödeme GUID'sini, defter numarasını, tarihsel ödeme TC/kalıcı personel kimliğini, proje kimliğini ve tutarı gösterir. Dekontu/alıcıyı kendiniz kontrol edip açık onay kutusunu işaretleyin; gerekçe ile kaydedin. Referans yokluğu ve 7 günü aşan tarih farkı ayrıca belirtilir.

Ödeme anındaki IBAN önceki ödeme defterinde tutulmadığından **güncel personel IBAN'ı tarihsel kanıt gibi kullanılmaz**. Bu ekran eski TC seçimini yeni personel kartına dönüştürmez; saklanan ödeme kimliğini esas alır. Aktarım, ödeme oluşturma/yeniden gönderme veya personel bakiyesi güncelleme çağırmaz.

Bir banka hareketi ↔ bir ödeme, tam tutarla bağlanır. Ödeme, hareket veya önceki eşleştirme durumu önizleme sonrası değişirse kaydetme reddedilir. Aynı MatchId/aynı aktör/içerikle tekrar gönderim yeni bağlantı üretmez. Benzersiz aktif bağlantı indeksleri ve sunucu transaction/şema korumaları birlikte kullanılır.

## Yanlış bağlantı ve ödeme iptali

Geçmişten aktif bağlantıyı seçip **Seçili bağlantıyı kaldır…** kullanın. MatchId ve rowversion kontrol edilir; kaldırma kendi işlem kimliği, aktör, gerekçe, zaman ve özet ile tek sefer saklanır. Eski seçim yeni bağlantıyı kaldıramaz. İlk bağlantı değerleri değiştirilemez; yalnız ilk gerekçeli kaldırma geçişine izin verilir.

Bağlantıyı kaldırmak ödemeyi iptal etmez ve para iadesi değildir. Ham hareket ve bağlantı geçmişi kalır. Ödeme iptali ayrıca mevcut korumalarla yapılır; aktif banka bağlantısı varken hem servis hem SQL tetikleyicisi iptali engeller. Hakediş tahsis koruması da devam eder.

## Şema, gizlilik ve bu teslimin sınırları

134 dört tabloyu (`BankAccounts`, `BankImportBatches`, `BankTransactions`, `BankPaymentMatches`), bütünlük/aktif tekillik/geçmiş indekslerini, koruma ve audit tetikleyicilerini ekler; oturum nesillerini yeniler. 100–133 değişmedi. Eski ödeme ve bakiyelerden banka hareketi tahmin edilmez.

Ham hareket ve eşleştirme değişiklikleri kimlik, tutar, önce/sonra durum, aktör, zaman, gerekçe ve korelasyonla audit edilir. IBAN ve serbest banka açıklaması audit kopyalarına taşınmaz. Banka verisi yalnız yetkili sunucu veritabanında tutulur; bu dilimde banka CSV dışa aktarımı, ham dosya arşivi veya yeni banka rolü yoktur.

Bankaya bağlanma/transfer, otomatik ödeme oluşturma, kısmi/toplu/birden çoğa eşleştirme, komisyon farkını otomatik kapatma, döviz dönüşümü, bağımsız ikinci banka onaylayanı ve tüm banka formatlarına hazır şablonlar bu dilimde yoktur. Tutar farkları ve tanınamayan işlemler eşleşmeden görünür; hiçbir fark sessizce bakiye düzeltmesine dönüşmez.

6 Kasım 2026 tarihinde doğrulanmış yedek sonrası 134 uygulandı; **1.3.27-dev / şema 134 / API 1.27** istemci/API kurulumu tamamlandı. Mevcut kayıtlar ve bakiyeler korundu, eski oturumlar yenilendi. Kısayol yeni istemciye yönlendirildi. İşlevsel/SQL senaryo/arayüz/eşzamanlılık denemeleri kullanıcı isteğiyle atlandı; yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. Gerçek banka ekstresiyle kontrollü doğrulama ayrı iştir. [Kurulum kaydı](1_3_27_DEV_KURULUMU.md).

Bu kaynak adımından sonra **7 ana iş paketi** açık kalır; [güncel kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md). Denemelerin atlanması, doğrulama/pilot işlerinin tamamlandığı anlamına gelmez.
