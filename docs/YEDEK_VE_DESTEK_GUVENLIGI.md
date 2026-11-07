# Yedek ve destek güvenliği

6 Kasım 2026 · Kaynak/paket **1.3.31-dev / şema 137 / API 1.31**. Yeni SQL yükseltmesi yoktur; 100–137 betikleri değiştirilmedi. İlk kaynak tesliminde kurulum yapılmamıştı; ardından [1.3.31-dev yerel kurulumu](1_3_31_DEV_KURULUMU.md) doğrulanmış, özel erişimli standart .bak yedeği sonrası tamamlandı. Şema değişmedi; kasa/kurtarma denemeleri yapılmadı. Denemeler kullanıcı isteğiyle atlandı.

## Kullanım ve erişim

Ayarlar → **Güvenli yedek / destek** ekranı yönetici içindir. Yerelde ve merkezi API'de şifreli yedek oluşturma, son arşivin tam doğrulaması, aynalamayı yeniden deneme, tutulacak şifreli yedek sayısını değiştirme ve güvenli tanı paketi kaydetme vardır. Kaydetme, doğrulama ve aynalama sunucuda çalışır; tarayıcı/istemciye yedek veya anahtar indirme uç noktası açılmadı. İşlemler güncel hesap/oturum/yönetici yetkisiyle ve audit kaydıyla yürür. Uzun işlem zaman aşımında durum ekranını yenileyin; bir yedek oluşmuş olabilir. Mali işlem oluşturulmaz.

Yeni uygulama yedekleri `.bak.abk` olarak saklanır. Yerel yönetici oturumundaki günlük yedek ve başarısız aynalama kontrolü bu biçime geçer. Merkezi API için kendiliğinden çalışan yeni bir günlük yedek görevi kurulmadı; yönetici ekranı veya işletmenin sunucu bakım planı kullanılır. Şema yükseltmesinin ve geri yüklemenin uygulama içi güvenlik yedekleri de şifrelenir. Ayrı operatör SQL komutları, dış bakım betikleri ve mevcut `.bak` dosyaları otomatik şifrelenmez.

Bu temel uygulama yolu **aynı Windows hesabının LocalDB** sunucusu içindir. SQL Server'ın açık geçici yedeği yazıp okuyabilmesi gerekir. Ayrı SQL Server/servis hesabı kurulumu sessizce açık yedeğe düşmez; ayrı servis hesabı erişim ve yedekleme tasarımı gerekir. Uygulama şifrelemesi disk/SQL veritabanı şifrelemesi veya Windows yöneticisine karşı koruma değildir.

## Şifreleme ve anahtarlar

Yedek içeriği AES-256-GCM ile 1 MiB parçalarda, 16 bayt doğrulama etiketiyle işlenir. Her arşiv için rastgele 256 bit tuzdan HMAC-SHA-256 ile ayrı veri anahtarı türetilir; o anahtar altında parça sayaçları farklı nonce değerleri üretir. Başlık ve sıra numarası doğrulamaya bağlıdır; içerik, parça sırası, kesilme, sonuna veri ekleme ve manifest/SQL yedek SHA-256 uyuşmazlığı reddedilir. En fazla 64 GiB kaynak desteklenir. Bütün dosya belleğe yüklenmez. Başlıktaki rastgele anahtar kimliği, kapsam özeti, boyut ve oluşturma zamanı açıktır; katalog, fiziksel yol ve SQL yedek kimliği şifreli manifesttedir. Şifreleme sonrasında tüm arşiv tekrar doğrulanır, ardından şifreli dosya yayımlanır.

Anahtar kümesi `%LOCALAPPDATA%\AccountingApp\backup-vault\keys.dpapi` içindedir; Windows DPAPI **CurrentUser** ile korunur. Kasa ve geçici açık veri klasörlerinin NTFS mirası kapatılıp çalışan Windows hesabı ve SYSTEM erişimi bırakılır. DPAPI çözme hakkı yine doğru kullanıcı profiline bağlıdır. Bağlantı/junction içeren kasa/geçici/aynalama yolları reddedilir. Windows hesabı veya profil kaybında DPAPI dosyasını tek başına kopyalamak yeterli değildir.

Sunucu paketindeki `AccountingApp.BackupRecovery.exe` anahtar işlemleri için **sunucuda, API'yi çalıştıran Windows hesabıyla, elle** kullanılır. Uygulama veya Windows açılışına eklenmedi; bu aracın konsolu yalnız operatör çalıştırınca açılır. Parola argüman, ortam değişkeni, dosya veya yönlendirilmiş stdin üzerinden alınmaz; ekranda gösterilmeden istenir.

