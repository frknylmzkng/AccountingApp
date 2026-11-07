# Çok kişili toplu manuel puantaj

İzleyen durum: [1.3.17-dev kurulumu](1_3_17_DEV_KURULUMU.md) tamamlandı ve kullanıcı doğruladı. Sonraki kaynak dilimi [tarihli tarife/dönem hesap temelidir](TARIHLI_TARIFE_VE_DONEM_HAKEDISI.md). Aşağıdaki ilk kaynak teslimi notu tarihsel kapsamdır.

2 Kasım 2026 · Kaynak/paket **1.3.17-dev** · Mevcut şema **127** · API **1.17**.

Kullanıcı 1.3.16-dev sürümünü doğruladı. Tek projede, yüklenen haftada birden fazla kişiye ait yeni vardiyalı ve vardiyasız çalışma satırlarını aynı önizlemede hazırlama eklendi. Yeni şema betiği yoktur; 100–127 değiştirilmedi. Canlı veritabanı, kurulu 1.3.16-dev istemci/API ve masaüstü kısayolu bu kaynak tesliminde değiştirilmedi. Denemeler kullanıcı isteğiyle atlandı.

## Kullanım

1. Projeden **Puantaj ve amir onayı…** ekranını açın, haftayı yükleyin. **Yayınlanmış vardiya atamaları → Çok kişili manuel giriş…** düğmesini kullanın. Güncel proje puantaj giriş hakkı gerekir.
2. **Yayınlanmış atamalardan ekle** sekmesinde gün filtresi ve Ctrl/Shift ile birden fazla atama seçin. **Seçili atamaları ekle** ile önizlemeye alın. Puantajı bulunan atamalar sunulmaz; önizlemeye eklenenler de aday listesinden çıkar. Plan saatleri yalnız referans olarak görünür, gerçekleşen alanlara kopyalanmaz.
3. Vardiyasız çalışan için **Vardiyasız kişi ekle…** düğmesini kullanın. Proje personeli aramasından kişiyi, özgün tarihli proje/ekip atamasını ve yüklenen haftadaki iş gününü seçin. Atama ilk aşamada iş gününü kapsamalıdır; kayıtta tüm gerçek çalışma aralığı ayrıca doğrulanır.
4. **Kaydedilecek satırlar / önizleme** sekmesinde bir veya birden fazla satır seçip **Seçili satırları gözden geçir…** düğmesini kullanın. Ortak saat uygulamak için seçili satırların iş günü aynı olmalıdır. Gerçek giriş/çıkış, mola ve ücretlendirilecek dakikayı açıkça girip önizlemeye uygulayın. Gece çıkışında ertesi günün tarihini seçin. Tek satırda çift tıklamak da düzenlemeyi açar.
5. Birden fazla satıra uygulama, seçili satırların önceki gerçekleşen alanlarının yerini alır; diğer satırlar değişmez. Saatler bilinmiyorsa işaretleri kaldırarak bilinçli biçimde eksik taslak bırakabilirsiniz. Önizlemeye eklemek gözden geçirilmiş sayılmaz; bütün satırların bu adımda açıkça kabul edilmesi gerekir.
6. Önizlemeyi ve bilgi sütununu kontrol edin. İstenmeyen satırları çıkarın. **Kaydet** ve ortak gerekçeyle en fazla **200 satırın tamamı** aynı SQL transaction içinde taslak olarak kaydedilir. Bir satırın hedefi, sürümü veya iş kuralı reddedilirse paket tamamıyla geri alınır; başarılı görünen ilk satırlar veritabanında kalmaz.

Paket satır numarası, tür içinde verilmiştir: **Vardiyalı #3**, **Vardiyasız #2** gibi. Servis doğrulama hatalarında aynı numara gösterilir. Tabloyu sıralamak hedefi veya paket içindeki özgün kimlikleri değiştirmez. Önizlemedeki bilgi sütunu eksik saat, ters saat, mola/net süre, uzun çalışma veya ücret dakikası gibi durumları gösterir; sunucudaki güncel doğrulamanın yerine geçmez.

## Kayıt kapsamı ve korumalar

