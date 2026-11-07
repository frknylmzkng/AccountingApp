# 1.3.20-dev yerel kurulum

6 Kasım 2026 · Şema **130** · Merkezi API **1.20**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, yedek kapsamı ve dosya özeti doğrulandı. Yedek, önce/sonra veri özetleri ve bakım kayıtları `artifacts/analysis/live-upgrade-130/` klasöründedir.

Yalnız 130 yükseltmesinin beklediği doğrulandı; uygulanmış 100–129 betikleri değiştirilmedi. 130 transaction içinde uygulandı. Mevcut personel, bakiye, defter, mali onay/aktarım, tarife, hesap ve diğer iş kayıtları önce/sonra satır sayıları ve SHA-256 özetleriyle karşılaştırıldı; korundu. Hesap oturum nesilleri yenilendi.

ParentCalculationId sütunu, asıl hesap yabancı anahtarı, kendine bağlanma CHECK kısıtı, değişmez bağlantı tetikleyicisi, filtreli tek asıl hesap indeksi ve asıl bağlantı indeksi doğrulandı. Kaynak tekilliği etkin kaldı. Hiçbir ek hesap otomatik oluşturulmadı; mevcut hesapların bağlantısı boş kaldı.

İstemci ve API, **1.3.20-dev** için ayrı kullanıcı programları klasörüne kuruldu. Paket ZIP özetleri ve çıkarılan dosyaların manifest özetleri doğrulandı. Mevcut bağlantı profilleri ve kullanıcı tercihleri korundu. API otomatik başlatma görevi yeni paketi kullanır; **Muhasebe - Güncel** kısayolu yeni istemciyi açar. Kurulum sonuçları `artifacts/analysis/install-1.3.20-dev/` altında saklanır.

İşlevsel, arayüz, ödeme ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı; masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/şema/paket ve API başlangıç kontrolleri yapıldı. [Ek dönem hesabı kullanım rehberi](EK_DONEM_HAKEDISI.md).
