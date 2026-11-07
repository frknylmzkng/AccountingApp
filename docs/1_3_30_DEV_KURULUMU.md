# 1.3.30-dev yerel kurulum

6 Kasım 2026 · Şema **137** · Merkezi API **1.30**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, kapsam manifesti ve SHA-256 doğrulandı. Ana yedek: `artifacts/analysis/live-upgrade-137/backups/AccountingApp-v2-<kapsam-kimliği>-20261106T1459097742656-pre137.bak`. Şema yükseltmesi ayrıca güvenlik yedeği oluşturdu; seçilen ana yedek korundu.

Yalnız 137 uygulandı; uygulanmış 100–136 betik kimlikleri korundu. Önce/sonra satır sayıları ve içerik özetleriyle mevcut personel, izin, proje, puantaj, hakediş, mali düzeltme, ödeme, avans, tahsis, bütçe, banka, mali dönem ve ücret/gider kayıtlarının aynı kaldığı doğrulandı. Hesap verileri, SessionEpoch ve rowversion dahil korundu. API'nin yeniden başlatılması bellekteki eski oturum tokenlarını geçersiz kıldı; yeniden giriş gerekir.

Yeni `ReportPresets` tablosu boş oluşturuldu. İki güvenilir CHECK, bir güvenilir proje yabancı anahtarı, birincil anahtar ve sahip/proje indeksinin etkin olduğu doğrulandı. Görünüm veya örnek veri oluşturulmadı; mali hareket ve bakiye değişikliği yapılmadı.

Paket kaynağı: `artifacts/release-bundles/report-center-137-delivery/`. ZIP özetleri, arşiv bütünlüğü, çıkarılan tüm manifest dosyaları ve sürüm bilgileri kontrol edildi. İstemci ve API ayrı `1.3.30-dev-20261106175937600` kullanıcı programları klasörüne kuruldu; önceki paketler korundu. Sunucu/istemci/veritabanı profilleri ve kullanıcı tercihleri değişmedi. Muhasebe - Güncel kısayolu yeni istemciyi açar. API HTTPS localhost:7443 üzerinde yalnız loopback adreslerinde, mevcut gizli WScript başlangıç göreviyle çalışır. Görünür konsol veya yeni oturum açılış görevi eklenmedi. Tek seferlik bakım görevi kaldırıldı. Windows yeniden başlatılmadı.

Kurulum kanıtları: `artifacts/analysis/install-1.3.30-dev/`; yedek/şema ve veri karşılaştırmaları: `artifacts/analysis/live-upgrade-137/`. Geri dönüş, uyumlu paket ve kontrollü yedek geri yükleme gerektirir; otomatik geri yükleme yapılmadı. GitHub push/Release yapılmadı.

İşlevsel, arayüz, rapor filtresi, kayıtlı görünüm, büyük ZIP, yetki iptali, yük ve eşzamanlılık denemeleri kullanıcı isteğiyle atlandı. Masaüstü uygulaması açılmadı. Yalnız yedek/veri/şema/paket bütünlüğü ve API başlangıcı kontrol edildi; bu kontroller işlevsel kabul yerine geçmez.

Kalan **4 ana iş paketi**; kurulum bu sayıyı azaltmadı. Sıradaki **yedek ve destek güvenliği**. [Kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md). [Rapor merkezi kullanım ve sınırları](RAPOR_MERKEZI_GENISLEMESI.md).
