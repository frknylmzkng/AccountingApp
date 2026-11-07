# Sürüm, paketleme ve kurulum

6 Kasım 2026: **1.3.32-dev / şema 137 / API 1.32** güncel doğrulama/planlama, imzalama ve çok bilgisayarlı bakım için [güncel rehberi](DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md) kullanın. Aşağıdaki metin tarihsel ilk paketleme dilimidir.

**İzleyen kurulum:** 31 Ekim 2026'da kullanıcı isteğiyle [1.3.0-dev istemci ve API bu bilgisayara kuruldu](1_3_0_DEV_KURULUMU.md); kısayol yeni sürüme geçirildi. Aşağıdaki canlı kurulumu değiştirmeyen teslim kaydı önceki kaynak geliştirme aşamasını anlatır. İşlev ve sihirbaz denemeleri kurulumda da atlandı.

30 Ekim 2026 · F14 · Kaynak uygulaması ve yerel geliştirme paketi. Kullanıcının isteğiyle işlev/kurulum/CI denemeleri atlandı.

## Tek sürüm kaynağı

`Directory.Build.props`, ürün sürümünü, dosya/assembly sürümünü, beklenen şema numarasını ve paketleme çalışma zamanı sürümünü tanımlar. Yeni temel sürüm **1.3.0** olarak hazırlanmıştır; bu, GitHub'da v1.3.0 yayımlandığı anlamına gelmez. Normal derleme/geliştirme paketi **1.3.0-dev**, EXE/DLL FileVersion **1.3.0.0** olur. Masaüstü, API, sözleşmeler, veritabanı aracı ve kurulum sihirbazı aynı sürüm kaynağını kullanır.

InformationalVersion ürün sürümü + kaynak commit referansını içerir. Git'e kaydedilmemiş değişiklikleri olan yerel paketin `sourceState` değeri `dirty` olur; commit referansı bu değişikliklerin tamamının o commit'te olduğu iddiası değildir. Doğrudan MSBuild derlemesinde kaynak temizliği doğrulanmadıysa `unverified` görünür. Gerçek Release paketi ancak temiz çalışma ağacından ve tam eşleşen commit etiketinden üretilebilir. `Directory.Build.targets`, Release için yanlış sürüm, etiket veya doğrulanmamış kaynak metadatasını reddeder.

Yeni bir yükseltme dosyası eklendiğinde merkezi `AccountingSchemaVersion` da güncellenir; paketleme betiği bu değerle en son yükseltme dosyasını karşılaştırır. Mevcut şema 112'dir; bu çalışmada yeni SQL yükseltmesi yoktur.

## Hakkında ve tanılama

Uygulamada **Yardım → Hakkında ve sürüm bilgisi**; masaüstü ürün/dosya sürümünü, InformationalVersion, kaynak commit referansı, kaynak durumunu, beklenen şema ve API sözleşmesini gösterir. Güncel API varsa sunucu derlemesi, uygulanmış yükseltme sayısı ve günlük/hash uyumu da canlı yetki kontrolünden sonra okunur. Bu kontrol migration günlüğü uyumudur; bütün SQL nesnelerinin fiziksel bütünlük taraması değildir.

Eski API'nin `/diagnostics` uç noktası yoksa yerel sürüm bilgisi gösterilir, sunucu bilgisi alınamadığı belirtilir. Parola, token, connection string veya personel verisi tanılamaya eklenmez. Uygulama kurulum yolu/hesap bilgisi raporlanmaz. Kaynak metadatası için çalışan uygulamanın veya veritabanının değişmesi gerekmez; yeni ekran yeni paket dağıtıldığında kullanılabilir.

## Tek komutla Windows paketi

PowerShell 7 ve sabitlenen .NET SDK ile depo kökünde:

```powershell
.\automation\Build-WindowsRelease.ps1
```

