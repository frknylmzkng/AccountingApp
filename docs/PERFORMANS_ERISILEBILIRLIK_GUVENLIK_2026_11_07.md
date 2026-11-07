# Performans, erişilebilirlik ve güvenlik incelemesi

7 Kasım 2026 · Kaynak ve yerel paket **1.3.33-dev** · Şema **137** · API sözleşmesi **1.32**. API istek ve yanıt biçimleri mevcut sözleşmeyle uyumlu. Aynalama düzeltmesi merkezi kurulumda sunucu paketinin güncellenmesiyle devreye girer.

| Alan | Kaynakta saptanan durum | Uygulanan düzeltme |
| --- | --- | --- |
| Yedek seçimi | Son yedeği bulurken tarih filtresinden geçen her arşiv tam doğrulamadan geçiriliyordu. | Adaylar önce tarihe göre sıralanır. İlk geçerli arşiv bulunduğunda doğrulama durur. Bozuk veya kapsam dışı adaydan sonraki dosyaya geçilir. |
| Sınırlı CSV raporları | İhraç işlemi satır sınırını kontrol etmek için sayfalama sorgusunun pencere toplamını da hesaplıyordu. | SQL en fazla 10.001 satır döndürür. Son satır taşma kontrolü için kullanılır. Liste önizlemesi toplam satır sayısını korur ve boş sonuçta fazladan sayfa sorgusu atlanır. |
| Aynalama | Hedefi kontrol etmekle dosyayı yayımlamak arasındaki sürede aynı adlı başka bir dosya gelebiliyordu. | Yeni kopya üzerine yazma kapalı olarak yayımlanır. Var olan aynı içerik okunurken korunur ve tekrar kopyalanmaz. Farklı dosya veya yarış durumunda yeniden deneme için hata durumu kaydedilir. |
| Şifreli arşiv durumu | Durum ekranı, başlığı okunabilen kesik arşivi listeye dahil edebiliyordu. | Beklenen dosya uzunluğu başlık, veri ve doğrulama etiketlerinden hesaplanır. Kısaltılmış ve sonuna bayt eklenmiş dosyalar durum okumalarında da reddedilir. Tam doğrulama içerik etiketlerini ve SHA-256 özetini kontrol eder. |
| Ekran okuyucu ve klavye | Sayfa numarası ve bazı filtre alanları açık bir erişilebilir ada sahip değildi. Ortak tablo açıklaması rapor merkezindeki ZIP kapsamını yanlış tarif ediyordu. | Ortak sayfa kontrolleri, rapor filtreleri ve kasa alanları adlandırıldı. Rapor ekranı klavye sırası görünümler, filtreler, komutlar, bağlantılar, sonuçlar ve sayfalama şeklinde ayarlandı. Sonuç tablosu ZIP kapsamını açıklar. [Windows Forms erişilebilirlik özellikleri](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/properties-on-windows-forms-controls-that-support-accessibility-guidelines) |

NuGet, masaüstü, API, Windows kurulum sihirbazı, veritabanı kurulum aracı ve kurtarma aracının doğrudan ve geçişli paketlerinde kullanılan kaynaklara göre bilinen güvenlik açığı raporlamadı. Windows ve SQL güvenlik durumu pilotta ayrıca değerlendirilir. [NuGet güvenlik denetimi](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)

Microsoft, 25 Ekim 2026 tarihli 10.0.14 güncellemesinde 10.0.13 sürümündeki bazı güvenlik düzeltmelerini tamamladı. Dağıtım çalışma zamanı 10.0.14, SDK 10.0.403 olarak güncellendi. 7 Kasım 2026 tarihinde .NET 10.0.14 içeren 1.3.33-dev istemci ve sunucu paketleri kuruldu. [Kurulum kaydı](1_3_33_DEV_KURULUMU.md). [Microsoft 10.0.14 güvenlik notları](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.14/10.0.14.md)

Kaynak incelemesi, Release derlemesi, ZIP/manifest dosya bütünlüğü ve bağımlılık danışma sorgusu yapıldı. Kullanıcı isteğiyle uygulama, veritabanı, yük, arayüz ve güvenlik senaryoları çalıştırılmadı. Bu nedenle süre, bellek tüketimi, ekran okuyucu uyumluluğu ve gerçek kurtarma başarısı için ölçüm sonucu bulunmuyor.

| Pilot doğrulaması | Kaydedilecek kanıt ve kabul ölçütü |
| --- | --- |
| Büyük rapor ve yedek | Aynı veriyle önceki ve yeni paket için sayfa açma ve aktarım süreleri, bellek ve SQL planı. CSV için 0, 10.000 ve 10.001 sonuçta satır/sınır davranışı. En yeni sağlam ve en yeni bozuk arşiv durumlarında seçilen kaynak ve okunan dosya sayısı. |
| Klavye, ekran okuyucu ve DPI | Yalnız klavyeyle filtreleme, sayfa değiştirme ve aktarım. Ekran okuyucuda alan adı, odak sırası ve ZIP kapsamı. 100%, 150%, 200% DPI ile yüksek kontrast ve ikinci monitörde okunabilirlik, odak ve taşma kaydı. |
| Şifreli yedek ve eşzamanlılık | Kapsam dışı arşiv, kesilmiş/ek veri taşıyan ve aynı boyutta değiştirilmiş arşiv, yanlış parola, iptal ve izin kaybı. Aynı adlı iki kopya yarışında hedef içeriğin özeti. Ayrı hedefte yetkili kurtarma ve iş kayıtlarının mutabakatı. |
| Dağıtım güvenliği | Temiz Windows kurulumunda gerçek bağlantı, LAN/TLS güveni, varsa Authenticode doğrulaması ve önceki istemci/API çiftine kontrollü dönüş. Uygulanan sürüm, paket özeti ve gerçek CI/Release kabul kaydı. |

Kaynak incelemesi tamamlandı. Kalan **1 ana iş paketi** gerçek kullanım pilotu, çalıştırılmayan kabul denemeleri ve teslimdir.
