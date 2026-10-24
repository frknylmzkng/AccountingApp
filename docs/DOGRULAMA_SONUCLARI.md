# Rehber uygulaması doğrulama sonuçları

24 Ekim 2026; uygulama kodu yerel Git kaydı: 03a9dcf.

- Tam master: **253 başarılı, 0 başarısız, 0 atlanan**; 15/15 alan. Uyarıları hata sayan Release derlemesi başarılı. Kanıt: artifacts/analysis/master-20261024-095439-621/master.trx, summary.json ve RAPOR.md.
- Veritabanısız master: 185 başarılı, 0 başarısız, 0 atlanan. Kanıt: artifacts/analysis/master-20261024-100543-560/master.trx ve RAPOR.md.
- Önceki 212 regresyona 41 rehber testi eklendi. Eski şema göçü, rol kontrolleri, audit rollback, ödeme yarışı/tekrar/iptal, eski bakiyeyle güncelleme, toplu izin, gerçek izole backup/restore, ayar/kolon/taslak, export ve yazdırma render'ı kapsanır.
- Ekranlar: 8 ekran × 2 tema, 16 PNG; yerleşim, alan görünürlüğü ve sınır testleri geçti. Görseller: artifacts/analysis/ui-preview/.
- Dağıtım: artifacts/releases/guide/; uygulama ve kurulum aracı aynı klasörde, .NET 10.0.13 self-contained. GUID adlı geçici DB'de paket kurulumu, yükseltme, bağlantı ve giriş hazırlığı başarılı; deneme uygulaması kapatıldı ve test DB/yedeği temizlendi. Kanıt: artifacts/analysis/guide-release-smoke.json.
- NuGet: üç projede doğrudan/geçişli bilinen paket açığı bildirilmedi. Kanıt: artifacts/analysis/guide-package-audit.json. Bu sonuç bütün uygulamanın güvenlik denetimi yerine geçmez.
- SQL Express: servis var; gerçek bağlantı denemesi -2146893019 güvenilmeyen TLS sertifikası hatasıyla durdu. Profil desteği ve ayar testleri hazır; sunucu sertifikası ayrıca yapılandırılmalıdır. LocalDB testi ve mevcut bağlantı çalışıyor.
- Kullanıcının açık onayıyla gerçek Muhasebe2.mdf veritabanı 24 Ekim 2026 tarihinde yedeklenip yükseltildi. COPY_ONLY/CHECKSUM yedeği RESTORE VERIFYONLY ile doğrulandı. Öncesi/sonrası personel/izin/tutar/hesap özetleri aynı kaldı. Mevcut hesapların Yönetici rolü, journal ve ödeme defterinin açılış bakiyeleri doğrulandı. Masaüstü kısayolu yeni pakete yönlendirildi; gerçek DB ile giriş hazırlığı başarılıdır. Kanıt: artifacts/analysis/guide-production-status.json ve guide-approved-backup.json.
- Uzak GitHub CI çalıştırılmadı; iş akışı hazır. Fiziksel yazıcıya test işi gönderilmedi; A4/80 mm çizimi ve çok sayfalı render test edildi.

Uygulama geçişi tamamlandı. Mevcut personel/izin kayıtları ve tutarlar korundu; mevcut tutarlar açılış bakiyesi, mevcut hesaplar Yönetici oldu. Geçiş öncesi doğrulanmış yedek `%LOCALAPPDATA%\AccountingApp\backups\schema-upgrades` klasöründedir; tam dosya yolu kanıt JSON dosyasında kayıtlıdır. Geri yükleme yapılmadı; yedek gerektiğinde kontrollü geri dönüş içindir. Kullanıcı parolasıyla etkileşimli giriş, kullanıcı tarafından mevcut bilgileriyle denenebilir; otomatik kontrol giriş hazırlığını doğruladı.
