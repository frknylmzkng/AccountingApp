# AccountingApp v1.3.33 gerçek kullanım pilotu ve kabul kılavuzu

Bu kılavuz **v1.3.33 Windows x64**, **şema 138** ve **API 1.32** içindir. Pilotun başlangıç durumu **Bekliyor**. Süreç, bir proje ve bir tam iş döneminde uygulama kayıtlarını işletmenin bağımsız iş kayıtlarıyla karşılaştırmayı, ardından güvenlik ve kurtarma senaryolarını ayrı ortamda doğrulamayı kapsar. [Windows paketleri](https://github.com/frknylmzkng/AccountingApp/releases/tag/v1.3.33)

- [Pilot hazırlığı](#1-pilotu-hazırlayın)
- [Gerçek kullanım akışı](#2-gerçek-kullanım-akışını-yürütün)
- [Ayrı ortamda kabul denemeleri](#3-ayrı-ortamda-kabul-denemelerini-yapın)
- [Sonuçları kaydetme ve teslim](#4-kabulü-kaydedin-ve-teslim-edin)

## 1. Pilotu hazırlayın

1. **Kapsamı seçin.** Bir aktif proje, bir ekip ve bir tam puantaj veya ödeme dönemi belirleyin. Dönemi tarih olarak yazın. İş günü karşılaştırmalarında İstanbul saatini kullanın. Örneğin haftalık puantaj ve haftalık hakediş kullanılıyorsa aynı haftayı izleyin. Aylık kapanış pilot kapsamındaysa bir tam ayı izleyin.
2. **Sorumluları belirleyin.** Personel ve puantaj girişini yapacak kişi, proje amiri, mali hazırlayan, bağımsız mali onaylayan, yedek sorumlusu ve son kabulü verecek kişi belli olsun. Dönem hesabı için farklı, bağımsız iki yönetici hesabı gerekir. Proje ödeme hazırlama ve onayında iki ayrı yetkili hesap kullanın. İşletmede bu ayrım sağlanamıyorsa ilgili kabul maddesini **Bekliyor** bırakın.
3. **Sürümü kaydedin.** Masaüstünden **Muhasebe - Güncel** ile giriş yapın. Merkez yönetici hesabında **Yardım → Hakkında ve sürüm bilgisi…** ekranından istemci sürümünü, beklenen şema ve API sözleşmesini, sunucunun sürüm ve şema uyumunu kaydedin. Bu bilgisayardaki merkezi API adresi `https://localhost:7443/` olmalıdır.
4. **Karşılaştırma verisini hazırlayın.** Onaylı vardiya çizelgesi, izin listesi, fiili çalışma kayıtları, ücret tarifeleri, banka ekstresi ve mevcut defterden dönem başlangıcı bakiyelerini alın. Pilot formuna kişi için takma kod, proje kodu ve işlem referansı yazın. Kanıtları sınırlı erişimli bir klasörde tutun. TC, IBAN, parola, erişim anahtarı ve ham banka ekstresini pilot tutanağına koymayın.
5. **Başlangıç yedeğini alın.** Yöneticiyle **Dosya → Ayarlar ve yedekleme… → Güvenli yedek / destek…** ekranını açın. **Şifreli yedek oluştur** ve ardından **Son yedeği doğrula** düğmelerini kullanın. Başarı zamanını ve arşiv adını kaydedin. Kurulumdan önce alınan yedeği de saklayın. Geri yükleme provası Bölüm 3’te ayrıca yapılır.
6. **Canlı işlem sınırını belirleyin.** Gerçek ortamda yalnızca iş gereği yetkilendirilmiş hareketleri kaydedin. Harici çizelge ve banka ekstresini bağımsız karşılaştırma kaynağı olarak tutun. Aynı gerçek ödeme için tek kayıt ve tek işlem referansı kullanın. Kasıtlı hata, tekrar gönderim, hesap pasife alma, dosya bozma ve geri yükleme denemeleri için ayrı ortam kullanın.

**Başlangıç kaydı**

| Alan | Pilot bilgisi |
| --- | --- |
| Sürüm / şema / API / kaynak commit | |
| Proje / ekip / başlangıç ve bitiş tarihi | |
| Operatör / amir / mali hazırlayan / onaylayan | |
| Yedek sorumlusu / kabul sorumlusu | |
| Başlangıç verisi / yedek / kanıt klasörü | |
| Beklenen günlük veri hacmi ve kabul edilebilir bekleme süresi | |

## 2. Gerçek kullanım akışını yürütün

Her adımda **Beklenen**, **Gerçekleşen** ve **Kanıt** alanlarını Bölüm 4’te doldurun. Pilot dönemi henüz tamamlanmadıysa kapanış maddelerini **Bekliyor** bırakın.

### P01 · Personel, ekip ve vardiya planı

1. **İşlemler → Projeler, ekipler ve tarihli atamalar** ekranında projeyi açın. Ekipleri ve pilot çalışanlarının tarihli atamalarını bağımsız çizelgeyle karşılaştırın. Başlangıç ve bitişin çalışılan günü kapsadığını kontrol edin.
2. **Vardiya ve izin takvimi…** ekranından pilot haftasını yükleyin. Vardiya taslağını açın veya gerçek iş planına göre oluşturun. Tarih, başlangıç, süre, mola, ekip ve gereken kişi sayısını kontrol edin.
3. **Kişi ata** ile gerçek vardiya listesini girin. Plan sahibi kontrol ettikten sonra **Yayınla** düğmesini kullanın. Vardiya sayısını, kişi atamalarını ve varsa açık kalan kapasiteyi çizelgeyle karşılaştırın.

**Geçme ölçütü** · Kişiler doğru proje ve çalışma döneminde görünür. Eksik kapasite varsa planda açıkça görünür ve kayda alınır. [Vardiya ve izin takvimi](VARDIYA_VE_IZIN_TAKVIMI.md)

### P02 · İzin ve gerçekleşen çalışma

1. Proje izin taleplerini takvimden, doğrudan kaydedilen Genel Personel izinlerini **İşlemler → İzin planı** veya personel kartından inceleyin. Bir kişide dönem içindeki izin günlerini ve onay durumunu kaynak kayıtla karşılaştırın. Aynı izin için işletmenin belirlediği tek kayıt akışını kullanın.
2. **Puantaj ve amir onayı… → Yayınlanmış vardiya atamaları** ekranında çalışanı seçip **Seçili atamaya puantaj…** ile gerçek giriş, çıkış, mola ve ücret dakikalarını girin. Gece vardiyasında çıkışın ertesi gününü belirtin. Ham olay kullanılıyorsa mevcut giriş/çıkışları önizleyip doğru kişi ve kayıtla eşleyin.
3. Taslağı gerekçeyle kaydedin. **Puantaj / istisnalar** sekmesinden eksik saatleri ve çakışmaları gözden geçirin. Onaya gönderin. Bağımsız proje amiri kendi hesabıyla kaydı açıp **Onayla…** veya **İade et / yeniden aç…** seçeneğiyle gerekçeli karar versin.
4. Aynı gün birden fazla projede gerçek çalışma varsa **Proje / mola dağılımı…** ile zaman parçalarını, molayı ve ücret dakikalarını dağıtın. Her amir kendi proje parçasını kontrol etsin. Ana puantaj toplamını parça toplamlarıyla ve planlanan/gerçekleşen kişi-saat raporuyla karşılaştırın.

**Geçme ölçütü** · Kişi, iş günü ve proje bazındaki onaylı net ve ücret dakikaları kaynakla eşleşir. Parçalar ana kaydı iki kez saydırmaz. Eksik ya da iade edilen kayıtlar istisna olarak görünür. [Puantaj ve amir onayı](PUANTAJ_VE_AMIR_ONAYI.md) · [Ham olay eşlemesi](HAM_OLAYLAR_VE_PUANTAJ_ESLEMESI.md) · [Proje ve mola dağılımı](GUN_ICI_PROJE_VE_MOLA_DAGILIMI.md)

### P03 · Dönem hakedişi ve deftere aktarım

1. **İşlemler → Hakediş, avans ve proje maliyeti** ekranında projeyi seçin. **Tarihli tarife / dönem hesabı…** ekranında ücret oranının çalışma gününde geçerli olduğunu kontrol edin.
2. Aynı dönem için hesap önizlemesini açın. En az bir çalışanın onaylı ücret dakikalarını, geçerli tarifesini ve hesaplanan tutarını bağımsız çizelgeyle karşılaştırın. Örnek hesapta 09.00–17.00 arası 480 dakikadan 60 dakika mola düşülür ve 420 net dakika kalır. Onaylı ücret süresi 420 dakika ve tarife 120 TL/saat ise bu kaynak satırı `420 × 120 / 60 = 840,00 TL` olur. Fark varsa kaynak kayda kadar izini sürün. Hesabı yalnız tutar doğrulandıysa hazırlayın.
3. **Değişmez hesap geçmişi** içinden hazırlanan hesabı açın. Bağımsız mali onaylayan kendi yönetici hesabıyla **Seçili hesaba mali onay…** seçeneğini kullanıp gerekçesini girsin. Hazırlama ve onay sonrasında bakiyenin ve defterin henüz değişmediğini kontrol edin.
4. **Aktarım önizle / deftere işle…** ile önceki ve sonraki bakiyeyi kontrol edin. Gerçek iş akışında onay verilmiş hesabı bir kez aktarın. **Seçili aktarım satırları** üzerinden belge ve defter numaralarını tutanağa yazın.

**Geçme ölçütü** · Kaynak dakika, tarife ve hesap satırı izlenebilir. Onaylayan ile hazırlayan ayrıdır. Aktarılan tutar, beklenen bakiye farkı ve defter hareketiyle eşleşir. [Mali onay ve aktarım](DONEM_HAKEDISI_MALI_ONAY_VE_AKTARIM.md)

### P04 · Proje ödemesi, avans tahsisi ve banka eşleştirmesi

1. **Projeler, ekipler ve tarihli atamalar** ekranında projeyi açıp **Proje ödeme hazırlama / onay…** ekranına geçin. Gerçek ödeme ya da avans için doğru kişiyi, türü, tutarı ve gerekçeyi kontrol edin. Hazırlanan talebin deftere yansımadan **Onay bekliyor** durumunda olduğunu kontrol edin.
2. İkinci yetkili hesap talebi inceleyip gerekçeli karar versin. Onaylanan talebin defter numarasını ve tutarını kaydedin. Bankadaki gerçekleşmeyi ayrı kaynaktan doğrulayın. Sonuç belirsizse yeni talep açmadan aynı talebi ve defter kaydını yeniden sorgulayın. Genel ödeme ekranındaki bekleyen işlem için **Ödeme sonucunu kontrol et** düğmesini kullanın.
3. Tahsis gerekiyorsa proje mali ekranından ilgili ödemeyi ve aynı kişinin, projenin ve çalışma döneminin açık hakedişini seçin. **Seçili ödeme ve hakedişi tahsis et…** önizlemesinde iki açık tutarı karşılaştırın. Gerekçeli kayıttan sonra tahsis geçmişini ve kalan tutarları doğrulayın.
4. Merkez yönetici hesabıyla **Ödemeler → Banka CSV / ödeme eşleştirme…** ekranını açın. Doğru banka hesabını ve gerçek ekstreyi seçin. Sütun eşlemesini, sabit banka işlem kimliğini, `TRY` para birimini ve çıkışların eksi işaretini kontrol edin. Önce sunucu önizlemesini inceleyin. Yetkili aktarımın ardından doğru banka çıkışını doğru ödeme referansıyla eşleştirin. Şüpheli hareketleri gerekçesiyle beklemede bırakın.

**Geçme ölçütü** · Her gerçek ödeme tek defter hareketine bağlanır. Aktif tahsis iki açık tutarın sınırında kalır. Banka eşleşmesinde kaynak hesap, iş günü, tutar ve ödeme referansı doğrulanır. [Proje ödeme onayı](PROJE_ODEME_HAZIRLAMA_VE_ONAY.md) · [Ödeme ve avans tahsisi](ODEME_VE_AVANS_TAHSISI.md) · [Banka CSV biçimi](BANKA_CSV_VE_ODEME_ESLESTIRME.md)

### P05 · Rapor, mutabakat ve kapanış

1. Projenin **Rapor merkezi…** ve **Proje mutabakat raporu…** ekranlarında aynı proje, tarih ve kişi filtrelerini uygulayın. En az iki sonuç sayfası varsa sayfa değiştirin. Sonuç sayısı, toplam ve seçili dışa aktarım bölümünü kontrol edin.
2. Ayrı ayrı hakediş, ödeme, avans, aktif tahsis, banka eşleşmesi ve varsa düzeltme toplamlarını kaynak belge ve işlem referanslarıyla karşılaştırın. Planlanan/gerçekleşen kişi-saat ile varsa bütçe/gerçekleşen maliyet farkını da kontrol edin. Satırları ekranda ve yetkili CSV/Excel çıktısında karşılaştırın. Farkın kaynağını açıklayın veya açık iş olarak kaydedin.
3. Tamamlanmış bir günü **Kesinleşmiş günlük kapanışlar** ekranından kontrol edin. **Ödemeler** ekranındaki güncel önizlemeyi kesin kayıttan ayrı değerlendirin. Kesin kayıtta tarih, beyan, toplam, fark ve kayıt kimliğini tutanağa alın.
4. Kapsamda mali dönem kapanışı varsa projeden **Kalıcı mali dönem kapanışı… → Dönemi önizle / kesinleştir…** seçeneğini kullanın. Açılış, yeni hareketler, geç yansımalar ve kapanış toplamlarını karşılaştırın. Gerçek kesinleştirmeyi yalnızca mali sorumlu onayından sonra yapın. Kesin kaydı yeniden açıp kimliğini, saklanan SHA-256 özetini ve varsa yetkili CSV’yi kaydedin.

**Geçme ölçütü** · Açıklanamayan mali fark **0,00 TL**. İstisnalar kaynak referansıyla kayıtlı. Rapor kapsamı seçili filtre ve izinlerle eşleşir. [Mali mutabakat](PROJE_MALI_MUTABAKAT_RAPORU.md) · [Rapor merkezi](RAPOR_MERKEZI_GENISLEMESI.md) · [Günlük kapanış](SECILEBILIR_RAPOR_VE_KALICI_KAPANIS.md) · [Mali dönem kapanışı](KALICI_MALI_DONEM_KAPANISI.md)

## 3. Ayrı ortamda kabul denemelerini yapın

Ayrı Windows kullanıcı profili veya test makinesi kullanın. Aynı **v1.3.33** paketlerini kurun. **Yeni yerel veritabanı ve ilk yönetici** seçeneğiyle benzersiz bir test veritabanı oluşturun. Merkezi senaryolar için bu test veritabanına ve ayrı HTTPS adresine bağlı bir API hazırlayın. **Hakkında ve sürüm bilgisi…** üzerinden test sürümünü ve bağlantıyı doğrulayın. Üretim kısayolunu ve profili ayrı tutun. Test veritabanına iki örnek proje, iki sahte personel, onaylayan ve hazırlayan hesapları ekleyin. Merkezi bağlantı ve farklı hesap senaryoları için gerçek pilot topolojisine denk bir test ortamı kullanın. [Çok bilgisayarlı dağıtım ve bakım](DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md)

| Kimlik | Yapılacak deneme | Geçme ölçütü |
| --- | --- | --- |
| K01 · Yetki | Proje A hesabıyla giriş yapın ve Proje B’yi, ücret/IBAN alanlarını, dışa aktarımı ve ödeme onayını erişim verilmeden açmayı deneyin. Yöneticiyle test hesabının proje erişimini veya hesabı pasife alıp eski oturuma dönün. | Yalnız tanımlı proje ve alanlar görünür. Yasak işlem çalışmaz. Yetkisi kaldırılan oturum sonraki korumalı işlemi tamamlayamaz. |
| K02 · Ayrı onay ve tekrar | Test projesinde geçerli atama ve hakediş hazırlayın. Hazırlayan hesapla kendi dönem hesabını ve ödeme talebini onaylamayı deneyin. İkinci hesapla geçerli işlemi tamamlayın. Aynı hesabı tekrar aktarmayı ve aynı talebe tekrar karar vermeyi deneyin. | İlk hesap reddedilir. İkinci hesabın geçerli işlemi yalnız bir kez deftere ve bakiyeye yansır. Aynı referansla ikinci hareket oluşmaz. |
| K03 · Belirsiz sonuç ve CSV tekrarı | Test API’sinde **Ödemeler** ekranından bir test ödemesi başlatın. Ödeme sırasında test bağlantısını keserek belirsiz sonuç durumunu oluşturun. Bağlantı dönünce **Ödeme sonucunu kontrol et** ile aynı işlem referansını sorgulayın. Aynı test ekstresini yeniden önizleyin. | Belirsiz durum gözlendiğinde işlem sonucu aynı referanstan bulunur veya arayüz aynı isteği sürdürür. Defterde en fazla bir ödeme ve banka aktarımında her sabit banka kimliği için tek hareket vardır. Belirsiz durum oluşmadıysa o kısmı **Bekliyor** bırakın. |
| K04 · Eşzamanlı değişiklik | Aynı test personelini iki oturumda açın. Birinde adı veya IBAN’ı değiştirip kaydedin. Diğerinde eski seçimle düzenleme ve silme isteği gönderin. İzin tarihlerini de bir oturumda değiştirip diğerinden eski izin seçimiyle kaydetmeyi deneyin. | Eski seçimle kayıt engellenir. İlk değişiklik ve güncel izin tarihleri korunur. Kişi kartında ve işlem geçmişinde doğru aktör ve gerekçe görülebilir. |
| K05 · Taslak ve kilit | Bir test puantajında alanları doldurup taslağın saklandığına dair durum bilgisini bekleyin. Formu kapatıp aynı Windows profili ve hesapla yeniden açarak taslağı yükleyin. Ayrı bir düzenlemede gezinirken uyarıyı kontrol edin. Kaydetmeden 10 dakika uygulamaya dokunmayın ve aynı hesapla kilidi açın. | Taslak yalnız alanları geri getirir. Kaydedilmemiş değişiklik uyarısı çıkar. Kilit açıldığında aynı pencere ve alanlar korunur. |
| K06 · Kesin kayıt ve ters hareket | Örnek verilerle tamamlanmış bir gün veya dönemi kesinleştirin. Sonraki açık dönemde yetkili ters hareket akışını kullanın. Önceki kesin kaydı tekrar açıp yeni dönem kaydı ve canlı önizlemeyle karşılaştırın. | Eski kesin kaydın kimliği, toplamı ve özeti eşleşir. Ters hareket kendi tarih ve referansıyla görünür. |
| K07 · Klavye, ekran ve hız | Dar pencere ve tam ekranda Genel Personel, Ödemeler ve Rapor merkezi ekranlarını açın. Tab ve Shift+Tab ile filtre, sayfalama ve komutları dolaşın. Kullanılan ölçeklerde ve gerekiyorsa ekran okuyucuda tekrar edin. Tipik ve en yoğun raporun açılış ve aktarım süresini üçer kez ölçün. | Düğmeler erişilebilir, odak görünür, alan adları anlaşılır. Aynı filtrede toplam değişmez. Süreler pilot başında belirlenen sınırı karşılar. |
| K08 · Yedek ve kurtarma | Aşağıdaki kurtarma provasını yalnız test ortamında tamamlayın. Kullanılıyorsa harici aynalamada geçici erişim kesintisi ve **Aynalamayı yeniden dene** senaryosunu uygulayın. | Doğrulama başarılıdır. Geri yükleme sonrası doğru veri ve referanslar geri gelir. Aynalama hata sonrası aynı arşivle tamamlanır. |

### K08 kurtarma provası

1. Test veritabanında bilinen bir personel, izin ve ödeme kaydı oluşturun. Takma kişi kodunu, izin tarihini, bakiye ve ödeme referansını yazın. Şifreli arşivi oluşturup tam doğrulayın.
2. Sunucu veya test Windows hesabında, API paketindeki **AccountingApp.BackupRecovery.exe** ile `export-keys` kullanarak parolalı bir `.abkey` dosyasını ayrı korumalı konuma alın. Parolayı ayrı saklayın. Cihaz kaybı senaryosu kapsamdaysa, arşivi ve anahtar dosyasını temiz bir test profiline taşıyıp `import-keys` ile içe aktarın ve `unseal` ile yeni özel klasöre çözün. Bu adımı anahtar taşınabilirliği kanıtı olarak kaydedin.
3. Test veritabanındaki izin veya personel adını değiştirin. Test API’sini durdurun ve yerel bağlantıda aynı test veritabanını açın. **Ayarlar ve yedekleme** ekranındaki geri yükleme işleminde seçilen arşiv ile hedef kapsamının eşleştiğini kontrol edin. Hedefi doğruladıktan sonra yalnız test veritabanını geri yükleyin.
4. Yeniden giriş yapın. Test personeli, izin, bakiye ve ödeme referansını yedek anındaki kayıtla karşılaştırın. Şema 138 uyumunu kontrol edin. Test API’si kullanılıyorsa tekrar başlatıp aynı test hedefini doğrulayın.
5. Ayrı makineye veya farklı SQL kataloğuna geçiş gerekiyorsa SQL bakım sorumlusu yeni test hedefini hazırlasın. Arşivin çözülmesini ve gerçek SQL geri yüklemesini ayrı ayrı kaydedin. Yeni hedefin dosya, katalog, bağlantı kapsamı ve erişimleri doğrulanır. [Anahtar yönetimi ve farklı hedefe kurtarma](YEDEK_VE_DESTEK_GUVENLIGI.md#doğrulama-ve-gerçek-kurtarma)

## 4. Kabulü kaydedin ve teslim edin

Her adımı başlangıçta **Bekliyor** olarak bırakın. Kanıtla doğrulanmış adımı **Geçti**, sapma olanı **Düzeltme gerekiyor** olarak işaretleyin. Pilot kapsamı dışındaki modüller için kapsam kararını ve gerekçeyi ayrıca yazın.

| Adım | Tarih / yapan | Beklenen | Gerçekleşen | Kanıt / takip kaydı | Durum |
| --- | --- | --- | --- | --- | --- |
| P01 · Personel ve vardiya | | | | | Bekliyor |
| P02 · İzin ve puantaj | | | | | Bekliyor |
| P03 · Dönem hakedişi | | | | | Bekliyor |
| P04 · Ödeme, tahsis ve banka | | | | | Bekliyor |
| P05 · Rapor ve kapanış | | | | | Bekliyor |
| K01 · Yetki | | | | | Bekliyor |
| K02 · Ayrı onay ve tekrar | | | | | Bekliyor |
| K03 · Belirsiz sonuç ve CSV | | | | | Bekliyor |
| K04 · Eşzamanlı değişiklik | | | | | Bekliyor |
| K05 · Taslak ve kilit | | | | | Bekliyor |
| K06 · Kesin kayıt ve ters hareket | | | | | Bekliyor |
| K07 · Klavye, ekran ve hız | | | | | Bekliyor |
| K08 · Yedek ve kurtarma | | | | | Bekliyor |

**Bir sorun görülürse** · Veri kaybı, ikinci mali kayıt, yetkisiz erişim, açıklanamayan saat veya tutar farkı ya da kurtarma hatasında ilgili akışı durdurun. Tarihi, sürümü, ekranı, işlem referansını ve varsa hata kodunu yazın. Bekleyen ödemenin sonucunu aynı referansla araştırın. Destek kaydı gerekiyorsa yönetici ekranındaki **Güvenli tanı paketi kaydet** seçeneğini kullanın.

**Kabul kararı** · Pilot sorumlusu ile gözden geçiren kişi, kapsamdaki adımların kanıtlarını birlikte incelesin. Açıklanamayan fark sıfır, kritik takip maddeleri kapalı ve kurtarma provası başarılıysa tarihi ve kabul verenleri kaydedin. Uygulamayı devralan kişilere doğru kısayolu, API adresini, rol sınırlarını, yedek sorumlusunu ve destek kayıt yolunu iletin.

| Teslim alanı | Kayıt |
| --- | --- |
| Karar / tarih / kabul verenler | Bekliyor |
| Onaylanan proje, dönem ve topoloji | |
| Mutabakat farkı / açık takip kayıtları | |
| Kurtarma kanıtı / kanıt klasörü | |
| Devralan kişi / teslim tarihi | |

**Kalan iş paketi 1** · Gerçek kullanım pilotu ve saha kabulü.
