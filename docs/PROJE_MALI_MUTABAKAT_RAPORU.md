# Proje bazlı hakediş, ödeme, avans ve tahsis mutabakatı

6 Kasım 2026 · **1.3.23-dev / mevcut şema 132 / API 1.23**. Yeni şema betiği gerekmez.

İzleyen kaynak dilimi: [1.3.24-dev mali filtreler ve sayfalama](MALI_FILTRELER_VE_SAYFALAMA.md). Aşağıdaki sınırlar bu belgenin ilk sürüm kapsamının tarihsel kaydıdır.

Rapor seçilen projenin mevcut mali kayıtlarını ve tahsislerini tek Serializable transaction içinde okur. Kaynaklar SQL tarafında ayrı ayrı özetlenir; bir hakedişin çok sayıda ödeme/tahsise bağlanması tutarları çoğaltmaz. Veri zamanı, bütün bölümler okunduktan sonra veritabanından UTC olarak alınır. Bu zaman geçmiş bir tarih filtresi değildir; rapor güncel durumun anlık görünümüdür.

## Ekran

Merkezde proje seçip **Hakediş, avans ve proje maliyeti → Proje mutabakat raporu…** düğmesini kullanın. Proje kullanıcıları kendi projelerinin finans bölümünden aynı raporu açar. Altı bölüm bulunur:

1. **Proje özeti:** ilk hakediş, aktarılmış mali düzeltme, net hakediş; avans, hakediş ödemesi ve sınıflandırılmamış ödeme çıkışı; her iki taraftaki aktif tahsis, ayrı açık avans/ödeme ve proje içi net konum.
2. **Kişi / çalışma dönemi:** aynı tutarların kalıcı kişi ve EmploymentId bazında özeti. Yeniden işe giriş dönemleri birleşmez. İsim proje hesabındaki değişmez kaynak snapshot'ından gelir; böyle bir snapshot yoksa isim tahmin edilmez, kalıcı kimlik gösterilir. TC/IBAN veya genel kart bakiyesi alınmaz.
3. **Hakediş belgeleri:** kaynak belge, ilk tutar, düzeltme, net tutar, aktif tahsis ve açık tutar. İptal edilmiş belgeler de tarihçede görünür; **özete giren hakediş** sütunu iptaller için sıfırdır.
4. **Ödeme ve avans:** ödeme defter/istek kimliği, türü, kaydedilmiş çıkış, aktif tahsis, açık tutar ve tahsis kapsamına uygunluk. İptal edilmiş kaynaklar geçmişte korunur; **özete giren ödeme** sütunu iptaller için sıfırdır. İptal nakit çıkışını ikinci kez toplamaz.
5. **Tahsis geçmişi:** aktif ve serbest bırakılmış bağlantılar, kaynak belge/defter numarası, tutar, gerekçe, aktör ve zaman. Kaynakların halen geçerli olup olmadığı ayrıca gösterilir. Özet yalnız aktif bağlantıları içerir.
6. **İstisnalar:** eksik kişi/çalışma dönemi bağlantısı, sınıflandırılmamış/işlem kimliği eksik veya geçersiz tutarlı ödeme, kaynak tutarını aşan tahsis ve geçersiz kaynak kapsamı/durumu. İstisna bulunması yeni kayıt veya otomatik veri düzeltmesi başlatmaz.

Ekranda veri zamanı, oluşturan hesap, istisna sayısı ve tahsisin iki tarafı arasındaki fark gösterilir. Bir bölüm 10.000 satırı aşarsa rapor kısaltılıp eksiksizmiş gibi sunulmaz; işlem durur. Bu sınır tahsis ekranındaki 500 satırdan bağımsızdır. Kapsam şu anda projenin tüm geçmişidir; tarih/kişi filtresi, geniş geçmiş sayfalaması ve kalıcı rapor snapshot'ı ayrı genişlemedir.

## Tutarların anlamı

