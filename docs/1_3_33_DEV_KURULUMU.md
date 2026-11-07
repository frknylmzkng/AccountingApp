# 1.3.33-dev yerel kurulum

7 Kasım 2026 tarihinde **1.3.33-dev istemci ve API** kuruldu. Paketler **.NET 10.0.14**, şema **137** ve API sözleşmesi **1.32** ile doğrulandı. **Muhasebe - Güncel** kısayolu yeni istemciye yönlendirildi. API mevcut gizli görevle `https://localhost:7443/` üzerinde çalışıyor. Yeni oturum için yeniden giriş gerekiyor.

Kurulum öncesi SQL Server COPY_ONLY/CHECKSUM yedeği alındı. Kapsam kimliği, SQL VERIFYONLY ve manifest SHA-256 özeti doğrulandı. Şifresiz `.bak` dosyası mevcut Windows hesabı ve SYSTEM erişimine açık özel klasörde saklanıyor.

`artifacts/analysis/preinstall-backup-1.3.33/backups/AccountingApp-v2-<kapsam-kimliği>-20261107T0353363750793-pre-1.3.33.bak`

Yedek öncesi ve sonrası 61 tablonun içerik özetleri eşleşti. Mevcut veriler, bağlantı profilleri ve kullanıcı tercihleri korundu. Kurulum için şema yükseltmesi gerekmedi. Geçici yedek görevi kaldırıldı. İşlevsel denemeler isteğe göre atlandı. Kurulum kanıtları `artifacts/analysis/install-1.3.33-dev/` klasöründe.

**1 ana iş paketi** açık. Gerçek kullanım pilotu, kabul denemeleri ve teslim. [Pilot matrisi](PERFORMANS_ERISILEBILIRLIK_GUVENLIK_2026_11_07.md)
