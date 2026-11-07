# 1.3.22-dev yerel kurulum

6 Kasım 2026 · Şema **132** · Merkezi API **1.22**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, yedek kapsamı ve dosya özeti doğrulandı. Yedek ve önce/sonra veri özetleri `artifacts/analysis/live-upgrade-132/` klasöründedir.

Yalnız 132 yükseltmesinin beklediği doğrulandı ve transaction içinde uygulandı. Uygulanmış 100–131 betikleri değiştirilmedi. Mevcut personel, bakiye, defter, mali onay/aktarım, tarife, asıl/ek hesap, mali düzeltme ve diğer iş kayıtları önce/sonra satır sayıları ve SHA-256 özetleriyle karşılaştırılarak korundu. Hesap oturum nesilleri yenilendi.

İki yeni tablo boş oluşturuldu; yedi etkin koruma/audit tetikleyicisi, bir güvenilir CHECK kısıtı, dört yabancı anahtar, beş indeks ve aktif tahsis görünümü doğrulandı. Mevcut mali düzeltme görünümleri, kaynak/asıl dönem tekilliği korundu. Otomatik tahsis, ödeme veya bakiye hareketi oluşturulmadı.

İstemci ve API **1.3.22-dev** için ayrı kullanıcı programları klasörüne kuruldu. ZIP özetleri ve çıkarılan dosyaların manifest özetleri doğrulandı. Sunucu/veritabanı profilleri ile kullanıcı tercihleri korundu. API otomatik başlatma görevi yeni paketi kullanır; **Muhasebe - Güncel** kısayolu yeni istemciyi açar. Son kurulum kayıtları `artifacts/analysis/install-1.3.22-dev/` altındadır.

İşlevsel, arayüz, ödeme, kesinti/eşzamanlılık ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı; masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/şema/paket ve API başlangıç kontrolleri yapıldı. [Ödeme ve avans tahsisi kullanım rehberi](ODEME_VE_AVANS_TAHSISI.md).
