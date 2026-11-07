# Ek ücret ve gider kuralları

6 Kasım 2026 · Kaynak/paket **1.3.29-dev** · Uygulanmış şema **136** · API **1.29**.

Bu modül proje bazında tarihli ücret/gider kuralları, değişmez hesap talepleri, bağımsız onay ve tek seferlik defter aktarımı ekler. Kural oluşturmak, talep hazırlamak ve onaylamak bakiye değiştirmez. Mali etki ayrıca **Deftere aktar** ile oluşur. Banka transferi, otomatik ödeme veya banka iadesi yapılmaz.

## Kural türleri ve açık hesap

| Tür | Tutar | Kaynak/koruma |
|---|---|---|
| Ek fazla mesai farkı | Aktarılmış saatlik oran × beyan edilen dakika × **ek** katsayı / 60 | Aynı projedeki aktarılmış, mali düzeltme görmemiş saatlik hesap satırı; dakika kaynak ücret dakikasını aşamaz |
| Günlük temel ücret | Bir günlük sabit TL × 1 | Tek iş günü; aynı çalışma döneminin saatlik/günlük/aylık temel kapsamıyla çakışamaz |
| Aylık temel ücret | Aylık TL × beyan hak birimi / açık bölen | Aynı takvim ayı; çalışma dönemi başına ayda tek aktif aylık talep; temel kapsam çakışması engellenir |
| Ek ödeme / prim | Sabit birim TL × beyan miktarı | Güncel çalışma dönemi, tarihli proje ataması ve ayrı kaynak belge anahtarı |
| Doğrudan proje gideri | Sabit birim TL × beyan miktarı | Personel seçilmez; gider maliyeti oluşur, personele borç/bakiye yazılmaz |

Tüm tutarlar TRY'dir. Tutar bir kez iki ondalığa, yarım değer sıfırdan uzağa yuvarlanır. Birim TL 0–1.000.000 aralığında pozitif olmalı; ek fazla mesai kuralında bu alan sıfırdır ve saatlik oran kaynaktan alınır. Beyan miktarı en fazla 1.000.000 ve dört ondalıklıdır; fazla mesai dakikası tam sayıdır. Ek katsayı 0'dan büyük, en fazla 5 ve dört ondalıklıdır; varsayılan yasal oran yoktur.

**Örnek:** Saatlik temel zaten 200 TL/saat üzerinden aktarılmışsa, 120 dakika için ek katsayı 0,5 girilmesi **200 TL ek** hakediş üretir. Tam 1,5 kat ücret yeniden yazılmaz. Katsayı, hangi sürenin ek ücrete hak kazandığı ve miktar kullanıcı/politika kararlarıdır; puantajdan otomatik fazla mesai eşiği, tatil veya yasal hak çıkarılmaz.

Aylık bölen 1–31 arasında açık seçilir. Örneğin 30.000 TL, bölen 30 ve beyan birimi 15 ise tutar 15.000 TL olur. Tam takvim ayında birim böleni aşamaz; kısmi aralıkta ayrıca takvim gün sayısını aşamaz. Tam şubat için bölen 30 ve birim 30 seçilmişse otomatik 28/30 kesinti yapılmaz. Aylık talep çalışma dönemi ve proje ataması içinde olmalıdır; işe giriş/çıkış tarihi dışına taşamaz. İzin, eksik gün veya hafta tatili birimleri otomatik belirlenmez.

Günlük tür tam gün/bir birim içindir; kısmi günün temel ücreti saatlik akışla hazırlanır. Günlük/aylık temel bir maliyet projesine kaydedilir. Aynı çalışma dönemindeki tarih çakışması **diğer projelerde de** kontrol edilir; iki projeye tam temel ücret yazılması engellenir. Çok projeye günlük/aylık ücret paylaştırma bu dilimde yoktur.

Gideri değişken belge tutarıyla kaydetmek için açıkça 1 TL birimli bir gider kuralı tanımlanıp belge tutarı miktar olarak beyan edilebilir; bu bir otomatik fatura okuma değildir. Personel masraf iadesi/harcırah borcu ayrı tür olarak eklenmemiştir; doğrudan proje gideri personele ödeme yapmaz.

## Erişim ve tarihli sürümler

Merkez yöneticisi Hakediş/avans/proje maliyeti ekranında projeyi seçip **Ek ücret / gider kuralları…** açar. Proje portalında maliyet ve ücret görüntüleme yetkisi olan kullanıcı geçmişi okuyabilir; hazırlama/karar/aktarım yalnız merkez yöneticisindedir. Proje kullanıcısına personelin proje dışındaki önceki/sonraki toplam bakiyesi verilmez. IBAN bu modülde okunmaz.

