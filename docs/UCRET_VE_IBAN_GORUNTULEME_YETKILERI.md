# Ücret ve IBAN görüntüleme yetkileri

31 Ekim 2026 · Kaynak/paket 1.3.7-dev · Hazırlanmış şema 118 · API 1.7.

Kurulum güncellemesi: [118 / 1.3.7-dev yerel kurulumu](1_3_7_DEV_KURULUMU.md) tamamlandı; yedek doğrulandı, denemeler atlandı. Aşağıdaki kaynak teslimi notları ilk teslimin tarihsel durumudur.

Kullanıcı 1.3.6-dev sürümünü doğruladı. Bu kaynak tesliminde iki bağımsız görüntüleme izni eklendi. Canlı veritabanı, kurulum, API görevi ve kısayol değiştirilmedi; denemeler kullanıcı isteğiyle atlandı.

## Yönetici için kullanım

Yönetici hesapları ekranında hesabı seçip **Ek hesap yetkileri** bölümünü açın:

- **Ücret / bakiye / finansal tutarları görüntüle**: personel bakiyesi, hakediş/avans/ödeme tutarı, toplam/ortalama ve maliyet okumasını açar. Uygulamada tarihli ücret tarifesi henüz yoktur; mevcut PARA maaş olarak yeniden yorumlanmaz.
- **IBAN görüntüle**: personel IBAN'ını okumayı açar. IBAN değiştirmek ayrıca personel düzenleme yetkisi ister.
- Ödeme/bakiye işlemleri ve kişisel veri dışa aktarma önceki ayrı izinler olarak korunur.

Yetki kaydı hedef hesabın özgün sürüm/oturum kimliğiyle ve yönetici kontrolüyle yapılır. Değişiklik, o hesabın oturum neslini yeniler; yeniden giriş gerekir. Değişiklikler eski/yeni bit değerleri ve gerekçeyle mevcut audit sistemine yazılır. Admin bu haklara her zaman sahiptir; bu ekrandan azaltılamaz.

| Ücret | IBAN | Genel personel / kart görünümü |
|---|---|---|
| Kapalı | Kapalı | Ad, kimlik ve izin gibi operasyon alanları; IBAN ve tutarlar gönderilmez |
| Açık | Kapalı | Finansal tutarlar görülebilir; IBAN gönderilmez |
| Kapalı | Açık | IBAN görülebilir; finansal tutarlar gönderilmez |
| Açık | Açık | İki alan da görülebilir; yazma/ödeme/export için ilgili ek hak gerekir |

118 uygulanınca yeni iki DB biti mevcut yönetici dışı hesaplarda **kapalı** başlar. Yönetici rolü etkin tam erişimini korur. Eski ödeme/export bitleri silinmez veya otomatik görüntüleme yetkisine dönüştürülmez. Eski ödeme biti açık olsa da ücret okuması kapalıyken ödeme yapılamaz. Yönetici gerekli hakları açıkça yeniden tanımlar. Yeni hesaplar da aynı kapalı varsayılanlarla başlar.

## Okuma ve düzenleme davranışı

Personel sayfaları, arama, izin planının personel verisi, personel kartı ve geçmiş, SQL personel özetleri ve proje finans özetlerinde hassas alanlar servis sonucu çıkmadan filtrelenir. Yetkisiz IBAN ve ondalık finansal sütunlar tipleri korunarak **DBNull/null** yapılır; gerçek değer API JSON'una veya masaüstü veri kaynağına taşınmaz. SQL toplam/ortalama DTO alanları da null olur. Arayüz hassas sütunları gizler; bakiye başlıklarında Yetki yok gösterir. Gizli değer sıfır bakiye gibi sunulmaz.

Personel kartının hakediş ve ödeme ayrıntı sekmeleri, proje maliyet belgeleri, ödeme fişi/kapanış raporları ve kesinleşmiş kapanış içerikleri ücret okuması ister. Diğer kart sekmeleri ve izin/ad-soyad işleri kullanılabilir. Eski defter/geçmiş listelerinde tutarlar boş döner. Genel rapor ve grafiklerin mevcut serbest metin açıklamalarındaki kullanıcı yazıları otomatik içerik sınıflandırmasına tabi değildir; izin bu uygulamanın yapılandırılmış IBAN ve finansal alanlarını korur.

Ad/soyad düzenlemesi için IBAN veya ücret okuması gerekmez. Genel/Joker ekranı, bu izinlerden biri eksikse personel kartının metadata servis yolunu kullanır. Gizli IBAN null olarak **gönderilmez/değiştirilmez**, SQL UPDATE IBAN alanını içermez; bakiye alanı bu yolda hiç yazılmaz ve sıfır defter hareketi üretilmez. IBAN izni varsa düzenleme yetkisiyle IBAN da değiştirilebilir. Özgün PersonId/rowversion kontrolü korunur.

