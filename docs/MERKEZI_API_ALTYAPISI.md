# Merkezi API altyapısı

**Bu bilgisayara kurulum, 30 Ekim 2026:** kullanıcı isteğiyle localhost kurulumu tamamlandı. `AllowUserLocalDb=true` açıkça seçildiğinde API Production ortamında mevcut Windows kullanıcısının LocalDB'sini kullanabilir; bu seçenek yalnızca `https://localhost` kabul eder, alternatif `Kestrel:Endpoints` ayarlarını reddeder ve dinleyiciyi kodda IPv4/IPv6 loopback'e sabitler. LocalDB kurulu araçla başlatılır, şema otomatik değiştirilmez. LAN kurulumu için bu seçenek kapalı kalır ve SQL Express gerekir. [Kurulum kaydı](YEREL_API_KURULUMU.md). Aşağıdaki ilk aşama metnindeki genel Production/LocalDB kısıtı bu açık yerel kurulum istisnasını içermez.

**İzleyen güncelleme, 30 Ekim 2026:** [masaüstü API bağlantısı](MASAUSTU_MERKEZI_API_BAGLANTISI.md) kaynakta eklendi. Sözleşme 1.1; oturum doğrulama, admin parola sıfırlama, audit sorgusu, kapanış detayı, sunucuda dosya üretimi ve pano uç noktaları da hazırlandı. Aşağıdaki 'ilk aşama' açıklamaları ilk altyapı tesliminin tarihsel kapsamıdır; güncel kullanım/geçiş ve doğrulama sınırları yeni belgededir. Canlı kurulum yapılmadı.

30 Ekim 2026 · F13 ilk aşama · Kullanıcının tercihi: önce merkezi API altyapısı.

## Tamamlanan kapsam

`api/AccountingApp.Server`, HTTPS üzerinden çalışan ASP.NET Core sunucusudur. Personel, izin, ödeme, proje finansı ve kalıcı kapanış işlemlerini mevcut uygulama servislerine aktarır. Transaction, kayıt sürümü, kalıcı personel kimliği, ödeme tekrar kontrolü ve ayrıntılı audit kuralları bu servislerde korunur. `api/AccountingApp.Contracts` istek/yanıt türlerini masaüstünden bağımsız .NET 10 kütüphanesinde tanımlar. API sürümü `/api/v1` yolundadır.

Bu ilk sunucu, mevcut WinForms derlemesindeki iş servislerini kullandığı için **Windows üzerinde** çalışır. WinForms ekranlarını açmaz. İş katmanının tamamen ayrı bir kütüphaneye çıkarılması ve Linux desteği bu aşamada yapılmadı. SQL Express API sunucusuyla aynı bilgisayarda çalışır; mevcut uzak SQL/MDF paylaşımı kısıtları kaldırılmadı. Production ortamında kullanıcıya özel LocalDB profili reddedilir.

Masaüstü uygulaması henüz API'ye bağlanmaz. Bu adım canlı veritabanını, kısayolu, açık uygulamayı veya mevcut masaüstü profilini değiştirmez. Yeni SQL yükseltmesi yoktur. F13'ün çok cihaz geçişi tamamlanmış sayılmaz.

## Oturum ve yetki

- Giriş mevcut parola hash'i ve kalıcı hatalı giriş sınırlamasını kullanır. İstemci rol, yetki veya oturum nesli göndererek giriş yapamaz. Fazladan JSON alanları reddedilir.
- Başarılı girişte 256 bit rastgele Bearer token verilir. Sunucunun belleğinde token'ın SHA-256 özeti tutulur. Token URL'ye konmaz; `Authorization: Bearer ...` başlığında taşınır. Gelecek istemci token'ı loglamamalı veya düz metin dosyaya yazmamalıdır.
- Varsayılan boşta süre 15 dakika, mutlak süre 8 saattir. Süreler monoton saatle takip edilir. Sunucu yeniden başlayınca bütün API oturumları sona erer. Kullanıcı başına varsayılan 8, toplam 5000 oturum sınırı vardır. `/session` ve hazır olma sorgusu süreyi uzatmaz; diğer yetkili istekler uzatır. Bu süre masaüstündeki fiziksel kullanıcı etkinliği kilidinden ayrıdır; arka plan istemci sorguları dikkatle tasarlanmalıdır.
- Her korumalı istekte şema günlüğü ve canlı hesap sürümü/nesli doğrulanır; servis ayrıca işlem transaction'ında gerekli yetkiyi tekrar doğrular. Hesap silme/pasife alma, parola veya yetki değişimi eski oturumu geçersiz kılar. İstemcinin gönderdiği kullanıcı adı audit aktörü olarak kullanılmaz.
- Geçici parolalı hesap yalnızca oturum bilgisi, kilit/açma, çıkış ve parola değiştirme akışlarını kullanır. Parola değişince yeniden giriş gerekir. Kilit açma mevcut hesabı aynı parola sınırlamasıyla yeniden doğrular.
- Personel düzenleme, ödeme, kişisel veri raporu ve yönetici işlemleri mevcut ayrı izinlere bağlıdır. Proje bazlı satır yetkisi bu aşamada eklenmedi; mevcut roller yetkili oldukları işlemlerde tüm projeleri görür.
- Kilit/çıkış daha önce yetkilendirilip başlamış transaction'ı geri almaz; sonraki istekleri engeller. Aynı anda devam eden yanıtın iptali istemcinin ayrıca yönetmesi gereken bir yaşam döngüsüdür.

