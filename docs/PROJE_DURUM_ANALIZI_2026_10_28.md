# Güncel proje durumu ve kalan sorunlar

28 Ekim 2026 · Kaynak: `b745d233f10898eb4db8606c80a8174536cf2365` · Dağıtılan uygulama: v1.2.0, uygulama kaynağı `0e58031`.

Kullanıcının belirlediği hedef: **vardiya ve proje bazlı çalışan ekiplerin takibi**. Bu rapor mevcut kodu değerlendirir. Benzer projeler ve saha senaryoları [ayrı analizdedir](GITHUB_VE_SAHA_ANALIZI_2026_10_28.md); uygulama sırası [geliştirme yol haritasındadır](GELISTIRME_YOL_HARITASI_2026_10_28.md).

İzleyen uygulama durumu: F01 için ödeme seçim/sürüm koruması yerel kaynakta eklendi; [düzeltme ve deneme adımları](ODEME_SECIM_SURUM_KORUMASI.md). F02 restore hata güvenliği ve F03 IBAN doğrulaması sonraki işlerdir. Aşağıdaki bulgular ilk incelemenin kanıtlarını korur; yayımlanmış v1.2.0 paketinin kapsamı değişmez.

## İnceleme yöntemi ve sınırlar

31 Ekim izleyen ürün işi: [proje/ekip/tarihli asıl atama temeli](PROJE_EKIP_VE_TARIHLI_ATAMALAR.md) 1.3.1-dev kaynakta eklendi. 113 şeması canlıya uygulanmadı; denemeler atlandı. Kurulu 1.3.0-dev kullanıcı tarafından doğrulandı ve bu çalışmada değiştirilmedi. Employment, proje bazlı erişim ve vardiya modülü açık kalır.

30 Ekim izleyen F14: [ortak sürüm metadatası, Hakkında, paketleme/etiket CI ve Windows sihirbazı](SURUMLU_DAGITIM_VE_KURULUM.md) kaynakta hazırlandı. 1.3.0-dev yerel pakettir; GitHub'da kararlı Release oluşturulmadı. Kullanıcının talebiyle işlev/kurulum/CI denemeleri atlandı; temiz ortam kurulumu ve imzalama saha/dağıtım işi olarak açık kalır. Kullanıcı F13 localhost API bağlantısını kendi kullanımında doğruladı; çok cihaz kanıtı değildir. Aşağıdaki F14 bulgusu ilk incelemenin tarihsel sürüm uyuşmazlığını kaydeder.

30 Ekim F13 izleyen adım: [masaüstü merkezi API bağlantısı](MASAUSTU_MERKEZI_API_BAGLANTISI.md), sunucuda rapor üretimi ve eksik parola/audit/detay uç noktaları eklendi. Yerel/API seçimi açık; ağ hatasında SQL'e geçilmez. Kalıcı ödeme niyeti ve veritabanı kapsam koruması korunur. Çok cihaz/ağ kesintisi denemeleri kullanıcı isteğiyle atlandı; dağıtım yapılmadı. Çok worker/proje kapsamı ve saha doğrulaması açık kalır.

30 Ekim izleyen F13 ilk aşama: kullanıcı tercihiyle merkezi Windows HTTPS API altyapısı, ayrı sözleşmeler ve mevcut servis uç noktaları eklendi. [Kurulum ve kalan işler](MERKEZI_API_ALTYAPISI.md). Masaüstü API'ye geçirilmedi; canlı DB ve kısayol korunuyor. Denemeler atlandı. F13 çok cihaz geçişi tamamlanmış değildir. Kullanıcı F12 otomatik kilit ve değişiklik uyarılarını kendi kullanımında doğruladı; yüksek DPI/çoklu monitör kanıtı ayrı kalır.

30 Ekim izleyen F12: boşta oturum kilidi ve kaydedilmemiş değişiklik uyarıları kaynakta eklendi. [Davranış ve doğrulama sınırları](OTOMATIK_KILIT_VE_DEGISIKLIK_UYARISI.md). Yeni şema gerekmez; test/ekran denemeleri atlandı. Yüksek DPI ve çoklu monitör saha kanıtı henüz eklenmedi.

30 Ekim izleyen F11: ilk tablo yerine açık rapor/kapsam/sütun seçimi; güncel önizlemeden ayrı immutable nakit kapanışı kaynakta eklendi. [112, hesap tanımı ve sınırlar](SECILEBILIR_RAPOR_VE_KALICI_KAPANIS.md). Canlı DB'ye uygulanmadı; denemeler atlandı.

