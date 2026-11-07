# Otomatik oturum kilidi ve kaydedilmemiş değişiklikler

30 Ekim 2026 · Yerel geliştirme. Yayımlanmış v1.2.0 paketine dahil değildir.

**İzleyen kısayol güncellemesi:** Kullanıcı isteğiyle Muhasebe - Güncel kısayolu bu yerel pakete yönlendirildi; paket hash'i ve bağlantı ayarı doğrulandı, eski kısayol yedeklendi. İki Windows otomasyon aracı kernel varlıklarını hazırlarken os error 3 verdiği için uygulama açılamadı. Denemeler yapılmadı. Özel işlem kaydı `artifacts/analysis/session-lock-deployment/` altındadır. Aşağıdaki dağıtım notu hazırlık aşamasının tarihsel durumudur.

## Kilit davranışı

Uygulama oturumu, bu uygulamanın pencerelerinde **10 dakika giriş etkinliği olmadığında** kilitlenir. Başka uygulamalarda çalışmak bu süreyi sıfırlamaz. Saat etiketi, veri yenileme veya otomatik yedek etkinlik sayılmaz. Yerel tekdüze geçen süre ve Windows giriş bilgisi kullanılır; uyku/uzun UI beklemesinden sonra gelen yeni giriş, geçmiş zaman aşımını kendiliğinden kaldırmaz. İşlem ve gezinme girişlerinde de zaman aşımı kontrol edilir.

Dosya menüsündeki **Oturumu kilitle** veya **Ctrl+Shift+L** ile manuel kilit de kullanılabilir. Bu sürümde süre sabit 10 dakikadır; Ayarlar'da değiştirme seçeneği yoktur. Bu bir uygulama kilididir; Windows oturumunu veya diğer programları kilitlemez.

Kilitte ana ekran ve açık uygulama diyaloglarının pencereleri gizlenir; yalnızca kilit penceresi gösterilir. Formlar sıfırlanmaz. Açılan kilitte aynı pencere, seçim, özgün kayıt sürümü ve girilmiş alanlar korunur. Kilit açmak için **aynı hesabın parolası** gerekir; başka bir kullanıcı bu formu devralamaz.

Doğrulama mevcut parola hash'i, kalıcı hatalı giriş sınırlaması, aktiflik, rol/ek izinler ve oturum sürümü/nesli kontrollerini kullanır. İnternetsiz yerel DB çalışır; veritabanı bağlantısı yoksa kilit kapalı kalır ve yeniden denenebilir. Parola/yetki/aktiflik değişmiş veya geçici parola süresi dolmuşsa eski oturum açılmaz; yeniden giriş gerekir. Başarılı açma Session.Unlock audit'i üretir; parola hiçbir günlük veya taslak dosyasına yazılmaz.

## Devam eden kayıtlar

Kilit, başlamış transaction'ı veya ödeme isteğini iptal etmez. İşlem tamamlanabilir; kullanıcıya ait sonuç/devam akışı kilit açılana kadar bekler. Yeni UI işlemleri ve ekran geçişleri kapalıdır. Bekleme, işlemin kendisi sürüyormuş gibi sayılmaz; gerçek işlem bittikten sonra kilitli oturum sonlandırılabilir.

Kilit penceresindeki **Oturumu sonlandır** açık form bilgilerinin kaybolacağını ayrıca onaylatır. Gerçek kayıt işlemi sürerken sonlandırma düğmesi kapalıdır. Kaydedilmiş veriler ve şifreli bekleyen ödeme günlüğü silinmez; sonucu belirsiz ödeme yeniden girişten sonra aynı RequestId ile kontrol edilmelidir. Oturum sunucuda iptal edilmişse güvenlik nedeniyle kaydedilmemiş veriyi koruyup devam etme seçeneği sunulmaz.

## Değişiklik uyarıları

Ekran değiştirme, normal oturum kapatma ve pencere kapatma öncesinde kaydedilmemiş girişler kontrol edilir. **Hayır** bulunduğunuz yerde kalır; **Evet** kaydetmeden devam eder. Otomatik veritabanı kaydı veya arka planda ödeme yapılmaz. Kaydetmek için ilgili ekranın mevcut onay/gerekçe akışı kullanılmalıdır.

Takip edilen gruplar:

- Genel/Joker personel kartı; Genel'de izin tarihleri ayrıca.
- Toplu izin planı tarihleri ve hedef seçimi.
- Yeni yönetici hesabı, hesap ek izinleri, parola değiştirme ve sıfırlama doğrulaması.
- Ayar alanları ve dosya/klasör seçicileri.
- Yeni ödeme tutarı; bekleyen günlüğe kaydedilen ödeme artık kaydedilmemiş taslak sayılmaz.
- Yeni proje, finans belgesi ve avans ayrı gruplar; kesin kapanışın gün/beyan alanları.

Personel seçimiyle alanları değiştirmek, liste sayfasını değiştirmek, F2/Temizle, F5/Yenile ve personel taslağını yüklemek de uygun yerlerde uyarır. Finans ekranında proje/personel değişikliği belge/avans taslağını etkiliyorsa önce sorulur. Arama ve önizleme filtreleri veritabanına kaydedilecek alan gibi değerlendirilmez.

Genel personelde kartı kaydetmek izin tarihlerini kaydetmez: diğer grubun kaybolma ihtimali ayrıca sorulur. Başarısız kayıt grubu temizlemez. İlgili grubu başarıyla kaydetmek başka grubu temizlemez. Başarılı kayıt/yeniden yükleme sonrasında programın doldurduğu alanlar yeni kullanıcı değişikliği sayılmaz. Değiştirilen değer başlangıç değerine döndürülürse gereksiz uyarı verilmez.

Parola işlemleri güvenlik istisnasıdır: mevcut davranış korunarak, başarısız deneme veya kapanıştan sonra parola kutuları temizlenir; parolanın taslak olarak tutulacağı varsayılmamalıdır.

Parola alanlarının değişiklik karşılaştırması için form ömrüyle sınırlı rastgele anahtarlı HMAC parmak izi kullanılır; ek bir düz metin parola kopyası takip durumunda tutulmaz. F6/F7 taslağı parola alanlarını içermez. Personel taslağını diske almak veritabanına kaydetmek değildir; ayrılma uyarısı devam eder.

## Dağıtım ve sınırlar

**Yeni SQL yükseltmesi yoktur.** Mevcut 112 şeması kullanılır; canlı veritabanı bu adımda değiştirilmedi. Yerel paket artifacts/releases/session-lock-unsaved/ altındadır. Masaüstündeki mevcut 112 rapor/kapanış paketi bu adımda değiştirilmedi; uygulama açılmadı.

Kullanıcı isteğiyle testler ve ekran/boşta bekleme denemeleri atlandı. Release çözüm derlemesi **0 uyarı / 0 hata** ile tamamlandı. Kilit beklemesi, iptal edilen ekran, zaman aşımı sınırı, ayrı değişiklik grupları ve canlı parola/yetki denetimi için regresyon kaynakları eklendi; yalnızca derlendi. Çoklu monitör, Windows/native dosya diyalogları, DPI ve uyku dönüşündeki gerçek pencere davranışı bu adımda denenmedi.

Bu değişiklik görev yöneticisiyle zorla kapatma, elektrik kesintisi veya işletim sistemi arızasında form alanlarını kurtarmayı garanti etmez. Kilit bir işlem güvenliği/Windows güvenlik sınırı değildir; mevcut servis yetkileri ve ödeme günlüğü korumaları sürer. Ekran kilidi ve uyarılar otomatik taslak/yedek veya işletim sistemi kilidi yerine geçmez.
