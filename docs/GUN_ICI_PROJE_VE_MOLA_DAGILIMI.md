# Gün içi çok proje zaman parçaları ve mola dağıtımı

2 Kasım 2026 kaynak güncellemesi: [bağımsız proje parçası onayı](BAGIMSIZ_PROJE_PARCASI_ONAYI.md), 1.3.14-dev / hazırlanmış 125 / API 1.14 ile eklendi. Onay/iade proje bazında bağımsız, gönderim ve dağılım hazırlığı bütün hedeflerde giriş hakkıyla yürür. Canlı 1.3.13-dev/124 korundu, denemeler atlandı. Aşağıdaki tek paket onay yetkisi ve sonraki iş notları ilk 1.3.13-dev tesliminin tarihsel kapsamıdır.

2 Kasım 2026 · Kaynak/paket **1.3.13-dev** · Şema **124** · API **1.13**.

Kurulum güncellemesi: doğrulanmış yedek sonrası 124 uygulandı; istemci/API kuruldu ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Eski veriler/ayarlar korundu, mevcut atamalar asıl kaldı; denemeler atlandı. [Kurulum kaydı](1_3_13_DEV_KURULUMU.md). Aşağıdaki kaynak teslimi notu kurulum öncesini anlatır.

Kullanıcı 1.3.12-dev sürümünün çalıştığını doğruladı. Aşama 4'ün gün içi proje/ekip süre dağılımı dilimi eklendi. Bu kaynak tesliminde canlı veritabanı, kurulu istemci/API 1.3.12-dev, şema 123 ve Muhasebe - Güncel kısayolu değiştirilmedi. SQL/işlev/CSV/UI/eşzamanlılık denemeleri kullanıcı isteğiyle atlandı. Derleme ve paket bütünlüğü kontrolü iş akışı kabulü değildir.

## Model: tek çalışma, birden fazla proje payı

Ana puantajın gerçek giriş/çıkışı, molası, ücretlendirilecek dakikası ve çalışma dönemi tek kaynak olarak kalır. Bu kayıt vardiyalı, vardiyasız veya ham olaylara bağlı olabilir. **Zaman parçaları**, bu aralığın hangi dakika aralıklarının hangi proje/ekip atamasına ait olduğunu ve toplam molanın bu parçalara nasıl dağıldığını açıkça kaydeder. Sahte puantaj/ham olay, ikinci bakiye veya hakediş üretilmez.

Parçalar ana aralığı tam, boşluksuz ve çakışmasız kapsar. Başlangıç dahil, bitiş hariçtir. Her parçanın brüt, mola, net ve ücretlendirilecek dakikası ayrıdır. Mola toplamı ana mola, ücret dakikası toplamı ana ücret dakikasıyla tam eşleşir. Mola otomatik oranlanmaz; yuvarlama/artık dakika dağıtımı tahmin edilmez. Bir parça tümüyle mola olabilir (net/ücret sıfır). Mola **dakika dağılımıdır**; molanın hangi kesin dakika aralığında kullanıldığı bu sürümde ayrıca saklanmaz.

Örnek: ana çalışma 08:00–17:00, brüt 540 dk, mola 60 dk, net ve ücretlendirilecek süre 480 dk.

| Proje | Parça | Brüt dk | Mola dk | Net / ücret dk |
|---|---|---:|---:|---:|
| A | 08:00–12:00 | 240 | 30 | 210 |
| B | 12:00–17:00 | 300 | 30 | 270 |
| Toplam | 08:00–17:00 | 540 | 60 | 480 |

A ve B'ye aynı aralık yazılamaz; 11:00–12:00 gibi ortak dakika reddedilir. 12:00–13:00 boş bırakılıp mola sayılmaz; bu süre de bir parçanın brütünde yer almalı, mola dakikası açıkça dağıtılmalı. Her parçanın ücret dakikası netini aşamaz; toplam ücret dakikası netten düşükse fark da parçalar arasında açıkça paylaşılır.

## Ön koşul: asıl ve gün içi ek atama

Eski tarihli asıl proje/ekip ataması ve çakışma kuralı korunur. **Proje / ekip / personel atamaları** ile **Projelerim** atama girişine **Gün içi ek proje/ekip ataması** seçeneği eklendi. Eski satırlar varsayılan olarak asıl kalır; geçmiş sınıflandırılmaz.

İki farklı projede aynı gün çalışılacaksa güncel EmploymentId altında A'daki asıl atamaya ek olarak B'de tarihli ek atama hazırlanır. Ek atama asıl atamayı değiştirmez veya kişiyi yeni döneme taşımaz. Aynı proje/ekipte çakışan mükerrer atama reddedilir; ayrı ekiplere ek atama mümkün olabilir. Gerçek vardiya/puantaj saat çakışmaları ortak kişi kimliğiyle ayrıca korunur. Atama türü sonradan değişmez; gerekçeli iptal ve yeni atama kullanılır.

