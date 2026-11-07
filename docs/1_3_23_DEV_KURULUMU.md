# 1.3.23-dev yerel kurulum

6 Kasım 2026 · Mevcut şema **132** · Merkezi API **1.23**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, yedek kapsamı ve dosya özeti doğrulandı. Yedek ve önce/sonra satır sayıları/SHA-256 özetleri `artifacts/analysis/backup-1.3.23-dev/` klasöründedir.

Bekleyen şema yükseltmesi olmadığı doğrulandı; hiçbir migration çalıştırılmadı. Personel, bakiyeler, defter, mali onay/aktarım, asıl/ek hesap, düzeltme, tahsis ve serbest bırakma kayıtları korundu. Yönetici kayıtlarının oturum kimlikleri ve rowversion değerleri de karşılaştırmada değişmedi. Şema günlüğü aynen kaldı.

İstemci ve API **1.3.23-dev** için ayrı kullanıcı programları klasörüne kuruldu. ZIP ve çıkarılan dosyaların manifest özetleri doğrulandı. Sunucu/veritabanı profilleri ve kullanıcı tercihleri korundu. API otomatik başlatma görevi yeni paketi kullanır; **Muhasebe - Güncel** kısayolu yeni istemciyi açar. Son kurulum kayıtları `artifacts/analysis/install-1.3.23-dev/` altındadır.

İşlevsel, arayüz, rapor, kesinti/eşzamanlılık ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı. Masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. [Proje mali mutabakat raporu kullanım rehberi](PROJE_MALI_MUTABAKAT_RAPORU.md).
