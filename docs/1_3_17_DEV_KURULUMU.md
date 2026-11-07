# 1.3.17-dev / mevcut 127 yerel kurulumu

2 Kasım 2026. Kullanıcının talimatıyla mevcut veritabanı COPY_ONLY/CHECKSUM ile yedeklendi; VERIFYONLY/CHECKSUM ve kapsam manifesti doğrulandı. Şema yükseltmesi yapılmadı: 127 ve migration günlüğü paketle eşleşti. İş/audit içerikleri, hesap güvenlik nesilleri ve ayarlar korundu; yeni kayıt veya ücret hesabı üretilmedi.

1.3.17-dev istemci ve API ayrı kullanıcı program klasörüne kuruldu. API 1.17 yalnız HTTPS localhost:7443 üzerinden çalışır. Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi. Paket dosya özetleri, profiller ve tercihler korundu; geçici bakım görevi kaldırıldı. API yeniden başladığı için eski API oturumları sonlandı; DB oturum nesilleri değiştirilmedi. İşlev denemeleri atlandı, masaüstü başlatılmadı. Kullanıcı ardından sürümün düzgün çalıştığını bildirdi.

Ön yedek `artifacts/analysis/live-backup-1.3.17-dev/backups/AccountingApp-v2-<kapsam-kimliği>-20261102T1501018338828-pre1.3.17-dev.bak` dosyasıdır. Özel kayıtlar `artifacts/analysis/live-backup-1.3.17-dev/` ve `artifacts/analysis/install-1.3.17-dev/` altında, Git dışındadır. GitHub push veya Release yapılmadı.

[Toplu manuel giriş kullanım ve kapsamı](TOPLU_MANUEL_PUANTAJ.md).
