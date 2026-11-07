# 1.3.15-dev / 126 yerel kurulumu

2 Kasım 2026. Kullanıcının talimatıyla mevcut veritabanı COPY_ONLY/CHECKSUM ile yedeklendi; VERIFYONLY/CHECKSUM ve kapsam manifesti doğrulandı. Yalnız 126 uygulandı. Mevcut iş/audit içerik özetleri korundu, hesap oturum nesilleri yenilendi. İki yeni eşleme/komut tablosu boş başladı; beş tetikleyici, üç kimlik fonksiyonu ve etkin bağ tekilliği metadata üzerinden doğrulandı. Otomatik hesap/personel eşlemesi yapılmadı.

1.3.15-dev istemci ve API ayrı kullanıcı program klasörüne kuruldu. API 1.15, şema 126 ile yalnız HTTPS localhost:7443 üzerinden çalışır. Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi. Paket dosya özetleri, bağlantı profilleri ve tercihler korundu; geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı; masaüstü başlatılmadı. Kullanıcı ardından sürümün düzgün çalıştığını bildirdi.

Doğrulanmış ön yedek `artifacts/analysis/live-upgrade-126/backups/AccountingApp-v2-<kapsam-kimliği>-20261102T0936236933879-pre126.bak` dosyasıdır. Özel bakım ve kurulum kayıtları `artifacts/analysis/live-upgrade-126/` ve `artifacts/analysis/install-1.3.15-dev/` altındadır; Git dışındadır. GitHub push veya Release yapılmadı.

[Çalışan–hesap eşlemesi kullanım ve kapsamı](CALISAN_HESAP_ESLEMESI.md).
