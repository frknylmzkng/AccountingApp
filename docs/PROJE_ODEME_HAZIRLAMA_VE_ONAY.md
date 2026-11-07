# Proje bazlı ödeme hazırlama ve onaylama

31 Ekim 2026 · Kaynak/paket 1.3.8-dev · Hazırlanan şema 119 · API 1.8.

Kurulum güncellemesi: [119 / 1.3.8-dev yerel kurulumu](1_3_8_DEV_KURULUMU.md) tamamlandı; yedek doğrulandı, denemeler atlandı. Aşağıdaki kaynak teslimi notları ilk teslimin tarihsel durumudur.

Kullanıcı 1.3.7-dev sürümünün çalıştığını doğruladı. Bu teslimde proje ödemelerinde hazırlayan ve onaylayan ayrıldı. İşlev/SQL/API/UI denemeleri kullanıcı isteğiyle atlandı. Canlı 1.3.7-dev/118, merkezi API görevi ve kısayol değiştirilmedi.

## Yetkileri tanımlama

Yönetici, Projeler/ekipler/tarihli atamalar ekranında projeyi açıp **Proje sorumlusu ve yetkiler…** bölümünde iki yeni bit tanımlar:

- **Ödeme hazırla (CanPreparePayments)**: bu projedeki personel için talep oluşturur; tek başına ödeme yapamaz.
- **Ödeme onayla/reddet (CanApprovePayments)**: başka hesabın hazırladığı talebi karara bağlar.

İki bit de mevcut maliyet okumasını ve hesabın ayrı **Ücret/bakiye görüntüleme** hakkını gerektirir. Sorumlu olmak veya ekip/atama düzenleme yetkisi bu hakları içermez. 119 mevcut proje üyelerine otomatik hazırlama/onay hakkı vermez; iki yeni bit kapalı başlar. Yönetici tüm projelerde işlem yapabilir; kendi hazırladığı talebi yönetici de onaylayamaz veya reddedemez. Aynı hesaba iki bit verilebilir fakat kendi talebinde karar veremez; en az iki ayrı etkin hesap gerekir.

Proje kullanıcıları yalnız atanmış projelerinde çalışır. Global CanPay veya genel personel düzenleme bu yeni proje yetkilerinin yerine geçmez. Genel merkezdeki yönetici dışı rollerin mevcut genel işlemleri korunur; projeye özel delegasyon mevcut kısıtlı ProjectUser üyeliği üzerinden verilir. Yeni yetki kaydı yine yönetici/hesap/proje/üyelik sürümü doğrulamasıyla yapılır. Geri alınmış eski üyelik aynı AccountId için yeni üyelik açılmasıyla canlandırılmaz.

## Kullanım

Proje kullanıcısı **Projelerim > Maliyetler > Ödeme hazırlama/onay…** düğmesini kullanır. Yönetici merkezde **Projeler > Projeyi aç > Proje ödeme hazırlama/onay…** düğmesinden aynı proje kuyruğunu açar.

1. Proje personeli sekmesinden kişiyi seçip talep hazırlayın. Bakiye ödemesi veya avans türünü, pozitif iki ondalıklı tutarı ve gerekçeyi girin.
2. Hazırlama yalnız SQL'de **Onay bekliyor** talebi oluşturur; PARA veya ödeme defterine dokunmaz.
3. Başka bir yetkili hesap Ödeme talepleri sekmesinden talebi seçer. Tutar/tür değiştirilemez; gerekçeyle onaylar veya reddeder.
4. Onay veritabanı bakiyesini ve ödeme defterini tek transaction içinde değiştirir, talebi aynı transaction'da Onaylandı/kaydedildi yapar. Banka transferi gönderilmez.
5. Red ödeme oluşturmaz. Talep silinmez veya tekrar düzenlenmez; gerekiyorsa yeni talep hazırlanır.

