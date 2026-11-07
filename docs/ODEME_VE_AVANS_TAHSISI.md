# Ödeme ve avansın hakedişe tahsisi

6 Kasım 2026 · **1.3.22-dev / API 1.22 / hazırlanmış şema 132**.

İzleyen kaynak dilimi: [1.3.24-dev mali filtreler ve sayfalama](MALI_FILTRELER_VE_SAYFALAMA.md). Aşağıdaki sınırlar bu belgenin ilk sürüm kapsamının tarihsel kaydıdır.

Tahsis, veritabanında kaydedilmiş bir ödeme/avansın hangi hakediş belgesini karşıladığını gösterir. **Yeni ödeme, banka işlemi, defter hareketi, bakiye düşümü veya maliyet oluşturmaz.** Mevcut ödeme ve hakediş çekirdeği aynen para hareketini kaydeder; tahsis yalnız aralarındaki bağlantıyı ve tutarı saklar.

## Ekran ve kullanım

Merkez yöneticisi **Hakediş, avans ve proje maliyeti → Ödeme / avans tahsisi…** ekranını açar. Proje kullanıcısı kendi projesinin finans bölümündeki aynı düğmeyi kullanır. Okuma için ücret, proje maliyeti ve proje ödeme erişimi; tahsis/serbest bırakma için mevcut **proje ödeme onay yetkisi** gerekir. Yalnız ödeme hazırlama hakkı yazma yetkisi vermez. Tahsis edilen ödeme zaten mevcut para çıkışıdır; tahsis için yeni hazırlayan/onaylayan ödeme süreci başlatılmaz.

1. Üst listeden kayıtlı avans veya hakediş ödemesini seçin. Ödeme tutarı, türü, işlem/defter numarası ve tahsis edilmemiş kısmı görünür.
2. Alt listeden aynı kişi, proje, personel grubu ve **aynı çalışma döneminin** hakedişini seçin. Alt liste seçilen ödeme kimliğine göre daralır; sunucu bu eşleşmeyi yeniden denetler.
3. **Seçili ödeme ve hakedişi tahsis et…** ekranında kaynak belgeyi, ödemenin ve hakedişin açık tutarlarını inceleyip pozitif tahsis tutarını girin. Tutar iki açık sınırı da aşamaz. Gerekçeyle kaydedin.
4. **Tahsis ve serbest bırakma geçmişi** sekmesi bağlantıyı, tutarı, hazırlayanı ve gerekçeyi gösterir. Bir ödeme birden fazla hakedişe, bir hakediş birden fazla ödemeye bölünebilir; her bağlantı ayrı kayıttır.
5. Yanlış bağlantı için geçmişten aktif tahsisi seçip **Seçili tahsisi serbest bırak…** işlemini gerekçeyle yapın. Kaydı düzenlemek/silmek yerine değişmez serbest bırakma olayı oluşur. Tutar iki tarafta yeniden tahsis edilebilir hale gelir; istenirse yeni doğru bağlantı kurulur. Kısmi serbest bırakma için eski bağlantının tamamını serbest bırakıp gereken tutarla yeni tahsis hazırlayın.

Her kaynak ve geçmiş listesi en yeni **500** kayıtla sınırlıdır; kişi filtresi yüklenen hakedişler üzerinde çalışır. Ödeme/hakediş limitleri yalnız görünür satırlardan değil, **bütün aktif tahsislerden** hesaplanır. Eski kayıtlar için arama/sayfalama sonraki genişlemedir. Ekran tüm geçmişin dışa aktarımı değildir.

## Örnek ve bakiye ayrımı

500 TL avans ödendiğinde bakiye zaten 500 TL azalmıştır. Daha sonra 1.000 TL hakediş aktarılıp avansın 500 TL'si bu belgeye tahsis edildiğinde bakiye tekrar azalmaz. Hakedişin tahsis edilmemiş kısmı 500 TL kalır. Sonraki 500 TL hakediş ödemesi kendi mevcut ödeme sürecinde bakiyeyi azaltır; bu ödemenin de aynı belgeye tahsisi hakedişin kalanını sıfırlar.