Betik sürümü kaynak dosyadan alır, Git commit/durumunu ve şema değerini okur, kilitli NuGet bağımlılıklarıyla dört yayın üretir: masaüstü, veritabanı aracı, Windows kurulum sihirbazı ve API. Çıktı daima depo içindeki **yeni** bir `artifacts/release-bundles/` klasörüne yazılır; mevcut paket/çalışan uygulama klasörünün üzerine yazılmaz. İsteğe bağlı `-OutputDirectory` de yalnızca bu deponun artifacts alanında yeni bir klasör olabilir.

Betik EXE/DLL FileVersion ve ProductVersion'ı dosyadan okuyarak beklenen sürüm+commit ile karşılaştırır. Masaüstü/API iş derlemelerinin SHA-256 eşitliğini doğrular. Yanlış sürümde ZIP üretimi durur. Ürün `appsettings.json` dosyasının özel bir kullanıcı profili yerine genel şablon olması gerekir; özel server profili, sertifika anahtarı, gerçek veritabanı, yedek ve ödeme günlükleri pakete alınırsa betik durur.

Çıktılar:

- `AccountingApp-v1.3.0-dev-desktop-win-x64.zip`: kökte `AccountingApp.Setup.exe`, yanında runtime/dependencies ve `payload/` içinde masaüstü/veritabanı aracı.
- `AccountingApp-v1.3.0-dev-server-win-x64.zip`: ayrı API sunucusu; özel yapılandırma/sertifika içermez.
- Her ZIP içinde `package-manifest.json`: sürüm, commit, kaynak durumu, şema, runtime ve her dosyanın boyutu/SHA-256 değeri.
- ZIP dışındaki desktop/server manifestleri, `provenance.json`, kısa `release-notes.md` ve `SHA256SUMS.txt`.

Checksum ve manifest bozulmayı/uyuşmazlığı belirler; tek başına yayıncının kriptografik kimlik doğrulaması değildir. Authenticode imzası veya imzalı provenans bu aşamada eklenmedi. Geliştirme paketi kararlı Release olarak etiketlenmez.

## Kurulum sihirbazı

Masaüstü ZIP'in **tamamı** çıkarılır ve kökteki **AccountingApp.Setup.exe** açılır. Dosyaları ZIP içinden tek tek çalıştırmak veya yalnız EXE'yi taşımak desteklenmez. Sihirbaz kendi paketi ve payload dosyalarını manifest üzerinden doğrular; yükte beklenmeyen dosya veya klasör bağlantısı varsa kurulum durur. Bu çalışma sırasında sihirbaz başlatılmadı.

İlk adımda dört seçenek bulunur:

1. **Mevcut bağlantıyı koru:** kullanıcı tercihini kullanır. Yerel modda özel profil boşsa önce mevcut masaüstü kısayolunun yanındaki profili bulur; mevcut veritabanını yeniden kurmaz. API modunda mevcut HTTPS adresi ve kapsam sabitlemesi korunur.
2. **Merkezi HTTPS API:** adres ve API sözleşmesi doğrulanır. İstemci Windows hesabında güvenilir sertifika gerekir. API sunucusu/hizmeti/scheduled task bu sihirbazla kurulmaz.
3. **Mevcut yerel profil:** seçilen JSON ve gerçek MDF eşleşmesi kontrol edilir. Migration günlüğü bu paketle tam uyumlu olmalıdır; eksik/farklı şemada yedekli yükseltme ayrı bakım işlemine bırakılır.
4. **Yeni LocalDB ve ilk yönetici:** LocalDB aracı önceden kurulu olmalıdır; ayrıca indirilmez veya Windows bileşeni sessizce yüklenmez. Kullanıcı adı ve maskeli parola/tekrar alanları kullanılır; parolalar komut satırına veya yapılandırma dosyasına yazılmaz. Rastgele benzersiz isim/MDF yoluyla **yeni** veritabanı oluşturulur, ilk yönetici güvenli hash ile kaydedilir ve bu yeni veritabanına paketin yükseltmeleri uygulanır. Var olan MDF'ye bu seçenek uygulanmaz.

