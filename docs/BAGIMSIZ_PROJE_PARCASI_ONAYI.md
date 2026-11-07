# Proje zaman parçalarının bağımsız onayı

İzleyen durum: [125 / 1.3.14-dev kurulumu](1_3_14_DEV_KURULUMU.md) tamamlandı ve kullanıcı doğruladı. 1.3.15-dev kaynakta [çalışan–hesap eşlemesi ve kimlik temelli onay koruması](CALISAN_HESAP_ESLEMESI.md) eklendi. Aşağıdaki metin 1.3.14-dev kaynak diliminin tarihsel kapsamını anlatır.

2 Kasım 2026 · Kaynak/paket **1.3.14-dev** · Hazırlanan şema **125** · API **1.14**.

Kullanıcı 1.3.13-dev sürümünün çalıştığını doğruladı. Proje amirinin yalnız kendi proje parçaları üzerinde karar verebildiği bağımsız onay/iade akışı eklendi. Canlı veritabanı, kurulu 1.3.13-dev istemci/API, şema 124 ve masaüstü kısayolu değiştirilmedi. Kullanıcı isteğiyle SQL/işlev/UI/eşzamanlılık denemeleri atlandı. Derleme ve paket bütünlüğü kontrolü iş akışı kabulü değildir.

## Akış ve roller

Dağılım hazırlama/kaldırma ve kaynak puantajı onaya gönderme, önceki gibi kaynak ve bütün hedef projelerde **CanEnterTimesheets** ister. Gönderimde yeni bir **onay turu** ve her değişmez parça için Pending karar satırı oluşturulur. Başka proje amirinin kararını hazırlayan kişi kendiliğinden vermez; eski kararlar yeni tura taşınmaz.

Amir **Projelerim / proje → Puantaj ve amir onayı… → Projeye ait zaman parçaları** sekmesini açar:

1. Yalnız mevcut projenin zaman parçasını seçer. Başka proje parçaları, adları ve gerekçeleri gösterilmez.
2. **Seçili parçayı onayla…** ile kişi, kendi parçasının aralığı, mola/net/ücret dakikası ve mevcut tur durumunu inceler; Kaydet ile gerekçe verir.
3. Bu proje için **CanApproveTimesheets** yeterlidir; kaynak projeyi veya diğer projeleri okuma/onaylama yetkisi aranmaz. Karar başka parçalara onay yazmaz.
4. **Seçili parçayı iade et / yeniden aç…** aynı kapsamda bekleyen veya onaylanmış parçayı iade eder. Onaylı ana paket de bu yolla yeniden açılabilir.
5. **Parça karar geçmişi…** bu projedeki parçanın bütün değişmez onay/iade olaylarını, aktör, UTC zaman, gerekçe ve turun kapanış bilgisiyle gösterir. **Kaldırılmış dağılımları da göster** eski sürümlere ait parçaları erişilebilir tutar; bunlarda yeni karar verilemez.

Bir projede birden fazla parça varsa her biri açıkça seçilerek karara bağlanır; toplu onay bu dilimde yoktur. Ana kaynak listesinde dağıtılmış kaydın toplu onay/iade düğmeleri kapalıdır. API'deki eski toplu uç da dağıtılmış kayıt için onay/iade veremez; bütün projelerde yetkili yönetici bile parça yolunu kullanır. Dağılımı olmayan normal puantajın önceki onay akışı korunur.

## Ana durum ve proje süreleri

- Turdaki en az bir parça Pending ise ana puantaj **Submitted** kalır. İlk amirin onayı başka projeyi onaylamaz ve ana kaydı tamamlamaz.
- Son parça da Approved olunca ana puantaj **Approved** olur. Son kararın, bu kararın audit'inin, kalıcı komutun ve ana durumun türetilmesi tek transaction içindedir. Son kararda bütün parçaların güncel tarih/atama koşulları iç kural olarak yeniden doğrulanır; amire diğer proje verileri açıklanmaz.
- Bir proje kendi parçasını iade edince tur kapanır ve ana kayıt **Returned** olur. Diğer amirlerin karar satırları değiştirilmez; tarihsel kalır. Kapalı turun tamamı etkin onaylı süre hesabından çıkar. Böylece düzeltme öncesi parçalar yeni dağılım/gönderime sessizce onay sağlamaz.
- Yeniden gönderim, aynı dağılım korunmuş olsa bile yeni RoundId ve bütün parçalar için yeni Pending kararları açar. Gerekirse önce eski dağılım kaldırılıp yeni sürüm hazırlanabilir.

**Onaylı proje net/ücret dakikası**, açık/güncel turda yalnız bu projenin Approved parçalarını toplar; diğer proje beklerken de kendi onaylı parçası görünür. Ana paket tamamlanınca bu süre bir kez daha eklenmez. İade/tur kapanışı, kaldırılmış dağılım ve eski turun kararları etkin toplama girmez. Ana puantajın bütün çalışma saati ayrı kaynak sütunlarında kalır; diğer projenin parçası ikinci kez sayılmaz.

Kaynak projenin Submitted ana kaydı genel paket bekleyen sayacında yer alabilir; gelen parçalar açısından yalnız yerel Pending karar bekleyen sayılır. Bu sayaç, yerel parça durumu ile ana paket durumunun aynı olduğu anlamına gelmez.

## Görev ayrımı ve eski seçim

Yönetici dahil, ana puantajı hazırlayan, son düzenleyen, dağılımı hazırlayan veya turu gönderen hesap kendi parçasını onaylayamaz. Etkin bağlı ham olayları giren/eşleyen hesap da onaylayamaz. Hesap kimliği karşılaştırılır; aynı gerçek kişinin farklı hesaplarını tespit etme bu dilimde yoktur. İade, yanlış hazırlığı geri çevirebilmek için bu öz onay yasağından ayrı değerlendirilir; yine yerel proje onay yetkisi gerekir.