Avans henüz hakediş yokken açık kalabilir; belge daha sonra oluştuğunda kullanıcı tahsis eder. Otomatik FIFO/mahsup yapılmaz. Mevcut proje ödeme limiti hakediş ve kaydedilmiş ödeme tutarlarını zaten içerir; tahsis bu hesapta ikinci ödeme gibi düşülmez. Günlük kapanış, banka gerçekleşmesi ve eski snapshot'lar değişmez. Kaydedilmiş ödeme, bankaca doğrulanmış ödeme anlamına gelmez.

## Düzeltme ve iptal korumaları

- Tahsis tutarı ödeme çıkışını ve güncel, düzeltilmiş hakedişi aşamaz. Aynı kaynağın farklı bağlantılarda kullanılabilen toplamı SQL koruması ve sunucu kontrolüyle sınırlıdır.
- Önizleme kaynak kimliklerini, belge sürümünü, düzeltilmiş tutarı ve iki taraftaki açık tutarları kapsar. Araya tahsis, serbest bırakma, iptal veya mali düzeltme girerse yeni önizleme gerekir. Yalnız serbest bırakılan bağlantılar açık kapasiteye geri döner.
- Tahsis işlem kimliği/içerik özeti tekrar gönderimi korur. Yanıt belirsizse açık pencerede aynı isteği yeniden gönderin; pencere kapandıysa önce geçmişi yenileyin. Aynı istek serbest bırakmadan sonra tekrar gelse bile yeni aktif tahsis oluşturmaz.
- Aktif tahsisli ödeme veya manuel hakediş belgesi iptal edilemez. Önce ilgili tahsisler gerekçeyle serbest bırakılır. Bu, eski ödeme seçimi ve çalışma dönemi iptal korumalarını kaldırmaz; kaynak iptali kendi kurallarına göre ayrıca yapılır.
- Bağlı mali düzeltme hakediş tutarını aktif tahsis toplamının altına indiremez. Düzeltme önizlemesi aktif tahsis tutarını gösterir; gerekli bağlantılar serbest bırakıldıktan sonra yeni önizleme oluşturulur. SQL tetikleyicisi de aynı sınırı korur.
- Serbest bırakmak ödemeyi iptal/iade etmez ve kişinin borcunu silmez. Yeni ödeme veya banka iadesi ayrı süreçtir.

## Kapsam ve geçiş

Aynı kişi olsa bile eski ve yeni EmploymentId arasında para tahsis edilmez. Aynı geçmiş çalışma dönemine ait ödeme/hakediş, kart arşivde olsa da kimlik bağlantısı üzerinden eşlenebilir; güncel karta yazma yapılmaz. Yeni tahsis etkin proje ister; proje pasifken geçmiş okunabilir ve temizleme için tahsis serbest bırakılabilir.

Yalnız proje kimliği ve çalışma dönemi doğrulanmış, iptal edilmemiş **Advance/Settlement Payment** satırları ve etkin **Accrual** belgeleri kapsamdadır. Projesiz/sınıflandırılmamış veya kimlik bağlantısı eksik eski kayıtlar için tahminî eşleştirme yapılmaz. Bunlar ayrı veri mutabakatı gerektirir. Otomatik tahsis, proje/dönemler arası aktarım, banka eşleştirmesi ve tahsis raporu dışa aktarımı bu dilimde yoktur.

132 betiği iki değişmez tahsis/serbest bırakma tablosu, aktif tahsis görünümü, kaynak limit/iptal/düzeltme korumaları ve audit tetikleyicileri ekler. Kim, ne zaman, hangi gerekçeyle bağlantı kurdu/çözdü bilgileri transaction içinde saklanır. Mevcut kayıtlardan otomatik tahsis üretilmez, bakiye/defter geçmişi değiştirilmez; hesap oturum nesilleri yenilenir. Uygulanmış 100–131 betikleri korunur.

Bu teslimde canlı **1.3.21-dev / şema 131 / API 1.21**, bağlantı ayarları ve kısayol değiştirilmedi. Denemeler kullanıcı isteğiyle atlandı. Derleme ve paket bütünlüğü kontrolü SQL, işlev, arayüz veya eşzamanlılık doğrulaması değildir.
