# Dönem hakedişi: mali onay ve tek seferlik aktarım

2 Kasım 2026 · Kaynak/paket **1.3.19-dev** · Hazırlanan şema **129** · API **1.19**.

Kullanıcı 1.3.18-dev sürümünü doğruladı. [128 hesap temeli](TARIHLI_TARIFE_VE_DONEM_HAKEDISI.md) üzerine **Hazırlanmış → Mali onaylı → Deftere aktarılmış** akışı eklendi. Hesabın mühürlenmesi ile mali onay artık ayrı kavramlardır. Canlı 1.3.18-dev / API 1.18 / şema 128, veritabanı ve kısayol bu kaynak tesliminde değiştirilmedi; denemeler atlandı.

## Yetki ve görev ayrımı

Hesap hazırlama, mali onay ve aktarım bu dilimde **yönetici** işlemleridir. İki ayrı hesabın aynı kişiye ait olması bağımsız onay sayılmaz: kalıcı hesap–personel eşleme geçmişiyle aynı kişiyi temsil eden hazırlayan/onaylayan hesaplar reddedilir. Hesaptaki çalışanlardan biriyle eşlenmiş hesap da kendi alacağına mali onay veremez. Eşlemeler kaldırılmış olsa da kimlik kanıtı görev ayrımında kullanılır; eşlenmemiş iki hesabın gerçekte aynı kişiye ait olduğu kendiliğinden bilinemez.

Aktarımı, geçerli bağımsız mali onaydan sonra hazırlayan veya onaylayan yönetici de yapabilir; üçüncü bir kullanıcı zorunluluğu yoktur. Aktarım anında bağımsızlık yeniden kontrol edilir. Sonradan aynı kişiye ait olduğu anlaşılan hesaplarla verilmiş fakat henüz aktarılmamış onay otomatik olarak uygulanmaz. Kayıtlı onayı geri alma/değiştirme bu sürümde yoktur.

Görüntüleme ücret görüntüleme + proje maliyet okuma gerektirir. Proje kullanıcısına yalnız kendi proje tutarı, belge/defter bağlantısı ve aktarım bilgisi döner; **global önceki/sonraki personel bakiyesi döndürülmez**. Aktarım önizlemesi ve bakiye ayrıntısı yöneticiye açıktır. Mevcut pano/dışa aktarma kontrolleri sürer.

## Kullanım

1. Projeden **Tarihli tarife / dönem hesabı…** ekranını açın. Önceki gibi tarifeleri ve onaylı puantajı kullanarak dönem hesabını hazırlayın. Hazırlama oran/dakika/kaynak snapshot'ını mühürler; henüz bakiye veya defter oluşturmaz.
2. **Değişmez hesap geçmişi** sekmesinde hesabı seçin. **Seçili hesabın satırları** ile kaynakları ve tutarları inceleyin. Farklı, bağımsız yönetici **Seçili hesaba mali onay…** düğmesi ve gerekçeyle onay verir. Mali onayda da bakiye/defter değişmez.
3. Onaylı satırda **Aktarım önizle / deftere işle…** açılır. Sistem kişi ve çalışma dönemi bazında toplamı, güncel bakiye ve aktarım sonrası bakiyeyi sunucudan yeniden hesaplar. Arşiv, kapalı/değişmiş çalışma dönemi, tarih, pasif proje veya bakiye sınırı engelleri burada gösterilir. Engel varsa Kaydet kullanılamaz.
4. Önizleme kontrol edilip **Kaydet + gerekçe** kullanıldığında bütün kişilerin işlemleri **tek SQL transaction** içinde tamamlanır. Bir kişinin hedefi veya tutarı geçersizleşirse bütün paket geri alınır.
5. Hesap geçmişinde mali onaylayan, aktaran ve UTC zamanları görünür. **Seçili aktarım satırları** pozitif tutarlar için belge numarası ve defter numarası bağlantılarını gösterir. Proje maliyeti ve mevcut personel/finans geçmişi yeni belgeleri doğal olarak içerir.

## Aktarımın muhasebe uygulaması içindeki etkisi

Her kişi/EmploymentId toplamı için:

- **Pozitif tutar:** Bir `Accrual` finans belgesi, aynı tutarda bir `Allocation` defter satırı ve personel bakiyesine bir artış oluşturulur. Belge ile defter, kaynak CalculationId ve EmploymentId'ye bağlanır. Belgenin tarih aralığı o kişinin kaynaklarının ilk/son iş günüdür; ortak hesap döneminin ilişki kimliği ayrıca korunur.
- **Sıfır tutar:** Açık bir aktarım satırı kalır, fakat sıfır tutarlı finans belgesi/defter hareketi veya bakiye güncellemesi yapılmaz. Birden fazla sıfır hedef desteklenir.

