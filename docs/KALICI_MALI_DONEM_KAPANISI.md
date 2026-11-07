# Kalıcı mali dönem kapanışı

6 Kasım 2026 · Kaynak/paket **1.3.28-dev** · Uygulanmış şema **135** · API **1.28**.

Bu ekran seçili projenin mali kayıt olaylarını belirli bir dönem için değişmez olarak kesinleştirir. Günlük nakit kapanışından ayrıdır: hakediş/diğer gider, ödeme/iptal, avans, tahsis/serbest bırakma ve banka ödeme bağlantısı/kaldırma olaylarını birlikte saklar. Kapanış yeni hakediş, ödeme, tahsis veya bakiye hareketi oluşturmaz.

## Dönemin anlamı

**Dönem İstanbul mali kayıt günlerine göredir; çalışma/hizmet dönemi değildir.** Başlangıç ve bitiş günleri dahildir. UTC karşılıkları, Europe/Istanbul kimliği, kesinleştirme zamanı, proje başlığı, aktör ve gerekçe kayıtta dondurulur. Yalnız tamamlanmış günler kapatılabilir; bugün/gelecek gün kesinleştirilemez. Tarih aralığı 1900–2199'dur.

Örneğin ekim çalışmasına ait bir hakediş kasımda onaylanıp deftere aktarılırsa ekim kapanışı değişmez. Kasım kapanışında bu hakediş yeni mali kayıt olarak yer alır ve önceki hizmet dönemini kapsayan kapanışa bağlanır. Bu kayıt ikinci bir hakediş oluşturmaz; var olan aktarımı rapora yalnız bir kez alır.

Her projenin tek bir kapanış zinciri vardır. İlk başlangıç kullanıcı tarafından seçilir. Sonraki başlangıç son kesin kapanışın **ertesi günü** olmak zorundadır; boşluk/çakışma ve ikinci kök zincir engellenir. Sıfır olaylı dönem de kapanabilir. Kesin dönem düzenlenemez, silinemez veya yeniden açılamaz.

## Ekranı kullanma

1. Merkez yöneticisi Hakediş/avans/proje maliyeti ekranında projeyi seçip **Kalıcı mali dönem kapanışı…** açar. Yetkili proje kullanıcısı proje portalındaki **Kesin mali dönem kayıtları…** ile geçmişi okuyabilir.
2. İlk başlangıç ve tamamlanmış bitiş günü seçilir. Sonraki başlangıç sunucudaki son kapanıştan gelir ve değiştirilemez.
3. **Dönemi önizle / kesinleştir…** ile sunucu açılış, dönem ve kümülatif kapanış toplamlarını üretir. Önizleme ilk 200 kaynak olayını gösterir; toplam satır sayısı ve eski kapanış bağlantı sayısı ayrıca görünür.
4. Açılış/devri, yeni olayları, geç yansımaları ve toplamları kontrol edip açık kesinleştirme onayını işaretleyin; gerekçe ile kaydedin. En fazla **10.000 yeni olay / 16 MiB UTF-16 snapshot** tek kapanışta saklanır. Sınır aşılırsa daha erken bitiş veya daha kısa dönem kullanılır; ilk dönem açılış olaylarını da içerir.
5. Geçmişten kesin kaydı açın. Özet ve kaynak olayları canlı veriden yeniden hesaplanmaz; saklanan kayıt okunur. Kaynak listesi SQL tarafında sayfalıdır. Bağlantılı bir satırdan ilgili eski kapanış/satırın sayfası açılabilir.
6. Ayrı dışa aktarma yetkisiyle seçili **kesin özet** veya **tüm kesin kaynak olayları** CSV olarak kaydedilir. Sayfanın tamamı değil, seçili bölümün bütün sabit sonucu kullanılır. Dönem, aktör, kapanış kimliği ve gerçek saklanan SHA-256 özeti CSV'ye eklenir; aktarım audit edilir. Sonradan tarih/kişi filtresi uygulanmaz. CSV 32 MiB ile sınırlıdır.

