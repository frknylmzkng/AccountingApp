# Tahsis ve mutabakatta tarih/kişi filtreleri ve sayfalama

6 Kasım 2026 · **1.3.24-dev / mevcut şema 132 / API 1.24**. Yeni migration yok.

## Filtre kullanımı

Tahsis ve proje mutabakatı ekranlarının üstündeki **Kayıt tarihi (İstanbul)** alanlarında başlangıç/bitiş işaretlerini açın. İşareti kapalı uç sınırsızdır. İki gün de dahildir; sunucu İstanbul saat dilimindeki gün sınırlarını UTC'ye çevirir. Başlangıç bitişten sonra olamaz; desteklenen yıllar 1900–2199'dur.

**Kişi seç…** ilgili projenin mevcut personel havuzundan kalıcı PersonId seçer. Eski/ayrılmış kişi için rapor veya kayıttan bilinen kalıcı kişi numarası elle girilebilir. Arama alanı ayrıca aynı projedeki değişmez hesap snapshot'larında geçen kişi adını içerme yöntemiyle arar. Ad snapshot'ı olmayan eski kayıtlar kalıcı kişi numarasıyla filtrelenmelidir. Aynı adlı birden çok kişi ad aramasına uyabilir; tek kişi için numara kullanın. PersonId aynı olsa da çalışma dönemleri raporda birleşmez.

**Filtreyi uygula** yeni kapsamı yükler ve sayfaları başa alır. **Filtreyi temizle** bütün tarihçeye döner. Filtre taslağı uygulanmış kapsamdan farklıyken tahsis, serbest bırakma ve CSV işlemleri devre dışıdır. Yenile/F5 uygulanmış kapsamı yeniler; filtre taslağını kendiliğinden uygulamaz. Uygulanan kapsam rapor başlığında veya tahsis ekranının altında yazılır.

## Tahsis ekranı

- Ödeme/avans, eşleşen hakedişler ve tahsis geçmişi için bağımsız sayfa kontrolleri vardır. Önceki/sonraki, sayfaya git ve 25/50/100/200 satır seçenekleri kullanılır. 500 satırlık görünüm sınırı yeni ekran akışında kaldırıldı.
- Ödeme listesi ödeme **CreatedUtc**, hakediş listesi belge **CreatedUtc**, tahsis geçmişi tahsisin **CreatedUtc** değerine göre filtrelenir. Serbest bırakma tarihi veya hakedişin çalışma günü aralığı tarih filtresinin yerine kullanılmaz.
- Ödeme seçilince aynı kişi/proje/grup/EmploymentId'ye ait hakedişler sunucuda filtrelenip sayfalanır; yalnız yüklenmiş ilk hakediş sayfasında arama yapılmaz. Seçilen ödeme de uygulanmış kaynak filtresine uymalıdır. Eski avansı yeni hakedişe bağlamak için iki kaynak tarihini de içeren aralık seçin veya tarihi açın.
- Ödeme sayfası değişince ödeme seçimi ve bağlı hakediş seçimi temizlenir. Hakediş ve geçmiş sayfalarında da yeni sayfanın satırı açıkça seçilmelidir. Filtre değişince bütün seçimler kaldırılır; başarısız yüklemede eski seçimle işlem açılmaz.
- Parasal limitler **bütün aktif tahsislerden** hesaplanır. Tarih/kişi filtresi veya sayfalama ödeme/hakediş kapasitesini artırmaz. Tahsis kaydetme ve serbest bırakma mevcut kimlik, yetki, önizleme ve tekrar istek korumalarını kullanır.

## Mutabakat raporu

Kişi/çalışma dönemi, hakediş, ödeme, tahsis ve istisna bölümleri bağımsız sayfalanır. Özet tek satırdır ve **filtreye uyan tüm kaynakların** toplamını gösterir. Toplam kayıt ve sayfa bilgisi SQL tarafında hesaplanır. Veri silinip/eklenip istenen sayfa boş kalırsa son geçerli sayfa yüklenir. Sıralama kayıt kimlikleriyle tamamlanır; sayfa geçişinde aynı veri anının korunacağı varsayılmaz. Her yenilemede veri zamanı değişebilir.

