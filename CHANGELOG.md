# Değişiklik günlüğü

Tarihler Türkiye saatine (Europe/Istanbul, UTC+03:00) göredir. Geliştirme günleri aşağıda belirtilir; Git commit tarihi değişikliklerin kaynak geçmişine kaydedildiği tarihtir. Geçmiş tarihlere ayrı commit üretilmedi.

## 28 Ekim 2026

Responsive arayüzleri ve önceki kayıt/sürüm, rapor ve yedek aynalama düzeltmelerini içeren **v1.2.0 Windows x64 paketi** GitHub Releases'te yayımlandı. Paket, `0e58031` commit'inin 610 test başarılı / 0 başarısız Actions çalışmasından alındı; SHA256 özeti aynı kaldı. ZIP ve SHA256SUMS.txt dosyalarına herkese açık erişim doğrulandı. README'deki indirme/sürüm/özet bağlantıları ve paketleme komutları v1.2.0'a güncellendi; güncelleme, ilk kurulum, kısayol ve bütünlük doğrulama adımları açıklandı. [Sürüm notları](https://github.com/frknylmzkng/AccountingApp/releases/tag/v1.2.0).

## 27 Ekim 2026

GitHub Actions'taki responsive test hataları düzeltildi. İlk çalışmada 42 kontrol, sanal ekranın pencere izleme boyutu sınırı yüzünden geniş düzene ulaşamıyordu. Testlere ortak, gerçek boyutu doğrulayan pencere yardımcısı ve büyük boyuttan küçülme regresyon testi eklendi; uygulama kodu veya test beklentileri gevşetilmedi. Güncel yerel tam paket: **610 başarılı, 0 başarısız, 0 atlanan test**, Release 0 uyarı / 0 hata; geçici LocalDB motoru temizlendi. Kanıt: artifacts/analysis/master-20261027-202455-754/. [Neden ve düzeltme](docs/CI_RESPONSIVE_TESTLER.md).

Responsive arayüzlerin toplu kaynak gönderimi hazırlandı: 12 ekran/akış, regresyon testleri, örnek verili önizlemeler ve tarihli belgeler toplandı. Önceki onaylı sürüm/rapor/aynalama düzeltmeleri ayrı altyapı commit'inde tutuldu. Güncel kaynakla tam doğrulama: **609 test başarılı, 0 başarısız, 0 atlanan**, Release 0 uyarı / 0 hata; entegrasyon ve geri yükleme testlerinin geçici LocalDB motoru temizlendi. Yerel paketler, kişisel bağlantı ayarları ve kullanıcı veritabanı Git kapsamı dışında kaldı. [Ekranlar ve gönderim kapsamı](docs/RESPONSIVE_ARAYUZLER.md). Mevcut v1.1.0 Release ZIP'i değiştirilmedi.

Parola Sıfırlama ve Geçici Parola sonuç pencereleri pencereye uyarlanıyor: yeniden boyutlandırma/büyütme, yan yana/alt alta bölümler, satır kıran uzun hesap adları ve açıklamalar, ortalanmış içerik ve daima görünür işlem düğmeleri eklendi. Yönetici parolası/karakter seçimi, maskeleme, özgün hedef hesap, işlem kilitleri; sonuç penceresinin süre/salt okunurluk/gösterme tercihi korunuyor. **364 veritabanısız test**, üç temada boşta/kilitli sıfırlama ve maskeli/gösterilen örnek sonuç için **936 boyutlandırma durumu**, yayımlanan DLL ile **42 görsel kontrol** başarılı. Gerçek parola sıfırlanmadı. Paket: artifacts/releases/password-reset-adaptive/. [Önizlemeler ve deneme adımları](docs/PAROLA_SIFIRLAMA_RESPONSIVE.md).

Parola Değiştirme pencereye uyarlanıyor: bilgi/kurallar bölümleri yan yana veya alt alta; alan açıklamaları sola/üste geçiyor. Form ve işlem düğmesi birlikte ortalanıyor; kısa pencerede alanlar kayarken düğme görünür kalıyor. Bölüm geçişleri, satır kıran açıklama ve doğal Tab sırası eklendi. Girilen parolalar/boşluklar/karakter seçimi, maskeleme, uzunluk sınırı, geçici parola kısıtı ve işlem kilidi korunuyor. **352 veritabanısız test**, üç rol × iki oturum türünde boşta/kilitli **624 boyutlandırma durumu** ve yayımlanan DLL ile **42 görsel kontrol** başarılı. Gerçek parola değiştirilmedi. Paket: artifacts/releases/password-change-adaptive/. [Önizlemeler ve deneme adımları](docs/PAROLA_DEGISTIRME_RESPONSIVE.md).

İzin Planlama pencereye uyarlanıyor: tarihler iki/tek sütuna, süre bilgisi aynı/ayrı sıraya geçiyor; sabit boyutlu onay düğmesi, satır kıran açıklama, bölüm geçişleri ve dikey kaydırma eklendi. Personel listesi kalan yüksekliği ve genişliği paylaşıyor. Girilmiş tarihler, sıralanmış çoklu seçim, tekil kişi sayımı, özgün personel/izin sürümleri ve rol kısıtları korunuyor. **346 veritabanısız test**, üç rol × üç tarih durumunda **468 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Gerçek izin kaydı oluşturulmadı. Paket: artifacts/releases/leave-plan-adaptive/. [Önizlemeler ve deneme adımları](docs/IZIN_PLANLAMA_RESPONSIVE.md).

Ayarlar pencereye uyarlanıyor: bağlantı, yedekleme, görünüm ve durum bölümleri iki/tek sütuna; alan açıklamaları sola/üste, tema seçenekleri yan yana/alt alta geçiyor. İşlem düğmeleri dört/iki sütunda, üst bölüm geçişleri sabit; uzun durum metni değiştiğinde yeniden ölçülüyor. Kaydedilmemiş yollar, tema seçimleri, durum bilgisi ve etkin/pasif kontroller korunuyor. **337 veritabanısız test**, yönetici oturumunda üç tema × üç durum metniyle **468 boyutlandırma durumu**, iki yetkisiz erişim kontrolü ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Gerçek ayar/yedek değiştirilmedi. Paket: artifacts/releases/settings-adaptive/. [Önizlemeler ve deneme adımları](docs/AYARLAR_RESPONSIVE.md).

Ödemeler pencereye uyarlanıyor: ödeme/günlük kapanış bölümleri genişliğe göre yan yana veya alt alta; personel/hesap defteri çok geniş alanda yan yana yerleşiyor. Sabit bölüm geçişleri, satır kıran sonuç açıklaması, dikey kaydırma ve alanı paylaşan tablo sütunları eklendi. Girilen tutarlar, tarih/fiş seçeneği, özgün personel/ödeme seçimi ve belirsiz ödeme kilitleri korunuyor. **326 veritabanısız test**, üç rol × beş ödeme durumunda **780 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Gerçek ödeme/iptal/yazdırma yapılmadı. Paket: artifacts/releases/payments-adaptive/. [Önizlemeler ve deneme adımları](docs/ODEMELER_RESPONSIVE.md).

Yönetici pencereye uyarlanıyor: yeni hesap alanları 1–3 sütuna; hesap ekleme/işlem bölümleri yan yana veya alt alta geçiyor. Sabit bölüm geçişleri, düğme yükseklikleri, dikey kaydırma ve alanı paylaşan hesap sütunları eklendi. Girilmiş bilgiler, parola maskesi, özgün hesap seçimi/sürümü ve yetki korumaları sürüyor. **311 veritabanısız test**, yönetici oturumunda yeni hesabın üç yetki seçeneğiyle **156 boyutlandırma durumu**, iki yetkisiz erişim kontrolü ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/admin-adaptive/. [Önizlemeler ve deneme adımları](docs/YONETICI_RESPONSIVE.md).

İletişim pencereye uyarlanıyor: kartlar geniş alanda yan yana, dar alanda alt alta; sınırlı içerik genişliği, ortalama, sabit düğme yüksekliği ve dikey kaydırma eklendi. Telefonlar salt okunur/seçilebilir kalıyor; kopyalama başarı/hata bilgisi satır kırabilen alt alanda da gösteriliyor. **306 veritabanısız test**, üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Kopyalama testleri yerel taklit hedefle yapıldı; gerçek panoya dokunulmadı. Paket: artifacts/releases/contact-adaptive/. [Önizlemeler ve deneme adımları](docs/ILETISIM_RESPONSIVE.md).

Genel Durum pencereye uyarlanıyor: sayım kartları dört/iki sütuna, bakiye kartları yan yana/alt alta düzene geçiyor; sabit bölüm geçişleri, satır kıran izin açıklaması ve alanı paylaşan tablo sütunları eklendi. Büyük tutarlarda metin boyutu ayarlanıyor; toplamlar, hassasiyet, izin satırları ve özgün seçim korunuyor. **303 veritabanısız test**, üç rolde **156 boyutlandırma durumu**, uç tutar/tema için **30 durum** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/summary-adaptive/. [Önizlemeler ve deneme adımları](docs/GENEL_DURUM_RESPONSIVE.md).

Kişi Arama pencereye uyarlanıyor: filtreler 1–3 sütuna geçiyor; sabit yüksekliğe sahip arama/temizleme düğmeleri, bölüm geçişleri, dikey kaydırma ve alanı paylaşan sonuç sütunları eklendi. Boyut değişiminde filtreler, sonuç tablosu ve özgün seçim/sürüm korunuyor; yeni sorgu çalışmıyor. Enter/F2/F5 ve salt okunur rol/oturum akışı sürüyor. **299 veritabanısız test**, üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/search-adaptive/. [Önizlemeler ve deneme adımları](docs/KISI_ARAMA_RESPONSIVE.md).

Joker Personel pencereye uyarlanıyor: alanlar 1–3 sütuna, dar alanda işlem düğmeleri iki sıraya geçiyor; sabit bölüm geçişleri, dikey kaydırma ve alanı paylaşan tablo sütunları eklendi. Genel Personel ile ortak PersonnelEditor kullanılıyor; özgün TC/sürüm, sıralanmış seçim, kaydedilmemiş alanlar ve rol korumaları korunuyor. **296 veritabanısız test**, Joker için üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/joker-adaptive/. [Önizlemeler ve deneme adımları](docs/JOKER_PERSONEL_RESPONSIVE.md).

Genel Personel ekranı pencereye uyarlanıyor: bilgi/izin bölümleri geniş alanda yan yana, dar alanda alt alta; personel alanları 1–3 sütun, dar alanda iki sıralı işlem düğmeleri. Sabit bölüm geçişleri, dikey kaydırma ve alanı paylaşan tablo sütunları eklendi. Yeniden boyutlandırmada kaydedilmemiş alanlar, izin tarihleri, özgün seçim/sürümler ve rol korumaları sürüyor. **293 veritabanısız test**, yeni üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/general-adaptive/. [Önizlemeler ve deneme adımları](docs/GENEL_PERSONEL_RESPONSIVE.md).

Ana ekran gerçek uyarlanabilir yerleşime geçirildi: işlem kartları kullanılabilir alana göre 1–4 sütuna geçiyor; dar alanda ağaç menü gizleniyor, dikey kaydırma bütün işlemleri erişilebilir tutuyor. Başlık/çıkış sabit, sığan içerik ortalanmış; roller ve klavye erişimi korundu. **290 veritabanısız test**, 282 boyutlandırma durumu ve yayımlanan yerel DLL ile **21 rol/boyut görsel kontrolü** başarılı. Yeni paket: artifacts/releases/dashboard-adaptive/. [Önizlemeler ve deneme adımları](docs/ANA_EKRAN_TAM_EKRAN_DUZENI.md).

Ana ekranın büyük pencere düzeni yenilendi: sol menü ve işlem grupları ortak bir alanda ortalanıyor; gruplar pencereyle genişliyor, eşit sütunlar/aralıklar ve sabit 40 piksel mantıksal düğme yüksekliği kullanılıyor. Geniş ekranlarda kontrol alanı sınırlandırıldı; küçük pencere ve mevcut rol/klavye/gezinti davranışı korundu. 287 veritabanısız test ve 12 rol/boyut yerleşim kontrolü başarılı; Release 0 uyarı / 0 hata. Gerçek veriler ve açık uygulama değiştirilmedi. [Önizlemeler ve ayrıntılar](docs/ANA_EKRAN_TAM_EKRAN_DUZENI.md). Kanıt: artifacts/analysis/master-20261027-083049-321/ ve artifacts/analysis/dashboard-responsive/.
## 26 Ekim 2026

Kullanıcı isteğiyle güncel yerel aynalama paketi açıldı ve Muhasebe - Güncel kısayolu yönlendirildi. Gerçek Ayarlar formunda, ayrı veritabanı ve test klasörleriyle 6 kontrol başarılı: başarısız aynalamada yerel yedek korunması, düğmeden yeniden deneme, otomatik hata/onarım, durum bilgisinin F5 ile güncellenmesi ve yinelenen yedek/audit oluşmaması. Tek ana yedek, değişmeyen yerel doğrulama zamanı ve kaynak/hedef SHA256 eşitliği doğrulandı. Otomatik üretim kontrol yolu doğrudan çağrıldı; fiziksel USB çıkarılmadı. Gerçek kayıtlar ve kullanıcı tercihleri kullanılmadı/değiştirilmedi. Geçici veritabanı/motor temizlendi; güncel uygulama açık bırakıldı. Kanıt: artifacts/analysis/mirror-live-ui/. [Ayrıntılar](docs/YEDEK_AYNALAMA_YENIDEN_DENEME.md).

Başarısız yedek aynalaması, 24 saatlik yerel yedek kontrolünden bağımsız yeniden deneniyor. Eksik/bozuk hedef aynı doğrulanmış kaynakla onarılıyor; kalıcı aynalama durumu, SHA256 doğrulaması ve atomik kopyalama eklendi. Yerel başarı/aynalama audit'i ayrıldı; Ayarlar'a durum ve Aynalamayı yeniden dene düğmesi, otomatik kontrole doğru kısmi başarı bildirimi eklendi. Aynalanan eski dosya saklama temizliğinde korunuyor. **532 test başarılı, 0 başarısız, 0 atlanan**; 23 test durumu eklendi, Release 0 uyarı / 0 hata. Yeni SQL betiği yok; gerçek veritabanı, açık uygulama ve kısayol değiştirilmedi. [Ayrıntılar ve deneme adımları](docs/YEDEK_AYNALAMA_YENIDEN_DENEME.md). Kanıt: artifacts/analysis/master-20261026-195505-188/.

Güncel yerel rapor paketi açıldı ve Muhasebe - Güncel kısayolu yönlendirildi. Gerçek Ödemeler formunda F5 yapılmadan, ayrı test veritabanında 6 fiş/kapanış raporu (A4/80 mm) ve veri değişmeme kontrolü başarılı. Kapanış toplamları 45 → 65 → 20 TL olarak güncellendi; 30 TL eski fiş seçimi iptal durumunu gösterdi. Geçici test ortamları temizlendi. Standart modal fiş önizlemesi gözlendi; otomasyonun pencere hedefleme kısıtı sonrası kalan önizlemeler aynı paket çizimiyle modeless test penceresinde doğrulandı. Gerçek kayıtlar kullanılmadı, fiziksel çıktı gönderilmedi. Ayrıntı ve kanıt: docs/FIS_VE_KAPANIS_GUNCEL_VERI.md, artifacts/analysis/payment-report-live-ui/.

Ödeme fişi RequestId ile yeniden okunuyor; günlük kapanış açık ekrandaki eski tablodan değil seçilen günün güncel veritabanı verisinden üretiliyor. Yetki/veri/zaman aynı Serializable transaction içinde; raporda veri zamanı ve oluşturan kullanıcı yer alıyor. Sonradan iptalin özgün günün güncel toplamından çıkarılması açıkça belirtiliyor. Geçmiş fişe güncel veya yeniden oluşturulan personelin IBAN/bakiye/izin bilgileri eklenmiyor. **509 test başarılı, 0 başarısız, 0 atlanan**, Release 0 uyarı / 0 hata; 19 rapor testi eklendi. Yeni şema yok; gerçek veritabanı ve açık uygulama değiştirilmedi. [Ayrıntılar ve deneme adımları](docs/FIS_VE_KAPANIS_GUNCEL_VERI.md). Kanıt: `artifacts/analysis/master-20261026-171519-136/`.

Kullanıcı onayıyla mevcut veritabanının CHECKSUM yedeği alınıp VERIFYONLY ile doğrulandı ve yalnızca 105 izin sürümü yükseltmesi uygulandı. Mevcut verilerin özet karşılaştırması aynı kaldı. Muhasebe - Güncel kısayolu yeni yerel pakete yönlendirilip uygulama açıldı. Gerçek Genel Personel/İzin Planlama ekranlarında ayrı veritabanıyla 4 senaryoda eski seçim reddi ve güncel seçimle kaydetme dahil **8 ekran kontrolü başarılı**; geçici veritabanı/motor temizlendi. Kanıt: artifacts/analysis/leave-live-ui/. [Ayrıntılar](docs/IZIN_SURUM_KORUMASI.md).

İzin kaydetmeye personel RecordVersion + ayrı LeaveVersion kontrolü eklendi. Genel Personel ve toplu İzin Planlama özgün seçim sürümlerini taşır; beklenen izin yokluğu da doğrulanır. Yeniden oluşturulan personel veya başka kullanıcının izin değişikliği eski seçimle etkilenmez; tek çatışmada bütün plan/audit geri alınır. Eski eşzamanlı izin yardımcısı aynı sürümlü yolu kullanır, iç sürüm raporlardan gizlenir. Ek `105-leave-rowversion.sql` betiği hazırlanıp ayrı test veritabanında doğrulandı; eski betikler ve gerçek veritabanı değiştirilmedi. **490 test başarılı, 0 başarısız, 0 atlanan**; Release 0 uyarı / 0 hata. Test motoru temizlendi. [Ayrıntılar ve deneme adımları](docs/IZIN_SURUM_KORUMASI.md). Kanıt: `artifacts/analysis/master-20261026-162016-546/`.

Hesap silme/pasife alma/etkinleştirme işlemlerine özgün kullanıcı adı + SessionVersion + SessionEpoch kontrolü eklendi. Seçim onaydan önce alınır; hedef transaction içinde kilitlenerek ve SQL yazma koşulunda doğrulanır. Yeniden oluşturulan veya rol/parola/durumu değişen hesapta eski seçim reddedilir; giriş sayacı ve audit korunur. Eski AdminStore.Delete aynı güvenli servise yönlendirildi. Kendi hesabı/son yönetici ve oturum iptali korumaları sürdürüldü. **456 test başarılı, 0 başarısız, 0 atlanan**; Release 0 uyarı / 0 hata. Ayrı test motoru temizlendi; gerçek hesaplar kullanılmadı. Yeni SQL betiği yok. Yerel geliştirme; [ayrıntılar ve deneme adımları](docs/HESAP_SECIM_SURUM_KORUMASI.md). Kanıt: `artifacts/analysis/master-20261026-145710-633/`.

Personel silme koruması yayımlanan yerel DLL ile gerçek Genel/Joker ekranlarında, yalıtılmış veritabanında denendi: eski seçim reddi ve yenilenmiş seçimle silme dahil **4 ekran kontrolü başarılı**. Geçici test veritabanı/motoru temizlendi; gerçek personel verisi değiştirilmedi. Kullanıcı isteğiyle eski uygulama kapatıldı, yerel test paketi açıldı ve Muhasebe - Güncel kısayolu yönlendirildi.

Personel silme için özgün RecordVersion kontrolü eklendi: Genel/Joker seçim sürümü onaydan önce alınır, izin/defter değişmeden önce transaction içinde kilitlenerek doğrulanır ve DELETE koşulunda tekrar aranır. Yeniden oluşturulan veya başka kullanıcı/ödeme tarafından değiştirilen kayıtta eski seçim reddedilir; izinler, bakiye, defter ve audit korunur. Eski PersonnelStore.Delete yolu da sürümü zorunlu tutar. **414 test başarılı, 0 başarısız, 0 atlanan**; Release derlemesi 0 uyarı / 0 hata. Ayrı test motoru temizlendi; gerçek veritabanı kullanılmadı. Yeni SQL betiği yok. Yerel geliştirme; [ayrıntılar ve deneme adımları](docs/PERSONEL_SILME_SURUM_KORUMASI.md). Kanıt: `artifacts/analysis/master-20261026-134118-955/`.

Windows 2000 arayüz geliştirmeleri tek kaynak güncellemesinde toplandı: klasik/açık/koyu tema, ortak menüler ve klavye erişimi; giriş, ana ekran, Genel/Joker Personel, Kişi Arama, Genel Durum, İletişim, Yönetici, Ödemeler, Ayarlar, İzin Planlama, Parola Değiştirme/Sıfırlama ve geçici parola pencerelerinin ölçü ve yerleşimleri. Önceki geliştirme notları aşağıda tarihleriyle korunur. README ve örnek verili ekran görselleri güncellendi.

Tam doğrulama: **392 test başarılı, 0 başarısız, 0 atlanan**; Release derlemesi **0 uyarı / 0 hata**. Entegrasyon ve geri yükleme kontrolleri gerçek veritabanından ayrı geçici LocalDB motorunda çalıştı; motor temizlendi. Kanıt: `artifacts/analysis/master-20261026-025032-473/`. Bu gönderim kaynak kodu ve belgeleri günceller; mevcut v1.0.0 Release paketi değiştirilmez.

## 25 Ekim 2026

Parola Sıfırlama ekranı ve geçici parola sonucu düzenlendi: kompakt sabit pencereler, hedef hesap başlığı, ayrı doğrulama/parola bölümü ve hizalı düğmeler. Vazgeç/Esc desteği eklendi; vazgeçildiğinde yönetici parolası temizlenir. Maskeleme, süre ve mevcut güvenlik akışı korunur. 100 görünüm/parola/oturum kontrolü başarılı; gerçek hesap sıfırlanmadı. Geliştirme yereldir.

Parola Değiştirme ekranı düzenlendi: kompakt başlangıç ölçüsü, üstte oturum açıklaması, hizalı üç maskeli alan, altta parola kuralları ve sağda değiştirme düğmesi. Enter, mevcut/geçici parola akışı ve işlem sonrası temizleme korunur. Normal/küçük/büyük pencere ve mevcut parola/oturum kısıtları dahil 99 kontrol başarılı; gerçek parola değiştirilmedi. Geliştirme yereldir.

İzin Planlama ekranı düzenlendi: üstte hizalı başlangıç/bitiş, gün sayısı, seçili kişi sayısı ve onay; altta geniş Genel personel/izin listesi. Bir kişinin tekrarlanan izin satırları sayımı şişirmez, özgün TC kullanılır. Mevcut çoklu seçim ve toplu kaydetme/onay akışı korunur. Normal/küçük/büyük pencere dahil 108 veritabanısız kontrol başarılı; gerçek izin kaydı oluşturulmadı. Geliştirme yereldir.

Ayarlar ekranı düzenlendi: ayrı bağlantı, yedekleme ve görünüm/fiş bölümleri; hizalı dosya/klasör alanları ve seçim düğmeleri; altta kaydetme/yedekleme/geri yükleme işlemleri. Normal/küçük/büyük pencere, tema seçimi ve mevcut profil/yedek/oturum korumaları dahil 108 veritabanısız kontrol başarılı. Gerçek veritabanına yedekleme/geri yükleme uygulanmadı; geliştirme yereldir.

Ödemeler ekranı düzenlendi: solda tutar/ödeme/sonuç kontrolü, sağda günlük kapanış ve fiş ayarları; altta ayrı personel ve hesap defteri listeleri. Geniş IBAN, okunabilir Türkçe başlıklar ve sağa hizalı tutarlar. Görünür kolonlar değişse de fiş/iptal için özgün ödeme kimliği korunur. Küçük/normal/büyük pencere görünümü ve çift ödeme/kayıt seçimi/oturum korumaları dahil 100 veritabanısız kontrol başarılı; geliştirme yereldir.

Yönetici ekranı düzenlendi: solda yeni hesap alanları, sağda seçili hesabın etkinleştirme/pasife alma/parola sıfırlama/silme işlemleri, altta tam genişlikte hesap listesi. Parola maskesi ve hesap/oturum korumaları korunur; okunabilir kolon ölçüleri, Türkçe yetkiler ve UTC bitiş saati gösterimi eklendi. 90 görünüm/kayıt seçimi/oturum kontrolü ve son değişiklikle 17 görünüm kontrolü başarılı; geliştirme yereldir.

İletişim ekranı düzenlendi: kompakt başlangıç ölçüsü, yan yana kişi kartları, büyük ve seçilebilir telefon alanları, yerel panoya kopyalama düğmeleri. Mevcut iletişim bilgileri korunur; belge görselinde örnek bilgiler kullanılır. 90 görünüm/kayıt seçimi/oturum geçişi kontrolü başarılı; geliştirme yereldir.

Genel Durum açılışındaki turuncu geçiş giderildi: eski arka plan ve çerçevesiz başlangıç ayarı kaldırıldı, tema pencere gösterilmeden uygulanıyor. 71 görünüm/ekran geçişi kontrolü başarılı; düzeltme yereldir.

Genel Durum yerleşimi yenilendi: üstte dört kişi sayacı, ayrı ödeme toplamı ve ortalama kutuları, altta geniş izin takibi. Çoklu izin kayıtlarında kişi sayısı/ödeme toplamının şişmediği doğrulandı. 95 görünüm/özet/rapor/ekran geçişi kontrolü başarılı; geliştirme yereldir.

Kişi Arama ekranı düzenlendi: üstte hizalı TC/ad/soyad filtreleri, sağda arama ve filtre temizleme düğmeleri, altta tam genişlikte sonuçlar. Enter ile arama ve sonuç sayısı başlığı eklendi. 88 görünüm/kayıt seçimi/ekran geçişi kontrolü başarılı; geliştirme yereldir.

Joker Personel düzenlendi: üstte hizalı alanlar ve geniş IBAN, altta tam genişlikte personel listesi; işlem düğmeleri tek sırada. Boş izin kolonları gösterilmez; görünür kolonlar değişse de sıralama sonrası özgün kişi/sürüm bilgisi korunur. 45 görünüm/kayıt seçimi kontrolü başarılı; değişiklik yereldir.

Genel Personel ekranı yeniden yerleştirildi: üstte personel bilgileri ve ayrı izin bölümü, altta tam genişlikte liste; geniş IBAN alanı, hizalı işlem düğmeleri ve okunabilir kolon başlıkları. 44 görünüm/kayıt seçimi kontrolü başarılı. Bu geliştirme yereldir.

Ana ekranın başlangıç ölçüsü ve yerleşimi düzenlendi: genişletilmiş Bölümler ağacı, Personel ve izin / Ödeme ve rapor / Hesap ve destek grupları, eşit yükseklikte düğmeler ve alt sağda çıkış. Büyük pencerede düğmelerin gereksiz büyümesi engellendi. 70 görünüm/oturum ve son hizalama için 15 görünüm kontrolü başarılı; geliştirme yereldir.

Girişin mavi dış alanı ve iç içe pencere görünümü kaldırıldı; sabit pencere doğrudan 620 × 400 boyutlu giriş içeriğine oturtuldu. Büyütme kapalı kaldı. 23 görünüm/giriş hazırlığı kontrolü başarılı; güncelleme yereldir.

Kullanıcı geri bildirimiyle girişin büyütülmesi kapatıldı: sabit boyutlu pencere, devre dışı büyütme düğmesi ve normal açılış. Önceki tam ekran tasarımı yerine bu davranış seçildi; diğer ekranlar büyütülebilir. 23 görünüm/giriş hazırlığı kontrolü başarılı; değişiklik yereldir.

Windows 2000 denemesinde tam ekran giriş iyileştirildi: DPI'ya uygun, boyutu sınırlandırılmış giriş kartı mavi çalışma alanının merkezinde tutulur. 243 veritabanısız test başarılı; normal/dar/tam ekran yerleşimleri kontrol edildi. Bu arayüz değişikliği yereldir.

**Yerel arayüz denemesi — Windows 2000:** Klasik renkler ve kontroller; giriş/ana ekran ile Genel/Joker yerleşimleri yenilendi. Dosya, İşlemler, Rapor, Görünüm ve Yardım menüleri; role uygun ağaç kısayolları ve tema seçimi eklendi. 387 test başarılı. Ayrı deneme paketi ve kısayolu hazır; bu arayüz denemesi henüz GitHub'a push edilmedi. [Görseller ve deneme adımları](docs/WINDOWS_2000_DENEME.md).

| Değişiklik | Yapılanlar | Doğrulama |
| --- | --- | --- |
| Hatalı giriş sınırı | Hesap başına 15 dakikada 5 hata için 5 dakika geçici kilit; kalıcı SQL sayacı; eşzamanlı deneme ve iptal koruması. Parola işlemleri aynı bütçeyi kullanır. Açık oturumlar etkilenmez; yönetici sıfırlaması hedefin kilidini temizler. | 383 test başarılı; 19 yeni test durumu |
| Parola değiştirme/sıfırlama | Her rol kendi parolasını doğrulayarak değiştirebilir. Yönetici yeniden doğrulamayla 30 dakika geçerli rastgele geçici parola oluşturur. Kullanıcı yeni parolasını belirlemeden diğer ekranlara erişemez. Yeni parolalar 15–1024 karakter; yalnızca hash saklanır. Eski oturumlar ve eski hedef seçimi reddedilir. | 364 test başarılı; yayımlanan paketle ayrı ortamda 32 uygulamalı kontrol |
| Hesap durumu/oturum iptali | Pasife alma ve etkinleştirme; kendi hesabı/son yönetici koruması; servislerde güncel yetki ve oturum kontrolü. Açık ekranlar 30 saniyelik kontrolle girişe döner. Yedekten dönüş eski oturumu canlandırmaz. | 338 test başarılı; yayımlanan paketle oturum ve zamanlayıcı denemesi |
| Eşzamanlı personel düzenleme | Genel/Joker kayıtlarının bütün alanlarında rowversion kontrolü. Eski ekran başka kullanıcının ad, soyad, IBAN veya bakiye değişikliğini ezmez. Özgün satır seçimi, taslak koruması; iç sürümler listelerden/raporlardan gizli. | 316 test başarılı; kullanıcı denemesi başarılı |
| Belirsiz ödeme sonucu | Ödeme gönderilmeden önce işlem kimliği ve hedef bağlamı DPAPI ile kalıcılaştırılır. Sonuç belirsizse aynı işlem incelenir/yeniden denenir. İkinci ödeme ve farklı uygulama örneklerinin çakışması önlenir. | 292 test başarılı |
| Excel toplamı | Çoklu izin satırlarının aynı personelin tutarını tekrar tekrar toplaması düzeltildi. | 292 test takımında doğrulandı |

Teknik raporlar ve kullanıcı kılavuzları:

- [Hatalı giriş sınırı](docs/HATALI_GIRIS_SINIRLAMASI.md)
- [Parola işlemleri](docs/PAROLA_DEGISTIRME_VE_SIFIRLAMA.md) · [uygulamalı doğrulama](docs/PAROLA_UYGULAMALI_DOGRULAMA.md)
- [Oturum iptali](docs/OTURUM_IPTALI_VE_HESAP_DURUMU.md) · [deneme kılavuzu](docs/OTURUM_GUVENLIGI_DENEME_KILAVUZU.md) · [uygulamalı doğrulama](docs/OTURUM_UYGULAMALI_DOGRULAMA.md)
- [Eşzamanlı düzenleme](docs/ESZAMANLI_PERSONEL_DUZENLEME.md) · [personel deneme kılavuzu](docs/PERSONEL_DENEME_KILAVUZU.md)
- [Ödeme kurtarma ve Excel](docs/ODEME_KURTARMA_VE_EXCEL_TOPLAMI.md)

## 24 Ekim 2026

- Eski personel kaydının ödeme iptalinin aynı TC ile yeniden oluşturulan kaydın bakiyesini etkilemesi engellendi.
- Geri yüklemede seçilen yedeğin temizlik/yükseltme sırasında silinmesi önlendi.
- Bu düzeltmeler **270 test** ile doğrulandı; yeni şema gerektirmez. [Teknik rapor](docs/ODEME_VE_GERI_YUKLEME_DUZELTMELERI.md).
- Daha önceki temel sürümde .NET 10, merkezi bağlantı ayarları, kurulum aracı, parola hash'i, ekran/çıkış yönetimi ve personel/izin/ödeme düzeltmeleri kaydedildi.
- Rehber mevcut işlevlere uyarlandı: roller, servis yetkileri, transaction/audit, ödeme defteri, raporlar, şifreli taslaklar, yedekleme ve şema yükseltmesi. LocalDB başlangıcı, izole test motoru, Windows CI, self-contained paket ve README kurulum/indirme adımları eklendi.
- Önceki Git kayıtları: [03a9dcf](https://github.com/frknylmzkng/AccountingApp/commit/03a9dcf), [305b3de](https://github.com/frknylmzkng/AccountingApp/commit/305b3de), [95fd89d](https://github.com/frknylmzkng/AccountingApp/commit/95fd89d), [6d4c451](https://github.com/frknylmzkng/AccountingApp/commit/6d4c451). [Rehber uygulama takibi](docs/REHBER_UYGULAMA_TAKIBI.md).

## Şema yükseltmeleri

| Betik | Tarih | Amaç |
| --- | --- | --- |
| 101-personnel-rowversion.sql | 25 Ekim 2026 | Eşzamanlı personel düzenleme koruması |
| 102-account-session-version.sql | 25 Ekim 2026 | Hesap durumu, oturum sürümü ve epoch |
| 103-password-change-reset.sql | 25 Ekim 2026 | Zorunlu parola yenileme ve geçici parola süresi |
| 104-login-throttle.sql | 25 Ekim 2026 | Ayrı kalıcı hatalı giriş sayacı |

Önceden uygulanmış 001 ve 100 betikleri değiştirilmedi. İlk giriş hazırlığı eksik yükseltmelerden önce doğrulanmış yedek alır. Bütün istemciler güncel sürümü kullanmalıdır.

## Son doğrulama ve yayın kapsamı

Tam yerel regresyon: **383 başarılı, 0 başarısız, 0 atlanan**. Uyarıları hata sayan Release derlemesi ve self-contained paket üretimi başarılı. Kanıt: artifacts/analysis/master-20261025-090245-255/. Geçici LocalDB motoru temizlendi; gerçek hesaplar test amacıyla değiştirilmedi.

Derleme çıktıları, yerel kanıtlar, gerçek veritabanları/yedekler, günlükler ve ödeme taslakları Git'e alınmaz. Kaynak push'u yeni GitHub Release yayımlamaz: mevcut **v1.0.0** Windows indirmesi 25 Ekim geliştirmelerini içermez. Son yerel paket artifacts/releases/login-throttle/ altındadır. Uzak GitHub Actions sonucu yerel testten ayrı değerlendirilir.
