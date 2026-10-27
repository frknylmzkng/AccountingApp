# Geliştirme ve iyileştirme analizi

26 Ekim 2026 güncel incelemesinin izleyen durumu: personel silme, hesap hedefi ve izin kaydetme için eski seçim/sürüm korumaları tamamlandı. Ardından ödeme fişi ve günlük kapanışın eski ekran verisinden üretilmesi düzeltildi; **509 test başarılı**. [Güncel rapor verisi ve deneme adımları](FIS_VE_KAPANIS_GUNCEL_VERI.md). Güncel inceleme kaydı: `artifacts/analysis/review-20261026/PROJE_ANALIZI.md`; onun 5. bulgusu olan başarısız yedek aynalamasının yeniden denenmesi de tamamlandı; 532 test başarılı. [Aynalama düzeltmesi](YEDEK_AYNALAMA_YENIDEN_DENEME.md). Sıradaki bulgu IBAN doğrulamasıdır. Aşağıdaki 24 Ekim analizi tarihsel kayıttır.

Tarih: 24 Ekim 2026. İncelenen kaynak sürümü: `6d4c451`.

İzleyen uygulama durumu: 1.1'deki eski ödeme iptali ve 1.3'teki seçilen yedeğin silinmesi için korumalar eklendi; 270 test başarılı. Kapsam, sınırlar ve paket bilgileri [düzeltme raporunda](ODEME_VE_GERI_YUKLEME_DUZELTMELERI.md). Aşağıdaki bulgular ilk incelemenin kaydıdır.

25 Ekim 2026: 1.2'deki belirsiz ödeme sonucu ve 1.4'teki çoklu izin Excel toplamı düzeltildi; 292 test başarılı. Ayrıntılar [ödeme kurtarma ve Excel raporunda](ODEME_KURTARMA_VE_EXCEL_TOPLAMI.md).

25 Ekim 2026: 1.5'teki eşzamanlı personel düzenlemesi için tüm kayıt alanlarını kapsayan sürüm kontrolü ve taslak koruması eklendi; 316 test başarılı. Ayrıntılar [eşzamanlı düzenleme raporunda](ESZAMANLI_PERSONEL_DUZENLEME.md).

25 Ekim 2026: 2. bölümdeki oturum iptali sorunu düzeltildi; hesap etkinliği ve güncel yetki kontrolleri eklendi. 338 test başarılı. Ayrıntılar [oturum iptali raporunda](OTURUM_IPTALI_VE_HESAP_DURUMU.md), kullanıcı adımları [deneme kılavuzunda](OTURUM_GUVENLIGI_DENEME_KILAVUZU.md).

25 Ekim 2026: Mevcut parolayı doğrulayarak değiştirme, yönetici parolasıyla yeniden doğrulanan sıfırlama ve süreli geçici parolayla zorunlu yenileme eklendi. 364 test başarılı. Ayrıntılar ve deneme adımları [parola güvenliği kılavuzunda](PAROLA_DEGISTIRME_VE_SIFIRLAMA.md).

25 Ekim 2026: Hesap başına kalıcı giriş sınırlaması eklendi: 15 dakikada 5 hata, 5 dakika geçici kilit. Giriş ve parola işlemlerindeki yeniden doğrulamalar aynı sayacı kullanır; açık oturumlar etkilenmez. 383 test başarılı. Ayrıntılar [hatalı giriş kılavuzunda](HATALI_GIRIS_SINIRLAMASI.md). Aşağıdaki ilk inceleme tablosu tarihsel bulgu olarak korunmuştur.

Bu rapor mevcut personel, izin ve ödeme işlevlerine odaklanır. Bulgular kaynak kodu incelemesine dayanır; aşağıdaki uç durumlar canlı veritabanında denenmedi. Bu çalışma kapsamında uygulama kodu, şema, kullanıcı verileri ve GitHub yayını değiştirilmedi. Önceki 260 testlik doğrulama kaydı mevcut; bu analizde test paketi yeniden çalıştırılmadı.