```text
AccountingApp.BackupRecovery.exe export-keys "E:\KurtarmaAnahtarlari\muhasebe-20261106.abkey"
AccountingApp.BackupRecovery.exe import-keys "E:\KurtarmaAnahtarlari\muhasebe-20261106.abkey"
AccountingApp.BackupRecovery.exe rotate-key
```

Anahtar dışa aktarımı 16–256 karakterlik ayrı parola ister; dışa aktarımda tekrar girilir. `.abkey` kümesi PBKDF2-SHA-256 (600.000 iterasyon, rastgele tuz) ve AES-GCM ile korunur. Mevcut dosyanın üzerine yazılmaz. Uzun, başka yerde kullanılmayan bir parola seçin; araç zayıf/ele geçirilmiş parolaları sözlük üzerinden saptamaz. Dosyayı arşivlerden ayrı bir erişim ortamında, parolayı da ayrı bir parola kasasında saklayın. Paylaşılacak tanı paketine veya GitHub'a eklemeyin. Dışa aktarma kaydı yalnız dosyanın yazıldığını söyler; ayrı cihazda bulunduğunu veya parolayı hatırladığınızı kanıtlamaz.

Döndürme yeni etkin anahtar üretir; eski anahtarlar silinmez ve eski arşivler yeni anahtarla yeniden yazılmaz. Ardından tüm kümeyi yeniden dışa aktarın. En fazla 64 anahtar tutulur; sınıra gelince sessiz anahtar silme yoktur. İçe aktarım mevcut anahtarları koruyarak birleştirir; aynı kimlikte farklı anahtar reddedilir. Yalnız anahtarın dosyada bulunması, sağlam arşiv veya denenmiş kurtarma anlamına gelmez. Anahtar kaybı ve parolalı dışa aktarımın da bulunmaması halinde şifreli yedeği açacak arka kapı yoktur.

## Açık geçici veri ve saklama

SQL BACKUP/VERIFY ve çözme işlemlerinin açık `.bak` dosyaları, kasa altında rastgele kimlikli, özel erişimli bir çalışma klasöründe tutulur. İşlem tamamlanınca bilinen dosyalar kaldırılır; dosya silme SSD/journal üzerinde fiziksel güvenli silme garantisi değildir. Elektrik kesilmesi/çökme sonrasında bu klasörler kalabilir. `clean-stale` komutu yalnız 24 saatten eski ve kullanım kilidi olmayan kasa çalışma klasörlerinin bilinen dosyalarını temizler; başka klasörleri veya seçilen kurtarma çıktısını silmez. Kilitli klasör atlanır. Araç çıktısında kişisel veri basılmaz.

```text
AccountingApp.BackupRecovery.exe clean-stale
AccountingApp.BackupRecovery.exe mirror "<kapsam-kimliği>" "E:\MuhasebeSifreliYedekleri"
```

Komutta yer tutucuyu kendi veritabanı kapsam kimliğinizle değiştirin. Aynalama yolu yalnız sunucu operatörü aracından yapılandırılır; API istemcisi dosya yolu göndermez. Şifreli arşivin kaynak ve kopya özeti karşılaştırılır; aynı adlı farklı dosya ezilmez. Başarısız kopya yeniden denenebilir; ana arşiv korunur. Aynalama anahtar dışa aktarımı yapmaz. Aynı disk veya aynı bilgisayardaki ikinci klasör makine kaybına karşı bağımsız yedek değildir.

Varsayılan son **14**, ayarlanabilir **2–90** şifreli yedek tutulur. Temizlik başarılı yeni yedek/kopya sonrasında ve yalnız etkin anahtar için dışa aktarma kaydı varsa başlar. Seçim dosya adıyla kronolojiktir. Aynı kapsamın tam doğrulanabilen şifreli arşivleri dışında dosya silinmez; bozuk/anahtarsız arşivler otomatik temizlenmez. Kullanımda olan kurtarma kaynağı korunur; bu yüzden sayı geçici olarak sınırı aşabilir. **Eski açık `.bak`, manifest ve geçmiş yükseltme yedekleri dokunulmadan kalır**; bunlar için ayrıca erişim/saklama planı uygulayın. Ayrı aynalama kopyası da aynı kapsam ve korumalarla temizlenir.

Kasa dosyası politikası ve SQL audit iki farklı depodur; bir depolama/audit hatasında politika kaydedilmiş olabilir. Ayarı yeniden okuyup durumunu kontrol edin. Politika değişikliği anında toplu silme yapmaz. Kasa anahtarlarını ve kurtarma parolasını yedek temizlik işlemine dahil etmeyin.

## Doğrulama ve gerçek kurtarma

