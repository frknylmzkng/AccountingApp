# Seçilebilir raporlar ve kalıcı günlük kapanış

30 Ekim 2026 · Yerel geliştirme. Yayımlanmış v1.2.0 paketine dahil değildir.

**İzleyen canlı geçiş tamamlandı:** Kullanıcı onayıyla CHECKSUM/VERIFYONLY ve v2 manifestiyle doğrulanmış yedek alındı; yalnızca 112 uygulandı. Mevcut iş/finans/audit verileri korundu; oturum nesilleri beklendiği gibi yenilendi. Boş kapanış tablosu, üç etkin trigger, hash/JSON/tutar kısıtları, benzersiz gün, iptal bağlantısı ve indeksler doğrulandı. Eski kayıtlara bağlantı/kapanış uydurulmadı. Muhasebe - Güncel kısayolu 112 uyumlu pakete yönlendirildi. Test/ekran denemesi yapılmadı; uygulama açılmadı. Özel yedek/kanıtlar `artifacts/analysis/live-upgrade-112/` altında Git dışındadır. Aşağıdaki dağıtım notu hazırlık aşamasının tarihsel durumudur.

## Rapor seçimi

Rapor → Excel'e aktar veya CSV'ye aktar menüsü artık ilk tabloyu kendiliğinden seçmez. Önce **rapor adı ve kapsamı**, ardından **aktarılacak sütunlar** seçilir. Boş sütun seçimi reddedilir; teknik/saklı alanlar görünür tablo raporuna eklenmez. Gizlenmiş sütunlar gerekiyorsa önce ilgili tabloda görünür yapılmalıdır.

- Çok tablolu ekranlarda personel, defter, proje maliyeti veya yüklenmiş finans belgesi görünümü ayrı seçeneklerdir.
- Sayfalı listeler yalnızca açık sayfayı aktarır; kapsam penceresi bunu açıkça belirtir. Diğer sayfalar veya yalnızca seçili satırlar kendiliğinden dahil edilmez.
- Ödemeler'de seçili ödemenin **güncel fişi** ve tarih/beyan alanlarından oluşturulan **güncel ödeme önizlemesi** ayrıca seçilebilir.
- Genel Durum'da bütün etkin personeli kapsayan SQL özeti, açık izin sayfasından ayrı bir rapordur.
- Kesinleşmiş kapanışlarda seçili kaydın **saklanan özeti** ve **saklanan tüm hareketleri** ayrı seçeneklerdir; görünür kapanış listesi sayfasıyla sınırlı değildir.

Normal tablo seçenekleri ekranda yüklenmiş verinin görüntüsüdür; güncel veriye ihtiyaç varsa önce F5 kullanın. Fiş, güncel önizleme ve SQL özeti sağlayıcıları hazırlama sırasında tekrar sorgulanır. Kesin kapanış sağlayıcıları ise yalnızca saklanan içeriği okur.

Dosya adında seçilen raporun adı bulunur. Excel'de ayrıca Rapor bilgisi sayfası; seçilen rapor, kapsam ve dışa aktarma UTC zamanını taşır. Bu zaman kaynağın okunduğu veya kapanışın yapıldığı zaman olarak sunulmaz. CSV standart başlık/veri yapısını korur; rapor/kapsam ek sayfası içermez. UTC alanlarında CSV tam zaman ve Z işaretini, Excel tarih+saat biçimini korur.

Sütun seçiminden sonra da çoklu izin satırlarının bakiye toplamı bir kişi için bir kez hesaplanır. Bakiye sütununun kaldırılması Excel yardımcı toplamını bozamaz. CSV/Excel hücre korumaları ve canlı dışa aktarma yetkisi sürer. Audit, rapor adı/kapsamı, sütunlar, satır sayısı ve biçimi kaydeder; raporun kişisel veri satırlarını audit'e kopyalamaz. Dosya yayımlama ile SQL audit commit'i dağıtık atomik işlem değildir; önceki dışa aktarma sınırı korunur.

## İki farklı günlük rapor

**Güncel ödeme önizlemesi**, kayıt gününe ait şu anda iptal edilmemiş ödemeleri gösterir; sonradan iptal edilirse geçmiş günün bu önizlemesi değişebilir. Kesin kapanış değildir.

**Kesinleşmiş günlük nakit kapanışı**, o iş gününün Payment ve Reversal defter hareketlerinin değişmez görüntüsüdür:

- Nakit çıkışı: o gün kaydedilmiş bütün ödemeler (avanslar dahil).
- Nakit iadesi: o gün kaydedilmiş ödeme iptal/iade hareketleri.
- Net = çıkış − iade; fark = beyan − net. İadelerin fazla olduğu gün net/beyan negatif olabilir.
- Hakediş, bakiye düzeltme ve personel arşivleme hareketleri nakit kapanışı toplamına katılmaz.