## Genel değerlendirme

Uygulamanın temel altyapısı artık kapsamlı: güvenli parola hash'leme, roller, oturum/ekran yaşam döngüsü, transaction kullanan işlemler, aynı işlem kimliğine ait tekrar ödeme koruması, ödeme defteri, audit, merkezi bağlantı ayarları, yedekleme/geri yükleme, Excel/CSV, yazdırma, kullanıcı tercihleri ve Windows CI/paketi var. Bunları yeniden geliştirmek yerine iş geçmişini koruyan veri modelini ve belirsiz işlem sonuçlarını sağlamlaştırmak daha değerli.

Önce veri doğruluğu ve kurtarma riskleri, ardından izin/ödeme akışları, güvenlik ve dağıtım kolaylığı ele alınmalı. Yeni stok, POS veya tedarikçi modülleri mevcut personel uygulamasının bu aşamadaki önceliği değil.

## 1. Öncelikli düzeltmeler

### 1.1. Eski ödemenin yeni personel kaydına aktarılabilmesi — yüksek öncelik

Kaynak: [PersonnelService](../Services/PersonnelService.cs), [ödeme şeması](../database/upgrades/100-roles-audit-ledger.sql).

Personel ve ödeme ilişkisi TC + grup ile kuruluyor. Personel silindiğinde ödeme geçmişi tutuluyor; aynı TC ve grupta yeniden personel oluşturulabiliyor. Eski ödemenin iptali ise o anda bulunan personel satırının bakiyesine ekleme yapıyor. Kalıcı personel/çalışma dönemi kimliği yok.

Örnek senaryo: 100 TL bakiyeli kayda 30 TL ödeme yapılır, kalan 70 TL olur. Kayıt silinir ve aynı TC ile 100 TL bakiyeli yeni kayıt açılır. Eski 30 TL ödeme iptal edilirse yeni kaydın bakiyesi 130 TL olabilir. Bu, koddan çıkarılan senaryodur; kullanıcı verisinde gerçekleştiğine dair kanıt yoktur.

Öneri: değişmeyen `PersonId` ve yeniden işe giriş için `EmploymentId` kullanmak; silme yerine arşivlemek; defteri ilgili döneme yabancı anahtarla bağlamak. İptal satırında özgün ödemenin kimliği ve iptal gerekçesi tutulmalı. Geçici koruma olarak kapanmış dönemin ödemesinin yeni kayda eklenmesi engellenmeli.

Kabul ölçütü: silme/arşivleme → yeniden kayıt → eski ödeme iptali senaryosu yeni dönemin bakiyesini değiştirmemeli; geçmiş ayrı izlenebilmeli.

### 1.2. Sonucu belirsiz ödemeyi ekran değişiminden sonra tekrar kaydetme — yüksek öncelik

Kaynak: [PaymentsForm](../PaymentsForm.cs), [SessionForm](../SessionForm.cs).

Sunucu aynı `RequestId` ile gelen ödeme tekrarını doğru biçimde engelliyor. Ancak kimlik yalnızca ödeme formunun belleğinde saklanıyor. İşlem sırasında normal kontroller kapanırken araç çubuğundaki gezinme/çıkış düğmeleri açık kalıyor. Ödeme veritabanına yazılıp yanıt alınamazsa ekranı yeniden açmak yeni kimlik üretiyor; kullanıcının yeniden denemesi ikinci ödeme oluşturabilir.

Öneri: işlem kimliğini, parametrelerini ve bekleyen durumunu gönderimden önce kalıcı saklamak; yeniden açılışta kimlikle sonucu sorgulamak. Kritik işlem sırasında gezinmeyi güvenle yönetmek; kullanıcıya “sonuç kontrol ediliyor” durumu göstermek. Aynı mantık uygulamanın kapanması ve bağlantı kopması için de geçerli olmalı.

