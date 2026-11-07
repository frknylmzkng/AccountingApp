# Ham giriş/çıkış olayları ve kontrollü puantaj eşlemesi

2 Kasım 2026 kaynak güncellemesi: [gün içi çok proje / mola dağılımı](GUN_ICI_PROJE_VE_MOLA_DAGILIMI.md), 1.3.13-dev / hazırlanmış 124 / API 1.13 ile eklendi. Ham olaya bağlı ana puantaj dağıtılabilir; kaynak saat/olay değişikliği için önce dağılım kaldırılır. Canlı 1.3.12-dev/123 korunuyor, denemeler atlandı. Aşağıdaki sonraki dilim notları ilk kaynak tesliminin tarihsel kapsamıdır.

2 Kasım 2026 · Kaynak/paket **1.3.12-dev** · Şema **123** · API **1.12**.

Kurulum güncellemesi: doğrulanmış yedek sonrası 123 uygulandı; istemci/API kuruldu ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Mevcut veriler/ayarlar korundu, denemeler atlandı. [Kurulum kaydı](1_3_12_DEV_KURULUMU.md). Aşağıdaki kaynak teslimi notu kurulum öncesini anlatır.

Kullanıcı 1.3.11-dev sürümünün çalıştığını doğruladı. Aşama 4'ün ham olay ve kontrollü eşleme dilimi eklendi. Canlı veritabanı, kurulu 1.3.11-dev istemci/API, şema 122 ve Muhasebe - Güncel kısayolu değiştirilmedi. Kullanıcı isteğiyle SQL/işlev/CSV/UI/eşzamanlılık denemeleri çalıştırılmadı. Derleme ve paket bütünlüğü kontrolü işlev senaryosu doğrulaması değildir.

## Temel ayrım

**Ham olay**, açık In/Out yönü ve özgün zamanı olan giriş/çıkış kanıtı kaydıdır; tek başına çalışma süresi, mola, hakediş veya onay oluşturmaz. **Eşleme**, kullanıcının seçtiği bir In ve bir Out olayını mevcut taslak/iade puantaja bağlar. Yalnız eşleme yapıldığı için onay, ödeme veya hakediş doğmaz.

Olayın kişi, grup, çalışma dönemi, proje/ekip ataması, kaynak/satır kimliği, yönü, yerel/UTC zamanı ve saat dilimi değişmez. Silme veya saat düzeltme yapılmaz; yanlış kullanılmamış olay gerekçeli **yoksayılır** ve doğru kayıt yeni kaynak kimliğiyle girilir. Yoksayma geri alınmaz; aynı kaynağı tekrar almak eski olayı yeniden etkinleştirmez.

Eksik çıkış varsa sonraki giriş Out'a çevrilmez. Ardışık iki In/Out kendiliğinden eşlenmez. Kullanılmamış In için çıkış/eşleme, kullanılmamış Out için giriş/eşleme beklediği gösterilir. Bu etiket tek başına karşı olayın fiziksel olarak bulunmadığı iddiası değildir.

## Olay girişi ve CSV

1. Proje üzerinden **Puantaj ve amir onayı…** ekranında haftayı yükleyin. **Ham giriş / çıkış olayları…** açın. Liste haftayı ve gece çıkışlarını görebilmek için sonraki günün olaylarını kapsar.
2. **Tek ham olay…** veya **CSV ham olay / önizle…** kullanın. Proje havuzundan kişiyi ve güncel dönemindeki tarihli proje/ekip atamasını açıkça seçin. TC ile veya serbest cihaz kart numarasıyla otomatik kişi eşleme yapılmaz.
3. Kaynak sistem adını sabit tutun: örneğin `saha-A-cihaz-01`. **Kaynak sistem + dış olay kimliği veritabanı genelinde, projeler arasında da tekildir ve büyük/küçük harf duyarlıdır.** Bir cihazın olayını başka projeye kopyalamak çoğaltma yolu değildir. Kaynak adını yalnız tekrar denemeyi aşmak için değiştirmeyin.
4. Manuel kayıtta kaynak olay kimliğini, açık Giriş/In veya Çıkış/Out yönünü ve gerçek yerel zamanı girin. Yön zorunludur; sıra sayısından tahmin edilmez.
5. CSV önizlemesinde kişi/atama dosyanın tamamı için seçilidir. UTF-8, noktalı virgül, en çok **200 satır / 1 MB**; tek kişi/atama dosyasıdır. Başlık sırası şöyledir:

```text
ExternalId;Direction;OccurredLocal
```

Yön tam `In` veya `Out` olmalı. Zaman İstanbul yerel saatiyle `yyyy-MM-dd HH:mm`, `yyyy-MM-dd HH:mm:ss` veya `yyyy-MM-dd HH:mm:ss.FFFFFFF` biçimindedir; en çok yedi kesir basamağı korunur. Offset/Z içeren zamanlar bu sözleşmede kabul edilmez; belirsiz/geçersiz saat dilimi anları reddedilir. Tarih aralığı 1900–2199. UTF-8 BOM kabul edilir. Eksik/yanlış başlık, yön, zaman veya dosya içi tekrar reddedilir.

