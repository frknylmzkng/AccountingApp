# Vardiya ve izin takvimi

31 Ekim 2026 · Kaynak/paket 1.3.9-dev · Şema 120 · API 1.9.

Kurulum güncellemesi: mevcut veritabanı yedeklenip 120 uygulandı; istemci/API kuruldu ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Veriler ve ayarlar korundu, denemeler atlandı. [Kurulum kaydı](1_3_9_DEV_KURULUMU.md). Aşağıdaki kaynak teslimi notu kurulum öncesini anlatır.

Kullanıcı 1.3.8-dev sürümünün çalıştığını doğruladı. Haftalık proje/ekip takvimi, vardiya şablonları, kişi planlama, önizlemeli hafta kopyalama ve ayrı izin talepleri eklendi. Bu kaynak tesliminde canlı kurulum, veritabanı ve kısayol değiştirilmedi; denemeler kullanıcı isteğiyle atlandı.

## Yetki ve takvimi açma

Yönetici Projeler/ekipler/tarihli atamalar ekranında projeyi açıp **Vardiya ve izin takvimi…** düğmesini kullanır. Proje kullanıcısı **Projelerim** ekranından aynı düğmeyle kendi projesine girer. Takvim okuması mevcut proje okuma kapsamını kullanır; ücret veya IBAN içermez.

Proje sorumlusu ve yetkiler bölümünde iki bağımsız yeni hak vardır:

- **Vardiya / izin talebi planla (CanPlanShifts):** şablon ve vardiya hazırlama, kişi atama, yayın/iptal, hafta kopyalama ve izin talebi oluşturma.
- **İzin onayla / reddet / iptal et (CanApproveLeaves):** o projedeki talebe karar verme.

120'de mevcut üyeler bu hakları otomatik almaz; yeni bitler kapalı başlar. Yönetici tam yetkilidir. Sorumlu olmak, ekip/atama düzenleme, maliyet görüntüleme, ücret okuma veya ödeme onayı bu hakları kendiliğinden açmaz. Kısıtlı proje kullanıcısının üye olmadığı proje API'den de okunamaz/yazılamaz. Genel merkez rollerinin önceki genel okuması korunur; yönetici dışındaki genel roller otomatik planlama/onay almaz.

## Haftalık görünüm

Bir gün seçip Haftayı yükle düğmesine basın; o günün pazartesisi ve sonraki altı gün gösterilir. Önceki/sonraki hafta ve ekip filtresi vardır. Haftalık özet ekip satırlarıyla vardiya, saha, saat, atanan/gereken kişi ve durumu gösterir. Gece vardiyası kesiştiği iki günde de görünür; asıl vardiyalar listesinde tek kayıttır. Özetteki tekrar eden hücreler çalışma süresi veya ücret toplamı değildir.

Diğer sekmeler: Vardiyalar, Vardiya personeli, İzin talepleri, Genel doğrudan izinler ve Şablonlar. İptal/ret geçmişi seçilince eski sonuçlar da görünür. Personel seçimi sayfalıdır. Her haftalık sorgunun dört sonuç tablosu aynı transaction içinden gelir; şablon ve ekip listeleri ayrı okumadır. Hafta başına her sonuç tablosu en fazla 2.000 kayıt taşır; aşılırsa ekip filtresiyle daraltılması istenir. Şablon listesi proje başına 500 ile sınırlıdır.

Saatler **Europe/Istanbul** iş saat dilimindedir. UI'daki yerel takvim saatleri bilgisayarın saat dilimine çevrilmez. Başlangıç dahil, bitiş hariçtir: 09:00–17:00 ile 17:00–22:00 çakışmaz. 22:00 başlayan 480 dakikalık vardiya ertesi gün 06:00 biter ve tek ShiftId'dir. Girişler saat dilimsiz ve tam dakikadır; 1900–2200 aralığı kullanılır. Geçersiz/belirsiz saat dilimi saatleri reddedilir. UTC başlangıç/bitiş ve saat dilimi kimliği kayıtta dondurulur.

## Vardiya hazırlama ve yayınlama