Durum ekranındaki arşiv sayısı doğrulanmış şifreli manifesti okunabilen dosyaları, zaman bilgisi geçmiş oluşturma/kopyalama kaydını gösterir. Ekranı açmak tüm dosyaları SQL ile yeniden doğrulamaz. **Son yedeği doğrula** tam şifre çözme, kapsam/bütünlük ve SQL RESTORE VERIFYONLY çalıştırır; hedef veritabanına RESTORE yapmaz. Anahtar dışa aktarımı ve aynalama başarısı da tam kurtarma provası sayılmaz. Prova kaydı bu sürümde tutulmaz; ekran bunu açıkça söyler. Gerçek geri yükleme, ayrı hedef ve mutabakatla pilot/teslim paketinde açık iştir.

Yerel SQL bağlantısında mevcut geri yükleme ekranı `.bak.abk` dosyasını kabul eder; kapsamı doğrular, özel klasöre çözer ve mevcut SQL header/manifest eşleşmesi, güvenlik yedeği, seçili kaynak koruması, oturum iptali ve transaction/şema kontrolleriyle geri yükler. Eski v2 `.bak` ve yanındaki `.backup.json` yolu da korunur. Merkezi API üzerinden RESTORE yapılmaz; açık kullanıcılar ve sunucu süreçleri kontrollü bakımda kapatılır.

Yalnız çevrimdışı çözme örneği:

```text
AccountingApp.BackupRecovery.exe unseal "E:\Yedek\AccountingApp-v2-...bak.abk" "C:\Kurtarma\YeniKlasor"
```

Hedef yeni klasör olmalıdır. Bu komut özel erişimli klasöre açık `.bak` ve manifest çıkarır, **SQL geri yükleme yapmaz**. Çıktı kişisel veridir; başarı mesajı gelmeyen veya eksik çıktıyı kullanmayın. Bakım bitince bilinçli olarak kaldırın; temizleme komutu bu operatör çıktısını silmez. Yeni bilgisayara geçişte önce parolalı anahtarı içe aktararak çözme mümkündür; uygulamanın mevcut kapsam kontrolü başka sunucu/yol/kataloğu otomatik aynı hedef saymaz. Yeni hedefe kontrollü SQL bakımında doğru dosya/katalog ve bağlantı kapsamı/pinleri ayrıca doğrulanmalıdır; bu sürüm kimlik denetimini aşan otomatik farklı-hedef RESTORE eklemez.

## Kişisel veri içermeyen tanı paketi

**Güvenli tanı paketi kaydet** yalnız yöneticiye açıktır. ZIP içinde sabit `diagnostics.json`, SHA-256 içeren `manifest.json` ve açıklama bulunur. İzin verilen alanlar sürüm, commit, kaynak durumu, beklenen şema/API sürümü, uygulanmış yükseltme sayısı/uyumu, .NET/Windows sürümü, işlemci mimarisi ve genel kasa durum bayrakları/sayısıdır. Şema bilgisi sunucudan gelir. Ham loglar taranıp sansürlenmez; hiç dahil edilmez. Serbest metin, hata mesajı, stack trace, kullanıcı/makine adı, personel/hesap, TC, IBAN, ücret, veritabanı/kapsam adı, dosya yolu, URL, bağlantı profili, token, parola, anahtar veya kurtarma dosyası eklenmez. Teknik sürüm bilgisi yine sistem ortamını anlatır; paylaşmadan önce içeriği inceleyebilirsiniz. Destek paketi kendiliğinden kimseye gönderilmez.

## Teslim sınırları

Kaynak ve dağıtım derlemeleri ile ZIP/manifest özetleri kontrol edilir. Kullanıcı isteğiyle gerçek yedek/şifreleme/çözme, yanlış parola, bozulma, izin/oturum iptali, SQL/RESTORE, aynalama, disk dolması, UI/DPI, yük ve kurtarma senaryoları **çalıştırılmadı**. Bu güvenlik özelliğinin üretim kabulü ve gerçek kurtarma provası tamamlanmış sayılmaz. İlk kaynak tesliminde canlı kurulum yapılmamıştı. Sonraki kurulumda istemci/API/kısayol güncellendi, kurulum öncesi yedek klasörü özel erişime alındı; şema ve kasa anahtarları değiştirilmedi. GitHub'a gönderilmedi. Araçların derlenmesi çalıştırılmaları değildir.

Bu temel kaynak kapsamından sonra **3 ana iş paketi** kalır: kurulum/dağıtım sonlandırması; performans/erişilebilirlik doğrulaması; gerçek kullanım pilotu/teslim. Şifreleme ve kurtarma denemeleri son iki pakette açık kalır. Ayrı SQL Server servis hesabı ve farklı hedefe otomatik kurtarma ayrıca kapsamlandırılmalıdır.

Teknik dayanaklar: [Microsoft AES-GCM ve nonce gereksinimi](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.encrypt?view=net-10.0), [Windows veri koruma kapsamları](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection).
