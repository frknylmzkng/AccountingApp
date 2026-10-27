# Fiş ve günlük kapanışta güncel veri

26 Ekim 2026. Güncel analizdeki 4. bulgu giderildi: açık Ödemeler ekranındaki eski defter satırları artık fiş veya kapanış raporunun veri kaynağı değildir.

## Davranış

- **Seçili ödeme fişi:** seçim anındaki özgün RequestId alınır. Ödeme, güncel oturum yetkisi doğrulanarak veritabanından yeniden okunur. Başka oturum ödeme iptal etmişse F5 gerekmeksizin fişte “İptal edildi” ve iptal zamanı görünür. Ödeme bulunamazsa önizleme oluşturulmaz; eski veriyle devam edilmez.
- **Kapanış önizlemesi:** seçilen gün, beyan ve kâğıt seçimi işlem başlamadan alınır. Ödemeler seçilen yerel günün UTC başlangıç/bitiş aralığıyla yeniden sorgulanır. Başlangıç dahil, ertesi gün başlangıcı hariçtir; yaz/kış saati günleri sabit 24 saat varsayılmaz.
- **Tutarlılık:** yetki kontrolü, rapor verisi ve SQL Server veri zamanı aynı Serializable transaction içindedir. Ayrıntılar, adet ve toplam aynı okunan satırlardan üretilir. Okuma hatası, iptal veya oturum iptalinde önbellekten rapor üretilmez. Rapor okuması ödeme/bakiye/defter/audit değişikliği yapmaz.
- **Veri anı:** iki raporda da UTC veri zamanı ve raporu oluşturan kullanıcı bulunur. Rapor o anın durumudur; önizleme açıkken daha sonra yapılan işlemleri kendiliğinden değiştirmez. Güncel rapor için önizlemeyi yeniden oluşturun.

## Kapanış kuralı

Bu rapor kesinleştirilmiş ve saklanan bir tarihsel kapanış değildir; başlık ve açıklamada **güncel durum önizlemesi** olduğu belirtilir. Ödeme kendi kayıt gününe aittir. Sonradan iptal edilmişse özgün gününün güncel toplamından çıkarılır; iptal hareketi iptal gününe yeni bir ödeme olarak eklenmez. Bu, önceki hesaplama kuralını korur ve görünür hale getirir. Sabit tarihsel kapanış/iptal hareketli ayrı dönem muhasebesi bu değişikliğin kapsamı değildir.

## Eski fiş ve yeniden oluşturulan personel

Fiş ödeme defterindeki tutar, grup, kaydeden, kayıt/iptal zamanı ve durumdan hazırlanır. Güncel personel IBAN'ı, bakiyesi ve izin tarihleri artık geçmiş ödeme fişine eklenmez: bu bilgiler ödeme anının görüntüsü değildir ve aynı TC ile yeniden oluşturulan başka bir personel kaydına ait olabilir. TC maskeli kalır. Personel silinse bile mevcut ödeme fişi işlem kimliğiyle alınabilir. Ödeme anındaki IBAN/ad gibi tarihsel özet alanları bu çalışmada oluşturulmadı.

## Deneme adımları

Yalnızca deneme personellerini ve iki oturumu kullanın:

1. A'da Ödemeler ekranını açıp bir ödeme satırını seçin. B'de o ödemeyi iptal edin. A'da F5 yapmadan Seçili ödeme fişi: durum iptal edilmiş görünmeli.
2. A'nın ekranı açıkken B'de yeni ödeme kaydedin. A'da o gün ve doğru beyanı girip Kapanış önizlemesi açın: yeni ödeme adet/toplama dahil olmalı.
3. B'de bir ödeme iptal edin. A'da F5 yapmadan kapanışı yeniden oluşturun: iptal edilen ödeme çıkarılmalı.
4. Önceki bir günün ödemesini bugün iptal edin: önceki günün güncel önizlemesi azalmalı; bugünkü ödeme toplamına iptal tutarı ödeme olarak eklenmemeli.
5. Raporda veri zamanı ve oluşturan kullanıcıyı kontrol edin. Fiş referansı seçilen ödemenin RequestId değeridir.

## Doğrulama ve paket

Tam takım: **509 başarılı, 0 başarısız, 0 atlanan**; önceki 490 test korundu, 19 rapor testi eklendi. Release derlemesi: 0 uyarı, 0 hata. Kanıt: `artifacts/analysis/master-20261026-171519-136/master.trx` ve `summary.json`.

