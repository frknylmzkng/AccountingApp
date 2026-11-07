# 1.3.16-dev / 127 yerel kurulumu

2 Kasım 2026. Kullanıcının talimatıyla mevcut veritabanı COPY_ONLY/CHECKSUM ile yedeklendi; VERIFYONLY/CHECKSUM ve kapsam manifesti doğrulandı. Yalnız 127 uygulandı. Mevcut iş/audit içerikleri korundu, hesap oturum nesilleri yenilendi. Yeni vazgeçme makbuzu tablosu boş başladı; iki koruma/audit tetikleyicisi, yabancı anahtar ve birincil indeks metadata üzerinden doğrulandı. Otomatik taslak/gönderim oluşturulmadı.

1.3.16-dev istemci ve API ayrı kullanıcı program klasörüne kuruldu. API 1.16, şema 127 ile yalnız HTTPS localhost:7443 üzerinden çalışır. Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi. Paket dosya özetleri, bağlantı profilleri ve tercihler korundu; geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı; masaüstü başlatılmadı. Kullanıcı ardından sürümün düzgün çalıştığını bildirdi.

Doğrulanmış ön yedek `artifacts/analysis/live-upgrade-127/backups/AccountingApp-v2-<kapsam-kimliği>-20261102T1441314204166-pre127.bak` dosyasıdır. Özel kayıtlar `artifacts/analysis/live-upgrade-127/` ve `artifacts/analysis/install-1.3.16-dev/` altındadır; Git dışındadır. GitHub push veya Release yapılmadı.

[Kalıcı taslak kurtarma kullanım ve kapsamı](KALICI_PUANTAJ_TASLAK_KURTARMA.md).
