# 1.3.28-dev yerel kurulum

6 Kasım 2026 · Şema **135** · Merkezi API **1.28**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedekleri alındı; RESTORE VERIFYONLY, kapsam manifesti ve SHA-256 doğrulandı. Başarılı yükseltmenin ana yedeği `artifacts/analysis/live-upgrade-135/backups/AccountingApp-v2-<kapsam-kimliği>-20261106T1318008839724-pre135.bak` dosyasıdır. Yükseltme ek güvenlik yedeği de oluşturdu. Önce/sonra satır sayıları ve veri özetleri aynı analiz klasöründedir.

İlk 135 taslağında SQL Server'ın ayrılmış `LineNo` sözcüğü kullanıldığı için uygulama hata verdi ve transaction geri alındı. İlk deneme kanıtları `artifacts/analysis/live-upgrade-135/first-attempt/` altındadır. Uygulanmamış 135 betiği ve runtime SQL `[LineNo]` olarak düzeltildi; istemci/API paketleri yeniden oluşturuldu. Yeni doğrulanmış yedek sonrası yalnız düzeltilmiş 135 uygulandı. 100–134 betikleri/journal kimlikleri değişmedi; otomatik veritabanı geri yüklemesi yapılmadı.

Mevcut personel, izin, proje, puantaj, hakediş, mali düzeltme, ödeme, avans, tahsis, bütçe ve banka kayıtları satır sayıları/SHA-256 özetleriyle karşılaştırıldı. Beklenen SessionEpoch/rowversion değişikliği hariç yönetici bilgileri ve eski audit satırları korundu. Oturum nesilleri yenilendi; yeniden giriş gerekir. İki yeni kapanış tablosu boş, on bir etkin tetikleyici, sekiz güvenilir CHECK, beş güvenilir yabancı anahtar, dokuz indeks ve on yedi sütunlu bir kaynak olay görünümü doğrulandı. Hiçbir dönem kapatılmadı, kaynak sahiplenilmedi veya mali hareket üretilmedi.

İstemci ve API ayrı `1.3.28-dev-20261106161832757` kullanıcı programları klasörüne kuruldu. Kullanılan paket `artifacts/release-bundles/fiscal-periods-135-corrected-delivery/` altındadır; ilk kaynak paketi SUPERSEDED olarak işaretlendi. ZIP, SHA256SUMS ve bütün manifest dosyaları doğrulandı. Sunucu/istemci/veritabanı profilleri ve kullanıcı tercihleri korundu. API yalnız HTTPS localhost:7443 üzerinde gizli başlangıç göreviyle çalışıyor. Muhasebe - Güncel kısayolu yeni istemciyi açar. Kurulum kanıtları `artifacts/analysis/install-1.3.28-dev/` altındadır. Geçici bakım görevi kaldırıldı; önceki paketler korundu. Şema geri dönüşü uyumlu paket ve kontrollü yedek geri yükleme gerektirir.

İşlevsel, arayüz, dönem/önizleme/kesinleştirme/düzeltme/CSV, eşzamanlılık ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı. Masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. Windows yeniden başlatılmadı.

**6 ana iş paketi** kaldı. Sıradaki **ek ücret/gider kuralları**. [Güncel kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md). [Mali dönem kullanım ve kapsam rehberi](KALICI_MALI_DONEM_KAPANISI.md).