Kabul ölçütü: commit sonrası yanıt kaybı, ekran değişimi ve yeniden başlatmada aynı kullanıcı niyeti en fazla bir ödeme üretmeli.

### 1.3. Geri yüklenecek yedeğin temizleme sırasında silinebilmesi — yüksek öncelik

Kaynak: [BackupService](../BackupService.cs), `RestoreAsync`, `BackupAsync`, `Prune`.

Geri yükleme önce güvenlik yedeği alıyor; bu işlem en yeni 14 yedeği tutup eskileri siliyor. Seçilen geri yükleme dosyası aynı klasördeki en eski 14. yedekse yeni güvenlik yedeği onu silebilir. Kaynak dosyanın doğrulaması bundan sonra yapılıyor. Sonuç geri yüklemenin durması ve seçilen eski yedeğin kaybı olabilir; bu yol doğrudan veritabanını silmiyor.

Öneri: seçilen kaynağı temizleme kapsamı dışında sabitlemek veya güvenli geçici kopyasını almak; kaynağı önce doğrulamak; güvenlik yedeği ve saklama temizliğini koordineli yürütmek. Aynı veritabanı adına sahip farklı profillerin yedekleri için sunucu/veritabanı kimliği de tutulmalı.

Kabul ölçütü: 14 dolu yedek arasından en eskisi seçilerek izole veritabanına geri yükleme tamamlanmalı; seçilen dosya korunmalı.

### 1.4. Çoklu izin kaydında Excel toplamının şişmesi — yüksek öncelik

Kaynak: [PersonnelService.ListAsync](../Services/PersonnelService.cs), [ReportExport.Excel](../ReportExport.cs).

Personel listesi izinlerle birleştirildiğinde birden fazla eski izin satırı bulunan kişinin bakiyesi her satırda tekrarlanıyor. Excel tüm decimal satırlarını topluyor. Örneğin 1.000 TL bakiyeli kişinin iki izin kaydı varsa 2.000 TL toplam oluşabilir. Özet ve ödeme ekranlarında personel tekilleştiriliyor; aynı koruma genel Excel toplamında yok.

Öneri: personel özeti ile izin dönemi raporunu ayırmak; toplamı personel kimliğine göre hesaplamak. Ödeme defteri dışa aktarımı için de anlık bakiye, hareket tutarı ve kümülatif bakiyenin hangi koşulda toplanabileceği açık tanımlanmalı.

Kabul ölçütü: tek kişinin birden fazla izin dönemi olması personel toplamını artırmamalı; ekran ve Excel aynı toplamı göstermeli.

### 1.5. Eşzamanlı personel düzenlemesinde bilgi kaybı — orta/yüksek öncelik

Kaynak: [PersonnelService.SaveAsync](../Services/PersonnelService.cs), [PersonnelRepository.WriteAsync](../Data/PersonnelRepository.cs).

Mevcut koruma eski bakiye ile güncellemeyi engelliyor. Ancak iki ekran aynı bakiyeyi görürken biri IBAN'ı, diğeri adı değiştirirse ikinci ekran eski IBAN'ı geri yazabilir. Diğer alanlar için sürüm kontrolü yok.

Öneri: SQL `rowversion` ile tüm kayıt için iyimser eşzamanlılık; çakışmada değişen alanları gösterme ve kullanıcının girişini koruma. İzin düzenlemelerinde de kayıt kimliği ve sürüm kontrolü kullanılmalı.

Kabul ölçütü: aynı kaydı iki ayrı oturumda düzenlemek diğer kullanıcının değişikliğini sessizce ezmemeli.

## 2. Güvenlik ve hesap yönetimi

