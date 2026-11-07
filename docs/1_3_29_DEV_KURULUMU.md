# 1.3.29-dev yerel kurulum

6 Kasım 2026 · Şema **136** · Merkezi API **1.29**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, kapsam manifesti ve SHA-256 doğrulandı. Başarılı yükseltmenin ana yedeği `artifacts/analysis/live-upgrade-136/backups/AccountingApp-v2-<kapsam-kimliği>-20261106T1422341278194-pre136.bak` dosyasıdır. Şema yükseltmesi ayrıca güvenlik yedeği oluşturdu. Önce/sonra satır sayıları ve veri özetleri aynı analiz klasöründedir.

Yalnız 136 uygulandı; 100–135 betikleri ve migration kimlikleri korundu. Personel, izin, proje, puantaj, hakediş, mali düzeltme, ödeme, avans, tahsis, bütçe, banka ve kesin dönem kayıtları karşılaştırıldı. Beklenen SessionEpoch/rowversion değişikliği hariç yönetici bilgileri ve eski audit satırları korundu. Eski oturumlar geçersizleştirildi; yeniden giriş gerekir.

Beş yeni ücret/gider tablosu boş olarak oluşturuldu. On beş etkin tetikleyici, yeni tablolarda dokuz güvenilir CHECK, on iki güvenilir yabancı anahtar ve on altı indeks doğrulandı. Eski finans/defter tablolarında üç nullable kaynak sütunu, iki yabancı anahtar, iki tekil indeks ve bir CHECK eklendi. Geçmiş satırlara ücret türü/kuralı tahmin edilmedi. Hiçbir kural, talep, onay, aktarım veya ters aktarım oluşturulmadı; bakiye değişmedi.

İstemci ve API ayrı `1.3.29-dev-20261106172253565` kullanıcı programları klasörüne kuruldu. Kullanılan paket `artifacts/release-bundles/wage-rules-136-delivery/` altındadır. ZIP ve çıkarılan bütün manifest dosyalarının özetleri doğrulandı. Sunucu/istemci/veritabanı profilleri ve kullanıcı tercihleri korundu. API yalnız HTTPS localhost:7443 üzerinde gizli başlangıç göreviyle çalışıyor. Muhasebe - Güncel kısayolu yeni istemciyi açar. Kurulum kanıtları `artifacts/analysis/install-1.3.29-dev/` altındadır. Geçici bakım görevi kaldırıldı; önceki paketler korundu. Şema geri dönüşü uyumlu paket ve kontrollü yedek geri yükleme gerektirir.

İşlevsel, arayüz, ücret/gider formülü, bağımsız onay, kapsam çakışması, aktarım/ters aktarım, eşzamanlılık ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı. Masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. Windows yeniden başlatılmadı.

**5 ana iş paketi** kaldı. Sıradaki **rapor merkezi genişlemesi**. [Güncel kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md). [Ücret/gider kullanım ve kapsam rehberi](EK_UCRET_VE_GIDER_KURALLARI.md).