30 Ekim izleyen F10: personel sayfalaması, bounded finans seçicisi, SQL özetleri ve pencere toplamı korunarak defter sayfalaması eklendi. [İndeks ve kapsam](PERSONEL_SAYFALAMA_VE_SQL_OZETLERI.md). Denemeler atlandı; 111 canlı DB'ye uygulanmadı. Aşağıdaki performans bulguları ilk incelemenin tarihsel durumudur.

30 Ekim izleyen F09: manuel hakediş, ayrı avans, proje gideri ve maliyet özeti eklendi; [kapsam ve kalan finans işleri](HAKEDIS_AVANS_PROJE_MALIYETI.md). Canlı 110 doğrulanmış yedek sonrası uygulandı. Denemeler atlandı; puantaj/tarife, çift kişi onayı, belge bazında ödeme tahsisi ve banka mutabakatı henüz yoktur.

29 Ekim izleyen F08: beş iş tablosunda eski-yeni değer, kayıt kimliği, aktör, gerekçe ve zaman kaydı ile sayfalı yönetici geçmişi kaynakta eklendi. [Kapsam, 109 ve doğrulama sınırları](AYRINTILI_ISLEM_GECMISI.md). Canlı DB'ye uygulanmadı; denemeler atlandı.

29 Ekim izleyen durum: F07 ödeme ve kişisel veri dışa aktarma izinleri personel düzenlemeden ayrıldı; [108 yükseltmesi ve kullanım](AYRI_ODEME_VE_DISA_AKTARMA_YETKILERI.md). Canlı DB'ye uygulanmadı, denemeler atlandı. Proje kapsamı ve hassas alan görüntüleme ayrımı henüz eklenmedi. Aşağıdaki tablo ilk incelemenin tarihsel bulgularıdır.

