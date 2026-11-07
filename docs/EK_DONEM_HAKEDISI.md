# Geç gelen çalışma için bağlı ek dönem hesabı

6 Kasım 2026 · **1.3.20-dev / API 1.20 / hazırlanmış şema 130**.

İzleyen kaynak dilimi: [1.3.21-dev bağlı mali düzeltme ve ters hareket](BAGLI_MALI_DUZELTME_VE_TERS_HAREKET.md). Aşağıdaki kapsam 1.3.20-dev tesliminin tarihsel kaydıdır.

Asıl dönem hesabı deftere aktarıldıktan sonra, aynı dönem için sonradan kaydedilip onaylanan çalışma ayrı bir **ek hesap** olarak hazırlanabilir. Asıl hesap, tutarı, kaynak snapshot'ları, mali onayı ve aktarımı değişmez. Bu ekleme Aşama 5'in geç gelen çalışma dilimidir; önceki yanlış tutarı azaltan düzeltme/ters hareket değildir.

## Kullanım

1. Projenin tarife/dönem hakedişi ekranında **Değişmez hesap geçmişi** sekmesini açın. Deftere aktarılmış **Asıl hesap** satırını seçin.
2. **Seçili asıl hesap için ek önizleme** düğmesine basın. Tarihler asıl hesapla aynı aralığa alınır; ek hesap için başka bir dönem seçilmez.
3. Önizlemede yalnız hiçbir hesapta kullanılmamış, onaylı ana puantajlar veya onaylı proje parçaları gösterilir. Önceki asıl/ek hesap kaynakları tekrar alınmaz. Yeni kaynak yoksa kayıt kapalıdır. Eksik/çakışan tarife veya dönem içindeki sonuçlandırılmamış puantajlar önce çözülmelidir.
4. **Hesabı değişmez kaydet…** ile asıl hesap bağlantısını ve yeni toplamı kontrol edip gerekçeyle kaydedin. Yeni hesap numarasıyla değişmez ek hesap oluşur; bu adım bakiye değiştirmez.
5. Ek hesabın kaynaklarını inceleyip bağımsız hesapla mali onay verin, ardından aktarım önizlemesini inceleyip deftere aktarın. [129 onay ve aktarım korumaları](DONEM_HAKEDISI_MALI_ONAY_VE_AKTARIM.md) aynen geçerlidir. Asıl hesabın onayı ek hesabı onaylamış sayılmaz.

Hazırlama/onay/aktarım yöneticidedir; okuma için ücret ve ilgili proje maliyeti yetkileri gerekir. Hesap geçmişinde **Hesap türü** ve **Asıl hesap no** alanları bağlantıyı gösterir. Bir asıl hesaba birden fazla ek bağlanabilir; ek hesaba başka ek bağlanmaz.

## Korunan kurallar

- Bir proje/tarih aralığı için tek asıl hesap vardır. 130 yalnız eklerin aynı dönemde ayrı kimlikle kaydedilmesine izin verir. Mevcut tüm hesaplar asıl hesap olarak kalır; otomatik ek oluşturulmaz.
- Asıl hesap aynı projede, aynı tarih aralığında ve deftere aktarılmış olmalıdır; ek hazırlamada proje etkin olmalıdır. ParentCalculationId bağlantısı SQL yabancı anahtarı ve değişmezlik tetikleyicisiyle korunur.
- Kaynak tekilliği bütün asıl/ek hesaplar arasında ortaktır. Aynı çalışma farklı tarih filtresi veya yeni istek kimliğiyle yeniden borç oluşturamaz. Önizleme/kaydetme arasında yeni kaynak, tarife veya önceki kullanım değişirse yeni önizleme gerekir.
- Hazırlama aynı transaction içinde kaynakları yeniden okur ve mühürler. Aynı hesap kimliği/içerikle tekrar gönderim önceki sonucu verir. Bir kaynak başka hesapta tüketilmişse yeni yazma yapılmaz. Pencere kapandıktan sonra yeni işlem başlatmadan geçmişi yenileyin.
- Her ekin mali onayı ve aktarımı kendisine aittir. Hazırlayan/hak sahibi öz onayı, eski çalışma döneminin yeni karta aktarılması ve çift aktarım engelleri korunur. Aktarım banka ödemesi değildir.
- Yeni kaynak iş günündeki mevcut geçerli tarifeyle hesaplanır; eski hesabın tarife snapshot'ı değiştirilmez. Bir hesap en fazla 2000 yeni kaynak alır; ekin tarih aralığı daraltılamadığı için bu sınır aşılırsa kayıt engellenir.

## Sınırlar ve sonraki adım

Bu özellik mühürlü kaynakları açmaz, önceki çalışma süresini/oranını değiştirmez, aktarımı iptal etmez ve negatif fark oluşturmaz. Hesaba alınmış bir puantaja yeni parça ekleme kilidi korunur; ek hesap yeni, ayrı onaylı kaynakları tüketir. Ödenmiş tutarlar, manuel hakediş/avanslar otomatik mahsup edilmez. **Bağlantılı, bağımsız onaylı ters hareket/düzeltme** ve ardından ödeme/avans tahsisi Aşama 5'in açık işleridir.

130 yükseltmesi mevcut hesaplara nullable ParentCalculationId, tek asıl hesap için filtreli tekil indeks, asıl bağlantı indeksi, FK/CHECK ve değişmezlik tetikleyicisi ekler; oturum nesilleri yenilenir. Uygulanmış 100–129 betikleri değiştirilmedi. Bu teslim kaynak ve yerel paket hazırlığıdır; canlı **1.3.19-dev / şema 129 / API 1.19** ve kısayol değiştirilmedi. Kullanıcı isteğiyle test, SQL senaryo ve arayüz denemeleri atlandı; derleme ve paket bütünlüğü kontrolü işlevsel doğrulama değildir.
