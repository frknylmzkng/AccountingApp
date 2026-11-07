# Vardiyasız puantaj

2 Kasım 2026 kaynak güncellemesi: [ham olay ve kontrollü puantaj eşlemesi](HAM_OLAYLAR_VE_PUANTAJ_ESLEMESI.md), 1.3.12-dev / hazırlanmış 123 / API 1.12 olarak eklendi. Canlı 1.3.11-dev/122 ve kısayol korundu; denemeler atlandı. Aşağıdaki ham olaylara ilişkin açık iş notları ilk kaynak tesliminin tarihsel durumudur.

1 Kasım 2026 · Kaynak/paket **1.3.11-dev** · Şema **122** · API **1.11**.

2 Kasım 2026 kurulum güncellemesi: doğrulanmış yedek sonrası 122 uygulandı; ilk dinamik SQL sözdizimi hatası düzeltilip paketler yeniden üretildi. İstemci/API ve Muhasebe - Güncel kısayolu 1.3.11-dev sürümüne geçirildi. Veriler/ayarlar korundu, denemeler atlandı. [Kurulum kaydı](1_3_11_DEV_KURULUMU.md). Aşağıdaki kaynak teslimi notu kurulum öncesini anlatır.

Kullanıcı 1.3.10-dev sürümünün çalıştığını doğruladı. Aşama 4'ün izleyen dilimi olan, önceden vardiya planı bulunmayan gerçek çalışmanın tarihli proje/ekip atamasına kaydı eklendi. Bu kaynak tesliminde canlı veritabanı, kurulu 1.3.10-dev istemci/API, şema 121 ve masaüstü kısayolu değiştirilmedi. Denemeler kullanıcı isteğiyle atlandı.

## Kullanım

1. **Projeler / ekipler / tarihli atamalar** veya **Projelerim** üzerinden projeyi ve **Puantaj ve amir onayı…** ekranını açın; çalışmanın başlangıç gününü içeren haftayı yükleyin.
2. **Puantaj / istisnalar → Vardiyasız çalışma…** düğmesini kullanın. Yalnız seçili projenin güncel çalışma dönemi havuzundaki kişiyi seçin. TC girişi veya tüm personel dizini açılmaz.
3. Kişinin tarihli proje/ekip atamasını seçin. Atama, güncel çalışma dönemi, etkin proje ve ekip çalışmanın tamamını kapsamalı. Atama yoksa önce onu hazırlayın; sistem sahte vardiya/atama oluşturmaz.
4. İş gününü ve gerçek giriş/çıkışı İstanbul saatiyle tam dakika girin. Bilinmeyen saatin işaretini kaldırabilirsiniz. İlk görünen 09:00/17:00 değerleri işaretli değildir; kendiliğinden gerçekleşen saat sayılmaz. Gece çalışmasında çıkışın ertesi gününü açıkça seçin.
5. Gerçek molayı ve ayrı ücretlendirilecek dakikayı girip gerekçeyle taslak kaydedin. Net süre UTC çıkış–giriş farkından mola düşülerek hesaplanır. Ücret/hakediş üretilmez; otomatik fazla mesai/mevzuat katsayısı yoktur.
6. Haftalık listede **Çalışma türü: Vardiyasız** görünür. Taslağı sürümlü düzenleyip onaya gönderin. Hazırlayan / son düzenleyen / gönderen hesaptan farklı yetkili hesap onaylar veya iade eder. Onaylı kayıt önce yeniden açılır, sonra düzeltilip yeniden onaya gönderilir.

İş günü girişin günüdür; gece tek kayıt kalır. Eksik giriş varken iş günü kullanıcı tarafından belirlenir; giriş sonradan eklendiğinde aynı günle eşleşmelidir. Çıkış iş gününün başlangıcından sonra olmalıdır. İş günü, kişi, grup, çalışma dönemi ve proje ataması kayıt oluşturulunca değişmez. Bunlardan biri yanlışsa gerekçeli iptal edip yeni kayıt açın; bu alanları değiştirerek başka güne/döneme taşıma yapılmaz.

Vardiyasız bir gün içinde aynı proje için birden fazla ayrı çalışma aralığı kaydedilebilir. Aynı aralığı iki kez onaya göndermek/onaylamak engellenir. Bu, gün içi çok proje dağıtımı değildir; mevcut asıl proje ataması kuralları sürer.

## Yetki ve güvenlik

Yeni yetki biti eklenmedi. **CanEnterTimesheets** kayıt/düzenleme/gönderme/iptal, **CanApproveTimesheets** onay/iade/yeniden açma için kullanılır. Eski ödeme, ücret/IBAN, dışa aktarma ve takvim hakları genişletilmez. Yönetici dahil aynı hesap kendi hazırladığı/son düzenlediği/gönderdiği puantajı onaylayamaz. Hesap ayrımı gerçek kişi eşlemesi yerine geçmez.

