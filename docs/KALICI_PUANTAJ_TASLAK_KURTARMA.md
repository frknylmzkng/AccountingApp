# Kalıcı puantaj taslak kurtarma

İzleyen durum: [127 / 1.3.16-dev kurulumu](1_3_16_DEV_KURULUMU.md) tamamlandı, kullanıcı doğruladı. 1.3.17-dev kaynakta [çok kişili manuel giriş](TOPLU_MANUEL_PUANTAJ.md) aynı taslak/sonuçlandırma korumasına eklendi. Aşağıdaki ilk kaynak teslimi kapsamı tarihsel nottur.

2 Kasım 2026 · Kaynak/paket **1.3.16-dev** · Hazırlanan şema **127** · API **1.16**.

Kullanıcı 1.3.15-dev sürümünü doğruladı. Bu kaynak diliminde vardiyalı/vardiyasız puantaj, CSV önizlemesi ve proje/mola dağılımı için yerel kalıcı taslak eklendi. Çalışan 1.3.15-dev / API 1.15 / şema 126, veritabanı ve kısayol değiştirilmedi. Kullanıcı isteğiyle denemeler atlandı.

## Henüz gönderilmemiş alanları kurtarma

1. Projeyi ve **Puantaj ve amir onayı…** ekranını açın. İlgili puantaj giriş/düzenleme, vardiyasız giriş, CSV önizleme veya dağılım önizleme ekranında kalıcı taslak çubuğu bulunur.
2. Alan değişiklikleri son değişiklikten yaklaşık **1,2 saniye sonra** Windows profiline şifreli kaydedilir. **Taslağı şimdi sakla** beklemeden saklar. Eksik giriş/çıkış, belirtilmemiş ücret dakikası, henüz tamamlanmamış parça listesi de korunur. Normal pencere kapanışında bekleyen alan değişiklikleri saklanmaya çalışılır. Durum çubuğundaki saklandı/saklanamadı bilgisini kontrol edin.
3. Uygulama kapansa veya süreç sonlansa da dosya kalır. Aynı Windows profili, hesap, bağlantı, veritabanı ve projeyle yeniden giriş yapın. Özgün kayıt/atama ekranını açın; **Taslağı açıkça kurtar** düğmesini kullanın. CSV için aynı haftanın atamalarını yükleyin. Yeni vardiyasız girişte aynı kişiyi seçin; taslaktaki iş günü geri gelir.
4. Eski taslak bulunduğunda yeni alan girişi/gönderim engellenir; dosya sessizce değiştirilmez. Gönderilmemiş taslağı kullanmak istemiyorsanız açık onayla silebilirsiniz. Kurtarmak sunucuya kaydetmez, onaya göndermez ve onay vermez. Alanları inceleyip normal **Kaydet** ve gerekçe akışını kullanın.

Özgün puantaj, kişi, vardiya veya tarihli atama sürümü değişmişse taslak güncel sürüme taşınmaz. Farklı hedefe otomatik birleştirme yoktur. Hesap güvenlik nesli değişmişse de alan kurtarma yapılmaz. Bu durumlarda **Kalıcı taslaklar / kurtarma… → Salt okunur incele…** ile eski değerleri inceleyin ve güncel kaydı ayrıca düzenleyin. Kaldırılmış hedef projelerin ayrıntılı dağılım önizlemesi, o projelerde güncel okuma hakkı olmadan açılmaz.

## Sonucu belirsiz gönderimi kurtarma

Kaydet ile gönderilecek tam istek; özgün işlem/puantaj/parça kimlikleri, sürümler ve gerekçe dahil, ağ isteğinden **önce** dayanıklı şifreli dosyaya yazılır. Dosya yazılamazsa istek gönderilmez. Gönderim başladıktan sonra alanlar dondurulur; farklı içerikle aynı kimlik kullanılmaz. Başarı yanıtından sonra dosya temizlenir. Temizleme başarısızsa dosya kalır ve aynı makbuz üzerinden sonuçlandırılabilir.

Uygulamayı yeniden açınca puantaj ekranında **Kalıcı taslaklar / kurtarma…** düğmesini kullanın:

- **Sunucu sonucunu kontrol et:** aynı hesap/proje ve içerik özetiyle kalıcı makbuzu sorgular. Kaydedilmişse yerel dosya temizlenir. Makbuz yokluğu yeni bir istek üretme izni sayılmaz; dosya korunur.
- **Aynı isteği yeniden gönder…:** özgün CommandId, içerik, gerekçe ve sürümleri tekrar kullanır. Daha önce kaydedildiyse mevcut başarılı sonuç bulunur; yeni kayıt üretilmez. Güncel yetki ve oturum denetimi sürer. Hesap nesli değişmişse makbuz bulunmayan eski istek yeni nesilde uygulanmaz; sonuç kontrolü veya kalıcı vazgeçme kullanılabilir.
- **Taslağı sil / istekten vazgeç…:** gönderilmemiş taslak açık onayla yerelden silinir. Gönderilmiş istekte sunucuya ulaşılıp sonuç alınmadan dosya silinmez. Henüz kaydedilmemişse sunucuda o CommandId için değişmez iptal makbuzu bırakılır. Gecikmiş aynı istek artık yazamaz. Zaten kaydedilmişse iş kaydı korunur, yalnız yerel kurtarma dosyası temizlenir. İş kaydını geri almak mevcut gerekçeli iade/iptal akışlarının işidir.

Gönderimle vazgeçme aynı SQL komut anahtarı üzerinde transaction/range lock sırasını kullanır. Böylece ya başarılı makbuz bulunur ya da geç gelen isteği durduran iptal kaydı oluşur. Vazgeçme audit'i gerekçe, hesap ve UTC zaman içerir; sunucu tam taslak gövdesini saklamaz.

## Kapsam ve gizlilik

Yerel klasör `%LOCALAPPDATA%\AccountingApp\timesheet-drafts\` altındadır. Alt klasör/dosya adları kapsam ve hedef özetidir. İçerik mevcut DPAPI CurrentUser / atomik dosya altyapısıyla şifrelenir; hesap kimliği, veritabanının fiziksel/katalog/soy kimliği, bağlantı ve proje kapsamıyla ayrılır. Aynı adlı başka veritabanı veya silinip aynı adla açılan hesap eski taslağı devralmaz. Parola, API token'ı, IBAN ve parasal bakiye taslağa alınmaz. Puantajdaki ücretlendirilecek **dakika** parasal tutar değildir.

Her kapsamda en çok 200 taslak, dosya başına en çok 3 MB serileştirilmiş içerik sınırı vardır. Aynı hedefin dosya kilidi iki pencerenin birbirinin taslağına yazmasını önler. Bozuk/okunamayan dosyalar korunur; sessizce yeni dosyayla değiştirilmez. Kurtarma listesinden içerik okumak, tekrar göndermek ve vazgeçmek için güncel proje puantaj giriş hakkı gerekir. Ayrıntılı çok proje önizlemesi bütün parça projelerinde okuma ister. Salt okunur önizleme mevcut oturum ve pano korumalarını kullanır.

Dosyalar aynı bilgisayar/Windows profiline aittir; başka cihazdan kurtarma, sunucuda taslak içerik senkronizasyonu, arka planda çevrimdışı gönderim ve otomatik çatışma birleştirme yoktur. Taslak bir veritabanı yedeği değildir. Son değişiklikle otomatik saklama arasındaki yaklaşık 1,2 saniye, ani güç kesintisinde kaybolabilir; saklama hatası durum bilgisiyle görünür. CSV'de yalnız başarılı önizleme verisi korunur; ham dosyanın kopyası veya henüz okunamamış hatalı satırlar tutulmaz.

Bu dilim, puantaj veri girişi ve dağılım hazırlığını kapsar. Amir kararları, ham olay aktarımı/eşlemesi, vardiya/izin formları, hesap işlemleri ve finans/ödeme komutları otomatik taslak kuyruğuna eklenmez. Personel ekranlarının önceki F6/F7 taslağı ve ödeme bekleyen istek günlüğü aynen ayrı kalır. Normal kaydedilmemiş değişiklik uyarısında pencereyi kapatmayı kabul etmek, kalıcı taslağı silmez; silmek ayrıca açık eylemdir.

## Şema ve doğrulama sınırı

`127-persistent-timesheet-drafts.sql` yalnız kalıcı vazgeçme makbuzu tablosunu ve değişmezlik/audit tetikleyicilerini ekler; hesap oturum nesillerini yeniler. 100–126 betikleri değiştirilmedi. İçerik için sunucu taslak tablosu veya otomatik kayıt dönüşümü yoktur. İstemci/API birlikte güncellenmelidir; mevcut API başlangıcı şemayı kendiliğinden yükseltmez.

Bu teslimde SQL yükseltmesi, uygulama/ekran, süreç kesintisi, ağ kaybı veya eşzamanlılık denemesi yapılmadı. C# derlemesi ve paket bütünlüğü kontrolü çalışma zamanı davranışının doğrulanması değildir. İzleyen Aşama 4 işi çok kişili toplu manuel giriş; çalışan self servis ayrı kapsamdır.