Kesinleştirme yalnız merkez yöneticisindedir. Okumada canlı oturum, ücret görüntüleme, proje maliyeti ve proje ödeme görünümü birlikte aranır. Proje kullanıcısında mevcut maliyet ve ödeme hazırlama/onaylama haklarından biri gerekir; maliyet hakkı tek başına ödeme ayrıntılarını açmaz. CSV için ayrıca proje dışa aktarma hakkı gerekir. İlgili eski kapanış da aynı proje yetkileriyle okunur.

## Açılış, dönem ve kümülatif toplamlar

- **İlk açılış:** ilk başlangıçtan önce kaydedilmiş proje olayları ayrı Opening satırları olarak saklanır. Geçmiş atılmaz ve başlangıç sıfır varsayılmaz.
- **Sonraki devir:** önceki kapanışın doğrulanmış kümülatif toplamı aynen alınır.
- **Bu dönem:** önceki kapanışlarda kullanılmamış ve bitiş sınırından önce kaydedilmiş olaylar. Normal dönem olayları ile önceki dönemden geç görünen olaylar burada ayrılır.
- **Kümülatif kapanış:** açılış/devir + bu dönem. Her kaynak olay proje/olay türü/kaynak kimliği tekilliğiyle yalnız bir kapanışta kullanılır.

| Tutar | Kaynak ve anlam |
|---|---|
| İşçilik/hakediş | İlk hakediş belgesi + belge iptali farkı + bağımsız onaydan geçmiş mali düzeltme aktarım farkları |
| Diğer gider | Expense belgesi ve bağlantılı iptal farkı |
| Ödeme çıkışı / iptal | Özgün Payment ve kaynak ödeme kimliğine bağlı Reversal defter olayları |
| Avans çıkışı / iptal | Aynı ödeme olaylarının avans alt kümesi; toplam ödemeye tekrar eklenmez |
| Net tahsis | Tahsis + serbest bırakmanın negatif etkisi; para çıkışı değildir |
| Net banka bağlantısı | Ödeme bağlantısı + kaldırmanın negatif etkisi; banka hesabı bakiyesi değildir |
| Toplam maliyet | İşçilik + diğer gider; ödeme/tahsis/banka bağlantısı tekrar maliyete yazılmaz |
| Net ödeme | Ödeme çıkışı − ödeme iptal tutarı; fiilî banka iadesi kanıtı anlamına gelmez |
| Projeye bağlı hakediş − net ödeme | Bu projeye bağlı mali konum; personelin bütün bakiyesi değildir |

Projesiz eski Opening/Allocation/Payment kayıtları, başka projeler, taslak/onay bekleyen çalışma veya mali belgeler, proje bütçesi ve eşleştirilmemiş ham banka hareketleri bu kapsama girmez. Taslakların ileride mali kayda dönüşmesi sonraki dönemde görünür. Kapanış tüm banka ekstresini veya IBAN'ları saklamaz; ham banka verisi kendi yetkili ekranında kalır.

İptal satırında proje, gerektiğinde asıl ödeme üzerinden bulunur; eski iptal satırının ProjectId alanının boş olması iptali kaybettirmez. Kaynak ödemeye bağlanamayan eski iptal kanıtı veya işaret/tutar tutarsızlığı varsa kapanış reddedilir. Kümülatif maliyet/iptal, ödeme/avans, tahsis ve banka bağlantısı sınırları kontrol edilir; dönem içi negatif düzeltme geçerlidir.

## Kapalı dönemden sonraki düzeltme

Örneğin ekimde 1.000 TL hakediş ve 600 TL ödeme kapanmıştır. Kasımda mevcut bağımsız onaylı mali düzeltme akışından hakediş 900 TL'ye indirilirse:

- Ekim kesin kaydı 1.000 TL hakediş ve 600 TL ödemeyi korur.
- Kasım devri bu eski toplamlarla başlar; kasımın yeni mali düzeltme satırı **−100 TL** olur.
- Düzeltme satırı ekimin kaynak belge satırına/kapanışına bağlanır. Kasım kümülatif hakedişi 900 TL olur; yeni bir ödeme yapılmaz.

Ödeme iptali, tahsis kaldırma ve banka bağlantısı kaldırma da aynı ilkeyi izler. İlk işlem eski dönemdeyse sonraki olay eski kapanış/satır kimliğine bağlanır. Eski hizmet dönemine ait geç hakediş, kaynak satırı henüz bulunmadığından ilgili eski dönem/açılış kapanışına satırsız bağlantı taşır. Geç görünen kayıtlar Carry olarak ayrılır; eski kesin belge değiştirilmez.

Kaynak düzeltme onayı ve para/bakiye işlemleri mevcut iş akışlarında yapılır. Kapanış bu onayları aşan bir düzeltme girişi sunmaz. Kaynak kayıt zamanı kapalı döneme geri alınamaz; hizmet tarihinin geçmiş olması yeni mali kaydın kayıt zamanını geriye çekmez. Kapalı ödeme gövdesi korunur; sonraki kaynak bağlantılı iptal için mevcut banka/tahsis ve kimlik kontrolleri devam eder.

## Tutarlılık ve saklama

Önizleme özeti tarih aralığı, UTC sınırları, proje sürümü, önceki kapanış kimliği/hash'i ve tüm yeni kaynak olaylarını kapsar. Arada kaynak veya zincir değişirse eski önizleme ile kesinleştirme reddedilir. Aynı ClosingId/aynı aktör/içerikle tekrar gönderim yeni kapanış üretmez; ağ sonucu belirsizse açık penceredeki aynı isteği yeniden gönderin. Pencere kapandıysa önce kesin kapanış geçmişini kontrol edin.

Başlık, kaynak satırları, kaynak sahiplenme ve kesinleştirme tek Serializable transaction içindedir. Satırlar JSON snapshot ile iki yönde uzlaşmadan IsSealed geçişi yapılamaz. Başlık ve satır SHA-256 özetleri saklanır; okumada başlık, para toplamları, önceki kapanış devir/hash bağlantısı ve okunan sayfanın satırları doğrulanır. Gelecekteki proje adı, kaynak durum veya saat dilimi kuralı değişikliği geçmiş görüntüsünü yeniden hesaplatmaz.

Şema **135** iki tablo (`FiscalPeriodClosings`, `FiscalPeriodLines`), mali kaynak olay görünümü, zincir/olay tekillikleri, kapalı kayıt zamanı ve ödeme gövdesi korumaları ile kesinleştirme audit'ini ekler; hesap oturum nesillerini yeniler. 100–134 betikleri değiştirilmez. Veri tabanı yöneticisinin korumaları devre dışı bırakmasına karşı dış ortamda imzalı/değiştirilemez arşiv bu dilimde bulunmaz.

6 Kasım 2026 tarihinde doğrulanmış yedek sonrası düzeltilmiş 135 uygulandı; **1.3.28-dev / şema 135 / API 1.28** yerel istemci/API kurulumu tamamlandı. İlk taslaktaki ayrılmış SQL `LineNo` adı `[LineNo]` olarak düzeltildi; ilk başarısız işlem geri alınmıştı. Mevcut kayıtlar ve bakiyeler korundu, eski oturumlar yenilendi, kısayol güncellendi. İşlevsel, SQL senaryo, arayüz ve eşzamanlılık denemeleri kullanıcı isteğiyle atlandı; yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. [Kurulum kaydı](1_3_28_DEV_KURULUMU.md).

Bu adımdan sonra **6 ana iş paketi** kalır. Sıradaki **ek ücret/gider kuralları**: fazla mesai, gündelik/aylık ücret ve açık politika/onay sürümleri. [Güncel kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md).