Hakediş kaynakları belge kayıt tarihiyle, ödeme kaynakları ödeme kayıt tarihiyle seçilir. Çalışma tarihi, bankanın işlem tarihi veya eski bir tarihteki mali durum yeniden oluşturulmaz. İptaller, mali düzeltmeler ve tahsislerin **güncel** durumu kullanılır. Tahsis ayrıntıları, seçilen kaynaklardan en az biri kapsamdaysa bağlantıyla birlikte görünür; tahsisin kendi kayıt tarihine göre elenmez. Tahsis ekranındaki geçmiş tarih filtresi ise tahsisin kayıt tarihidir.

Bu iki kaynak kümesi farklı olabilir. Örneğin mart avansı nisandaki hakedişe bağlanmışsa, yalnız nisan kaynaklarını seçmek hakedişi kapsar fakat ödemeyi dışarıda bırakır. Bu geçerli bağlantı raporda **kapsam dışı ödemeden tahsis** olarak gösterilir. Tersi **kapsam dışı hakedişe tahsis** alanına girer.

| Alan | Anlamı |
|---|---|
| Tahsis kapsamları farkı | Seçilen ödemelerden tahsis − seçilen hakedişlere tahsis |
| Kapsam dışı ödemeden tahsis | Seçili hakedişi karşılayan, kapsam dışındaki aynı proje ödemesi |
| Kapsam dışı hakedişe tahsis | Seçili ödemenin kapsam dışındaki aynı proje hakedişine bağlanan kısmı |
| Açıklanamayan fark | Tahsis kapsamları farkı − (dışarıya tahsis − dışarıdan tahsis) |

Ekran başlığı açıklanamayan farkı ve **filtredeki tüm istisnaların** adedini gösterir; yalnız açık istisna sayfasını saymaz. Kişi/dönem satırındaki kapsam farkı tarih sınırını geçen bağlantılardan kaynaklanabilir. Farkın sıfır olması banka gerçekleşmesini veya bütün kayıtların doğruluğunu kanıtlamaz; istisna ve kaynak ayrıntıları ayrıca incelenmelidir.

## CSV kapsamı ve güvenlik

**Seçili bölümü güncel CSV olarak kaydet…** uygulanan tarih/kişi filtresini sunucuya taşır. Sunucu aynı filtrede **seçili bölümün bütün satırlarını** yeniden okur; açık sayfa ile sınırlamaz. Özet de bütün filtre kapsamındadır. CSV'de filtre açıklaması, kaynak tarihi anlamı, veri zamanı, bölüm ve oluşturan hesap saklanır. Seçili bölüm 10.000 satırı veya dosya 32 MB'ı aşarsa işlem durur; filtre daraltılmalıdır. Ekranda ise 10.000 satırlık toplam sonuç engeli kaldırılmıştır.

Yetkiler her sorgu ve dışa aktarmada sunucuda tekrar denetlenir. Proje sınırı, ücret/ödeme/maliyet erişimi ve ayrı dışa aktarma hakkı korunur. Kişi numarası başka projeye erişim sağlamaz; SQL filtreleri parametrelidir, arama karakterleri kaçışlanır. Genel bakiye, TC ve IBAN rapora eklenmez. Tahsis ve mali düzeltme tutarları filtre yüzünden değişmez.

API'de `POST /payment-allocations/browse` bölüm, filtre, sayfa ve seçili ödeme kimliğini; `POST /project-reconciliation/browse` filtre ve bölüm bazlı sayfa isteklerini alır. CSV için mevcut ProjectExport sözleşmesine isteğe bağlı Filter eklendi. Eski temel GET tahsis panosu uyumluluk yolu olarak kalır; yeni ekran sayfalı POST yolunu kullanır. İstemci/API birlikte **1.3.24-dev / API 1.24** olarak güncellenmelidir.

Bu kaynak tesliminde canlı **1.3.23-dev / şema 132 / API 1.23**, veritabanı ve kısayol değiştirilmedi. 100–132 betikleri korundu. Kullanıcı isteğiyle SQL, arayüz, işlev, eşzamanlılık ve sayfa/filtre senaryo denemeleri atlandı. Derleme ve paket bütünlüğü kontrolü çalışma zamanı doğrulaması değildir.
