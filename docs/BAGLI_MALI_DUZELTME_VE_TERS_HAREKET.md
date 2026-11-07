# Bağlı, bağımsız onaylı mali düzeltme ve ters hareket

6 Kasım 2026 · **1.3.21-dev / API 1.21 / hazırlanmış şema 131**.

İzleyen kaynak dilimi: [1.3.22-dev ödeme ve avans tahsisi](ODEME_VE_AVANS_TAHSISI.md), aktif tahsis altına düşen mali düzeltmeye ek koruma getirir. Aşağıdaki metin 1.3.21-dev kapsamının tarihsel kaydıdır.

Deftere aktarılmış asıl veya ek dönem hesabının **kişi/çalışma dönemi satırı**, asıl kayıtlar değiştirilmeden mali olarak düzeltilebilir. Hazırlık → bağımsız mali onay → güncel önizlemeyle tek seferlik aktarım akışı kullanılır. Her düzeltme özgün aktarım satırına, belgeye ve varsa önceki aktarılmış düzeltmeye bağlıdır.

## Kullanım

1. Projenin tarife/hakediş ekranında **Değişmez hesap geçmişi** sekmesinden aktarılmış hesabı seçip **Seçili hesabın mali düzeltmeleri…** düğmesine basın.
2. **Aktarım satırları / güncel tutar** sekmesinde ilgili kişiyi seçin. İlk aktarım, düzeltilmiş güncel toplam ve kaynak belgeyi inceleyin. Sıfır ilk aktarımın mali belgesi olmadığı için bu sürümde düzeltilemez.
3. **Seçili kişi için düzeltme hazırla…** ekranına o mali satırın **yeni toplam tutarını** yazın; fark tutarını yazmayın. Gerekçede hata ve doğru tutarın dayanağını belirtin. Yeni toplam sıfırsa tam ters hareket olur.
4. Farklı bir yönetici **Değişmez düzeltme geçmişi** üzerinden eski/yeni/fark, kaynak belge, ilk defter satırı ve gerekçeyi inceleyerek **Seçili düzeltmeye bağımsız onay…** işlemini yapar. Düzeltmeyi veya asıl hesabı hazırlayan kişi ve asıl hesabın hak sahipleri, başka hesaplarını kullansalar da servis tarafından engellenir. Tarihsel hesap–kişi bağlantıları bu denetime dahildir.
5. **Düzeltme aktarım önizlemesi…** ile mevcut bakiye ve sonraki bakiyeyi kontrol edin; gerekçeyle aktarın. Onay tek başına bakiye değiştirmez. Aktarımda fark kadar bir Allocation defter satırı, bakiye değişikliği ve değişmez aktarım makbuzu aynı transaction içinde oluşur.

Örnek: İlk tutar 1.000 TL, doğru tutar 800 TL ise yeni toplam **800**, fark **−200 TL** olur. Sonradan bunun 850 TL olması gerektiği anlaşılırsa güncel satırdan yeni bir düzeltme hazırlanır: eski 800 → yeni 850, fark **+50 TL**. Tam ters hareket için yeni toplam **0 TL** seçilir. İlk 1.000 TL'lik belge ve hesap snapshot'ı her durumda korunur.

## Tekillik, sürüm ve geçmiş