1. Şablonlar sekmesinde Yeni şablon seçin: ad, isteğe bağlı saha, ekip/proje geneli, başlangıç saati, süre (1–1.440 dk), mola (süreden kısa), gereken kişi (1–500) ve etkinlik durumu girilir. Şablon sürümle düzenlenir; silinmez, pasife alınır.
2. Vardiyalar > Şablondan vardiya ile başlangıç gününü seçin. Etkin şablon değerleri **yeni taslağa kopyalanır**. Şablon sonradan değişirse eski vardiya değişmez. Saha boşsa projenin mevcut sahası yeni kayda alınır.
3. Taslak vardiyayı seçip Kişi ata düğmesiyle proje personelini seçin. Tam aralığı kapsayan etkin proje/ekip ataması ve güncel çalışma dönemi gerekir. Sadece proje havuzunda görünmek yeterli değildir.
4. Kapasite aşımı, başka projedeki taslak/yayınlı vardiya veya onaylı/doğrudan Genel izniyle çakışma reddedilir. Molanın yeri belirlenmediği için tüm vardiya aralığı kişiye ayrılmış sayılır.
5. Yayınla düğmesi kişi/dönem/atama/izin kontrollerini yeniden yapar. Eksik kapasite gösterilir; kullanıcı onayıyla eksik kişiyle yayınlamak mümkündür. Bu bir plan eksikliğidir, otomatik kişi ataması yapılmaz.
6. Yayından sonra saat, kişi veya kapasite değiştirilmez. Gerekçeli iptal ve yeni vardiya açılır. Taslak kişi ataması iptal edilebilir; fiziksel silinmez. Vardiya iptalinde eski kişi kayıtları tarihsel olarak durur.

Proje sorumlusu sütunu mevcut proje sorumluluğunu gösterir; tarihsel vardiya sorumlusu snapshot'ı değildir. Oluşturan/yayınlayan/iptal eden ve zamanları ayrıca tutulur. Mola ve planlanan net dakika **LocalPlannedMinutes.v1** politikasıyla yerel takvim süresi eksi mola olarak gösterilir; bu gerçekleşen puantaj, ücretlendirilen süre veya mevzuat hesabı değildir.

## Önizlemeli hafta kopyalama

Haftayı kopyala/önizle düğmesi, açık proje/ekip filtresindeki ve başlangıcı kaynak haftaya düşen iptal edilmemiş vardiyaları listeler. Önceki haftadan gece devamı olarak görünen vardiya ikinci kez kopyalanmaz. Hedef pazartesi haftası seçildiğinde yeni başlangıç/bitiş, atanan/gereken kişi sayıları önizlenir. Kaynak vardiya snapshot'ı ve etkin kişi atamaları hedefe **taslak** kopyalanır; güncel şablon değerleriyle sessiz yeniden hesaplama yapılmaz.

En fazla 50 vardiya / 200 kişi ataması tek işlemde kopyalanır. Kaynak vardiya sürümleri, hedef proje/ekip/tarih, her kişinin aynı çalışma döneminde kaldığı, hedef roster aralığı ve bütün çakışmalar sunucuda kontrol edilir. Yeni kopyalar arasındaki çakışmalar da kontrol edilir. Bir hata varsa batch dahil her şey rollback olur. Eksik kadro aynı kalabilir; hedef ayrıca yayınlanır. İzinler kopyalanmaz.

BatchId, proje/haftalar, aktör, normalize gerekçe ve sıralı kaynak kimliği/sürümlerinin SHA-256 niyet özetiyle kalıcıdır. Kayıtlı aynı niyet tekrarı yeni vardiya üretmez; farklı niyet reddedilir. Hedef vardiyada kaynak ShiftId, kaynak sürüm ve CopyBatchId tutulur. Kaynak şablon pasifleşmiş olsa bile snapshot kopyası kullanılabilir; hedef ekip/proje ve kişiler yeniden doğrulanır.

## İzin talebi ve karar

İzin talepleri > İzin talebi ile proje personeli seçilir. Başlangıç/bitiş İstanbul saati girilir. Tam gün için 00:00 → ertesi gün 00:00, yarım gün için örneğin 09:00 → 13:00 kullanılır. Tek talep en fazla 366 gün olabilir. Talep için tüm aralığı kapsayan roster ve çalışma dönemi gerekir.

Talep önce Requested durumundadır ve vardiya zamanını tek başına bloke etmez. Ayrı izin onay yetkilisi onay, ret veya iptal verir. Onay sırasında proje/roster/güncel dönem, talep anındaki kişi sürümü, diğer onaylı izinler, doğrudan Genel izinleri ve bütün projelerdeki taslak/yayınlı vardiyalar tekrar kontrol edilir. Çakışmada talep beklemede kalır; önce ilgili vardiya/izin gerekçeli düzeltilir. Ad/IBAN/bakiye gibi kişi rowversion'ını değiştiren ara işlem varsa talep güncel seçimle yeniden açılmalıdır; eski talep iptal edilir.

Requested → Approved/Rejected/Cancelled, Approved → Cancelled geçişleri vardır. Ret ve iptal son durumdur; aynı talep yeniden açılmaz. Tarihler ve kişi/dönem sonradan değiştirilmez; yeni talep açılır. Onay bilgisi iptal sırasında korunur, ayrıca iptal eden/zaman/gerekçe tutulur. Gerekçeler audit ve kayıt gövdelerinde tutulur; normal haftalık proje okuması izin gerekçesi gövdesini dağıtmaz.

Kullanıcı hesabı ile çalışan PersonId eşlemesi bu aşamada yoktur: operatör kişi adına talep açar. Bir hesap iki takvim hakkına sahipse hazırladığı izin talebini karara bağlayabilir; bu hesap, izinli kişinin kendisi olarak varsayılmaz. Finansal ödeme akışının farklı hazırlayan/onaylayan kuralı değiştirilmemiştir.

