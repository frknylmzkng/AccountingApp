# Proje bütçesi ve gerçekleşen maliyet sapması

6 Kasım 2026 · **1.3.26-dev / uygulanmış şema 133 / API 1.26**.

## Kullanım

Seçili projenin finans bölümünden **Proje bütçesi / maliyet sapması…** ekranını açın. Merkez yöneticisi aynı düğmeyi Hakediş, avans ve proje maliyeti ekranında kullanabilir. Okuma mevcut ücret + proje maliyeti erişimini gerektirir; bütçe kaydı yalnız yöneticidedir. CSV için ayrıca proje dışa aktarma yetkisi gerekir.

**İlk bütçeyi / yeni revizyonu kaydet…** ekranında tüm proje ömrü için iki yeni toplam girilir:

- İşçilik bütçesi TL.
- Diğer gider bütçesi TL.

Fark tutarı girilmez. Her tutar sıfır veya pozitif ve iki ondalıklıdır; toplam mevcut para sınırını aşamaz. Gerekçe zorunludur. Önceki bütçe değiştirilemez/silinemez; önceki kimliğe bağlı yeni revizyon eklenir. Aynı proje için tek revizyon zinciri korunur. Başka kullanıcı arada bütçeyi değiştirmiş veya proje bilgisi değişmişse eski seçimle kayıt reddedilir. Sonuç belirsizse açık pencerede aynı isteği yeniden gönderin; pencere kapandıysa önce geçmişi yenileyin.

Proje pasifken geçmiş ve maliyet okunabilir, yeni bütçe kaydedilemez. Bütçe bir plan kaydıdır; bakiye, hakediş, ödeme, avans veya tahsis oluşturmaz. Bütçe aşımı mali işlemleri otomatik engellemez.

## Hesap ilkeleri

| Alan | Kural |
|---|---|
| Toplam bütçe | Güncel işçilik bütçesi + güncel diğer gider bütçesi |
| Gerçekleşen işçilik | İptal edilmemiş hakediş belgeleri ve aktarılmış bağlı mali düzeltmelerin net toplamı |
| Gerçekleşen diğer gider | İptal edilmemiş Expense belgeleri |
| Gerçekleşen toplam | İşçilik + diğer gider |
| Sapma | Gerçekleşen − bütçe; pozitif değer aşım, negatif değer bütçe altıdır |
| Kalan bütçe | Bütçe − gerçekleşen; aşımda negatif olabilir |
| Bütçe kullanımı % | Gerçekleşen / bütçe × 100; sıfır veya tanımsız bütçede boş |

Örneğin 100.000 TL işçilik ve 20.000 TL diğer gider bütçesinde, gerçekleşen 110.000 + 15.000 TL ise toplam sapma **+5.000 TL**, kalan bütçe **−5.000 TL**, kullanım **%104,17** olur. İşçilik aşımı ve diğer giderdeki bütçe altı tutarı ayrı görünür.

Bütçe hiç girilmediyse gerçekleşen maliyetler yine gösterilir; bütçe/sapma/yüzde alanları boş kalır. Açıkça **0 TL bütçe** kaydedilmişse gerçekleşen tutar kadar aşım hesaplanır; yüzde hesaplanmaz. Tanımsız bütçe sessizce sıfır kabul edilmez.

Ödeme, avans, serbest bırakma ve tahsis maliyete yeniden eklenmez. Hazırlık/onay bekleyen puantaj veya aktarılmamış hakediş hesabı gerçekleşmiş gider sayılmaz. Hakediş hesabı ve manuel belgeler arasında otomatik mükerrerlik eşleştirmesi yapılmadığı mevcut sınırı korunur; bağımsız olarak kaydedilmiş belgelerin iş anlamı kullanıcı tarafından denetlenmelidir.

## Bölümler ve CSV

- Özet: güncel bütçe, revizyon bilgisi, gerçekleşen tutarlar ve kategori/toplam sapmaları.
- Değişmez bütçe revizyonları: önceki bağlantı, tutarlar, para birimi, gerekçe, aktör, sıra ve zaman.
- Gerçekleşen maliyet belgeleri: ilk/düzeltilmiş tutar ve özete giren aktif maliyet. İptaller tarihçede görünür; aktif katkıları sıfırdır.

Geçmiş ve maliyet belgeleri bağımsız SQL sayfalaması kullanır. Özet sayfadan bağımsız olarak projenin bütün kayıtlarını toplar. CSV seçili bölümün tüm sonuçlarını sunucuda yeniden oluşturur; 10.000 satır/bölüm ve 32 MB sınırı vardır. Proje, kapsam, veri zamanı ve oluşturan hesap dosyada tutulur; dışa aktarma audit edilir. Önizleme ve CSV farklı veri zamanlarında oluşabilir.

Kapsam **tüm proje ömrü ve güncel TRY bütçesidir**. Dönemsel/ekip bazlı bütçe, bütçe yürürlük tarihleri, geçmiş bir tarihteki bütçe durumunun rekonstrüksiyonu, döviz çevrimi, bütçe onay iş akışı ve bütçeye göre ödeme blokajı bu dilimde yoktur. CSV'ye tarih/kişi filtresi gönderilirse sessizce yok sayılmaz, istek reddedilir.

## Geçiş ve teslim

133 yalnız yeni değişmez bütçe revizyon tablosu, zincir tekilliği, yabancı anahtar/CHECK, koruma ve audit tetikleyicilerini ekler. Mevcut projelere otomatik bütçe atanmaz; geçmiş tutarlar tahmin edilmez. Geçişte hesap oturum nesilleri yenilenir. Uygulanmış 100–132 betikleri değiştirilmedi.

6 Kasım 2026 tarihinde doğrulanmış yedek sonrası 133 uygulandı; **1.3.26-dev / API 1.26 / şema 133** yerel kurulumu tamamlandı. Eski veriler/bakiyeler korundu ve oturumlar yenilendi. Kısayol yeni istemciye yönlendirildi. Denemeler kullanıcı isteğiyle atlandı; yalnız yedek, migration/veri, paket ve API başlangıç kontrolleri yapıldı. [Kurulum kaydı](1_3_26_DEV_KURULUMU.md).

Windows başlangıcı için ayrı, sınırlı yapılandırma değişikliği yapıldı; [tanı ve değişiklik kaydı](WINDOWS_BASLANGIC_VE_KONSOL_2026_11_06.md). [Güncel kalan iş paketleri](KALAN_IS_PAKETLERI_2026_11_06.md).