## Sunucu kurulumu

Kurulum aşağıdaki adımlarla ayrıca yapılır; bu geliştirme sırasında uygulanmadı.

1. Windows sunucusunda yerel SQL Express, uygulama veritabanı ve bu derlemeyle **tam uyumlu yükseltme günlüğü** hazırlanır. Bu kaynak sürümünde son yükseltme 112'dir. API başlangıçta ve korumalı isteklerde günlük adları/hash'lerini kontrol eder; şema kurmaz, yükseltmez, yedeklemez veya geri yüklemez. Günlük kontrolü tam şema bütünlük taramasının yerine geçmez.
2. API için ayrı Windows çalışma hesabı belirlenir. Profil Windows bütünleşik SQL kimliğini kullanır; SQL sunucu sertifikası doğrulanır (`Encrypt=Mandatory`, `TrustServerCertificate=false`). Çalışma hesabına gerekli uygulama tablosu işlemleri ve migration günlüğü okuma verilir. Şema yükseltme/geri yükleme ayrı bakım hesabıyla yapılır; API hesabına bu yetkiler verilmez.
3. `server.example.json`, dağıtım klasöründe `server.json` adıyla oluşturulur. `DatabaseProfilePath`, sunucuya özel profilin açık yoluna ayarlanır. `database-profile.example.json` bu ayrı profilin biçimini gösterir; gerçek dosyalar ve sertifikalar Git'e eklenmez. Profilin `FilePath` alanı SQL Express için boş, `Name` dolu olmalıdır. Özel profiller yayına otomatik kopyalanmaz.
4. `AccountingApi:ListenUrl` varsayılanı `https://localhost:7443` olur. Gerçek sunucu DNS adı, `AllowedHosts` ve Kestrel TLS sertifikası kurulumda açıkça ayarlanır. `Kestrel:Certificates:Default:Subject` ve `Store`/`Location` kullanılarak Windows sertifika deposu seçilebilir; hizmet hesabına özel anahtarı okuma izni verilir. Sertifikanın istemcilerde güvenilir ve DNS adına uygun olması gerekir. Özel anahtar/parola kaynak kodda veya komut satırında tutulmaz.
5. Yayın klasörü çalışma dizini olarak seçilip `AccountingApp.Server.exe` çalıştırılır. Hizmet kaydı/otomatik başlatma bu aşamada hazırlanmadı. Yerel geliştirme için `DOTNET_ENVIRONMENT=Development` ve güvenilir geliştirme sertifikasıyla LocalDB kullanılabilir; production kısıtı geliştirme kolaylığı için kaldırılmaz.

Sunucu ayarları yeniden yüklenmez; değişiklikte süreç yeniden başlatılır. `AccountingApi__DatabaseProfilePath`, `AccountingApi__ListenUrl` gibi ortam değişkenleri JSON'u geçersiz kılabilir. ASP.NET Core ortamı ve içerik kökü de kurulumda açıkça belirlenmelidir. Boş veya hatalı profil/TLS ayarıyla otomatik başka bir veritabanına geçilmez.

Kaynak koddan Windows x64 paketi üretimi:

```powershell
.\dotnet.cmd publish api/AccountingApp.Server/AccountingApp.Server.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -p:TreatWarningsAsErrors=true -o artifacts/releases/central-api
```

Paketin tamamı birlikte dağıtılır. Bu API paketi masaüstü güncelleme paketi değildir; mevcut masaüstü kısayolu bu EXE'ye yönlendirilmez.