| Geliştirme | Mevcut durum ve amaç |
| --- | --- |
| Oturum iptali | Yetki oturum açılışında belleğe alınıyor. Başka oturumdaki hesabın silinmesi açık oturumun yazma yetkisini otomatik kaldırmıyor. Hesap etkinliği ve oturum güvenlik sürümü, hassas işlemlerde yeniden doğrulanmalı. |
| Parola değiştirme/kurtarma | Kullanıcının mevcut parolasıyla değiştirme; yöneticinin kontrollü sıfırlaması ve ilk girişte değiştirme akışı eklenmeli. Parola hash'i mevcut; sorun hash'leme eksikliği değil. |
| Hatalı giriş sınırlama | Artan bekleme ve kontrollü geçici kilit, başarısız deneme izlemesi ve yönetici kurtarma akışı eklenmeli. |
| Boşta kalma kilidi | Kullanıcının belirleyebildiği süre sonunda kilit ve yeniden doğrulama eklenebilir. |
| Kişisel veri görünürlüğü | TC/IBAN varsayılan maskeleme; tam görüntüleme ve dışa aktarma ayrı izinlerle yönetilebilir. Mevcut görüntüleyici rolünün rapor erişimi ürün kararı olarak netleştirilmeli. |
| Daha yararlı audit | Şu an aktör ve eylem var; hangi personel/ödeme değiştiğini gösteren varlık kimliği, işlem kimliği ve değişen alanlar eklenmeli. Ham parola, TC ve IBAN loglanmamalı. |

Kaynaklar: [AccountService](../AccountService.cs), [UserSession](../Models/UserSession.cs), [audit şeması](../database/upgrades/100-roles-audit-ledger.sql). Parola değişiminde mevcut parolayı doğrulama ve giriş denemelerini sınırlama, [OWASP Authentication rehberi](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html) ile uyumlu geliştirme başlıklarıdır.

Uygulama içi roller, aynı Windows hesabıyla veritabanı dosyasına doğrudan erişebilen kişiye karşı ayrı bir güvenlik sınırı sağlamaz. Birden fazla bilgisayar/kullanıcı için güçlü yetki ayrımı istenirse merkezi hizmet/API ve ayrı veritabanı yetkileri tasarlanmalı.

## 3. Günlük kullanımda en faydalı yeni özellikler

### Personel kartı ve arşiv

Tek sayfada personel bilgileri, işe giriş/ayrılış dönemleri, kalan tutar, ödeme hareketleri, izin geçmişi ve değişiklik geçmişi. Aktif/arşiv filtreleri ve kontrollü yeniden işe giriş. Genel/Joker bilgisi zamanla aynı personel modelindeki grup alanı haline getirilebilir; mevcut kayıtlar ve defter geçişte birebir korunmalı.

### Gerçek izin geçmişi ve takvim

Şu an tek izin satırı varsa yeni kayıt onu değiştiriyor; birden fazla eski satır varsa toplu değişiklik reddediliyor. Her izin dönemi ayrı kimlikle eklenip düzenlenebilmeli. İzin türü, durum, açıklama ve çakışma kontrolü eklenebilir. Aylık takvim ve bölüm/grup filtresi günlük kullanımı kolaylaştırır. Talep/onay/iptal ve hak ediş bakiyesi işletmenin kuralları netleşince eklenmeli; mevcut “başlangıç ve bitiş dahil” hesap davranışı açık gösterilmeli.

### Ödeme ve dönem takibi

Ödeme yöntemi, açıklama, referans, ilgili dönem, planlanan tarih ve vade takibi; kişi/tarih/durum filtreli hareket ekranı. Bakiyeyi elle değiştirmek de gerekçeli bir düzeltme hareketi olarak açıkça ayrılmalı. Sistemdeki “kalan tutar” maaş, borç veya avans bakiyesi olarak hangisini ifade ediyorsa ekran isimleri bu anlamla tutarlı olmalı.

### Geçmişi değişmeyen fiş ve kapanış