Önizlemeyi kontrol edip Kaydet ile gerekçe verin. Sunucu kaynak kişi/roster rowversion ve çalışma dönemi/aralık kontrollerini yapar. Bir satır hatalıysa bütün yeni olaylar ve komut kaydı transaction ile geri alınır. Olayın tarihi, seçilen etkin dönem/proje/ekip atamasının kapsamında olmalı. Gece tam 00:00 Out olayı önceki kapalı aralığın bitişi olarak değerlendirilebilir; In aynı anda yeni güne aittir.

Aynı kaynak kimliği ve aynı kişi/dönem/atama/yön/zaman tekrar geldiğinde çoğalmaz; farklı içerik reddedilir. Kaynak kimliği başka projede kullanılmışsa diğer projenin ayrıntıları açıklanmaz ve kayıt kopyalanmaz. Aynı CommandId yanıt kaybında aynı içerikle kurtarılır; aktör/proje/içerik özeti farklıysa reddedilir. Dialog ilk gönderimden sonra niyeti/gerekçeyi dondurur. Doğrulama hatasından sonra içeriği değiştirmek için kapatıp yeniden açın.

## Kontrollü eşleme ve dakika politikası

Önce mevcut [vardiyalı](PUANTAJ_VE_AMIR_ONAYI.md) veya [vardiyasız](VARDIYASIZ_PUANTAJ.md) puantaj taslağını oluşturun. Saatler eksik olabilir. Puantaj listesinde ilgili **Draft/Returned** kaydını seçip ham olay ekranını açın; hedef kişi, gün ve vardiya başlıkta görünür. Hedefin özgün seçim sürümü saklanır; başka operatörün değişikliğini sessizce benimsemez.

**Seçili puantaja olay eşle…** ekranında uygun In ve Out olaylarını ayrı ayrı seçin. Yalnız aynı proje, PersonId, grup, EmploymentId ve roster kimliğindeki, yoksayılmamış/kullanılmamış olaylar listelenir. In olayının yerel günü puantajın değişmez iş günüyle aynı olmalı; Out geceyi aşabilir. Aynı olay birden fazla etkin eşlemede kullanılamaz. Bir puantajın tek etkin eşlemesi bulunur.

**EventMinuteFloor.v1** açık eşleme politikası kullanılır: ham saniye ve kesirler yerinde korunur; puantaj tam dakika modeli için iki uçta da dakikadan küçük bölüm aşağıya atılır. Örneğin `08:59:45.123 → 17:00:20.500` olayları, kullanıcı önizlemeyi kabul ettiğinde `08:59 → 17:00` puantaj saatlerine bağlanır. Bu ücret, fazla mesai veya mevzuat kuralı değildir. Ham zaman, oluşacak giriş/çıkış, UTC brüt/net dakika, gerçek mola ve ayrı ücretlendirilecek dakika ekranda kontrol edilerek **Kaydet** ile uygulanır.

Giriş Out'tan önce olmalı; yuvarlama sonucunda sıfır/ters veya 24 saatten uzun süre, neti tüketen mola veya neti aşan ücret dakikası kabul edilmez. Ücret dakikası belirtilmeden eşlenmiş taslak tutulabilir; mevcut gönderim/onay kontrolleri bu eksiği engeller. Eşleme eski taslak saatlerini açıkça gösterip yeni olay saatleriyle değiştirir; taslak/iade durumunu korur. Geçerli kişi/dönem/roster, özgün sürümler ve kaynak vardiyanın yayın durumu yeniden kontrol edilir. Çakışma, izin ve diğer istisnalar puantaj listesinde görünür ve gönderim/onayda yeniden doğrulanır.

Eşleme sonrası ham olay ekranında hedef sürümünü otomatik yenileyerek yeni bir niyet üretilmez. Yeni eşleme için ekranı kapatıp güncel puantajı yeniden seçin. Puantaj listesinde **Ham olay bağlı** sütunu vardır; bağlı kaydın manuel düzenleme/iptal düğmeleri devre dışıdır. Ham kaynak saatini korumak için önce eşleme kaldırılmalıdır. Normal gönderim/onay/iade akışı devam eder.

## İade, kaldırma ve yanlış olay