IBAN zorunlu mevcut personel ekleme akışı nedeniyle, IBAN okuması olmayan hesap bu ekranlardan yeni kart ekleyemez; yetkili kişi ekler. IBAN açık ama ücret kapalıysa sıfır açılış bakiyesiyle eklenebilir. Para değişikliği ayrı ödeme hakkı ve ücret okuması gerektirir. IBAN kapalıyken Genel/Joker metadata düzenlemesi para alanını değiştirmez; ödeme işlemleri ayrı Ödemeler ekranından yapılabilir.

Taslak saklama/yükleme, izin dışı IBAN ve para kutularını dahil etmez. Önceki şifreli taslağın gizli alanları F7 ile ekrana geri getirilemez. Bekleyen ödeme dosyasının tutarı ücret izni olmadan ekrana getirilmez; dosya silinmez, yetkili kullanıcı mevcut tekrar/kurtarma yoluyla devam eder. Ödeme RequestId modeli ve kalıcı niyet biçimi değiştirilmez.

## Ödeme, export ve proje sınırları

Ödeme ve ödeme sonucunu inceleme ücret okuması ister. Kayıt için ayrıca CanPay gerekir; ücret izni tek başına ödeme açmaz. Yetki dialogunda ödeme seçilince ücret okuması da seçilir; ücret kapatılınca ödeme seçimi kaldırılır. Servis, ödeme açık/ücret kapalı yeni bir yetki kaydını reddeder. Sıfır bakiye kontrolü içeren yeni çalışma dönemi ve grup geçişi işlemleri de ücret okuması ister.

Genel **Excel/CSV/pano** yolunda istemci serbest başlıklı tablo/metin gönderebildiği için, sadece sütun adına bakarak hassas alanı yeniden adlandırma yoluyla kontrolü aşmasına izin verilmez: bu genel yol hem ücret hem IBAN okuması **ve** ayrı kişisel veri export hakkı ister. Dolayısıyla görünür ad/izin tablosu dahil genel aktarım bu haklardan biri eksikse reddedilir. Bu bilinçli sıkı sınır, alanı gizledikten sonra aynı eski veriyi başka başlıkla dışarı göndermeyi önler. Alan bazlı güvenli rapor şablonlarıyla daha dar aktarım sonraki bir genişletme olabilir.

Ödeme fişi ve kapanış gibi sunucunun sabit içerikten ürettiği raporlar IBAN içermediğinden ücret okuması + mevcut yazdırma/export izniyle çalışır. Projeye özel CSV'nin kaynağı sunucudur: proje export izni ve maliyet raporunda ayrıca ücret okuması + CanViewCosts gerekir; atama raporu ücret izni gerektirmez.

Proje kullanıcısına yeni görüntüleme bitleri, Yönetici hesapları > Ek hesap yetkileri bölümünden verilebilir. Bu, global personel/IBAN dizinine erişim açmaz; mevcut proje kapsamı aynen korunur. Proje kullanıcısının maliyet okuması için hem yeni ücret izni hem ilgili projenin CanViewCosts hakkı gerekir. Global ödeme/export bu rolde yine kapalıdır. IBAN biti tek başına yeni bir proje IBAN uç noktası açmaz.

## Teknik teslim ve sınırlar

118 yalnız CanViewSalary ve CanViewIban bitlerini ekler; audit hesap trigger'ı ve güvenli audit alan listesi bu bitleri kapsayacak şekilde yeni migration içinde güncellenir. Eski 100–117 betikleri değiştirilmez. Oturum doğrulaması her istekte DB'deki iki biti, hesap sürümünü ve epoch'u karşılaştırır. İstemcinin bildirdiği görünürlük hakkına güvenilmez. İstemci/API birlikte 1.3.7-dev / API 1.7 olmalıdır; null toplamlar ve yeni izinler sözleşmeye yansıtılmıştır.

Canlı 118 yükseltmesi ve kurulum bu teslimde yapılmadı. Önce yedekli yükseltme ve sonra istemci/API kurulumu gerekir. Bu uygulama/servis sınırıdır; aynı Windows hesabıyla DB dosyasına doğrudan erişim veya eski yedeklerdeki içerik değiştirilmez.

Release derlemesi ve paket bütünlüğü kontrol edilir. SQL migration, farklı izin birleşimleri, revocation, ödeme/kurtarma, export, taslak, proje kapsamı ve arayüz denemeleri kullanıcı isteğiyle çalıştırılmadı.