Bu ekran yeni kayıt içindir. Başka kullanıcının mevcut puantajının üzerine toplu yazma yapmaz; mevcut kayıt listeden özgün sürümüyle düzenlenir. Aynı vardiya ataması veya vardiyasız kişi/iş günü aynı pakete iki kez eklenemez. Farklı günler ve farklı kişiler aynı pakette bulunabilir; paket tek projeye aittir. Toplu ekran üzerinden mevcut kişilerin yeni işe giriş dönemi, proje ataması veya vardiyası oluşturulmaz.

Vardiyalı satırlar mevcut kayıt servisinin özgün atama/vardiya sürümleriyle; vardiyasız satırlar özgün PersonId, EmploymentId, personel/grup ve tarihli atama sürümleriyle doğrulanır. Kişi seçiminden sonra personel sürümü değişmişse güncel sürüm sessizce benimsenmez. Tarihli atama, etkin dönem, arşiv, proje/ekip ve kaynak tekilliği kontrolleri mevcut ortak kayıt yollarında kalır.

Eksik veya hatalı saatler, önceki puantaj akışındaki gibi taslak istisnası olabilir; bu ekran bunları otomatik onaylı çalışma saymaz. Gönderim aşamasındaki gerçek aralık/çakışma/izin ve amir onayı kontrolleri sürer. Kaydetmek onaya göndermez, amir onayı vermez, hakediş veya ödeme üretmez. Ücretlendirilecek **dakika** tutar değildir; plan süresinden otomatik ücret veya mola türetilmez.

## Kalıcı taslak ve belirsiz sonuç

Toplu manuel giriş, [kalıcı taslak kurtarma](KALICI_PUANTAJ_TASLAK_KURTARMA.md) altyapısına bağlandı. Eklenen/çıkarılan satırlar, önizlemeye uygulanmış değerler, gözden geçirme durumu, özgün kaynak sürümleri ve kayıt kimlikleri şifreli saklanır. Henüz **Alanları önizlemeye uygula** ile kabul edilmemiş açık alan düzenleme penceresinin ara değerleri toplu taslağa dahil değildir.

Yeniden aynı proje ve haftanın toplu ekranını açıp taslağı açıkça kurtarın. Vardiya atamalarının başlangıç kapsamı/sürümleri karşılaştırılır; vardiyasız personel/dönem/atamalar güncel salt okunur sorguyla yeniden kontrol edilir. Değişmiş seçimler yeni sürümlerle değiştirilmez. Kurtarmak sunucuya yazmaz. Uygulanamayan taslak ayrıntıları, kurtarma listesinden salt okunur incelenebilir.

Tek dış CommandId ve içerik özeti bütün paket için geçerlidir; vardiyasız satırların alt bilgileri ayrı işlem makbuzu üretmez. Gönderimden önce tam istek dayanıklı saklanır. Yanıt kaybolursa yeniden başlatma sonrasında aynı kimlik/içerikle sonuç sorgulanır veya tekrar gönderilir; yeni bir paket kimliği üretilmez. 127'deki kalıcı vazgeçme makbuzu toplu paketi de kapsar; gecikmiş eski istek yazamaz. Zaten kaydedilmiş paketten vazgeçmek iş kayıtlarını geri almaz, yalnız yerel kurtarma dosyasını temizler.

## Dağıtım ve sınırlar

Yeni API uç noktası `timesheets/manual-batch`, mevcut oturum/proje yetkisi ve kayıt servislerini kullanır. Vardiyasız tekli ve toplu kayıt aynı satır uygulama metodunu paylaşır; tek transaction/makbuz dış katmanda tutulur. İstemci ve API birlikte **1.3.17-dev / 1.17** olarak güncellenmelidir; mevcut şema 127 yeterlidir. Bu teslimde kurulum, GitHub push veya Release yapılmadı.

UI, SQL, eşzamanlılık, ağ kesintisi ve işlev denemeleri yapılmadı. Derleme ve paket bütünlüğü kontrolü çalışma zamanı davranışının doğrulanması değildir. Aşama 4 bu kaynak teslimiyle saha kabulü tamamlanmış sayılmaz; çalışan self servis, cihaz entegrasyonları ve daha geniş pilot doğrulamaları açık kalır. Ücret tarifesi/dönem hakedişi sonraki aşamadır.