| Alan | Hesap |
|---|---|
| Net hakediş | İptal edilmemiş ilk hakediş + aktarılmış mali düzeltme farkları |
| Ödeme çıkışı | İptal edilmemiş Payment kaynakları; avans/hakediş ödemesi/diğer ayrı |
| Tahsis edilmemiş hakediş | Net hakediş − hakediş tarafındaki aktif tahsis |
| Tahsis edilmemiş ödeme | Ödeme çıkışı − ödeme tarafındaki aktif tahsis |
| Tahsis edilmemiş avans | Avans çıkışı − avans kaynaklı aktif tahsis |
| Proje içi net konum | Net hakediş − ödeme çıkışı |
| Tahsis tarafları farkı | Ödemeden aktif tahsis − hakedişe aktif tahsis |

Örneğin 1.000 TL net hakediş ve 500 TL avans varsa proje içi net konum **500 TL**'dir. Avans henüz tahsis edilmemişse açık hakediş **1.000 TL**, açık ödeme **500 TL** görünür. Avansın 500 TL'si tahsis edildiğinde açık hakediş **500 TL**, açık ödeme **0 TL** olur; net konum değişmez. Dolayısıyla **tahsis edilmemiş hakediş, ödenmemiş tutar değildir**. Tahsis veya serbest bırakma yeni ödeme/maliyet oluşturmaz.

Tahsis tarafları farkının sıfır olması tek başına kayıtların doğru eşlendiğini veya banka mutabakatını kanıtlamaz. Kişi/dönem satırları, kaynak kayıtlar ve istisnalar birlikte incelenmelidir. Negatif net konum proje içinde hakedişten fazla ödeme bulunduğunu gösterebilir; kişinin başka projelerindeki alacak/borcu bu rapora katılmaz. Diğer proje giderleri bu işçilik/hakediş mutabakatının kapsamı dışındadır.

## CSV ve yetki

**Seçili bölümü güncel CSV olarak kaydet…** düğmesi yalnız seçili altı bölümden birini, sunucunun yeniden okuduğu güncel veriden üretir. Ekrandaki eski veya istemcinin gönderdiği tablo dışa aktarılmaz. CSV'de proje/kimliği, bölüm, kapsam, veri zamanı ve raporu oluşturan hesap her satırda bulunur; boş bölümde bu bilgileri içeren boş veri satırı yazılır. Ekran ve dosya farklı zamanlarda üretilebilir; veri zamanlarını karşılaştırın. Dosya boyutu 32 MB ile sınırlıdır.

Okuma mevcut ücret + proje maliyeti + proje ödeme erişimi ister. Dışa aktarma ayrıca mevcut proje dışa aktarma yetkisini ister. Bu haklardan biri yoksa API de isteği reddeder. Ücret/TC/IBAN yetkileri genişletilmez; başka projenin belge/kişi ayrıntıları rapora alınmaz. CSV işlemi gerekçeli audit olayıyla kaydedilir ve yetki kilitleri teslim bitene kadar korunur.

Rapor, bankadan gerçekleşme bilgisi getirmez; projesiz kayıtları projeye dağıtmaz; hazırlık/onay bekleyen hakedişi veya ödeme talebini gerçekleşmiş para hareketi saymaz. Projesi tanımlı eski sınıflandırılmamış ödemeler toplamda ayrı gösterilir ve istisna oluşturur. Kaynak/mutabakat farkları otomatik mahsup edilmez.

## Teslim durumu

1.3.23-dev istemci ve API birlikte kullanılmalıdır; şema **132** korunur. Uygulanmış 100–132 betikleri değiştirilmedi. Canlı **1.3.22-dev / API 1.22**, veritabanı, bağlantı ayarları ve kısayol bu kaynak tesliminde değiştirilmedi. Kullanıcı isteğiyle SQL, işlev, arayüz ve eşzamanlılık denemeleri atlandı; derleme ve paket bütünlüğü kontrolü çalışma zamanı doğrulaması değildir.