Sunucu karar için yerel proje üyeliği/CanApproveTimesheets, aktif dağılım kimliği/sürümü, RoundId, parça karar rowversion ve ana puantaj rowversion değerlerini doğrular. İade edilmiş, kapanmış, kaldırılmış veya yeniden gönderilmiş turun eski seçimi yeni tura uygulanamaz. Kişi/dönem, kaynak ve hedef roster, yayınlanmış vardiya, izin ve başka çalışma çakışması kontrolleri onayda sürer. Eski kişi/roster yeniden oluşturulsa da değişmez kimliklere bağlanmış kayıt başka kişiye/döneme taşınmaz.

Kısmi kararda diğer projenin karar sürümü veya ana gövde değiştirilmez. Aynı parçayı iki kişinin eşzamanlı onaylamasında eski karar sürümü reddedilir. Son onay/iade ortak dağılım ve kişi kilidi altında türetilir; yarım aggregate durum bırakılmaz. Aynı CommandId yanıt kaybında aynı aktör/proje/içerikle kurtarılır; farklı içerik reddedilir. Dialog niyeti ve gerekçeyi ilk gönderimde dondurur. Hata sonrası içerik değiştirmek gerekiyorsa kapatıp yeniden açın.

## 1.3.13-dev ve daha eski toplu kayıtlar

125 geçmişteki toplu onayı sahte bağımsız amir onaylarına dönüştürmez. Mevcut dağılımlar **UsesSegmentApproval=0** ile kalır; Approved eski kaydın önceki proje süreleri korunur. Sekmede **Eski toplu onay** veya **Eski toplu gönderim; önce iade** etiketi görünür. Bu kayda yeni parça onayı verilemez.

İlgili proje amiri kendi eski parçasını iade edebilir; başka projelerin hakkı gerekmez. Bu karar LegacyReturn olarak değişmez olay geçmişine yazılır, ana kayıt Returned olur. Hazırlayan kişi yeniden gönderdiğinde dağılım tek yönlü olarak yeni modele geçer ve ilk bağımsız tur açılır. Taslak/iade eski dağılım ilk gönderimde doğrudan yeni modele geçer. Kullanıcı adı/parola, geçmiş aktörler ve eski kayıt içerikleri yeniden yazılmaz.

Yeni hazırlanan dağılımlar baştan bağımsız onay modelindedir. Normal iş akışında model geriye çevrilemez. Eski kararlar, kapanmış turlar, kaldırılmış dağılımlar ve yoksayılmış ham olaylar yeniden etkinleştirilmez.

## Geçmiş, gizlilik ve sınırlar

Güncel karar satırı son durumu tutar; ayrıca her onay/iade ayrı **değişmez karar olayı** olarak eklenir. Aynı amirin onayı sonradan iade edilse de ilk onay aktörü, zamanı ve gerekçesi kaybolmaz. Önce/sonra audit de aynı transaction içindedir. Karar geçmişi API'si yalnız istenen projenin parçalarını okur; kaynak projeyi bilmek başka projedeki gerekçeyi açmaz. Serbest karar gerekçesi audit JSON gövdelerinde maskelenir; proje kapsamlı karar geçmişinde kendi gerekçesi görünür.

Tüm dağılım geçmişi gibi başka proje ayrıntıları içeren ekranların önceki bütün hedeflerde okuma kontrolü korunur. Proje amiri bağımsız karar için bu ekrana muhtaç değildir; kendi parça sekmesi ve kendi karar geçmişini kullanır. Ücret/IBAN, ödeme ve dışa aktarma yetkileri genişletilmez. Parasal hakediş, bakiye, banka veya bordro üretilmez.

Bu sürümde bir iade bütün turun etkinliğini kapatır; yalnız iade parçasını düzeltip diğer onayları aynen taşıma yoktur. Onay turu hazırlanmış değişmez dağılıma aittir; saat/mola/ücret gövdesini düzeltmek için önce iade ve önceki dağılım korumaları uygulanır. Tek amir kararından sonra diğer projelerin adları/ret gerekçeleri gösterilmez; tur kapandığı bilgisi ortak durumdur. Toplu parça onayı, delege/onay vekâleti, çalışan–hesap eşlemesi ve kapanmış mali dönemle hakediş uzlaştırması sonraki işlerdir.

## Veri ve dağıtım

125; dağılımın tek yönlü bağımsız onay işaretini, **TimesheetApprovalRounds**, **TimesheetSegmentApprovals**, **TimesheetSegmentDecisionEvents** tablolarını; tek açık tur/tek karar indekslerini, kaynak/durum/öz onay/değişmezlik ve audit korumalarını ekler. Eski 100–124 betikleri değiştirilmez. Mevcut dağılımlara sahte karar/tur üretilmez; eski onaylar ve iş verileri korunur. Migration oturum nesillerini yeniler; kullanıcı adı/parola ve önceki haklar korunur.

Paket **1.3.14-dev**, API **1.14**, şema **125** birlikte kurulmalı; canlı yükseltme öncesi doğrulanmış yedek gerekir. API migration'ı otomatik uygulamaz. Bu kaynak tesliminde canlı yedek/yükseltme/kurulum veya GitHub yayını yapılmadı; denemeler atlandı. Parça listesi 2000 kayıt, yerel karar geçmişi 1000 olay sınırındadır; tarihsel dağılımları açmak liste sınırını etkileyebilir.