1. Yeni kural için sabit kod, ad, tür, geçerlilik tarihleri ve açık hesap değerleri girilir. Kod A–Z/0–9/_/- ile 1–30 karakterdir.
2. Kaydet yalnız taslak sürümü oluşturur. Başka bağımsız yetkili onay veya ret verir; hazırlayan taslağı para etkisi olmadan iptal edebilir. Aynı kişiyle eşlenmiş farklı hesap adı bağımsız onay sayılmaz.
3. Yeni sürüm, kararı verilmiş son sürümü izler. Kod ve tür korunur; gövde/önceki karar değiştirilmez. Yanlış tür için yeni kod gerekir.
4. Onaylı daha yüksek sürüm yalnız kendi geçerlilik aralığında öncekinin yerine geçer. Talep aralığı yeni sürümle kısmen kesişiyorsa aralık bölünmelidir. Diğer tarihlerde kapsayan eski onaylı sürüm kullanılabilir.
5. Yeni onaylı sürüm eski sürüme göre hazırlanmış/henüz aktarılmamış talepleri geçersiz kılabilir. Bu talepler iptal edilip güncel önizleme ile hazırlanır. Önceden aktarılmış tutarlar yeni kuralla yeniden hesaplanmaz.

## Talep, onay ve aktarım

Onaylı kuralı seçip **Onaylı kuraldan talep…** açın. Personelli türlerde kalıcı çalışma dönemini, tarih aralığını, hak birimini ve sabit **kaynak belge anahtarını** seçin. Ek fazla mesai için kişi/tarih aralığının aktarılmış saatlik kaynaklarını ayrıca yükleyip bir satırı açıkça seçin; kaynak otomatik seçilmez.

Günlük/aylık/ek ödemede aralığı kapsayan iptal edilmemiş sürekli proje ataması gerekir. Ek fazla mesainin kanıtı kendi aktarılmış saatlik kaynağıdır. Hedef veya tarih uymazsa önizleme reddedilir; eski seçim yeni çalışma dönemine uygulanmaz.

Sunucu önizlemesi kural sürümü, çalışma dönemi sürümü, atama/saatlik kaynak kanıtı, birim/rate/katsayı/bölen ve tutarı kapsar. Alan değiştirilince önizleme geçersizleşir. Hesabı ve miktarı açık onay kutusuyla kontrol ettikten sonra gerekçeli kaydedin. Hesap snapshot'ı/hash'i ve ilk kişi adı korunur. Sonraki ad/IBAN değişiklikleri ücret hesabını yeniden yazmaz.

Talebi hazırlayan veya hak sahibiyle aynı kişi mali onay veremez. Onay/ret bağımsız yetkili tarafından yapılır. Onaylı fakat aktarılmamış talep de gerekçeyle iptal edilebilir. Ret/iptal eski kayıtları silmez; ayrılan belge/temel ücret/fazla mesai kaynağı serbest kalır.

**Deftere aktar** güncel personel, aynı çalışma dönemi, bakiye ve mali durumu tekrar önizler. Bakiye veya kart sürümü değişmişse yeni önizleme gerekir. Yalnız PARA alanı, kalıcı kimlik/çalışma dönemi ve rowversion koşuluyla güncellenir; başka kullanıcının ad/IBAN düzenlemesi üzerine yazılmaz. Arşivlenmiş veya başka çalışma dönemine geçmiş karta otomatik aktarım yapılmaz.

Personelli aktarım tek transaction içinde pozitif hakediş belgesi, personel bakiye artışı, Allocation defteri, çalışma dönemi bağları ve aktarım makbuzu oluşturur. Doğrudan gider yalnız Expense belgesi/makbuz oluşturur. Talep başına tek Post aktarımı olabilir. Kaynak belge anahtarı proje içinde tek aktif talebe aittir; kural kodunu/sürümünü değiştirmek aynı belgeyi yeniden kullandırmaz.

Talep/karar/aktarım kimlikleri aynı aktör ve içerikle tekrar gönderildiğinde yeni mali hareket üretmez. Ağ sonucu belirsizse açık pencerede **Aynı isteği yeniden gönder** kullanın. Pencere kapandıysa önce talep ve makbuz geçmişine bakın; yeni belge anahtarıyla ikinci talep açmayın.

## Saatlik temel ve fazla mesai kaynak koruması

- Ayrılmış veya aktarılmış günlük/aylık temel aralığında saatlik hesap hazırlanamaz. Saatlik önizleme bunu engel olarak gösterir; SQL de aynı kuralı uygular.
- Önceden saatlik hesaplanmış günlere günlük/aylık temel hazırlanamaz. Bu denetim çalışma dönemi üzerinden proje sınırları arasında yapılır; başka projenin adı/tutarı hata mesajına taşınmaz.
- Ek fazla mesai kaynağının normal saat ücreti zaten deftere aktarılmış olmalıdır. Kaynak ücret dakikasının yalnız beyan edilen kısmına ek katsayı uygulanır. Baz satır başına tek aktif ek fazla mesai talebi bulunur.
- Aktif ek fazla mesai talebi varken aynı temel hakedişin mali düzeltme aktarımı engellenir. Önce ek talep iptal/ters aktarım ile sonuçlandırılır. Temel mali düzeltme görmüşse eski saatlik oran otomatik yeni fazla mesai hesabına çevrilmez; açık ve bağımsız onaylı ek ödeme akışı değerlendirilir.
- Birden fazla fazla mesai kademesi aynı baz satır üzerine otomatik dağıtılmaz. Günlük/aylık temelden fazla mesai oranı otomatik türetilmez.

