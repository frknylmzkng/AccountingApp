# 1.3.0-dev yerel kurulum kaydı

31 Ekim 2026 · Kullanıcı isteğiyle kuruldu; işlev denemeleri atlandı.

Masaüstü ve API için üretilmiş **1.3.0-dev** ZIP'leri ve dosya manifestleri doğrulanarak `%LOCALAPPDATA%\Programs\AccountingApp\1.3.0-dev-20261031002819922\` altına ayrı kuruldu. `desktop/payload/WinFormsApp2.exe` istemcidir; `server/AccountingApp.Server.exe` API'dir. Dosya sürümleri **1.3.0.0**, ürün sürümleri **1.3.0-dev** + kaynak commit referansıdır. Paket geliştirme paketidir, GitHub'da kararlı 1.3.0 Release yayımlanmadı.

**Muhasebe - Güncel** kısayolu yeni istemciye yönlendirildi. API adresi **https://localhost:7443/**, sözleşme 1.1 ve erişim yalnız IPv4/IPv6 loopback'tir. Mevcut Windows kullanıcısının HTTPS sertifikası ve LocalDB bağlantısı korundu. Windows oturumu açılışındaki **AccountingApp Local API** görevi aynı başlatıcıyı kullanır; başlatıcı artık `artifacts/deployments/local-api/active-installation.json` içindeki sürümlü kurulum yolunu ve ürün sürümünü doğrulayarak API'yi açar.

Kullanıcı tercihleri byte özetiyle korunmuştur; API adresi/veritabanı kapsam bağı değiştirilmedi. Mevcut kullanıcı adı ve parolalar aynıdır. Sunucu yeniden başladığı için önceki API token'ları sona erer; kullanıcı yeni kısayoldan yeniden giriş yapar. Eski uygulama/sunucu klasörleri silinmedi. [Sürüm bilgisi ve yeni özelliklerin kapsamı](SURUMLU_DAGITIM_VE_KURULUM.md).

Önce doğrulanmış COPY_ONLY/CHECKSUM yedeği alındı. Veritabanı taşınmadı, şema 112 değiştirilmedi ve bekleyen ödeme günlüğü bulunmadı. Önceki kısayol, başlatıcı, görev tanımı, ayarlar, yedek ve geçiş kayıtları `artifacts/analysis/install-1.3.0-dev/` altında Git dışında tutulur. Geçişteki ilk geri dönüşler bu klasörde tarihsel kanıt olarak kalır; son durum `deployment.json` dosyasıdır. Yeni kurulumun kökünde de `installation.json` bulunur.

Kurulum sihirbazı çalıştırılmadı; paket açma/ayar taşıma ve sürüm geçişi kontrollü kurulum adımlarıyla yapıldı. Yalnız dosya özetleri, sürüm metadatası, görev/süreç hedefi, proxy kullanılmayan ve sertifika doğrulaması açık HTTPS canlılık yanıtı, loopback dinleyicisi ve kısayol hedefi kontrol edildi. Giriş, ödeme, parola, Hakkında ekranı, diğer UI akışları ve Windows yeniden oturum açılışı denenmedi. Masaüstü uygulaması başlatılmadı; kullanıcı kısayolu açabilir.