Liste ve personel havuzu sayfalıdır. Bu ekranda TC, IBAN ve kişinin genel bakiyesi gösterilmez. Talep tutarı, hazırlayan, gerekçeler, karar, defter kimliği ve daha sonra gerçekleşen ödeme iptal zamanı görünür. Ad/soyad güncel karttan okunur; tarihsel kişi adı snapshot'ı olduğu iddia edilmez. Approved geçmişte ödemenin kaydedildiğini ifade eder; daha sonra iptal edilmişse ayrı iptal sütunu görülür.

## Tutar ve hedef kontrolleri

Hazırlama ve onayda sunucu, SQL UTC saati + Europe/Istanbul iş gününe göre projenin etkin/tarih aralığında olduğunu ve kişinin güncel EmploymentId'sinde bugün geçerli iptal edilmemiş proje ataması bulunduğunu kontrol eder. Eski bir tarihli atamanın havuzda görünmesi tek başına ödeme için yeterli değildir.

Talep PersonId, grup, serverdan çözülen TC, EmploymentId, özgün kişi rowversion'ı, son Closure sınırı, miktar ve türü saklar. Kaynak/hedef başka kişi veya dönemle değiştirilemez. Hazırlamadan sonra ad/IBAN/bakiye/dönem/sürüm değişirse onay reddedilir; talep beklemede kalır, yetkili kişi reddedip güncel seçimle yeniden hazırlatır. Atama iptali, proje pasifleşmesi ve geçerlilik tarihleri de onayda yeniden kontrol edilir.

**Bakiye ödemesi**, hem kişinin mevcut bakiyesini hem de bu kişi/proje/çalışma döneminin geçerli hakedişi eksi iptal edilmemiş proje ödemelerini aşamaz. Önceki avanslar bu proje hesabındaki ödemelere dahildir. Başka projedeki hakediş bu projeye gerekçe yapılamaz. Eski, projeye bağlanmamış bakiyeler otomatik proje hakedişine dönüştürülmez.

**Avans**, mevcut uygulamadaki gibi gelecekteki hakedişe karşı negatif bakiye oluşturabilir; yalnız onaydan sonra ve mevcut sayısal bakiye sınırı içinde kaydedilir. Bu dilimde avans bütçesi/tavan tarifesi veya puantajdan hakediş üretimi yoktur. Tutarı hazırlayan ve ayrı onaylayan görür. Talep para rezerve etmez: aynı kişinin iki talebi hazırlanmışsa ilk ödeme kişi sürümünü değiştirir, sonraki talep güncel seçimle yeniden hazırlanmalıdır.

Hazırlayanın hesabı aktif, güvenlik epoch'u aynı, ücret okuması ve hazırladığı eski üyelik üzerinden hazırlama+maliyet hakkı hâlâ geçerli olmalıdır. Hesap silme/yeniden oluşturma, güvenlik değişikliği veya üyeliğin geri alınması eski talebi onaylanabilir yapmaz. Token'ın normal süresinin dolması veya uygulamadan çıkmak tek başına SQL talebini silmez; onayda hesap güvenlik nesli ve canlı izinler değerlendirilir. Onaylayanın kendi oturumu/proje hakkı da her çağrıda canlı doğrulanır. Red, geçersizleşmiş bir hazırlığı kapatabilmek için hazırlayanın hâlâ yetkili olmasını gerektirmez.

## Tekrar, eşzamanlılık ve geçmiş

Hazırlamada RequestId istemcide ilk gönderimden önce sabittir. Aynı kimlik ve aynı niyet, yeni talep üretmeden mevcut sonucu döndürür; farklı kişi/tutar/tür/gerekçeyle tekrar reddedilir. İlk gönderimden sonra dialog alanları kilitlenir. Belirsiz yanıtta aynı açık dialogdan aynı istek yeniden gönderilir.

Onay/red dialogu özgün talep sürümünü ve gerekçeyi dondurur. Aynı aktör, aynı karar, aynı önceki sürüm ve aynı gerekçeyle kayıp yanıt tekrarı önce kayıtlı kararı bulur; yeni ödeme oluşturmaz. Farklı karar veya başka kullanıcının sonuçlandırdığı kayıt eski seçimden değiştirilemez. RequestId aynı zamanda PaymentLedger'ın mevcut unique RequestId'sidir.