Kısıtlı proje kullanıcısı yine yalnız kendi projesinin güncel kişi havuzundan atama yapabilir. B projesine henüz tanıtılmamış kişiyi ilk kez eklemek merkezi yetkili yönetici işlemidir; bu seçenek küresel personel dizinini açmaz. Ek atama kendi başına ödeme, ücret/IBAN okuma veya puantaj onay hakkı vermez; mevcut proje/hesap hakediş ve ödeme kontrolleri sürer.

## Dağılımı hazırlama

1. Kaynak projede **Puantaj ve amir onayı…** ekranından ana taslak/iade puantajı seçin. Gerçek saat, mola ve ücret dakikası tamamlanmış ve geçerli olmalı. İş günü değişmez.
2. **Proje / mola dağılımı… → Yeni dağılım / önizle…** açın. Hedef listesinde yalnız bu kişinin aynı dönemindeki etkin proje/ekip atamaları ve kullanıcının puantaj giriş hakkı olan projeler görünür. Eksik hedefi önce hazırlayın.
3. Her parça için hedef proje/atama, başlangıç/bitiş, bu parçaya ait mola ve ücret dakikasını girip **Parçayı önizlemeye ekle** kullanın. Parçalar başlangıca göre gösterilir. Yanlış önizleme satırını çıkarıp yeniden ekleyebilirsiniz. İlk hedef kendiliğinden seçilmez; en fazla 50 parça.
4. Önizlemede brüt/net ve ana mola/ücret toplamlarıyla karşılaştırmayı inceleyin. Tam kapsama, çakışma, sürüm, kişi/dönem, proje/ekip etkinliği ve bütün tarih aralıkları sunucuda tekrar kontrol edilir.
5. Kaydet ile gerekçe verin. Dağılım sürümü ve değişmez parçaları tek transaction içinde tamamlanır; bir satır hatasında hiçbiri kalmaz. Ana puantajın saat/mola/ücret gövdesi değişmez; düzenleyen aktör ve rowversion yenilenir. Onay verilmez.

Ana puantajın özgün RecordVersion, PersonVersion ve kaynak roster sürümü; her hedefin proje ve roster sürümü istek içinde taşınır. Eski seçim yeniden oluşturulan kayıt/döneme uygulanamaz. Komut kimliği, aktör/proje/içerik özeti ve receipt aynı transaction içinde saklanır. Yanıt kaybında aynı dondurulmuş istek/gerekçe yeniden gönderilir; ikinci dağılım oluşmaz. Doğrulama hatası sonrası içeriği değiştirmek için dialog kapatılıp yeniden açılır.

## Görev ayrımı: bütün projeler için tek onay paketi

Bu sürümde parçalar bağımsız puantajlar değildir; ana kayıtla **tek atomik onay paketi** oluşturur.

- Hazırlama ve kaldırma için kaynak ve bütün hedef projelerde **CanEnterTimesheets** gerekir.
- Onaya gönderim için kaynak ve bütün hedeflerde giriş hakkı; onay/iade/yeniden açma için bütün ilgili projelerde **CanApproveTimesheets** gerekir.
- Bir proje amiri yalnız kendi parçasını onaylayıp diğerlerini dolaylı onaylayamaz. Yalnız B yetkisi olan kullanıcı B parçasını görebilir; bütün paketin onayı kaynak projede tüm hakları olan hesap tarafından yapılır. Ayrı amirlerin parça bazında bağımsız onayı sonraki dilimdir.
- Hazırlayan, son düzenleyen veya gönderen hesap kendi kaydını onaylayamaz. Ham olay bağlıysa olayları giren/eşleyen hesap için de eski görev ayrımı sürer. Dağılımı hazırlayan son düzenleyen olarak kaydedilir.
- Kaynak ana puantajın okuması mevcut kaynak proje kapsamını korur. Diğer proje adlarını/parçalarını içeren tam dağılım geçmişi için geçmişteki bütün hedeflerde de okuma hakkı gerekir. Yalnız kaynak okuması diğer proje ayrıntılarını açmaz.

Gönderim/onayda bütün hedeflerin güncel kişi, dönem, roster, proje/ekip ve tarih kapsamı tekrar kontrol edilir. Tarihi veya iptal edilmiş hedef atamasıyla eski dağılım onaylanamaz. Mevcut ortak kişi kilidi ve diğer puantaj/izin/vardiya çakışma kontrolleri sürer; dağılım aynı dakikayı ikinci gerçek çalışma olarak üretmez.

## Liste, geçmiş ve proje özetleri