Bu bir **hakediş alacağı kaydıdır**. Nakit veya banka çıkışı, ödeme emri ya da banka gerçekleşmesi oluşturmaz. Mevcut avans/ödemeler bakiyeyi zaten azaltmışsa hakediş artışı bu bakiyeye eklenir; avans proje maliyetine yeniden yazılmaz. Günlük nakit kapanışına ödeme gibi eklenmez.

Geçmiş/kapalı çalışma döneminin alacağı yeni işe giriş dönemine veya yeni Genel/Joker kaydına taşınmaz. Aktif kartın PersonId, grubu ve CurrentEmploymentId'si kaynak dönemle aynı olmalıdır. Kapanmış döneme ayrı borç/alacak defteri ve devir desteği bu dilimde yoktur; bu durumlar paketin tamamını engeller. Eski TC/ad seçimiyle yeni kişiye kredi yazılmaz.

## Tekrar, belirsiz yanıt ve eşzamanlılık

Mali onay tek CalculationId için benzersizdir. Açık onay ekranı aynı ApprovalId, içerik özeti ve aktörle tekrarlandığında mevcut başarılı sonucu bulur. Hazırlanmış hesap sürümü seçimden sonra değişmişse yeni onay verilmez.

Aktarımın kendi PostingId'si ve tam istek özeti vardır. Önizleme özeti güncel personel/bakiye/çalışma dönemi sürümlerini ve mali onayı kapsar; değişiklik olmuşsa yeni önizleme istenir. İşlem içinde kaynak, hesap, onay ve hedefler yeniden doğrulanır; bakiye güncellemesi özgün kart sürümünü de koşul olarak kullanır.

Başarı yanıtı kaybolursa **aynı açık pencerede aynı isteği yeniden gönderin**. Kalıcı aktarım makbuzu bulunursa kişi sonradan değişmiş olsa bile ikinci bakiye artışı yapılmaz. Uygulama kapanmışsa hesap/aktarım ve finans geçmişini kontrol edin: yeni PostingId ile aynı hesabı ikinci kez aktarmak reddedilir. Bu finans akışına yeni bir çevrimdışı gönderim kuyruğu eklenmedi.

Onay, aktarım, kişi/çalışma dönemi, hesap kaynaklı finans belgesi ve defter bağlantıları SQL tekillikleriyle korunur. Bakiye, belge, defter, Employment bağlantıları, aktarım makbuzu ve audit birlikte commit edilir. Audit hatası veya iş kuralı reddinde kısmi aktarım kalmaz. SQL işlem kilitleri ve özgün sürüm kontrolleri, iki yöneticinin aynı hesabı paralel aktarmasını ikinci kez yazmaya dönüştürmez.

## İptal ve kapsam sınırı

Aktarım belgesi genel finans ekranından **tek başına iptal edilemez**; servis ve SQL koruması vardır. Aktarım defteri, satırları ve mali onay değiştirilemez/silinemez. Böylece belge iptal edilip makbuz dururken yeniden aktarım veya kısmi bakiye geri alma yolu oluşmaz. Bağlantılı mali düzeltme/ters hareket, onay geri alma, dönem iptali ve ödeme/avans belge tahsisi sonraki işlerdir.

Eski manuel hakediş belgeleri otomatik olarak bu hesaplara bağlanmaz veya mahsup edilmez. Kaynak kimliği taşımayan manuel belgeyle aynı gerçek çalışmanın daha önce kaydedilmiş olup olmadığını sistem çıkaramaz; yeni dönem aktarımından önce bu kayıtlar ayrıca incelenmelidir. Bordro/vergi/SGK/fazla mesai kuralı veya otomatik banka aktarımı yoktur.

## Şema ve teslim sınırı

`129-accrual-approval-posting.sql` üç yeni onay/aktarım tablosu, mevcut finans/defter tablolarına nullable kaynak bağlantıları, tekillik, değişmezlik ve audit korumaları ekler. Eski kayıtların yeni bağlantıları boş kalır. 100–128 değiştirilmedi. Eski 128 hesapları otomatik onaylanmaz veya aktarılmaz; **Hazırlanmış** olarak bağımsız mali onay bekler. Geçiş eski oturum nesillerini yeniler.

Kaynak/paket **1.3.19-dev / API 1.19 / şema 129** birlikte kullanılmalıdır. Bu teslimde canlı yükseltme/kurulum, GitHub push veya Release yapılmadı. SQL, işlev, bakiye/defter, ağ kesintisi ve eşzamanlılık denemeleri kullanıcı isteğiyle atlandı. Derleme ve paket bütünlüğü kontrolü çalışma zamanı davranışının doğrulanması değildir.