Mevcut fiş geçmiş ödeme ile güncel IBAN/bakiyeyi birlikte gösteriyor. Geçmiş fiş için işlem anındaki bilgilerin gerekli, maskelenmiş özeti saklanabilir. Günlük kapanış şu an anlık önizleme; daha sonra bir ödeme iptal edilince geçmiş günün toplamı değişebiliyor. Kapanış kimliği, kesim zamanı, onaylayan kullanıcı ve düzeltme hareketi olan saklanan kapanış modeli eklenmeli.

### Rapor merkezi ve sade ana ekran

Personel özeti, izin dönemi, ödeme hareketi ve dönem kapanışı ayrı raporlar olmalı. Çok tablolu ödeme ekranında hangi listenin aktarılacağı seçilebilmeli; mevcut ortak aktarım ilk tabloyu hedefliyor. Ana ekrana mevcut izin uyarılarının yanına vadesi yaklaşan ödemeler, son yedek ve bekleyen işlemler eklenebilir. Ana menü role göre sadeleştirilmeli; işle ilgili düğmeler ilgili ekranlarda toplanmalı.

## 4. Yedekleme ve işletim

- **Aynalama tekrar denemesi:** Ana yedek başarılı ama USB/ağ kopyası başarısızsa son 24 saatlik yerel yedek kontrolü yeni otomatik denemeyi atlıyor. Yerel yedek başarısı ve aynalama bekleyen durumu ayrı tutulmalı; ortam geri geldiğinde kopya yeniden denenmeli.
- **Durum görünürlüğü:** Son başarılı yerel/harici yedek, hata ve son geri yükleme provası ayarlarda ve ana ekranda görülebilmeli. “Ana yedek alındı, harici kopya bekliyor” anlaşılır biçimde gösterilmeli.
- **Uygulama kapalıyken yedek:** Mevcut otomatik kontrol uygulama/yönetici oturumu çalışırken devrede. Kesintisiz günlük yedek beklentisi varsa Windows zamanlanmış görevi ayrı kurulum seçeneği olmalı.
- **Geri yükleme bakımı:** Başka uygulama örnekleriyle çakışmayı da kapsayan bakım kilidi, aktif işlemleri durdurma ve sonuç ekranı tasarlanmalı. Süreç içi semaphore tek başına farklı süreçleri koordine etmiyor.
- **Yedek gizliliği:** Harici kopyalar için uygun şifreli saklama ve kurtarma anahtarı yönetimi değerlendirilmeli; kaybedilen anahtarın yedeği kullanılamaz kılacağı ürün akışında çözülmeli.
- **Düzenli kurtarma provası:** Mevcut doğrulama ve izole restore testleri korunmalı; gerçek yedekten izole ortama geri yükleme, kayıt sayıları ve hareket/bakiye tutarlılığı belirli aralıklarla doğrulanmalı.

## 5. Veri doğrulama, performans ve bakım

| Alan | Öneri |
| --- | --- |
| Girdi doğrulama | TC için yalnızca uzunluk/rakam kontrolü yerine desteklenen kimlik türüne uygun kontrol; IBAN için ülke/karakter/kontrol basamakları doğrulaması. Ad/soyadında doğrulanan ve kaydedilen normalize değer aynı olmalı; şu an Trim ile ölçülen metnin ham hali saklanıyor. |
| Listeleme | SQL tarafında sayfalama, tarih aralığı ve uygun indeksler. Tam TC aramasında sütunu metne çevirme yerine tipli eşitlik. Performans hedefi gerçek veri büyüklüğüyle ölçülmeli. |
| Özet sorgusu | Tüm personel/izin/IBAN satırlarını alıp saymak yerine yalnızca gerekli toplamları SQL'den getirmek. |
| Ekran düzeni | Farklı DPI ve küçük ekranlarda esneyen yerleşim; tutarlı sekme sırası, alan yanında hata, açık yükleniyor/boş sonuç durumları ve kaydedilmemiş değişiklik uyarısı. |
| Kod okunabilirliği | Kritik finans ve restore metotlarındaki uzun, bir satıra sıkışmış akışları okunabilir bloklara ayırmak; yinelenen SQL ve ekran kodunu azaltmak. |
| Test kapsamı | Mevcut testlere bu rapordaki yeniden işe giriş/iptal, belirsiz commit, eski yedek temizliği, çoklu izin toplamı ve iki oturumlu güncelleme senaryolarını eklemek. Test sayısı tek başına bu durumların korunduğunu göstermiyor. |
| Belgeler | Doğrulama belgesinde eski 253 test ve “Uzak GitHub CI çalıştırılmadı” satırları kalmış; sonraki 260 test ve başarılı CI kayıtlarıyla tarihli tek durum tablosunda uzlaştırılmalı. |