Ana listede **Projelere dağıtıldı** işareti vardır. **Ana puantaj net/ücret dk** sütunları bütün kaynak çalışmayı gösterir. **Onaylı proje net/ücret dakikası** özetleri farklı anlam taşır: dağılım yoksa ana kayıt, etkin tamamlanmış dağılım varsa yalnız o projeye ait parçalar SQL'de toplanır. Dağıtılmış ana kayıt tam süreyle tekrar sayılmaz. Taslak/gönderilmiş/iade kayıtları onaylı süre toplamına girmez.

**Projeye ait zaman parçaları** sekmesi yalnız seçili projenin parçalarını, kendi ekip, zaman, mola/net/ücret dakikası ve güncel puantaj durumuyla gösterir. Diğer projelerin adları, parçaları veya tam kaynak saatleri bu sekmede açıklanmaz. Sekme salt okunurdur; onay kaynak puantaj akışındadır. Gece parçaları gerçek başlangıç/bitişleriyle görünür; hafta gruplaması ana puantajın iş gününe göredir, yeni güne tekrar yazılmaz.

Dağılım geçmişinde bir sürüm seçilince parçaları ayrı sekmede görünür. Tamamlanmış gövde veya parçalar yerinde değiştirilmez. Önce kaldırma, sonra güncel ana kaydı yeniden seçip yeni dağılım gerekir. Eski sürümler, önce/sonra audit ve gerekçeler korunur. Hedef proje/ekip isimleri güncel kayıtlardan okunur; sürüm ve atama kimlikleri teknik snapshot olarak tutulur.

## Düzeltme ve ham olay ilişkisi

Onaylı/gönderilmiş kayıt önce bütün projelerde yetkili hesapla iade edilmeli/yeniden açılmalı. **Seçili dağılımı kaldır…** özgün dağılım ve güncel puantaj sürümünü ister. Kaldırma parçaları silmez; kaldırma aktörü, zamanı ve gerekçesi korunur. Ana saat/mola/ücret gövdesi temizlenmez. Dağılım kalkınca kaynak saat düzenleme veya yeni dağılım yapılabilir; ana kayıt hâlâ taslak/iadedir ve yeni onay gerekir.

Etkin dağılım varken ana saat, mola, ücret dakikası, ham olay eşlemesi/kaldırması veya iptal değişikliği engellenir; önce dağılım kaldırılmalı. Ham olay bağlıysa ondan sonra gerekirse olay eşlemesi de kaldırılır. Önceki ham olay kaldırma kuralı (saatleri temizleme) değişmedi. SQL korumaları da bağlı dağılım gövdesinin bozulmasını önler.

Parçaya bağlı roster iptali, parçaları dışarıda bırakan proje tarih değişikliği, bekleyen parçaların proje/ekip pasife alınması engellenir. Çalışma dönemi ve grup geçişleri ana çalışma aralığı/atamalar üzerinden korunur. İşlemler ortak dağılım kapısı ve kişi kilidiyle transaction içinde yürür; bu ilk sürüm yüksek eşzamanlılık için performans iddiası taşımaz.

## Veri ve dağıtım

124, PersonnelProjectAssignments.IsSupplementary varsayılan 0 alanını; **TimesheetAllocationSets / TimesheetSegments** tablolarını, değişmezlik/tamamlama/gövde/audit trigger'larını ve indekslerini ekler. Tamamlama, bütünlük toplamlarını ve kaynak gövdeyi SQL'de doğrular; tamamlanmış sete yeni parça eklenemez. Güvenli audit listesi teknik dağılım alanları için genişler. Eski 100–123 betikleri değiştirilmez; mevcut satırlara dağılım uydurulmaz veya atama türü değiştirilmez. Oturum nesilleri yükseltmede yenilenir; kullanıcı adı/parola ve önceki haklar korunur.

Paket **1.3.13-dev**, API **1.13**, şema **124** birlikte kurulmalı. Canlı yükseltme öncesi doğrulanmış yedek gerekir; API kendiliğinden migration uygulamaz. Bu kaynak tesliminde canlı yedek/yükseltme/kurulum veya GitHub yayını yapılmadı. Denemeler atlandı.

## Açık kalan işler

Ücret tarifesi, onaylı sürelerden parasal hakediş/maliyet üretimi, banka ödemesi ve resmi bordro bu adımda yoktur. Mevcut finans/ödeme defterine yazılmaz. Bağımsız proje amiri parça onayı, gerçek mola başlangıç/bitiş olayları, otomatik oransal dağıtım, toplu CSV dağılımı, çalışan–hesap eşlemesi, kalıcı taslak kurtarma ve klavye ile çok satırlı giriş açık kalır. Haftalık parça listesi 2000, hedef listesi 500, tam dağılım geçmişi 1000 sürüm sınırındadır. Aşama 4 bütünüyle tamamlanmış sayılmaz.