Bu koruma bu modülün temel talepleri ile saatlik hesap akışı içindir. Eski genel manuel finans belgesi yetkili ayrı akış olarak kalır; serbest belgeden otomatik ücret türü çıkarılmaz.

## Yanlış aktarımı tam ters hareketle düzeltme

Aktarılmış kayıt doğrudan Finans belgesi iptal düğmesiyle kapatılamaz. Talepte sırasıyla **Tam ters aktarım iste**, başka bağımsız yetkiliyle **Ters aktarımı bağımsız onayla**, ardından **Onaylı tam ters aktarımı yap** kullanılır. Hazırlayan/isteyen ve hak sahibi kendisi onaylayamaz. İstek ve onay para etkisi oluşturmaz.

Ters aktarım özgün talebin tam tutarıdır; keyfî negatif tutar veya kısmi ters hareket kabul edilmez. Önce aktif hakediş tahsisleri serbest bırakılmalıdır. Güncel aynı çalışma dönemi/bakiye ve özgün mali belge sürümü yeniden kontrol edilir. Mali belge iptal işareti, negatif Allocation/bakiye farkı, ters aktarım makbuzu ve rezervasyon kaldırma aynı transaction içindedir. Giderde personel bakiyesi/defteri kullanılmaz.

Ters aktarılmış talebin belgesi/kaynağı yeni düzeltilmiş talebe açılır; eski talep, onay ve iki makbuz saklanır. Ödeme önceden yapılmışsa bakiye negatif olabilir; gerçek banka iadesi yapılmaz. Önceki çalışma dönemi kapanmışsa yeni kartın bakiyesi değiştirilmez; otomatik eski dönem borç aktarımı yoktur. Bekleyen ters aktarım isteği/onayı özgün sabit hedefe aittir, ayrıca değiştirme/yeniden açma akışı bu dilimde yoktur; uygulanmadığı sürece mali etkisi olmaz.

## Raporlar, şema ve teslim

Yeni mali belgeler mevcut proje maliyeti/bütçe, hakediş-ödeme-tahsis mutabakatı ve mali dönem kapanışına normal kaynak olayları olarak girer. Personelli türler işçilik/hakediş, doğrudan gider diğer giderdir. Defter Allocation ile finans belgesi iki kez maliyete eklenmez. Kapalı dönem snapshot'ı değişmez; daha sonraki ücret/gider aktarımı veya tam iptali kayıt gününde sonraki döneme bağlanır. Ek banka ödemesi yine mevcut ödeme/tekrar kontrolü üzerinden ayrıca yapılır.

Kural ve talep listeleri SQL tarafında sayfalıdır. Talep ayrıntısında ilk hesap, kararlar ve aktarım makbuzları okunur. Bu dilimde ayrı ücret/gider CSV ekranı yoktur; mevcut mali belge/proje ve kesin dönem raporları mali etkileri kapsar. İleri kayıtlı filtre/rapor merkezi işi sonraki pakettedir.

136 beş tablo (`WagePolicies`, `WagePolicyDecisions`, `WageClaims`, `WageClaimActions`, `WageClaimTransfers`), finans/defter kaynak bağları, tekillik/formül/bağımsız onay/temel kapsam korumaları ve audit kayıtlarını ekler. Hesap oturum nesilleri yenilenir. Uygulanmış 100–135 betikleri değiştirilmez. Audit büyük snapshot ve IBAN kopyalamadan kimlik, tutar, aktör, zaman, gerekçe ve kaynak hash'lerini saklar.

Bu bir otomatik bordro veya mevzuat/SGK/vergi motoru değildir. Otomatik ücretli izin, tatil, haftalık fazla mesai tespiti, çok kademeli fazla mesai, günlük/aylık ücretin çok projeye dağıtımı, personel masraf iadesi, belge dosyası/OCR ve döviz bu temel dilimde bulunmaz. Yeni kapsam istenirse kalan iş listesine ayrıca eklenir.

6 Kasım 2026 tarihinde doğrulanmış yedek sonrası 136 uygulandı; **1.3.29-dev / şema 136 / API 1.29** yerel istemci/API kurulumu tamamlandı. Mevcut kayıtlar, bakiyeler ve kesin dönemler korundu; eski oturumlar yenilendi. Kısayol güncellendi. İşlevsel, SQL senaryo, arayüz, eşzamanlılık ve finansal hesap denemeleri kullanıcı isteğiyle atlandı; kural/talep/aktarım oluşturulmadı. Yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. [Kurulum kaydı](1_3_29_DEV_KURULUMU.md).

Bu temel kapsamdan sonra **5 ana iş paketi** kalır. Sıradaki **rapor merkezi genişlemesi**. [Güncel kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md).