İkinci adım sürüm, hedef, bağlantı seçimi ve yapılacak işlemleri gösterir. Son **Kur** düğmesi olmadan kopyalama/DB işlemi başlamaz. Açık Muhasebe uygulaması veya bekleyen `.pending` ödeme varsa kurulum durur; günlükler silinmez. Kullanıcı kapatmadan uygulama zorla sonlandırılmaz.

Yeni sürüm `%LOCALAPPDATA%\Programs\AccountingApp\<sürüm>-<zaman>/` altında ayrı bir klasöre kopyalanır. Eski paket ve veritabanı silinmez. Bağlantı tercihi ve **Muhasebe - Güncel** kısayolu hazırlık tamamlandıktan sonra değiştirilir; eski dosya tercihi/kısayol `installation-before/` altında korunur. Hata yakalanırsa önceki tercih/kısayol geri yüklenir. Paket manifesti kurulumda `distribution-manifest.json`, sonuç `installation.json` olarak tutulur. Kurulum sonrası uygulama otomatik açılmaz.

Yeni DB oluşturulup daha sonraki aşama başarısız olursa yeni DB/klasör otomatik silinmez; önceki uygulama verisine geri dönüş denenir, yeni kurulum izi inceleme için bırakılır. Ani elektrik kesilmesinde dosya/registry/kısayolun tamamı tek bir işletim sistemi transaction'ı değildir. Eski DB'yi değiştirmeyen seçenekler bu sınırı daraltır. Geri dönüşte bekleyen ödemeler ve şema uyumu kontrol edilir; eski EXE'nin yeni şemayla her zaman çalışacağı varsayılmaz.

## GitHub etiketi → Release taslağı

Bir kararlı sürüm hazırlanırken VersionPrefix kaynakta değiştirilir, değişiklikler commit/push edilir ve aynı commit'e tam eşleşen `v1.3.0` etiketi verilir. Bu çalışma sırasında commit, push veya etiket oluşturulmadı.

Mevcut Windows CI doğrulama işi korunur. Etiket koşusunda paket betiği `-Release -ReleaseTag <etiket>` ile çağrılır; temiz kaynak, HEAD/etiket eşitliği, sürüm, binary metadata ve şema şartları sağlanmazsa durur. Branch/PR koşusu yalnız `-dev` paket oluşturur.

Doğrulama işi başarılı olduktan sonra yalnız `v...` etiketi push olayında ayrı iş, ZIP/manifest/provenans/SHA256 dosyalarıyla **taslak GitHub Release** oluşturur. Çalışan veya yayımlanmış aynı etiketli Release varsa assets üzerine yazılmaz; iş durur. Taslak incelemeden sonra ayrıca yayımlanır. Branch/PR yazma yetkisi almaz; release işinin `contents:write` yetkisi yalnız kendi işindedir. Artifact indirme için resmi [download-artifact v8.0.2](https://github.com/actions/download-artifact/releases/tag/v8.0.2) kullanılır.

## Bu teslimin doğrulama sınırı

Release derlemesi, self-contained yayın, sürüm bilgisi okuma ve manifest/ZIP/SHA üretimi yapılmıştır. Kullanıcının talebiyle testler, kurulum sihirbazı, yeni veritabanı kurma, UI/API ve GitHub Actions denemeleri çalıştırılmadı. Gerçek Release etiketi ve GitHub yayını oluşturulmadı. Çalışan localhost API, mevcut masaüstü kısayolu, kullanıcı tercihleri ve canlı veritabanı değiştirilmedi. Temiz Windows/LocalDB kurulumu ve kesinti sonrası geri dönüş provası saha doğrulaması olarak açık kalır.

31 Ekim 2026: 1.3.2-dev paket manifesti ve provenance kaydı `apiVersion` alanını da taşır (bu sürüm 1.2). Kurulumdaki hazır olma beklentisi bu değerle birlikte güncellenmelidir; şema 114 ayrı bakım işlemidir. [Çalışma dönemleri ve geçiş kapsamı](CALISMA_DONEMLERI.md).
