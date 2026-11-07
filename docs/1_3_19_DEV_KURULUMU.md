# 1.3.19-dev yerel kurulum

6 Kasım 2026 · Şema **129** · Merkezi API **1.19**.

Kullanıcı onayıyla mevcut veritabanı COPY_ONLY/CHECKSUM yöntemiyle yedeklendi; RESTORE VERIFYONLY ve veritabanı kapsamı/özet bilgileri doğrulandı. Yedek ve bakım kayıtları `artifacts/analysis/live-upgrade-129/` klasöründedir.

129 betiğinin ilk uygulamasında yeni sütunların aynı komut grubundaki indeksler tarafından kullanılması SQL Server derleme hatasına yol açtı. İşlem geri alındı. Sütun ekleme, kısıt ve indeks komutları GO ile ayrıldı; düzeltilmiş istemci ve API paketleri `artifacts/release-bundles/accrual-posting-129-install-final/` altında yeniden oluşturuldu. Önceki denemenin kayıtları korunarak yeni yedek alındı ve yükseltme başarıyla uygulandı.

Mevcut personel, bakiye, ödeme defteri, hesap, tarife, dönem hesabı ve diğer iş kayıtları yükseltme öncesi/sonrası satır sayıları ve SHA-256 özetleriyle karşılaştırıldı; değişmedi. Hesapların oturum kimlikleri yenilendi. Üç yeni tablo boş olarak oluşturuldu; hiçbir dönem hesabı otomatik onaylanmadı veya aktarılmadı.

Yeni şemada üç tablo, sekiz etkin tetikleyici, yeni tablolarda beş güvenilir CHECK ve yedi yabancı anahtar; mevcut finans/defter tablolarında üç ek yabancı anahtar ve finans kaynak kısıtı doğrulandı. Tekil aktarım ve kaynak indeksleri etkin. Bu kontroller kurulum doğrulamasıdır; kullanıcı iş akışları denenmedi.

İstemci ve API, kullanıcı programları klasörüne ayrı **1.3.19-dev** dizini olarak kuruldu. ZIP SHA-256 ve çıkarılan dosyaların manifest özetleri doğrulandı. Mevcut sunucu/veritabanı bağlantıları ve kullanıcı tercihleri korundu. API otomatik başlatma görevi yeni pakete yönlendirildi; masaüstündeki **Muhasebe - Güncel** kısayolu yeni istemciyi açar.

Derleme dışında işlevsel, SQL senaryo, arayüz veya ödeme denemesi çalıştırılmadı. Masaüstü uygulaması otomatik açılmadı. Kullanım akışı için [dönem hakedişi mali onay ve aktarım rehberine](DONEM_HAKEDISI_MALI_ONAY_VE_AKTARIM.md) bakın.