Ödeme çekirdeği transaction alan bir ortak metoda ayrıldı. Genel ödeme yetki kontrolü kendi wrapper'ında korunur; yeni proje yolu ayrıca canlı proje onay hakkı ister. Kişi sürümü, kapanış sınırı, tutar ve RequestId tekrar kurtarma kontrolleri ortak çekirdekte korunur. Ödeme, karar ve audit ya birlikte commit olur ya birlikte geri alınır. SQL talep sürümü ve sonuç kısıtları da eşzamanlı ikinci kararın kalıcı etki üretmesini engeller. Serializable kilitler ve proje yetki lease'i commit'e kadar tutulur; kilit çatışması hata verirse yeniden gönderim aynı kimliği kullanır.

Talep/karar niyeti SQL'de kalıcıdır; sonuçlanmış talep ve önceki niyet değiştirilemez/silinemez. DB kısıtları farklı aktör, durum, kişi/TC/dönem ve gerçek defter kaydı bağlantısını da kontrol eder. Audit mevcut maskeleme sınırından geçer. Genel ödeme ters kayıt yetkisi yöneticide kalır; iptal eski talep kararını silmez veya yeniden onaylanabilir yapmaz.

Dialogdaki henüz gönderilmemiş istemci niyeti disk günlüğü değildir. Uygulama kapandıysa proje taleplerini yenileyip sonucu inceleyin. Hazırlama yanıtı kaybolduktan sonra farklı kimlikle yeni hazırlık açılması birden fazla bekleyen talep oluşturabilir; hazırlama ödeme yapmaz ve ilk onaydan sonraki kişi sürümü ikinci eski talebi engeller. Mevcut genel ödeme journal dosyası biçimi değiştirilmedi veya silinmedi.

## Eski ödeme yolları ve kurulum

Yeni **proje etiketli** ödemeler, genel submit çağrısıyla hazırlama/onay akışı atlanarak kaydedilemez. Proje talebine ayrılmış RequestId başka bir genel ödemede de kullanılamaz. Eski doğrudan proje avansı hazırlama çağrısı yeni ekrana yönlendiren hata verir. Daha önce kaydedilmiş proje ödemesinin aynı RequestId ile sonuç kurtarması bu kısıtlamadan önce çalışır. Eski bekleyen henüz kaydedilmemiş doğrudan proje isteği yeni debit yapamaz; mevcut kontrol/onayla temizleme yolu kullanılmalıdır. Projesiz genel merkez ödeme/bakiye işlemleri önceki CanPay kuralını sürdürür; bu dilim tüm genel finans işlemlerini çift onaya çevirmemiştir.

119, ProjectAccountAccess üzerinde iki kapalı varsayılanlı bit ve maliyet bağımlılığı CHECK'i; ProjectPaymentProposals tablosu, foreign key/durum/farklı aktör kısıtları, sayfalama indeksi, immutable/ödeme bağlantı trigger'ı ve audit trigger'ı ekler. Audit güvenli bit alan listesi genişletilir, hesap oturum nesilleri yenilenir. Uygulanmış 100–118 betikleri değiştirilmez. İstemci/API birlikte 1.3.8-dev / API 1.8 ve önce doğrulanmış yedekle 119 gerektirir. Canlı yükseltme ve kurulum bu teslimde yapılmadı.

Yalnız Release derlemesi/yayın ve paket bütünlüğü kontrolleri yapılır. SQL migration, iki hesapla hazırlama/onay, kendi talebi, yetki geri alma, eşzamanlı onay, kayıp yanıt, eski ödeme journal'ı, avans, proje maliyeti ve arayüz denemeleri kullanıcı isteğiyle çalıştırılmadı. Puantaj onayı ve vardiya modülü bu teslimde eklenmedi.
