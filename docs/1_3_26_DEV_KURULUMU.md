# 1.3.26-dev yerel kurulum

6 Kasım 2026 · Şema **133** · Merkezi API **1.26**.

Mevcut veritabanının kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY, kapsam manifesti ve SHA-256 doğrulandı. Asıl yükseltme öncesi ikinci güvenlik yedeği de oluşturuldu. Yedekler ve önce/sonra veri özetleri `artifacts/analysis/live-upgrade-133/` altındadır. Ana yedek dosyası: `backups/AccountingApp-v2-<kapsam-kimliği>-20261106T1140519251588-pre133.bak`.

Yalnız 133 uygulandı; 100–132 migration kimlikleri korundu. Mevcut personel, izin, proje, puantaj, hakediş, mali düzeltme, ödeme, avans ve tahsis kayıtları satır sayıları/SHA-256 özetleriyle karşılaştırıldı. Beklenen SessionEpoch/rowversion değişikliği hariç yönetici bilgileri ve eski audit satırları korundu. Hesapların oturum nesilleri yenilendi; yeniden giriş gerekir.

Yeni ProjectBudgetRevisions tablosu boş; iki etkin tetikleyici, üç güvenilir CHECK, iki güvenilir yabancı anahtar ve altı indeks doğrulandı. Mevcut projelere otomatik bütçe veya mali hareket eklenmedi.

İstemci ve API ayrı `1.3.26-dev-20261106144111716` kullanıcı programları klasörüne kuruldu. ZIP ve çıkarılan dosyaların manifest özetleri doğrulandı. Veritabanı/sunucu/istemci profilleri ve kullanıcı tercihleri korundu. API yalnız HTTPS localhost:7443 üzerinde, gizli wscript başlangıç göreviyle çalışıyor. Muhasebe - Güncel kısayolu yeni istemciyi açar. Kurulum kanıtları `artifacts/analysis/install-1.3.26-dev/` altındadır. Geçici bakım görevi kaldırıldı; önceki paket korunuyor, şema geri dönüşü için uyumlu paket ve kontrollü yedek geri yükleme gerekir.

İşlevsel, arayüz, bütçe/sapma/CSV, eşzamanlılık ve SQL senaryo denemeleri kullanıcı isteğiyle atlandı. Masaüstü uygulaması otomatik açılmadı. Yalnız derleme, yedek/veri/şema/paket ve API başlangıç kontrolleri yapıldı. Windows yeniden başlatılmadı; siyah masaüstü sorununun kök nedeni doğrulanmış değildir.

[Proje bütçesi kullanım rehberi](PROJE_BUTCESI_VE_MALIYET_SAPMASI.md).
