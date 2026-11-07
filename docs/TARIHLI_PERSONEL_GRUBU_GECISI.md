# Tarihli personel grubu geçişi

31 Ekim 2026 · Kaynak/paket 1.3.6-dev · Hazırlanan şema 117 · API 1.6.

Kurulum güncellemesi: [117 / 1.3.6-dev](1_3_6_DEV_KURULUMU.md) kullanıcı onayıyla kuruldu; yedek doğrulandı, denemeler atlandı. Aşağıdaki kaynak teslimi notları ilk teslimin tarihsel durumudur.

Kullanıcı 1.3.5-dev sürümünün çalıştığını doğruladı. Yol haritasının tarihli personel türü/grubu dilimi, mevcut Genel/Joker modeli üzerinde eklendi. Denemeler kullanıcı isteğiyle atlandı. 117 uygulanmadı; çalışan 1.3.5-dev/116 ve kısayol değiştirilmedi.

## Kullanım

Personel listesinden **Seçili personel kartını aç…** veya **Personel kartı ara…** ile etkin kartı açın. **Tarihli grup geçişi…** düğmesi karşı grubu gösterir. Yeni grubun ilk gününü seçip gerekçe girin. Personel düzenleme yetkisi gerekir; proje kullanıcısına erişim verilmez.

Örneğin Genel → Joker için 15 Ekim seçilirse Genel dönemi 14 Ekim sonunda kapanır, Joker dönemi 15 Ekim başlar. PersonId ve TC değişmez. Eski kart arşivde kalır, yeni grup etkin olur. Bu teknik grup/dönem değişimidir; işten ayrılış veya yeniden işe giriş bildirimi oluşturmaz.

İşlem tamamlanınca eski kart kapanır, geldiğiniz liste yenilenir. Yeni gruptan veya kart aramasından güncel kartı açabilirsiniz. Arşiv dahil aramalarda aynı kişi için eski ve yeni grup kartı görünmesi beklenir; bunlar ayrı kişiler değildir. Etkin personel ve bakiye özetlerinde yalnız bir kart sayılır.

Karttaki **Grup geçişleri** sekmesi kaynak/hedef grubu, geçiş tarihi, kaydeden, kayıt zamanı ve gerekçeyi gösterir. Çalışma dönemleri seçimi iki grubu da kapsar. Atama, hakediş ve ödeme kayıtlarında grup alanı bulunur. İzin geçmişi eski Genel dönemlerinden de okunabilir; Joker ekranında güncel izin düzenleme açılmaz. Grup geçişleri, arşiv olayları ve audit tüm dönemlerdir; diğer geçmişler seçilen EmploymentId ile filtrelenir. Üstteki bakiye hâlâ açılan kartın bakiyesidir; dönem filtresi geçmiş bakiye hesabına dönüşmez. Eski grup kartı sıfır ve arşivde kalır.

## Kabul edilen tarihler ve ön koşullar

- Tarih saat/saat dilimi içermeyen bir takvim günüdür. Eski bilinen başlangıçtan sonra olmalıdır. Bilinmeyen legacy başlangıcı tahmin edilmez.
- Bugün veya geçmiş tarih kabul edilir. Gün sınırı API'nin SQL UTC saati ve Europe/Istanbul iş saat diliminden hesaplanır. Gelecek tarihli planlama veya otomatik çalışacak iş bu dilimde yoktur; işlem kaydedildiğinde grup hemen değişir.
- Kaynak grup bakiyesi sıfır olmalıdır. Hedef gruptan önceki kart varsa arşivde ve sıfır bakiyeli olmalıdır. Otomatik bakiye aktarımı veya yetkisiz ödeme yapılmaz.
- Eski dönemdeki izin, iptal edilmemiş atama ve iptal edilmemiş hakediş geçiş gününe uzanmamalıdır. Gerekirse kendi yetkili ekranlarında gerekçeli düzeltme/iptal yapılıp uygun aralıkla yeniden kaydedilir; geçiş bunları sessizce kısaltmaz veya taşımaz.
- Kaynak dönemde geçiş günü veya sonrasında parasal defter hareketi ya da parasal hareketin iptal zamanı varsa geçmişe dönük geçiş reddedilir. Ödemeyi bugün sıfırlamak bugün geçişe izin vermez; daha sonraki uygun bir gün gerekir. Sıfır tutarlı Closure sınırı parasal hareket sayılmaz.
- Kaynakta önceden tanımlı ayrılış tarihi varsa geçişten önceki günle eşleşmelidir. Önceki dönemlerin bilinen tarihleriyle çakışma reddedilir.

## Veri ve güvenlik modeli