## Uç noktalar

Tüm yollar `/api/v1` önekiyle başlar. Aşağıda belirtilen rol genel bir özet; servis içindeki daha dar kayıt/yetki koşulları da uygulanır.

| Yol | Metot | İşlem / yetki |
|---|---|---|
| `/auth/login` | POST | Kullanıcı adı/parola; anonim HTTPS |
| `/session` | GET | Mevcut hesap kimliği ve yetki özeti |
| `/auth/logout`, `/auth/lock` | POST | Mevcut oturumu bitir/kilitle |
| `/auth/unlock`, `/auth/change-password` | POST | Mevcut hesap parolasını doğrula/değiştir |
| `/health/ready` | GET | Yönetici; canlı şema/hesap kontrolünden sonra hazır bilgisi |
| `/personnel` | GET | Sayfalı personel ve isteğe bağlı izinler; okuma |
| `/personnel/save`, `/personnel/archive`, `/personnel/restore` | POST | Personel düzenleme; para değişimi ayrıca ödeme izni |
| `/personnel/{group}/{personId}/history` | GET | Kalıcı personel geçmişi; okuma |
| `/leaves/plan` | POST | Atomik çoklu izin; düzenleme |
| `/summary`, `/ledger` | GET | SQL özeti / sayfalı ödeme defteri; okuma |
| `/payments/prepare`, `/payments/submit` | POST | Ödeme/avans hazırlama ve kaydetme; ödeme izni |
| `/payments/inspect` | POST | Aynı isteğin kayıtlı sonucunu sorgula; okuma |
| `/payments/reverse` | POST | Gerekçeli iptal; yönetici |
| `/reports/receipt/{requestId}`, `/reports/closing-preview` | GET | Güncel fiş/önizleme metni; dışa aktarma izni |
| `/projects` | GET / POST | Proje/maliyet özeti oku / yönetici proje oluştur |
| `/projects/{projectId}/entries` | GET | Son 500 proje belgesi; okuma |
| `/finance/post`, `/finance/reverse` | POST | Gerekçeli hakediş/gider veya sürümlü iptal; yönetici |
| `/closings` | GET | Sayfalı kalıcı kapanış listesi; okuma |
| `/closings/finalize` | POST | Tamamlanmış günün değiştirilemez kapanışı; yönetici |
| `/closings/{id}/summary`, `/closings/{id}/lines`, `/closings/{id}/print` | GET | Saklanan rapor; dışa aktarma izni |
| `/accounts` | GET | Parola/hash içermeyen hesap listesi; yönetici |
| `/accounts/add`, `/accounts/active`, `/accounts/permissions`, `/accounts/delete` | POST | Hesap yönetimi; yönetici, hedef sürümü/nesli gerekli |

`/health/live` anonim HTTPS canlılık bilgisidir; veritabanı hazır bilgisini göstermez. İlk açılış şema kontrolü başarısızsa sunucu dinlemeye başlamaz. Yönetici parola sıfırlama, dosya/yedek yönetimi, keyfi SQL, yerel yazıcı ve Excel dosyası üretimi API'ye açılmadı. Audit yazımı servislerde korunur; audit sorgu ekranının API karşılığı sonraki kapsamdadır.

## JSON ve eşzamanlılık sözleşmesi

İstek alanları `ApiContracts.cs` içinde tanımlıdır; JSON camelCase kullanır. Enum gövde alanları isimle (`General`, `Joker`, `Settlement`, `Advance`) gönderilir, sayısal enum gövdeleri reddedilir. Zorunlu constructor alanları eksik veya null olamaz. SQL rowversion değerleri 8 baytın base64 gösterimidir. Gün alanları saat ve saat dilimi olmadan `2026-10-29` biçiminde gönderilir. Gerekçe değiştirme işlemlerinde zorunludur; çekirdek kuralları uygulanır.

Tablo yanıtı `columns` tanımı ve `rows` dizisidir. İstemci gelen tipi açıkça okumalıdır: `Int64` tablo hücresi hassasiyet kaybını önlemek için ondalık **metin**, `Binary` base64, `Decimal` JSON sayısıdır. Parasal değerler istemcide decimal olarak işlenmelidir. DTO'ların normal `long` alanları .NET JSON sayısıdır; gelecekte JavaScript istemcisi eklenirken bunlar da 64 bit kayıpsız ayrıştırılmalıdır. Geçerli TC 11 basamaklı olduğundan JavaScript tam sayı sınırı içindedir. Sayfa `index` değeri 0 tabanlı, `size` 1–200'dür; sınır dışı boyut reddedilir. Personel geçmişi ve proje özeti henüz sayfalı değildir; büyük geçmiş için ayrı sayfalama sonraki adımdır.