Personel/izin/ödeme servisleri, transaction ve yetki kontrolleri, yedek/geri yükleme, dışa aktarma, oturum yaşam döngüsü, veri şeması, responsive testler ve dağıtım akışı incelendi. Son başarılı uzak CI: [40665608378](https://github.com/frknylmzkng/AccountingApp/actions/runs/40665608378), 610 başarılı / 0 başarısız / 0 atlanan test. Bu sayı mevcut test kapsamının sonucudur; aşağıdaki yeni uç durumların güvenli olduğunu kanıtlamaz.

Yeni bulgular için ayrı GUID adlı LocalDB motorunda sentetik verilerle küçük bir inceleme aracı çalıştırıldı. Gerçek veritabanına bağlanılmadı; açık uygulama ve masaüstü kısayolu değiştirilmedi. Geçici veritabanı düşürüldü ve motor silindi. Uygulama kaynak koduna düzeltme uygulanmadı; bu çalışma analizdir.

Yerel kanıtlar: `artifacts/analysis/review-20261028/probe-results.json`, `probe-run.json`, `probe/Program.cs`. İnceleme aracının derlemesi 0 uyarı / 0 hatadır. Bu araç ve çıktıları Git kapsamı dışındadır. Tam 610 test bu analiz sırasında yeniden çalıştırılmadı; yukarıdaki tamamlanmış CI raporu kullanıldı. Yük, ağ kesintisi, farklı fiziksel DPI ve saha cihazı testleri yapılmadı.

## Korunması gereken mevcut kazanımlar

- PBKDF2 parola saklama, süreli parola sıfırlama, zorunlu yenileme, kalıcı hatalı giriş sınırlaması.
- İşlem içinde güncel hesap/yetki/oturum sürümü denetimi; personel ve izin düzenlemelerinde sürüm kontrolü.
- Ödeme transaction'ı, atomik bakiye düşümü, kalıcı bekleyen ödeme günlüğü ve aynı RequestId ile tekrar koruması.
- Eski ödemenin yeni personel dönemine iptal edilmesini engelleyen Closure koruması.
- Yedek doğrulama, seçilen geri yükleme kaynağının korunması, aynalamanın tekrar denenmesi.
- Güncel veriden üretilen fiş/kapanış önizlemesi, çoğalan izin satırlarında doğru Excel bakiye toplamı.
- Responsive ekranlar, tek pencere/oturum yaşam döngüsü, otomatik Windows derleme ve test paketi.

## Öncelik ve kanıt tablosu

P1: yanlış kayıt/oturum veya veri değiştirme riski; ilk düzeltme paketi. P2: kullanım, denetim, güvenlik kapsamı veya büyüme ihtiyacı. P3: işletim ve bakım kolaylığı. “Eksik modül” mevcut özelliğin arızalı olduğu anlamına gelmez.

| Kimlik | Öncelik | Bulgu | Kanıt türü |
|---|---|---|---|
| F01 | P1 | Eski ödeme seçimi yeniden oluşturulan personelin bakiyesini azaltabiliyor | Genel ve Joker için izole servis akışında tekrarlandı |
| F02 | P1 | Geri yükleme sonrası şema kontrolü hatasında eski oturum yeniden geçerli olabiliyor | İzole geri yüklemeyle tekrarlandı |
| F03 | P1/P2 | IBAN yalnızca uzunlukla doğrulanıyor; ad/soyad normalize edilmeden saklanıyor | Geçersiz IBAN ve boşluklu adın kaydı tekrarlandı |
| F04 | P2 | Yedek kimliği yalnızca veritabanı adına dayanıyor | Koddan doğrulandı; iki profil arasında geri yükleme denenmedi |
| F05 | P2 | Personel silme geçmiş izinleri de kaldırıyor; kalıcı kişi/çalışma dönemi kimliği yok | Şema ve silme akışı |
| F06 | P2 | İzin kaydetme ayrı dönem eklemek yerine mevcut dönemi değiştiriyor | Repository akışı; özellik sınırı |
| F07 | P2 | Para, kişisel veri, dışa aktarma ve proje erişimi için ayrı yetkiler yok | Rol/yetki ve dışa aktarma akışı |
| F08 | P2 | Audit hedef kaydı, gerekçeyi ve değişen alanları göstermiyor | Audit şeması ve INSERT çağrıları |
| F09 | P2 | Hakediş, avans, proje maliyeti ve banka gerçekleşmesi ayrımı yok | Model/defter şeması; eksik iş modeli |
| F10 | P2 | Tam liste sorguları, bellekte özet ve genel Serializable kullanımı büyümede baskı oluşturabilir | Koddan performans riski; ölçülmüş yavaşlık değil |
| F11 | P2 | Dışa aktarma ilk tabloyu seçiyor; kapanış saklanan kesin kayıt değil | Ekran ve rapor kodu; özellik sınırı |
| F12 | P2 | Boşta kalma kilidi, kaydedilmemiş değişiklik uyarısı ve yüksek DPI saha kanıtı eksik | Yaşam döngüsü ve mevcut doğrulama belgeleri |
| F13 | P2 | Uzak sunucu ve çok cihaz kullanımı için mimari hazır değil | Bağlantı profili yalnızca yerel LocalDB/Express kabul ediyor |
| F14 | P3 | Release sürümü ile EXE sürüm bilgisi ayrışıyor; yayın elle yönetiliyor | v1.2.0 EXE FileVersion=1.0.0.0; ProductVersion=1.0.0+0e58031… |

## F01 — Ödeme başlatırken özgün personel seçimi kayboluyor

Kaynak: [PaymentsForm.cs](../PaymentsForm.cs), Pay: 89–97; [PaymentWorkflow.cs](../Services/PaymentWorkflow.cs), BeginAsync: 85–93; [PersonnelService.cs](../Services/PersonnelService.cs), PreparePaymentAsync: 78–88.

Ekran TC/grup/tutar gönderiyor; listedeki özgün RecordVersion iletmiyor. PreparePaymentAsync, ödeme düğmesine basıldığı andaki personeli kilitleyip o andaki ClosureId'yi alıyor. Eski personel daha önce silinip aynı TC/grupta yeniden oluşturulmuşsa, yeni kaydın kapanış kimliği isteğe dahil ediliyor. Sonraki Closure koruması bu daha eski ekran seçimini ayırt edemiyor.

**Tekrarlanan senaryo:** 100 TL bakiyeli kaydı listele ve seçimi tut → güncel sürümle sil → aynı TC/grupta 250 TL bakiyeli yeni kayıt oluştur → eski seçimden TC/grup ile 30 TL ödeme başlat. Hem Genel hem Joker için sonuç Recorded; yeni kaydın bakiyesi **220 TL**. Bu uygulama defterindeki kayıt etkisidir; bankaya gerçek para gönderilmedi.

Öneri: onay penceresinden önce özgün personel hedefini dondur; hazırlamada beklenen RecordVersion'ı doğrula. Hazırlama ile gönderim arasındaki değişiklikleri de hedef kimliği/sürümüyle denetle. Zaten kaydedilmiş RequestId tekrarında eski sürüm yüzünden başarılı sonucu kaybetme: önce aynı niyetin mevcut sonucunu güvenle bul, yeni yazmayı ayrıca sınırla. Kalıcı PersonId/EmploymentId modeline geçerken günlüğü ve eski bekleyen istek biçimini sürümle.

Kabul: eski seçimde sil-yeniden ekle, ad/IBAN/bakiye değişimi, onay penceresi sırasında değişim ve hazırlama-gönderim arası değişim yanlış hedefe yazmamalı. Mevcut kayıp yanıt/aynı RequestId/yeniden başlatma testleri geçmeye devam etmeli. Salt TC ile çalışan eski servis giriş noktaları da gözden geçirilmeli.

## F02 — Restore sonrası hata yolu oturum iptalini atlayabiliyor

29 Ekim 2026: şema hatası sonrası canlanma yerel kaynakta düzeltildi; [işlem sırası ve testler](GERI_YUKLEME_OTURUM_KORUMASI.md). Oturum nesli MULTI_USER öncesinde yenilenir; yenileme hatası OFFLINE ile kapanır. Aşağıdaki inceleme ilk bulgudur. Çok süreçli kalıcı bakım kapısı ve kesinti kurtarma daha geniş kapsamda kalır.

Kaynak: [BackupService.cs](../BackupService.cs), 121–125 ve 131–146; [SchemaUpgrade.cs](../SchemaUpgrade.cs), 35–39; [AccountAuthorization.cs](../Services/AccountAuthorization.cs).

Restore bittikten sonra MULTI_USER açılıyor; şema hazırlığı bundan sonra çalışıyor. SessionEpoch yenilemesi ise şema hazırlığı başarıyla dönerse yapılıyor. Şema kontrolü hata verirse restore edilmiş admin satırları ve eski güvenlik sürümleri kalabiliyor. Süreç içindeki SemaphoreSlim farklı uygulama süreçlerine ortak bakım kapısı sağlamıyor.

**Tekrarlanan senaryo:** sentetik yedeğe uygulamanın tanımadığı bir migration kaydı ekle → bu yedeği al → çalışan DB'de bu kaydı kaldır ve admin epoch'unu değiştirerek eski oturumu geçersiz kıl → güncel admin oturumuyla yalnızca test DB'sine restore yap. Restore sonrası şema kontrolü hata döndürdü; önceden geçersiz olan oturum için IsCurrentAsync **true** oldu. Bu, backup'tan dönen aynı SessionVersion/SessionEpoch'un yeniden kabul edilmesidir.

Öneri: geri yükleme öncesi uyumluluğu ayrı ortamda doğrula; bütün istemcilerin uyguladığı bakım durumu kullan. Restore edilmiş veritabanını normal işleme açmadan oturum neslini yenile. Yenileme veya şema doğrulaması tamamlanamıyorsa uygulamayı bakım durumunda tut; başarıyla oturum iptal edilene kadar normal okuma/yazmayı açma. Bakım işaretinin restore edilen DB ile geri sarılmaması ayrıca tasarlanmalı. SQL bağlantı/bakım kilidi ve hata kurtarma sırası birlikte ele alınmalı.

Kabul: uyumsuz şema, yükseltme hatası, audit hatası, çok süreç ve süreç kesintisi senaryolarında eski oturum geçersiz kalmalı. Audit hatası için mevcut test zaten var; sorun burada daha erken şema hatasıdır.

## F03 — Girdi doğrulaması ve saklanan değer ayrışıyor

29 Ekim 2026: TR IBAN kontrolü, normalize modelin servis/eski kayıt yolunda saklanması ve Genel/Joker salt okunur düzeltme listesi eklendi. Kullanıcı talebiyle testler atlandı; Release derlemesi başarılı. [Kapsam ve sınırlar](PERSONEL_VERI_DOGRULAMA.md). Aşağıdaki metin ilk incelemedir.

Kaynak: [PersonnelInput.cs](../PersonnelInput.cs), 30–48; [PersonnelService.cs](../Services/PersonnelService.cs), 28; [PersonnelRepository.cs](../Data/PersonnelRepository.cs), WriteAsync.

16–34 karakter aralığı dışında IBAN yapısı/kontrol basamağı denetlenmiyor. `!!!!!!!!!!!!!!!!` kabul edildi ve veritabanına kaydedildi. Adın Trim edilmiş uzunluğu ölçülüyor, fakat ham ad saklanıyor; `  Review  ` aynen saklandı. Servis de doğrulamanın normalize edilmiş çıktısını kullanmak yerine gelen nesneyi kaydediyor.

Öneri: tek normalize model üret ve aynı modeli kaydet. IBAN için açık ülke kapsamı belirle; TR destekleniyorsa 26 karakter/yapı/MOD-97 kontrolü uygula. Diğer ülkeler destekleniyorsa doğrulanmış ülke uzunluk tablosu kullan. Biçim kontrolünün hesap sahibini veya hesabın açık olduğunu doğrulamadığı UI'da doğru anlatılmalı. [TCMB IBAN yapısı ve kontrol algoritması](https://www.tcmb.gov.tr/wps/wcm/connect/EN/TCMB%2BEN/Bottom%2BMenu/IBAN/Communique).

Eski bozuk verileri topluca silme veya değiştirip tahmin etme; düzeltme listesi çıkar. TCKN/yabancı kimlik/personel numarası ayrı alan/tür olmalı; TC'yi gelecekte teknik birincil anahtar olarak kullanma.

## F04–F08 — Geçmiş, yetki ve denetim

29 Ekim 2026: F06 tarih düzenlemelerinde önceki değerlerin kaybolması kaynakta düzeltildi; [107 yükseltmesi ve kapsam](IZIN_DEGISIKLIK_GECMISI.md). İzin takvimi/başvuru/onay modülü eklenmedi. Testler atlandı; canlı DB 106'da tutuldu.

29 Ekim 2026: F05 kalıcı kimlik ve arşivleme yerel kaynakta uygulandı; [106 yükseltmesi, kimlik eşleme ve sınırlar](PERSONEL_KIMLIGI_VE_ARSIV.md). Canlı DB yükseltilmedi; testler önceki taleple atlandı.

29 Ekim 2026: F04 yedek kimliği/kapsam ayrımı yerel kaynakta uygulandı; [davranış ve eski yedeklerin ele alınması](YEDEK_KIMLIGI_VE_KAPSAM.md). Önceki talep doğrultusunda testler çalıştırılmadı; Release derlemesi başarılı. Aşağıdaki metin ilk bulgudur.

**Yedek kimliği:** Prefix yalnızca katalog adı hash'idir; HEADERONLY kontrolü de DatabaseName üzerinden yapılır. Aynı adın başka instance/profilde kullanılması yanlış kapsamlı temizleme veya yanlış yedeğin kabulü riskini doğurur. Kalıcı uygulama/veritabanı kimliği, kaynak sunucu/profil manifesti ve restore hedef özeti eklenmeli. Gerçekten başka bir ortama klonlama ayrı ve açık işlem olmalı.

**Personel geçmişi:** TC/grup üzerine kurulu Closure yaklaşımı mevcut hatalara koruma ekliyor; geçmiş çalışma dönemlerini kullanıcıya modellemiyor. DeleteAsync izinleri fiziksel siliyor. Arşiv, ayrılış tarihi, yeniden işe giriş ve kalıcı EmploymentId eklenmeli; defter geçmişini bugünkü kişi adıyla birleştirmemeli.

**İzinler:** SaveLeaveAsync TC'ye göre tek satır bekliyor ve onu güncelliyor. Yeni izin dönemi ekleme/geçmiş/iptal/onay ve vardiya ile saat bazında çakışma ayrı model gerektiriyor. Bu takvim günü davranışı mevcut özelliğin tanımıdır; kendiliğinden yasal izin hakkı veya ücret hesabı değildir.

**Yetkiler:** PersonnelManager personel düzenlemenin yanında ödeme de kaydedebiliyor. Viewer okuma ve rapor dışa aktarma yetkisini ortak kullanıyor; proje/saha sınırı yok. Personel düzenleme, ücret/IBAN görüntüleme, puantaj onaylama, ödeme hazırlama, ödeme onaylama ve dışa aktarma ayrı izinler olmalı. Aynı Windows hesabının DB dosyasına doğrudan erişebilmesi uygulama rollerinden ayrı güvenlik sınırıdır.

**Audit:** Actor/Action/zaman var; hangi kaydın neden değiştiği yok. EntityType/EntityId, CorrelationId, gerekçe ve hassas değerleri maskeleyen değişim özeti eklenmeli. Kimlik ve ödeme/izin olayları transaction içinde kalmalı; denetim ekranı ve dışa aktarma da yetkilendirilmeli. Geri alma, geçmişi silmek yerine bağlantılı düzeltme olmalı.

## F09–F14 — İş modeli, ölçek ve işletim

- Defterde Opening/Allocation/Payment/Reversal/Closure var; onaylı çalışma kaydı, dönem hakedişi, avans tahsisi, proje maliyeti ve banka mutabakatı yok. Önce “kalan tutar”ın onaylı hakediş eksi ödeme olduğu açıkça modellenmeli; mevcut PARA maaş gibi yeniden yorumlanmamalı.
- ListAsync bütün eşleşen personel/izin satırlarını getiriyor; Summary bunlardan bellekte özet çıkarıyor. TC araması CONVERT içeriyor. Bütün UnitOfWork işlemleri Serializable; kapsamlı okumalar büyüdükçe yazmaları bekletebilir. Sayfalama, tipli arama ve SQL özetleri eklenmeli; izolasyon seviyesi yetki/eşzamanlılık güvenceleri korunarak ölçümle ayrıştırılmalı. Körlemesine NOLOCK eklenmemeli.
- SessionForm.Export ilk DataGridView'i seçiyor; Ödemeler'de hangi raporun istendiği açık değil. Rapor türü, dönem, proje, alanlar ve veri zamanı seçilebilir olmalı. CSV/Excel güvenlik ve tekil bakiye toplamı testleri korunmalı.
- Kapanış şu anda açıkça anlık önizlemedir. Kesinleşmiş dönem, immutable sonuç, yeniden açma gerekçesi ve sonraki döneme düzeltme ayrı özelliklerdir. Gün sınırı istemci TimeZoneInfo.Local kullanıyor; ekipler farklı cihazlara geçerse işletme/proje saat dilimi ortak ayar olmalı.
- Oturum iptal kontrolü var; kullanıcı hareketsizken otomatik ekran kilidi yok. Kaydedilmemiş alanlar sayfa değişiminde kaybolabilir. Kullanımda özellikle puantaj girişini etkiler. Taslak, açık kaydet/vazgeç uyarısı ve erişilebilir klavye akışı tasarlanmalı.
- Responsive doğrulamalar 96 DPI'dır; yüzde 125/150/200 ve çok monitör, ekran okuyucu ve kontrast kontrolleri ayrıca gerekir. Test sayısını artırmak yerine bu eksik ortamları kapsamak hedeflenmeli.
- DatabaseSettings uzak SQL adreslerini kabul etmiyor. Çok ekip/cihaz kullanımında sadece bağlantı metnini açmak yetmez: merkezi API, sunucuda yetki/proje kapsamı, ortak saat, bakım ve kimlik yönetimi gerekir. [Microsoft LocalDB sınırları](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/sqlclient-support-localdb?view=sql-server-ver16).
- v1.2.0 EXE'si sürüm özelliğinde 1.0.0 gösteriyor. Etiket → MSBuild Version → ürün bilgisi → ZIP → SHA256 → Release eşlemesi otomatikleştirilmeli. Hakkında/diagnostic ekranında uygulama, şema ve commit görünmeli. İlk kurulum/LocalDB kontrolü ve kısayol kurulumu bir sihirbazda toplanmalı.

## İlk uygulanacak sıra

1. F01: ödeme hedefi için özgün seçim/sürüm, iki personel grubunda regresyon.
2. F02: restore hata yollarında bakım ve oturum nesli; çok süreç/kesinti testleri.
3. F03: normalize kayıt modeli ve IBAN doğrulama; eski veriyi raporlama.
4. F04/F07/F08: yedek kimliği, ayrıntılı yetki ve anlamlı audit.
5. Kalıcı kişi/çalışma dönemi + proje modeli; ardından vardiya, puantaj ve hakediş.

İlk üç madde düzeltilmeden yeni vardiya/hakediş akışını mevcut ödeme yoluna bağlamak önerilmez. Ayrıntılı veri göçü ve kabul ölçütleri [yol haritasında](GELISTIRME_YOL_HARITASI_2026_10_28.md) yer alır.
