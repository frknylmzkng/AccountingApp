# 1.3.14-dev / 125 yerel kurulumu

2 Kasım 2026. Kullanıcının talimatıyla mevcut veritabanı COPY_ONLY/CHECKSUM ile yedeklendi; VERIFYONLY/CHECKSUM ve veritabanı kapsam manifesti doğrulandı. Yalnız 125 yükseltmesi uygulandı. Mevcut iş/audit içerik özetleri korundu; hesap oturum nesilleri yenilendi. Üç yeni onay tablosu boş oluşturuldu, yedi yeni koruma/audit tetikleyicisi etkin olarak doğrulandı. Mevcut dağılımlar eski toplu onay modeliyle korundu; otomatik bağımsız karara dönüştürülmedi.

1.3.14-dev istemci ve API ayrı kullanıcı program klasörüne kuruldu. API 1.14, şema 125 ile yalnız HTTPS localhost:7443 üzerinden çalışıyor. Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi. Paket dosya özetleri, bağlantı profilleri ve tercihler korundu; geçici bakım görevi kaldırıldı. Masaüstü başlatılmadı, işlev denemeleri atlandı. Kullanıcı ardından sürümün düzgün çalıştığını bildirdi.

Doğrulanmış ön yedek `artifacts/analysis/live-upgrade-125/backups/AccountingApp-v2-<kapsam-kimliği>-20261102T0834339601105-pre125.bak` dosyasıdır. Özel bakım ve kurulum kayıtları `artifacts/analysis/live-upgrade-125/` ve `artifacts/analysis/install-1.3.14-dev/` altındadır; Git dışındadır. GitHub push veya Release yapılmadı.

[Bağımsız proje parçası onayı kullanım ve kapsamı](BAGIMSIZ_PROJE_PARCASI_ONAYI.md).