Testler eski ekran sonrası yeni ödeme/iptal, özgün seçim kimliği, silinen/yeniden oluşturulan personelin fişi, bulunmayan/ödeme olmayan işlem, gün sınırları, yaz/kış saati, sonraki gün iptali, bütün okuma rolleri, oturum iptali, işlem iptali ve commit edilmemiş veriyi okumamayı kapsar. Ödemeler formunun gerçek olay işleyicileri eski tabloyla çalıştırılıp önizlemeye gönderilen rapor da doğrulandı; bu test fiziksel yazıcı kullanmaz.

Testler ayrı LocalDB motorunda çalıştı; geçici motor temizlendi. Gerçek veritabanına ve açık uygulamaya müdahale edilmedi. Yeni SQL betiği gerekmiyor; 105 dahil mevcut şema kullanılıyor. Güncel yerel paket: `artifacts/releases/fresh-payment-reports/`. Masaüstü kısayolu bu geliştirme sırasında değiştirilmedi; GitHub'a push veya Release yapılmadı.

## 26 Ekim 2026 — yayımlanan yerel pakette ekran denemesi

Kullanıcı isteğiyle eski uygulama kapatıldı. Muhasebe - Güncel kısayolu `artifacts/releases/fresh-payment-reports/WinFormsApp2.exe` paketine yönlendirildi; bağlantı dosyası korunup SHA256 ile eşleştirildi. Giriş hazırlığı başarılı; gerçek bağlantıyla uygulama açık bırakıldı. Yeni şema işlemi yapılmadı.

Yayımlanan DLL ile aynı SHA256 değerine sahip gerçek Ödemeler formu, ayrı LocalDB motorunda iki sentetik hesapla denendi. Ekrandaki defter test boyunca ilk 30 TL ödeme ve boş iptal durumunu göstermeye devam etti; F5 yapılmadı. UI düğmelerinden oluşturulan **6 raporun tamamı doğru**, ayrıca personel/defter/audit değişmeme kontrolü başarılı:

| Önizleme | Diğer oturumdaki değişiklik | Beklenen ve doğrulanan sonuç |
| --- | --- | --- |
| A4 fiş | 30 TL ödeme iptali | 30 TL fişte İptal edildi, iptal ve veri zamanı, raporu oluşturan kullanıcı |
| A4 kapanış | İptalin ardından 45 TL yeni ödeme | 1 ödeme, toplam 45 TL, 50 TL beyana fark +5 TL |
| A4 kapanış | 20 TL daha ödeme | 2 ödeme, toplam 65 TL, fark −15 TL |
| A4 kapanış | 45 TL ödeme iptali | 1 ödeme, toplam 20 TL, fark +30 TL |
| 80 mm fiş | Eski 30 TL seçimi korunuyor | Güncel İptal edildi durumu; termal kâğıt ölçüsü |
| 80 mm kapanış | Güncel geçerli tutar 20 TL | 1 ödeme, toplam 20 TL, fark +30 TL; termal kâğıt ölçüsü |

İlk fiş üretim yolundaki standart modal baskı önizlemesi açılıp görüldü. Otomasyon aracının bağlı modal pencereyi kapatma/hedefleme kısıtı nedeniyle ilk geçici test süreci sonlandırıldı; ona ait veritabanı/motor ayrı temizlendi. Kalan denemeler, paket formunun gerçek düğme işleyicileri ve ürettiği raporlar korunarak, aynı PrintDocument ve yerel PrintPreviewDialog ile ayrı modeless test penceresinde tamamlandı. Uygulama kodu veya paket DLL'i değiştirilmedi. Bu yöntem normal uygulamadaki modal Kapat etkileşiminin otomatik doğrulaması sayılmaz.

A4 kapanış, 80 mm fiş ve 80 mm kapanış aynı paket `PaymentPrinting.Draw` koduyla ayrıca görüntüye çevrilip incelendi. Fiziksel yazıcıya iş gönderilmedi. Raporlar ham TC/IBAN içermedi; doğru işlem referansı ve kullanıcı/veri zamanı doğrulandı. Rapor okumaları test personeli, ödeme defteri ve audit'i değiştirmedi. Son geçici test süreci normal kapatıldı; veritabanı ve LocalDB motoru temizlendi. Gerçek kullanıcı kayıtları test edilmedi/değiştirilmedi.

Kanıtlar: `artifacts/analysis/payment-report-live-ui/ui-results.json`, `report-1.json`–`report-6.json`, ilgili sayfa PNG'leri, `verification.json` ve `cleanup.txt`. DLL SHA256: `877E4E39C936CD5E57D0287E2C589DD05E23E7B52F3F71ABED432B19F87DD92A`. Önceki tam takım 509 başarılı testtir; bu adım paket üzerinde ekran doğrulamasıdır. GitHub'a push veya yeni Release yapılmadı.
