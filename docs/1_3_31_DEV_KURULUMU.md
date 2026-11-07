# 1.3.31-dev yerel kurulum

6 Kasım 2026 · Şema **137 (değişmedi)** · Merkezi API **1.31**.

Mevcut 1.3.30-dev kurulumunun SQL yedekleme yoluyla kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, kapsam manifesti ve SHA-256 doğrulandı. Yedek: `artifacts/analysis/preinstall-backup-1.3.31/backups/AccountingApp-v2-<kapsam-kimliği>-20261106T1739394678105-pre-1.3.31.bak`.

Bu **kurulum öncesi yedek standart, şifresiz .bak** biçimindedir; şifreli kasa denemesi yapılmadı. Yedek klasörünün NTFS erişim mirası kapatılıp yalnız çalışan Windows hesabı ve SYSTEM için erişim bırakıldı; izinler geri okunarak doğrulandı. Klasördeki manifest ve doğrulama işareti aynı erişimi miras alır. Yedek kişisel veri içerir; herkese açık depoya yüklenmemelidir. Eski yedekler silinmedi veya dönüştürülmedi.

100–137 migration kimlikleri mevcut şemayla eşleşti. Yükseltme çalıştırılmadı; yeni SQL betiği gerekmedi. Yedek öncesi/sonrası 61 tablonun satır sayısı ve içerik özeti eşleşti; personel, hesap, bakiye, proje, puantaj, mali hareket, banka, ücret/gider ve rapor görünümleri korundu. Hesap epoch/rowversion verileri değiştirilmedi. API yeniden başlatıldığı için bellekteki eski tokenlar geçersiz; yeniden giriş gerekir.

Kaynak paket: `artifacts/release-bundles/backup-support-security-137-final/`. Paket SHA-256/CRC ve çıkarılan bütün manifest dosyaları/sürüm bilgileri doğrulandı. İstemci ve API ayrı `1.3.31-dev-20261106204008353` kullanıcı programları klasörüne kuruldu. Önceki sürümler korundu. Muhasebe - Güncel kısayolu yeni istemciyi açar. Sunucu/istemci/veritabanı profilleri, dosya ve kayıt defteri tercihleri korundu.

API yalnız HTTPS localhost:7443 loopback üzerinde mevcut gizli WScript başlangıç göreviyle çalışır. Geçici yedek görevi kaldırıldı; yeni görünür konsol veya Windows açılış görevi eklenmedi. `AccountingApp.BackupRecovery.exe` her iki pakette de bulunur ve yalnız operatör elle çalıştırınca konsol açar. Masaüstü ve kurtarma aracı otomatik açılmadı. Windows yeniden başlatılmadı.

Yeni şifreli yedek, anahtar üretme/dışa aktarma/içe aktarma/döndürme, şifre çözme, aynalama, saklama temizliği, SQL geri yükleme, UI ve eşzamanlılık denemeleri kullanıcı isteğiyle atlandı. Şifreli kasa anahtarı veya kurtarma parolası kurulum tarafından oluşturulmadı. Anahtar kümesinin ayrı, parolalı dışa aktarımı kullanım kılavuzundaki operatör işlemidir; kurulmuş araç, yapılmış kurtarma provası anlamına gelmez. Yalnız istenen gerçek kurulum öncesi SQL yedeği, veri/paket/ayar ve API başlangıç kontrolleri yapıldı.

Kurulum kanıtları: `artifacts/analysis/install-1.3.31-dev/`; SQL yedek ve veri özetleri: `artifacts/analysis/preinstall-backup-1.3.31/`. Otomatik veritabanı geri yükleme, GitHub push veya Release yapılmadı.

Kalan **3 ana iş paketi**; kurulum bu sayıyı azaltmadı. Sıradaki **kurulum ve dağıtım sonlandırması**. [Kalan işler](KALAN_IS_PAKETLERI_2026_11_06.md). [Yedek/kurtarma ve destek güvenliği rehberi](YEDEK_VE_DESTEK_GUVENLIGI.md).