- Onaylı veya gönderilmiş kaydın eşlemesi doğrudan değiştirilemez. Ayrı yetkili hesap önce yeniden açmalı/iade etmelidir.
- **Eşleme geçmişi → Seçili eşlemeyi kaldır…**, Draft/Returned puantaj ve özgün puantaj/eşleme sürümünü ister. Kullanıcı gerekçeyi verip inceleme ekranında Kaydet seçer.
- Kaldırmada olaylar ve bağlantı geçmişi silinmez; eşleme kaldırma aktörü/zamanı saklanır. **Puantajın giriş/çıkış, UTC saatleri, mola ve ücret dakikası temizlenir.** Böylece kaynak saatleri serbest bırakılırken eski taslakta ikinci bir bağımsız çalışma iddiası kalmaz. Eski değerler audit'te korunur. Bu kayıt eksik taslak olarak kalır; yeniden eşlenebilir, manuel düzeltilip yeniden gönderilebilir veya gerekçeli iptal edilebilir.
- Kullanılmayan yanlış olay **Seçili olayı yoksay…** ile kapatılır. Etkin eşlemeli olay önce serbest bırakılmalıdır. Düzeltmede eski kimliği yeniden kullanarak başka kişi/saat/döneme taşımayın; yeni kimlikle düzeltme kaydı girin.
- Aynı kişinin vardiyalı/vardiyasız çalışma, izin ve takvim çakışma korumaları sürer. Ham olay eşlemesi bu kontrollerin yerine geçmez; onaysız kayıt hakediş veya ödeme üretmez.

## Yetki, görev ayrımı ve geçmiş

Yeni hak verilmedi: **CanEnterTimesheets** olay aktarımı, eşleme, kaldırma ve yoksayma için; **CanApproveTimesheets** puantaj onay/iade/yeniden açma için kullanılır. Okuma mevcut proje kapsamındadır. Üyelik/oturum ve haklar sunucuda canlı denetlenir. Ücret/IBAN/ödeme/dışa aktarma izinleri genişletilmez. TC, IBAN, biyometri, konum veya ham cihaz yükü saklanmaz. Tablo kopyası mevcut yetkili dışa aktarma yolunu kullanır.

Yönetici dahil puantajı hazırlayan, son düzenleyen veya gönderen hesap kendi kaydını onaylayamaz. **Etkin bağlı ham olayları giren ya da eşleyen hesap da onaylayamaz.** Hesap kimliğiyle görev ayrımı uygulanır; aynı gerçek kişinin farklı hesaplarını tespit etme veya çalışan–hesap eşlemesi bu dilimde yoktur.

Olay ve eşleme gövdeleri SQL trigger'larıyla korunur. Kaynak kullanım tekilliği filtreli indekslerle, ilişki/kaynak saatleri kontrolleriyle ve transaction ile sağlanır. Eşleme + puantaj saatleri + audit + kalıcı komut tek transaction içinde saklanır. Kaldırma + saat temizliği + audit + komut da atomiktir. Kaynak/satır serbest metni audit'te maskelenir; olay/eşleme/kişi/dönem kimlikleri ve zaman/politika bilgisi korunur. Yetkili yönetici işlem geçmişinden önce/sonra değerlerini okuyabilir. Kaldırılmış eşlemenin tarihsel saatleri değişmez olaylardan ve kayıtlı politikadan hesaplanır; güncel puantaj saatleri diye sunulmaz.

## Şema, dağıtım ve sınırlar

123; **AttendanceEvents**, **AttendanceMatches**, anahtar/kısıt/indeks, üç koruma ve iki audit trigger'ı ile güvenli audit alan listesi genişlemesini ekler. Mevcut puantaj saatlerinden sahte ham olay üretilmez, eski çalışma/para verileri yeniden yazılmaz. 100–122 betikleri değiştirilmez. Oturum nesilleri yükseltmede yenilenir; kullanıcı adı/parola ve mevcut haklar korunur. Paket **1.3.12-dev**, API **1.12**, şema **123** birlikte kullanılmalıdır; API migration'ı otomatik uygulamaz. Canlı yükseltme öncesi doğrulanmış yedek gerekir.

Bu teslimde canlı yedek/yükseltme/kurulum, GitHub push veya Release yapılmadı. Denemeler atlandı. Haftalık okuma her liste için 2000 kayıtla sınırlıdır; daha büyük aktarım için bölünmüş dosya veya ileride sayfalama gerekir.

Fiziksel cihaz SDK'sı, webhook/otomatik arka plan alma, kart numarası–PersonId eşlemesi, çok kişili CSV, UTC offset'li olay sözleşmesi, otomatik çift önerisi, vardiya iş gününden önceki güne kayan giriş eşlemesi ve farklı yuvarlama politikası bu dilimde yoktur. Manuel / tek kişilik CSV alımı ve açık seçimle eşleme vardır. Sonraki Aşama 4 işi gün içi çok proje zaman parçaları ve mola dağıtımıdır; çalışan hesabı eşlemesi, kalıcı taslak kurtarma ve toplu manuel giriş de açık kalır. Aşama 5 ücret tarifesi/hakediş işi henüz tamamlanmış sayılmaz.
