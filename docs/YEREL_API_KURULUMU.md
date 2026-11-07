# Bu bilgisayardaki merkezi API kurulumu

**Son durum:** [1.3.1-dev ve şema 113 kurulumu](1_3_1_DEV_KURULUMU.md) 31 Ekim 2026'da tamamlandı. Otomatik başlangıç yeni sürümü kullanır; başlatıcı API hazır olmasını ve süreç yaşam süresini takip eder. Aşağıdaki yollar/ayrıntılar önceki ilk kurulumun tarihsel kaydıdır.

**31 Ekim 2026 güncellemesi:** API ve istemci [1.3.0-dev sürümlü kullanıcı klasörüne geçirildi](1_3_0_DEV_KURULUMU.md). Adres, sertifika, LocalDB ve kullanıcı tercihleri korundu; Muhasebe - Güncel kısayolu artık yeni istemciyi açar. Aşağıdaki ilk kurulum yolları tarihsel kayıttır.

30 Ekim 2026 · Kullanıcı isteğiyle kuruldu; işlev denemeleri atlandı.

API adresi **https://localhost:7443/**. Windows Zamanlanmış Görev adı **AccountingApp Local API**; mevcut kullanıcı Windows oturumu açtığında arka planda başlar. Kullanıcı parolası göreve kaydedilmez; yönetici hesabı/hizmeti kurulmaz. Görev `artifacts/deployments/local-api/Start-LocalApi.ps1` başlatıcısını kullanır. Başlatıcı aynı paketin çalışan API süreci varsa ikinci bir kopya açmaz. API Windows oturumu kapatıldığında kullanılamaz; bu kurulum makine genelinde, kullanıcıdan bağımsız bir Windows servisi değildir.

Masaüstü kullanıcısının tercihi merkezi API moduna geçirildi; adres/veritabanı kapsam bağı mevcut kaynağa sabitlendi. **Muhasebe - Güncel** kısayolu `artifacts/releases/desktop-central-api/WinFormsApp2.exe` dosyasını açar; çalışma klasörü aynı pakettir. Mevcut kullanıcı adı ve parolalar korunmuştur. API sunucu paketi `artifacts/releases/server-desktop-api` altındadır. [Bağlantı ve ödeme korumaları](MASAUSTU_MERKEZI_API_BAGLANTISI.md).

## Veritabanı ve HTTPS

Mevcut LocalDB veritabanı kullanılmaktadır; veriler başka bir SQL örneğine taşınmadı ve şema yükseltmesi uygulanmadı. Kurulumdan önce native COPY_ONLY/CHECKSUM yedeği alındı, VERIFYONLY ve mevcut kimlik manifestiyle doğrulandı. Başlangıçta migration adları/hash'leri ve veritabanı kapsamı uyumlu bulundu.

Mevcut SQL Express örneklerinin güvenilir TLS bağlaması yoktu ve Windows hesabının yönetici yetkisi bulunmuyordu. Bu servisleri veya başka uygulamaların SQL ayarlarını değiştirmek yerine **aynı kullanıcıya, yalnızca bu bilgisayara özel** kurulum seçildi.

`AccountingApi:AllowUserLocalDb` varsayılan olarak kapalıdır. Açıldığında adresin `localhost` olması zorunludur; alternatif Kestrel endpoint tanımları reddedilir, gerçek dinleyici `ListenLocalhost` ile IPv4/IPv6 loopback'e bağlanır. Yerel kurulum Production ortamında çalışır; Development ortamına geçilmedi, HTTP veya sertifika doğrulamasını kapatma kullanılmadı. Ağdaki başka bilgisayarlar bu kurulumla bağlanamaz. Çok bilgisayarlı kurulum SQL Express, sunucu DNS sertifikası ve ayrı bakım/hesap düzeni gerektirir.

HTTPS sertifikası yalnızca mevcut kullanıcının My/Root depolarındadır; SAN değeri `localhost`, özel anahtar dışa aktarılmamıştır. Sertifika bitiş tarihi **30 Ekim 2028**. Güvenlik duvarında ağ erişimi açılmadı, makine genelinde güven/servis değişikliği yapılmadı. API'nin özel `server.json` dosyası ve bağlantı profili Git dışındaki kurulum klasörlerindedir.

## Geri dönüş ve kanıtlar

Önceki kısayol, dosya/registry tercihleri, doğrulanmış veritabanı yedeği ve kurulum kaydı Git dışındaki `artifacts/analysis/local-api-deployment/` altında tutulur. Tercih geri dönüşü yalnızca uygulama kapalıyken ve bekleyen ödemeler mevcut kapsamda çözüldükten sonra yapılır; `.pending` dosyaları silinmez. Sunucu bakımında masaüstü kapatılır, API durdurulur ve mevcut yedekleme/geri yükleme kuralları uygulanır.

Kurulum doğrulaması yalnızca API süreci, loopback dinleyicisi, güvenilir HTTPS `/health/live` yanıtı, paket eşleşmesi, kaydedilen profil ve kısayol hedefini kapsadı. Şema/kimlik okuma ve güvenlik yedeği dışında iş verilerine işlem uygulanmadı. Kullanıcının isteğiyle giriş, ödeme, parola, UI, otomatik yeniden oturum açma ve çok cihaz denemeleri yapılmadı. Masaüstü uygulaması başlatılmadı; yeni kısayoldan kullanıcı açabilir.