İzin süresi **CalendarMinutes.v1** ile yerel takvim dakikasıdır. Yıllık hak, izin türüne göre ödeme, hafta sonu/tatil düşümü veya resmi izin mevzuatı türetilmez. Politika/saat dilimi ve aralık saklanır; bu kayıtlar ücret veya personel bakiyesini değiştirmez.

## Eski ekranlar ve geçmiş

Genel ve İzin Planlama ekranlarının mevcut doğrudan kayıt akışı ve merkez düzenleme yetkisi korunur; bunlar yeni proje talebine otomatik dönüştürülmez/onaylanmış sayılmaz. Genel doğrudan izinler sekmesi ayrı onay aşaması olmadığını açıkça gösterir; salt okunurdur. İlgili proje/çalışma dönemi roster bağlantısıyla görünürler. Genel/Joker geçmişleri ve 107 izin değişiklik geçmişi yerinde kalır; eski kayda sahte proje/karar atanmaz.

Eski SaveLeaves/izin kaydetme de ortak kişi kilidi ve çakışma kontrolünü kullanır: yeni vardiya veya onaylı izinle çelişen tarihler kaydedilemez. Yeni vardiya/izin onayı da eski izinleri kontrol eder. Genel doğrudan izin bitişi gün dahilidir; ortak çakışma kontrolüne ertesi gün 00:00 bitişi olarak çevrilir. Bu, eski formun gün hesabını değiştirmez.

Roster iptali, ona bağlı etkin vardiya veya onaylı izin varsa engellenir. Önce takvim kayıtları gerekçeli iptal edilir. Proje tarih/durum değişikliği veya ekip pasife alma da mevcut vardiyaları sessizce geçersizleştiremez. Çalışma dönemi tarih düzeltmesi, ayrılış/yeniden giriş ve tarihli grup geçişi yeni vardiya/onaylı izin aralıklarını da kontrol eder. Bekleyen eski dönem talebi yeni döneme taşınmaz; onaylanamaz fakat yetkili kişi iptal edebilir. Geçmiş görünümü iptal edilen roster bağlantılarındaki doğrudan izinleri de gösterebilir.

## İşlem güvenliği, kurulum ve sınırlar

Yazmalar canlı hesap/proje yetkisi, özgün rowversion, SQL transaction ve personel bazlı veritabanı uygulama kilidi kullanır. Kişi takvim kilidi projeler arasında ortaktır. Çakışmalar strict start < end karşılaştırmasıyla sunucuda kontrol edilir; SQL'de NOLOCK ile planlama yapılmaz. Eşzamanlı farklı işlemlerde SQL kilit çatışması/deadlock hata verirse transaction geri alınır; kullanıcı yeniler/aynı niyeti tekrar gönderir. Otomatik sınırsız retry yoktur.

Yeni şablon/vardiya/izin ve kopyalama dialoglarında ilk gönderimden sonra niyet ve kimlik sabittir; aynı açık dialogdan tekrar gönderim yapılır. Durum komutları zaten aynı sonuca ulaşılmışsa yeni kayıt üretmez. Kayıp yanıt sonrası ekranı yenileyerek SQL'deki sonucu inceleyin. Gönderilmemiş dialog niyeti disk taslağı değildir. Kişi atamasının kayıp yanıtında yenilenen atama listesinden sonucu inceleyin; yeni kimlikle ikinci atama, aktif unique kişi/çakışma kontrolünü aşamaz.

120, iki kapalı proje biti ve ShiftTemplates, PlannedShifts, ShiftAssignments, LeaveRequests, ScheduleCopyBatches tablolarını ekler. Foreign key/check/unique, gövde/karar dondurma, silme koruması ve audit trigger'ları vardır. Audit güvenli alan listesi planlama alanlarını kapsar; serbest ad/gerekçe ve hassas değerler mevcut politikalara tabidir. Eski 100–119 betikleri değiştirilmez. Mevcut hesap oturum nesilleri 120 uygulanınca yenilenir. İstemci/API birlikte 1.3.9-dev / API 1.9 ve önce doğrulanmış yedekle 120 gerektirir; API otomatik migration çalıştırmaz.

Bu teslimde derleme/yayın ve paket bütünlüğü kontrol edilir. SQL migration, gece/sınır saatleri, iki projede eşzamanlılık, eski izin yolları, batch rollback/kayıp yanıt, farklı haklar ve UI/DPI denemeleri kullanıcı isteğiyle çalıştırılmadı. Sürükle-bırak, otomatik kadro optimizasyonu, resmi tatil/izin hakkı, çalışan self servis eşlemesi, puantaj ve çalışma kaynaklı hakediş sonraki işlerdir; bu teslim ücret hesaplamaz veya ödeme kaydetmez.
