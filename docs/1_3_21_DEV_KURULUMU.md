# 1.3.21-dev yerel kurulum

6 Kasım 2026 · Şema **131** · Merkezi API **1.21**.

Mevcut veritabanı için kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, yedek kapsamı ve dosya özeti doğrulandı. Yedek ve önce/sonra veri özetleri `artifacts/analysis/live-upgrade-131/` klasöründedir.

Yalnız 131 yükseltmesinin beklediği doğrulandı ve transaction içinde uygulandı. Uygulanmış 100–130 betikleri değiştirilmedi. Mevcut personel, bakiye, defter, mali onay/aktarım, tarife, asıl/ek hesap ve diğer iş kayıtları önce/sonra satır sayıları ve SHA-256 özetleriyle karşılaştırılarak korundu. Hesap oturum nesilleri yenilendi.

Üç yeni tablo boş oluşturuldu; yedi etkin koruma/audit tetikleyicisi, iki güvenilir CHECK kısıtı, yeni tablolarda sekiz yabancı anahtar ve on indeks doğrulandı. İki maliyet/güncel tutar görünümü, defter düzeltme sütunu/yabancı anahtarı ve üç filtreli zincir/defter tekillik indeksi doğrulandı. Mevcut kaynak ve asıl dönem tekilliği korundu. Otomatik düzeltme, ters hareket veya ödeme yapılmadı.

İstemci ve API **1.3.21-dev** için ayrı kullanıcı programları klasörüne kuruldu. ZIP özetleri ve çıkarılan dosyaların manifest özetleri doğrulandı. Sunucu/veritabanı profilleri ile kullanıcı tercihleri korundu. API otomatik başlatma görevi yeni paketi kullanır; **Muhasebe - Güncel** kısayolu yeni istemciyi açar. Son kurulum kayıtları `artifacts/analysis/install-1.3.21-dev/` altındadır.

İşlevsel, arayüz, ödeme, kesinti/eşzamanlılık ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı; masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/şema/paket ve API başlangıç kontrolleri yapıldı. [Bağlı mali düzeltme ve ters hareket kullanım rehberi](BAGLI_MALI_DUZELTME_VE_TERS_HAREKET.md).