- Düzeltme gövdesi, onayı ve aktarımı değiştirilemez veya silinemez. Hatalı hazırlığı düzenlemek yerine yeni hazırlık oluşturulur. Aktarılmış hatayı düzeltmek için güncel tutardan sonraki bağlantılı düzeltme gerekir.
- Aktarım zincirinin tek ilk hareketi ve tek devamı SQL tekil indeksleriyle korunur. Aynı eski tutardan hazırlanmış iki düzeltmeden biri aktarılınca diğeri güncelliğini kaybeder; eski onay yeni tutara uygulanmaz.
- Aktarım önizlemesi kart/çalışma dönemi sürümü, bakiye, düzeltme ve onay bağlamına bağlıdır. Bakiye veya kart değişirse yeni önizleme gerekir. Başarısız işlemde defter, bakiye ve makbuz birlikte geri alınır.
- İşlem kimliği ve içerik özeti tekrar gönderimi korur. Commit sonrası yanıt kaybolursa açık pencereden aynı isteği yeniden gönderin. Pencere kapandıysa önce geçmişi yenileyin; farklı kimlikle tekrar aktarım da ikinci hareket oluşturmaz.
- Kim, ne zaman, hangi gerekçeyle eski tutarı hangi yeni tutara çevirdi bilgileri saklanır. Audit kayıtları aynı transaction içindedir. Kaynak defter ve ilk belge bağlantıları geçmişte görülebilir.

## Bakiye, maliyet ve ödeme ayrımı

Proje maliyeti, personel kartındaki hakediş, proje kalan hakedişi ve maliyet CSV raporu **aktarılmış** düzeltme farklarını bir kez hesaba katar. Hazırlanan veya yalnız onaylanan farklar maliyete girmez. Belge listesinde ilk tutar, düzeltme toplamı ve güncel tutar ayrı kalır. Dönem hesabının ilk snapshot toplamı değişmez; geçmişte düzeltme sonrası aktarım toplamı ayrıca gösterilir.

Bu işlem banka iadesi/ödeme yapmaz. Önceden ödeme yapılmışsa düzeltme bakiyeyi negatife indirebilir; bu alacak/borç durumudur ve kullanıcıya önizlemede gösterilir. Proje ödeme sınırı düzeltilmiş hakedişten okunur. Günlük nakit kapanış snapshot'ları yeniden yazılmaz; Allocation hareketi nakit ödeme sayılmaz. Ödeme/avans tahsisi ve banka iadesi ayrı işlerdir.

Aktarım ancak aynı aktif personel kartı ve aynı açık çalışma dönemi için yapılır. Arşivlenmiş/ayrılmış veya başka çalışma dönemine geçmiş kişinin eski düzeltmesi yeni bakiyeye yazılmaz; bu durumda işlem engellenir. Proje etkin olmalıdır. Kısmi/asıl hesap için toplu ters hareket yerine her kişi satırı ayrı hazırlanıp onaylanır.

## Yetki, kapsam ve geçiş

Hazırlama, bağımsız onay ve aktarım yönetici yetkisi ister. Okumada mevcut ücret ve ilgili proje maliyeti kapsamı korunur. Proje kullanıcısına genel kişi bakiyeleri açılmaz; bakiye önizlemesi yalnız yöneticidedir. İşlem doğrudan SQL bağlantısında ve merkezi API'de aynı servis üzerinden uygulanır.

Bu akış açık tutarlı mali düzeltmedir; mühürlü puantajı, oranı, onayları veya hesap kaynaklarını açmaz ve otomatik yeniden hesaplama yapmaz. Manuel bağımsız hakediş belgeleri, sıfır kaynaklı mali belgesiz aktarım satırları, kapalı çalışma dönemi alacak/borç taşınması ve onay geri çekme bu sürümde kapsanmaz.

131 betiği üç yeni tablo, iki okuma görünümü, deftere nullable düzeltme bağlantısı, tekillik/yabancı anahtar/değişmezlik/audit korumaları ekler. Mevcut kayıtlara otomatik düzeltme oluşturmaz; geçişte hesap oturum nesilleri yenilenir. Uygulanmış 100–130 betikleri değiştirilmez.

Bu teslimde canlı **1.3.20-dev / şema 130 / API 1.20**, bağlantı ayarları ve kısayol değiştirilmedi. Kullanıcı isteğiyle SQL, işlev, arayüz, eşzamanlılık ve kesinti denemeleri atlandı. Derleme ve paket bütünlüğü kontrolü çalışma zamanı doğrulaması değildir.
