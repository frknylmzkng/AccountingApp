# 1.3.27-dev yerel kurulum

6 Kasım 2026 · Şema **134** · Merkezi API **1.27**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı; RESTORE VERIFYONLY, kapsam manifesti ve SHA-256 doğrulandı. Yükseltme ayrıca ikinci güvenlik yedeği oluşturdu. Yedekler ve önce/sonra veri özetleri `artifacts/analysis/live-upgrade-134/` altındadır. Ana yedek: `backups/AccountingApp-v2-<kapsam-kimliği>-20261106T1238345994375-pre134.bak`.

Yalnız 134 uygulandı; 100–133 migration kimlikleri korundu. Mevcut personel, izin, proje, puantaj, hakediş, mali düzeltme, ödeme, avans, tahsis ve bütçe kayıtları satır sayıları/SHA-256 özetleriyle karşılaştırıldı. Beklenen SessionEpoch/rowversion değişikliği hariç yönetici bilgileri ve eski audit kayıtları korundu. Oturum nesilleri yenilendi; yeniden giriş gerekir.

Dört banka tablosu boş olarak oluşturuldu. Dokuz etkin koruma/audit tetikleyicisi, yedi güvenilir CHECK, beş güvenilir yabancı anahtar, on altı banka tablo indeksi ve bir ek ödeme aday indeksi doğrulandı. Eski ödemelerden banka hareketi veya bağlantı tahmin edilmedi; ödeme/bakiye hareketi oluşturulmadı.

İstemci ve API ayrı `1.3.27-dev-20261106153854923` kullanıcı programları klasörüne kuruldu. Kullanılan paket `artifacts/release-bundles/bank-reconciliation-134-utf8-final/` altındadır. ZIP ve çıkarılan dosyaların manifest özetleri doğrulandı. Veritabanı/sunucu/istemci profilleri ve kullanıcı tercihleri korundu. API yalnız HTTPS localhost:7443 üzerinde gizli başlangıç göreviyle çalışıyor; Muhasebe - Güncel kısayolu yeni istemciyi açar. Kurulum kanıtları `artifacts/analysis/install-1.3.27-dev/` altındadır. Geçici bakım görevi kaldırıldı. Önceki paket korunuyor; şema geri dönüşü uyumlu paket ve kontrollü yedek geri yükleme gerektirir.

İşlevsel, arayüz, CSV/eşleştirme/iptal, eşzamanlılık ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı. Masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. Windows yeniden başlatılmadı; siyah masaüstü sorununun kök nedeni ayrıca açık kalıyor.

**7 ana iş paketi** kaldı. Sıradaki **kalıcı mali dönem kapanışı**; bu kurulum yeni bir geliştirme paketini kapatmadı. [Güncel kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md). [Banka CSV kullanım ve kapsam rehberi](BANKA_CSV_VE_ODEME_ESLESTIRME.md).
