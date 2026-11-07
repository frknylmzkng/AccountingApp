# 1.3.32-dev yerel kurulum

7 Kasım 2026 · Şema **137 (değişmedi)** · Merkezi API **1.32**.

Kurulum öncesi mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, kapsam manifesti ve SHA-256 doğrulandı. Ana yedek: `artifacts/analysis/preinstall-backup-1.3.32/backups/AccountingApp-v2-<kapsam-kimliği>-20261107T0234357518048-pre-1.3.32.bak`.

Bu yedek **şifresiz, standart .bak** biçimindedir. Klasörün NTFS erişim mirası kapatılıp yalnız mevcut Windows hesabı ve SYSTEM erişimi bırakıldı; izinler geri okunarak doğrulandı. Yedek ve manifest kişisel veri içerir; herkese açık depoya yüklenmemelidir. Önceki yedekler silinmedi veya dönüştürülmedi.

Yedek öncesi/sonrası 61 tablonun satır sayısı ve içerik özetleri eşleşti. Personel, hesap, bakiye, proje, puantaj, mali hareket, banka, ücret/gider ve rapor görünümleri korundu. 100–137 migration kimlikleri eşleşti; yükseltme kodu çalıştırılmadı. Hesap epoch/rowversion verileri değişmedi. API yeniden başlatıldığı için eski bellek tokenları geçersiz; yeniden giriş gerekir.

Paket kaynağı `artifacts/release-bundles/deployment-finalization-137-final/` klasörüdür. Paket doğrulayıcı, ZIP/manifest/özet ve çıkarılan tüm dosya/sürüm kontrolleri başarılıdır. Paketler `unsigned` geliştirme paketidir; imzalama yapılmadı. İstemci ve API ayrı `1.3.32-dev-20261107053446013` kullanıcı programları klasörüne kuruldu. Önceki sürümler korundu. Muhasebe - Güncel kısayolu yeni istemciyi açar. Sunucu/istemci/veritabanı profilleri ve dosya/kayıt defteri tercihleri değişmedi.

API mevcut gizli WScript göreviyle yalnız HTTPS localhost:7443 loopback üzerinde çalışır. Geçici yedek görevi kaldırıldı. Yeni görünür konsol veya Windows başlangıç görevi eklenmedi. Masaüstü, grafik kurulum sihirbazı ve kurtarma aracı çalıştırılmadı; Windows yeniden başlatılmadı.

Kullanıcı isteğiyle işlev, UI, şifreli kasa/anahtar, SQL geri yükleme, aynalama, saklama, imza, yük/eşzamanlılık ve geri dönüş denemeleri atlandı. Yalnız istenen gerçek SQL kurulum yedeği, veri/paket/ayar/izin ve API başlangıç kontrolleri yapıldı. Anahtar veya kurtarma parolası oluşturulmadı. CI/Release/GitHub push yapılmadı. Bu kurulum gerçek dağıtım kabulü veya kurtarma provası değildir.

Kanıtlar `artifacts/analysis/install-1.3.32-dev/` ve `artifacts/analysis/preinstall-backup-1.3.32/` altındadır. Otomatik DB geri yükleme yapılmadı.

Kalan **2 ana iş paketi**: performans/erişilebilirlik ve güvenlik doğrulaması; gerçek kullanım pilotu/kurtarma/teslim. Kurulum bu sayıyı azaltmadı. [Kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md), [dağıtım rehberi](DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md).
