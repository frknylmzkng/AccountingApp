# 1.3.4-dev yerel kurulumu

31 Ekim 2026 · Kullanıcı isteğiyle kuruldu; denemeler atlandı.

İstemci ve API **1.3.4-dev**, dosya sürümü **1.3.4.0**, API sözleşmesi **1.4**, veritabanı şeması **115**. Yeni şema betiği veya veri dönüşümü uygulanmadı. [Bütünleşik personel kartı](BUTUNLESIK_PERSONEL_KARTI.md) bu sürümdedir.

## Yedek ve korunan ayarlar

Açık istemci ve bekleyen ödeme bulunmadı. API bakım için durduruldu. Mevcut veritabanının COPY_ONLY/CHECKSUM yedeği alındı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve yedek dosyası özeti doğrulandı. Veritabanındaki migration günlüğünün paketle aynı 115 şemasına ait olduğu salt okunur olarak doğrulandı. Hesap/oturum nesillerine veya iş kayıtlarına şema değişikliği yapılmadı.

Yedek `artifacts/analysis/install-1.3.4-dev/backups/` altındadır. Özel yedek kimliği ve dosya yolu `database-preparation.json`, veri özetleri `baseline.json` dosyasında tutulur; bu kayıtlar Git dışındadır.

Sunucu profili, istemci appsettings.json, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Kullanıcı adı/parola değiştirilmedi. Paketlerin SHA-256 ve manifestteki tüm dosya özetleri doğrulandı.

## Kurulum ve başlangıç

Paketler `%LOCALAPPDATA%\Programs\AccountingApp\1.3.4-dev-20261031103302559\` altına ayrı kuruldu. İstemci `desktop/payload/`, API `server/` altındadır. Önceki sürüm dosyaları silinmedi. Muhasebe - Güncel kısayolunun hedefi, çalışma klasörü ve simgesi yeni istemciyi gösterir.

Bakım aracı kapandıktan sonra kullanıcıya ait MSSQLLocalDB örneği zorlamadan durduruldu. AccountingApp Local API Windows görevi yeni aktif kurulum kaydıyla API'yi başlattı; HTTPS canlılık yanıtında API 1.4, beklenen süreç ve yalnız 127.0.0.1/::1 dinleyicileri doğrulandı. Adres `https://localhost:7443/` olarak kaldı. Görevin Running durumu kalıcı başlatıcı için normaldir.

Kurulum kanıtları ve önceki görev/kısayol/profil kopyaları `artifacts/analysis/install-1.3.4-dev/` altındadır. Son durum `deployment.json` ve `readback.json`; kurulu klasörde `installation.json` bulunur. Bu bir yerel geliştirme kurulumu; GitHub'a push veya Release yapılmadı.

İşlev, giriş, personel kartı, ödeme, yetki ve arayüz denemeleri yapılmadı. Yalnız yedek/şema uyumluluğu, paket bütünlüğü, ayar koruması, kısayol ve API başlangıç kontrolleri yapıldı. Masaüstü uygulaması açılmadı; Muhasebe - Güncel kısayolundan açılabilir.