Kişi ve atama listesi proje kapsamıyla sunucuda sınırlandırılır. Kaydetmede özgün personel ve roster rowversion değerleri, kalıcı PersonId ve EmploymentId denetlenir. Başka kullanıcının kişi/atama değişikliği, arşiv veya yeni döneme geçiş eski seçimi geçersiz kılar. Aynı puantajı iki operatörün düzenlemesinde eski sürüm reddedilir. Üyelik/oturum her sunucu isteğinde yeniden doğrulanır; TC, IBAN veya parasal tutar bu yollardan okunmaz.

Kalıcı CommandId + aktör/proje/içerik özeti, başarılı transaction ile birlikte kaydedilir. Yanıt belirsiz kaldığında dialog aynı dondurulmuş isteği/gerekçeyi yeniden gönderir; ikinci kayıt veya ikinci değişiklik oluşmaz. Aynı kimliği farklı içerikle kullanmak reddedilir. Başarılı oluşturma aynı puantaj/kaynak kimliği ve aynı gövdeyle tekrar gelirse mevcut kaydı değiştirmez; farklı içerik sürümlü düzenleme ister. Dialog doğrulama hatasından sonra içeriği değiştirmek için kapatılıp yeniden açılır.

## Çakışma ve eski kayıt koruması

- Eksik/ters/24 saatten uzun süre, molanın neti tüketmesi veya eksik/fazla ücret dakikası onaya gönderilemez. Bunlar mevcut istisna listesinde görünür.
- Aynı kişinin bütün projelerde gönderilmiş/onaylı vardiyalı ve vardiyasız çalışmaları, onaylı yeni izinleri ve eski doğrudan Genel izinleriyle çakışma aynı kişi kilidi altında kontrol edilir. Aralık bitişi hariçtir; uç uca saatler çakışmaz.
- Vardiyasız gerçek çalışma taslağı, taslak/yayınlanmış planlı vardiyayla çakışıyorsa istisna gösterir ve onaya gönderilmez. İlgili vardiyalı puantajı kullanın veya yanlış planı kendi yetkisiyle gerekçeli iptal edin. Plan saatini gerçekleşen sayan otomatik dönüşüm yoktur.
- Gönderilmiş/onaylı vardiyasız çalışma üzerine yeni vardiya ataması/yayını, hafta kopyası veya izin kaydı da yazılamaz. Çakışmada başka proje ayrıntıları gösterilmez.
- Roster iptali, proje/ekip pasife alma, dönem/grup geçişi ve tarih sınırı kontrolleri mevcut puantaj tablosunu kullandığı için bu kayıtları da dikkate alır. Onaylı toplam SQL'de yalnız Approved satırları kapsar; yeniden açma/iptal toplamdan çıkarır.
- Eski vardiyalı puantaj kimliği/tekilliği korunur. Planlı bir kayıt vardiyasız kayda veya tersi yönde dönüştürülemez. Önceki değer ve kararlar mevcut değişmez audit geçmişinde korunur. Taslak/iptal geçmişi silinmez.

## Şema ve dağıtım

122, Timesheets.ShiftAssignmentId alanını NULL kabul edecek şekilde değiştirir; yalnız dolu vardiya atamaları için filtreli tekil indeks ve doğrulanan foreign key kurar. Böylece eski planlı atamada tek puantaj korunurken birden fazla gerçek vardiyasız kayda izin verilir. NULL vardiya kimliği gerçek vardiyasız kaydı ifade eder; sahte vardiya kimliği üretilmez. Vardiyasız iş günü/giriş günü CHECK'i eklenir. Kaynak gövde koruma trigger'ı NULL geçişlerini de karşılaştırır ve kaynak kimliklerinin değiştirilmesini engeller.

Eski 100–121 betikleri değiştirilmez. Mevcut satırları taşıma/yeniden yazma, vardiya/puantaj üretme veya yeni yetki verme yapılmaz. Migration hesap oturum nesillerini yeniler; mevcut kullanıcı adı/parola ve haklar korunur. Kaynak/paket **1.3.11-dev**, API **1.11**, şema **122** birlikte kurulmalıdır; API migration'ı kendiliğinden uygulamaz. Canlı yükseltme öncesi doğrulanmış yedek gerekir.

Derleme/paket bütünlüğü kontrolü yapıldı; işlev, SQL yükseltmesi, CSV, eşzamanlılık ve UI senaryoları kullanıcı isteğiyle çalıştırılmadı. Bu teslimde canlı yedek/kurulum, GitHub push veya Release yapılmadı.

## Sonraki dilimler

İzleyen öncelik **ham giriş/çıkış olayları ve kontrollü puantaj eşlemesidir**. Cihaz/ham olay alımı, vardiyasız CSV aktarımı, farklı projelere gün içi zaman parçaları ve mola dağıtımı, çalışan–hesap eşlemesi, kalıcı taslak kurtarma ve çok satırlı manuel giriş açık kalır. Mevcut CSV biçimi yalnız yayınlanmış vardiya kişi atamalarını kabul etmeye devam eder. Ücret tarifesi ve onaylı çalışmadan hakediş üretimi sonraki Aşama 5 işidir.