Örnek: Pazartesi kaydedilen 300 TL ödeme, salı iptal edilirse pazartesi kesin kapanışında çıkış 300 kalır. Salı kapanışında iade 300 görünür. Önizleme ise pazartesinin güncel geçerli ödeme toplamından bu ödemeyi çıkarır. Bu fark iki raporun farklı sorulara cevap vermesinden kaynaklanır.

Önizleme ve kesin kapanış iş günü **Europe/Istanbul** saat dilimini kullanır. UTC başlangıç/bitiş aralığı kesin kayıtta saklanır; sonradan cihazın saat dilimi veya güncel defter durumu değişince yeniden hesaplanmaz. Kapsam bu veritabanının tamamıdır; kasa, banka hesabı veya proje bazında ayrı kapanış değildir. Banka transferi/gerçekleşmesi doğrulaması anlamına gelmez.

## Kesinleştirme ve erişim

**İşlemler → Kesinleşmiş günlük kapanışlar** menüsünü açın.

1. Tamamlanmış bir iş günü ve beyan edilen net TL tutarını girin. Bugün/gelecek gün kesinleştirilemez; bugün için önizleme kullanılır.
2. Yönetici olarak Günü kesinleştir düğmesine basın, kapsam/tutar uyarısını onaylayın ve gerekçe girin.
3. Saklanan listeden kaydı seçip gösterin; yazdırma ve dışa aktarma ayrı dışa aktarma iznini gerektirir.

Kapanış başlığı, satırlar, SQL sunucusunun kesinleştirme UTC zamanı, kullanıcı/kalıcı hesap kimliği, gerekçe, beyan/toplam/fark ve SHA-256 içerik özeti saklanır. Aynı gün/beyan tekrar gönderilirse mevcut kayıt döner; ikinci kapanış yaratılmaz. Aynı güne farklı beyanla güncelleme reddedilir. Gün anahtarı, transaction ve uygulama kilidi eşzamanlı kesinleştirmeyi sınırlar.

Başlık ile snapshot hash'i/tutarları okurken karşılaştırılır. Kayıt UPDATE/DELETE korumalıdır. Kapalı güne geriye dönük Payment/Reversal eklemek veya o günün özgün nakit hareketini değiştirmek/silmek engellenir. Sonraki açık günün iptali özgün defter satırına bağlanır ve o günün yeni iade hareketidir; eski snapshot değişmez. Mevcut personel kimliği, kapanış sınırı ve ödeme iptal kuralları kaldırılmaz.

Kapanış oluşturma yöneticiye, liste/görüntüleme mevcut Read yetkisine, yazdırma/Excel/CSV mevcut ayrı dışa aktarma iznine bağlıdır. İşlem içinde canlı hesap/yetki denetimi, transaction ve audit birlikte kullanılır. Ham snapshot TC içerebilir; kapanış ekranı/fişi/hareket dışa aktarımı TC'yi maskeler.

## Sınırlar ve dağıtım

Bu ilk aşama kapanış yeniden açma veya eski beyanı değiştiren revizyon sunmaz. Yanlış kesinleştirmeyi sonradan sessizce düzeltmek mümkün değildir. Ek düzeltme belgesi/çift kişi onayı ayrıca geliştirilebilir; mevcut iptal akışı sonraki güne yansır.

SQL yöneticisi korumaları kapatabilir; hash dijital imza veya harici değiştirilemez arşiv değildir. Veritabanı yedeğini geri yüklemek kapanışları da yedeğin tarihine döndürür. Eski iptal kayıtlarına eksik kaynak bağlantısı uydurulmaz; yeni iptaller kaynak defter kimliğini taşır.

112-immutable-daily-closings.sql hazırlanmıştır: DailyClosings, snapshot/hash kısıtları, kapalı dönem koruması, iptal kaynak bağlantısı ve gerekli indeksleri ekler. Eski oturumları yeniler. **Bu adımda canlı veritabanına uygulanmadı.** Canlı şema ve masaüstü kısayolu 111 paketinde kaldı. Yeni yerel paket artifacts/releases/report-selection-closings/ altındadır; açılış bekleyen yükseltmeyi uygulayabileceğinden yedek/şema geçişi öncesinde açılmadı.

Kullanıcı isteğiyle testler, ekran ve SQL denemeleri atlandı. Release çözüm derlemesi **0 uyarı / 0 hata**. Kalıcılık, sonradan iptal, değiştirilemezlik, tekrar, audit hatasında geri alım, yetki ayrımı, sütun seçimi ve UTC dışa aktarma için regresyon kaynakları eklendi; yalnızca derlendi. SQL trigger'larının çalışma davranışı bu adımda denenmedi.