Ödeme hazırlığından dönen **isteğin tamamı**, submit'ten önce istemcide güvenli, kalıcı günlüğe kaydedilmelidir. Yanıt kaybolursa yeni ödeme/RequestId üretilmez: aynı istek `/payments/inspect` ile sorgulanır, gerekiyorsa aynı RequestId/payload ile yeniden gönderilir. Yeni format 3 personel kimliği, sürümü, kapanış kimliği, tür/proje ve gerekçeyi taşır. İstemci bu alanları uydurarak doğrulamayı atlayamaz; servis güncel kayıtla karşılaştırır. Hazırlık henüz ödeme kaydı oluşturmaz. HTTP 5xx/bağlantı kopması işlemin kesin geri alındığı anlamına gelmez. Genel HTTP yazma istekleri körlemesine yeniden denenmez.

Hakediş/giderde aynı proje+tür+belge numarası ve aynı değişmez içerik tekrar güvenlidir; farklı içerik çatışmadır. Kapanış gün+tutar tekrar kontrolünü kullanır. Diğer düzenleme/iptal işlemlerinde belirsiz yanıt sonrası güncel kayıt ve audit incelenir. Güncel satır sürümü alınmadan otomatik üzerine yazılmaz. Oturumun yeniden açılması kaydedilmiş ödeme niyetini değiştirme gerekçesi değildir.

Hata gövdesi `code`, güvenli kullanıcı mesajı ve `traceId` taşır. 400 geçersiz istek, 401 yeniden giriş, 403 yetki/geçici parola, 409 kayıt/sürüm çatışması, 423 kilit, 429 sınır, 503 veritabanı/şema uyumsuzluğudur. Ödeme sonucu HTTP 200 ile `Recorded`, `Reversed`, `Rejected` veya `Unconfirmed` olabilir; istemci durum alanını değerlendirmelidir. Ham exception, SQL, parola veya token HTTP hata yanıtına yazılmaz.

## İşletim sınırları ve sonraki adım

HTTPS zorunlu, yanıtlar `no-store`; gövde 128 KiB, başlık 16 KiB, eşzamanlı bağlantı 100 ile sınırlıdır. IP başına 120 başlangıç token'ı, dakikada 60 yenileme sınırı vardır; ayrıca kalıcı hesap giriş sınırlaması çalışır. Forwarded header güveni, cookie oturumu ve CORS açılmadı. Bu sürüm doğrudan Kestrel HTTPS dinleyicisi içindir; proxy/load balancer kurulumu ayrı tasarlanmalıdır. Uygulama HTTP istek gövdesi, token veya parola loglamaz; altyapı/proxy logları da bunları kaydetmeyecek şekilde kurulmalıdır.

Bellek oturum deposu **tek API süreci** içindir. Birden çok worker/replica için dağıtık oturum/iptal ve ortak hız sınırı eklenmeden ölçeklenmez. Canlı DB bakım/restore sırasında API durdurulur; tüm masaüstü doğrudan DB istemcileri de kapatılır. Yedek ayrı kimlikli mevcut araçla alınır; API içinden restore yoktur. Aynı veritabanına doğrudan erişen eski masaüstleri varken merkezi bakım koordinasyonu henüz tamamlanmış değildir.

Sonraki iş: masaüstü için HTTP servis adaptörleri, token yaşam döngüsü, güvenli ödeme niyeti günlüğü, bağlantı kopması/timeout deneyimi, sunucu bakım protokolü ve pilot dağıtım. Proje bazlı erişim, vardiya/puantaj ve çok aşamalı onay ayrıca planlanır. Canlıya açma, servis kurulumu, sertifika/firewall değişikliği veya internete yayın bu çalışma kapsamında yapılmadı.

## Doğrulama durumu

Kullanıcının talebiyle test ve ekran/API denemeleri çalıştırılmadı. Release derlemesi ve yayın çıktısı hazırlanır; bu, endpointlerin TLS/SQL ortamında çalıştırıldığı veya çok cihaz kullanımının doğrulandığı anlamına gelmez. Canlı veritabanına bağlantı/şema değişikliği yapılmadı.