117, PersonnelGroupTransitions tablosu ekler. Her geçiş kaynak ve hedef EmploymentId, PersonId, iki grup, tarih, özgün kart/dönem sürümleri, TransitionId, kaydeden ve gerekçeyi tutar. Kaynak/hedef dönem bağlantıları foreign key ve unique kısıtlarla korunur. Geçiş geçmişi değiştirilemez/silinemez; eski dönemler ve kayıt bağlantıları yeniden yazılmaz. Dönem sayısı artar, geçmiş dönemler başka gruba taşınmaz.

Aynı kişi için tek açık Employment dönemi unique filtreli indeksle korunur. Kimlik trigger'ları, aynı PersonId/TC ile iki grupta yalnız eski kart arşivliyken bulunmaya izin verir; farklı kişiye ait TC veya iki etkin grup reddedilir. Eski grubun arşivini geri almak yeni dönemle paralel etkin kart oluşturamaz. Geçişle kapanmış dönem dondurulur; yeni dönemin başlangıcı geçiş tarihinden ayrı değiştirilemez. Daha yeni bir karşı-grup dönemi varken eski dönem düzenleme/yeniden açma engellenir.

Defter, hakediş ve atama EmploymentRecordLinks trigger'ları artık PersonId yanında PersonnelGroup ile de eşleşir. Böylece iki grup kartı varken yeni kayıt iki döneme bağlanmaz veya çoğalmaz. İzinlerin Calisan kimlik bağı koparılmaz; eski Genel kartı fiziksel silinmez. Geçişten önce uygulanmış 100–116 betikleri değiştirilmez, mevcut kişiye sahte geçiş kaydı üretilmez.

Servis canlı düzenleme yetkisi kontrolünden sonra assignment işlemleriyle ortak kişi kilidini alır. Kartları Genel → Joker sırasıyla ve dönemleri transaction içinde kilitler. Kaynak PersonId + kart rowversion + EmploymentId + dönem rowversion doğrulanır. İzin/atama/finans aralıkları tekrar sunucuda kontrol edilir. Eski seçimle başka döneme geçiş yapılamaz. Kaynak kapanışı, eski kartın arşivlenmesi, sıfır tutarlı defter Closure'ı, yeni dönem, hedef kart ve geçiş/audit kaydı tek transaction içindedir.

Önceden hedef grup kartı varsa mevcut kilit altında yeniden kullanılır; güncel kaynak ad/soyad/IBAN'ı hedef karta kopyalanır, eski iş kayıtları yerinde kalır. Geçiş tutar almaz ve para hareketi üretmez; Closure sıfırdır. Ayrı ödeme/export izinleri ve mevcut ödeme RequestId tekrar kurtarması değiştirilmez. Eski ödeme seçimi kaynak arşivi/Closure ve rowversion ile geçersizleşir; daha önce kaydedilmiş ödeme sonucu mevcut kurtarma yoluyla alınır.

Dialog ilk gönderimde tarih, gerekçe, özgün sürümler ve TransitionId'yi dondurur. Belirsiz yanıtta aynı açık dialogdan aynı istek tekrar gönderilir. Sunucu aynı TransitionId'nin tamamlanmış eşdeğer sonucunu eski sürüm kontrolünden önce bulur; farklı içerikle aynı kimlik reddedilir. Bu dialog niyeti bellektedir, disk üzerinde ödeme günlüğü değildir. Uygulama kapandıysa güncel kart ve Grup geçişleri sekmesinden sonucu inceleyin; eski kaynak sürümüyle yeni bir istek ikinci geçiş üretemez.

Geçiş olayı mevcut maskeli audit sınırından geçer. Kimlik/kişisel alanlar ayrıca ham audit gövdesine eklenmez. Geçiş listesi genel okuma, audit yönetici, tablo dışa aktarma mevcut kişisel veri export yetkisiyle çalışır. Geçişi geri almak/silmek için düğme yoktur; uygun yeni bir tarihli ters yönlü geçiş ayrı kayıt olarak yapılır. Yetki, sıfır bakiye ve aralık kuralları yeniden uygulanır.

## Kurulum ve doğrulanmamış alanlar

1.3.6-dev istemci/API birlikte ve yedek sonrası 117 yükseltmesiyle kullanılmalıdır. API otomatik migration uygulamaz. 117 oturum nesillerini yeniler. Canlı yükseltme/kurulum bu kaynak tesliminde yapılmadı.

Yalnız derleme/yayın ve paket bütünlüğü kontrolleri yapılır. SQL migration, grup geçişi, dönüş geçişi, belirsiz yanıt, eşzamanlılık, ödeme ve UI denemeleri yapılmadı. Tarihsel grup raporlarının tüm eski ekranlarda yeniden tasarımı, geleceğe planlanmış geçiş, bakiyeli geçiş, ücret tarifesi veya vardiya/puantaj bu dilimin kapsamı değildir.