## 6. Kurulum ve sürüm dağıtımı

Mevcut self-contained Windows ZIP'i ve README kurulumu kullanılabilir. Sonraki iyileştirme, ilk açılışta bağlantı testi, LocalDB ön koşulu, veritabanı kurulumu ve ilk yöneticiyi anlaşılır adımlarla yöneten sihirbaz olmalı. Standart kurulum/kaldırma ve kısayol oluşturma, uygulamayı yeni bilgisayara taşımayı kolaylaştırır.

CI şu an derleme/test ve paket artifact'i üretiyor; GitHub Releases yayını ayrıca yapıldı. Etiket tabanlı yayın akışı, sürüm numarasını uygulama/paket/Release arasında eşitleme ve SHA-256 üretimi otomatikleştirilebilir. Hakkında ekranında sürüm ve veri şeması görünmeli; yeni sürüm bildirimi eklenebilir. Güncelleme öncesi yedek ve geri dönüş planı korunmalı.

Tek bilgisayarlı kullanım için mevcut WinForms yaklaşımı sürdürülebilir. Birden fazla bilgisayardan eşzamanlı kullanım somut ihtiyaç haline gelirse MDF'yi ortak klasöre koymak yerine merkezi SQL ve yetki denetleyen hizmet tasarlanmalı. [Microsoft LocalDB belgesi](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb?view=sql-server-ver17), LocalDB'nin kullanıcı sürecinde çalışan yerel örnek yapısını ve kısıtlarını açıklar.

## 7. Önerilen uygulama sırası

1. **Veri kaybı ve yanlış tutar korumaları:** eski yedek temizliği, çoklu izin rapor toplamı, eski ödeme/yeni kayıt ayrımı, belirsiz ödeme sonucu ve kaydın tüm alanları için sürüm kontrolü. Her biri için izole regresyon senaryosu.
2. **Kalıcı personel/çalışma dönemi modeli:** arşiv, defter bağlantıları, iptal referansı ve daha anlamlı audit. Mevcut veriyi koruyan göç ve önce/sonra bakiye mutabakatı.
3. **Günlük iş akışı:** personel kartı, ayrı izin dönemleri/takvim, filtreli ödeme hareketleri ve net bakiye anlamı.
4. **Hesap ve kurtarma güvenliği:** oturum iptali, parola değiştirme/sıfırlama, giriş sınırlama; aynalama tekrar denemesi ve yedek durum paneli.
5. **Rapor ve dağıtım:** saklanan kapanışlar, işlem anı fişleri, rapor merkezi, ilk kurulum ve otomatik Release.
6. **İhtiyaca bağlı ölçekleme:** ölçümlü sorgu iyileştirmeleri; çok bilgisayar gereksinimi varsa merkezi mimari.

İlk geliştirme paketi için en somut hedef: mevcut işlevleri koruyarak yanlış ödeme/iptal ve yedek kaybı senaryolarını kapatmak; ardından personel kartı ile izin geçmişini eklemek. Süre tahmini, kalıcı kimlik göçünün kapsamı ve işletmedeki izin/ödeme kuralları netleştiğinde yapılmalı.
