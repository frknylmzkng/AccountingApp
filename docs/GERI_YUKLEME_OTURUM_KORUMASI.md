# Geri yükleme hata yolunda oturum koruması

29 Ekim 2026. [Durum analizinin](PROJE_DURUM_ANALIZI_2026_10_28.md) F02 bulgusundaki şema hatası sonrası eski oturumun yeniden geçerli olması düzeltildi.

## İşlem sırası

Seçilen yedek doğrulanır, mevcut verilerin güvenlik yedeği alınır ve yönetici yetkisi tekrar kontrol edilir. Geri yükleme SINGLE_USER modunda yapılır. Normal erişim açılmadan önce geri yüklenen bütün hesapların SessionEpoch değerleri yeni rastgele değerlerle değiştirilir; giriş sınırlama kayıtları aynı transaction içinde temizlenir. Bu işlem kullanıcı iptaliyle yarıda kesilmez.

Oturum iptali tamamlandıktan sonra MULTI_USER açılır, şema kontrolü/yükseltmesi ve geri yükleme işlem kaydı çalışır. Şema veya işlem kaydı hatası oturum iptalini geri alamaz. Aynı yedek tekrar yüklense de oturum nesilleri yeniden üretilir. Yedekten gelen ve geri yüklemeyi başlatan oturumların yeniden giriş yapması gerekir.

SessionEpoch sütunu olmayan eski yedekler desteklenir: mevcut oturum doğrulaması bu şemada başarılı olamaz; normal yükseltme sütunu yeni rastgele değerlerle oluşturur. Yeni SQL betiği gerekmez; önceki betikler değiştirilmedi.

Oturum iptalinin kendisi hata verirse MULTI_USER açılmaz; veritabanı OFFLINE durumuna alınır ve hata döndürülür. Bu durumda veritabanını kendiliğinden açıp eski hesapları kullanmaya devam etmek güvenli değildir. Yönetici, güvenlik yedeğini koruyarak hatayı gidermeli ve hesap nesillerinin yenilendiğini doğrulamalıdır.

## Regresyon kapsamı

Tam test sonucu: **649 başarılı, 0 başarısız, 0 atlanan**; Release derlemesi 0 uyarı / 0 hata. Üç yeni test vakası eklendi. Kanıt: `artifacts/analysis/master-20261029-123220-743/master.trx`; geçici LocalDB motoru temizlendi. İlk koşudaki test temizlik hatası, OFFLINE bağlantı reddinin SqlClient havuzunda önbelleğe alınmasından kaynaklandı; test veritabanı yeniden açıldıktan sonra havuz temizlenerek giderildi ve tam paket yeniden geçti.

Yerel Windows x64 test paketi: `artifacts/releases/restore-session-revocation/WinFormsApp2.exe`. Uygulama ve kurulum aracı .NET 10.0.13 çalışma zamanıyla paketlendi. Mevcut bağlantı ayarı aynen korundu. Masaüstü kısayolu ve çalışan uygulama bu değişiklik sırasında değiştirilmedi.

29 Ekim 2026 paket denemesi: doğrudan yayımlanmış DLL'e referans veren ayrı test aracıyla **46 kontrol başarılı**. Tanınmayan şema kaydı ve gerçek yükseltme geri alımı için ikişer restore denendi; yedekteki yönetici/salt okunur oturumları ve restore çağıranı reddedildi, eski oturumlarla personel listelenemedi, yedek bütünlüğü ve sentetik bakiye korundu. Şema onarımı/başlangıç tekrarı eski oturumları canlandırmadı. Kanıt: `artifacts/analysis/restore-session-package-test/results.json` ve `verification.json`; DLL SHA256 `4FF72C6802C60EE0B07468F659616832B26273EAD2559E0C7BCF04C5E2641477`. Geçici DB ve motor temizlendi. Windows otomasyon aracı `failed to write kernel assets ... os error 3` nedeniyle başlatılamadığından bu denemede gerçek uygulamayı kapatma/açma ve ekrandan geri yükleme adımları yapılamadı; doğrulama paket DLL'iyle servis düzeyinde yapıldı.

Yalnızca GUID adlı geçici LocalDB motoru ve test veritabanı kullanılır:

- Tanınmayan migration kaydı bulunan yedek: şema kontrolü reddeder; yedekteki yönetici ve salt okunur kullanıcının eski oturumları reddedilir.
- Gerçek yükseltme hatası: yanlış tipte LeaveVersion sütunu yüzünden yükseltme geri alınır; oturum iptali kalıcıdır.
- Her iki hata aynı yedekle iki defa tekrarlanır; şema onarımı ve başlangıç yükseltmesinin tekrar çalışması eski oturumları canlandırmaz. Gerçek personel listeleme servisi de eski oturumları reddeder.
- Hesap güncellemesini reddeden test trigger'ı: iptal tamamlanamadığı için veritabanının OFFLINE kaldığı ve oturum doğrulamasına kapalı olduğu kontrol edilir. Test kendi veritabanını tekrar açıp temizler.
- Önceki başarılı geri yükleme, audit hatasında iptal, seçilen yedeğin korunması ve eski şema yükseltme testleri korunur.

Bu düzeltme şema hatası yolundaki oturum canlanmasını ele alır. Geri yükleme öncesi ayrı veritabanında uyumluluk denemesi, bütün süreçlerin kullandığı kalıcı bakım kapısı ve süreç/elektrik kesintisinden otomatik kurtarma daha geniş yol haritası işleridir; bu değişiklik bunların tamamlandığı anlamına gelmez. Gerçek veritabanında geri yükleme veya şema değişikliği yapılmadı.
