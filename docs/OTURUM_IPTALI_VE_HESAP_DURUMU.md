# Oturum iptali ve hesap durumu

25 Ekim 2026. Geliştirme analizinin 2. bölümündeki açık oturum yetkisinin devam etmesi sorunu ele alındı.

## Kullanıcıya yansıyan davranış

Hesap yönetimi listesinde **Etkin** durumu gösterilir. Yönetici seçili hesabı **Pasife al** veya **Etkinleştir** düğmesiyle yönetebilir. Pasif hesap giriş yapamaz. Silme veya pasife alma sonrasında açık oturumun personel, izin, ödeme ve hesap işlemleri reddedilir; uygulama yeniden giriş ekranına döner.

Boşta duran uygulama 30 saniyede bir oturumunu kontrol eder. Hesap veya yetki değişikliği algılandığında ekran kapatılıp yeniden giriş istenir. İşlem sürüyorsa normal kapatma tamamlanana kadar beklenir; sonraki işlem kendi güncel yetki kontrolünden de geçer. Ağ hatası hesabın iptal edildiği şeklinde yorumlanmaz. Bağlantı yokken yeni işlemler veritabanından yetki doğrulayamadıkları için ilerleyemez.

Yeniden etkinleştirme eski oturumu canlandırmaz. Aynı kullanıcı adıyla silip yeniden hesap oluşturmak da eski oturuma yetki kazandırmaz. Rol veya parola hash'i değişirse açık oturum yeni giriş gerektirir. Bu çalışma parola değiştirme/sıfırlama ekranı veya hatalı giriş sınırlaması eklemez; bunlar analizdeki sonraki geliştirmelerdir.

Kendi açık oturumunun hesabı silinemez/pasife alınamaz. Hesap değişiklikleri seri hale getirilir; iki yönetici birbirinin hesabını aynı anda pasife almaya çalıştığında en az bir etkin yönetici korunur. Zaten etkin hesabı tekrar etkinleştirmek gereksiz oturum iptali oluşturmaz.

## Yetki kontrolü

Girişte hesabın etkinliği, rolü, `SessionVersion` ve rastgele `SessionEpoch` bilgisi alınır. Parola kontrolünden sonra, giriş audit'i yazılmadan önce bu bilgiler tekrar doğrulanır; parola kontrolü sırasında değişmiş bir hesapla oturum başlatılmaz. Doğrulanmış oturum nesnesi ekran geçişleri boyunca korunur.

Oturum kullanan personel/izin/ödeme ve hesap servisleri, yalnızca bellekteki role güvenmez. Okumalarda ve değişikliklerin transaction'ında hesabın hâlâ etkin olduğu, rolü ile güvenlik sürümünün eşleştiği kontrol edilir. Yetki için alınan hesap satırı kilidi transaction bitene kadar tutulur. Önceden yetkilendirilmiş bir transaction tamamlanırken iptal işlemi bekler; iptal tamamlandıktan sonra eski oturumla yeni yazma başlayamaz. Hesap yönetimi işlemleri ayrıca ortak SQL uygulama kilidi kullanır.

Yedekleme/geri yükleme başlangıcı, geri yüklemenin kritik aşaması, ayar kaydı, taslak, dışa aktarma ve kapanış yazdırma girişlerinde de güncel yetki doğrulanır. Başlamış geri yükleme gibi bütünlük gerektiren işlemler ortasında kesilmez. Hassas işlem tamamlanırken yapılan iptal, sonraki işlemlerde geçerlidir. Oturum iptali, daha önce ekrana veya dosyaya alınmış verileri geriye dönük geri alamaz.

Geçersiz oturum hatası form tarafından yakalanır; işlem durumu temizlendikten sonra uygulama oturumu bırakıp giriş ekranına döner. Önceki oturuma ait gecikmiş bir kontrol sonucu yeni giriş yapılmış oturumu kapatamaz.

Belirsiz ödeme isteği oturum iptalinde silinmez veya kesin ret sayılmaz. Reddedilmiş bir istek sonucunu onaylamak da güncel hesap kontrolünü gerektirir. Yeniden yetkilendirilmiş hesap, aynı Windows kullanıcısı/veritabanındaki bekleyen isteğin kimliğini koruyarak devam edebilir.

## Şema ve geri yükleme

Yeni `102-account-session-version.sql`, mevcut hesapları etkin bırakarak `IsActive`, otomatik `SessionVersion` ve rastgele `SessionEpoch` alanlarını ekler. Önceden uygulanmış betikler değiştirilmedi. Mevcut yükseltme akışı önce CHECKSUM yedeği alıp VERIFYONLY ile doğrular; hesaplar, parola hash'leri, roller ve iş kayıtları korunur. Tekrar açılışta sürümler kendiliğinden değiştirilmez.

Veritabanı geri yüklemesi geçmiş hesap durumlarını ve rowversion sayacını geri getirebilir. Bu yüzden geri yükleme sonrasında bütün hesapların rastgele oturum dönemi yenilenir. Eski bir oturum, yedekteki değerler tekrar görünse bile canlanamaz. İptal, geri yükleme audit'inden önce ayrı transaction ile kalıcılaştırılır; audit kaydı başarısız olsa da eski oturumların iptali geri alınmaz.

Üretim veritabanı bu çalışma sırasında açılıp yükseltilmedi. Yeni sürüm ilk giriş hazırlığında yedekli yükseltmeyi uygular. Açık eski uygulama kopyaları kapatılıp bütün kullanıcılar güncel sürüme geçirilmelidir. Uygulama içi kontroller, aynı Windows hesabıyla veritabanına doğrudan erişebilen kişiye karşı ayrı bir veritabanı güvenlik sınırı oluşturmaz.

## Doğrulama kapsamı

- Pasif hesapla girişin, eski oturumla personel/izin/ödeme okuma ve yazmalarının reddi; veri/defter/audit'in değişmemesi.
- Yeniden etkinleştirme, aynı adı silip yeniden oluşturma, rol/parola değişikliği ve yanlış oturum dönemi.
- İptal edilmiş yöneticiyle hesap işlemi, ödeme iptali ve yedek oluşturmanın engellenmesi.
- Kendi hesabını pasife alma koruması, etkinleştirme no-op'u ve eşzamanlı yönetici iptalleri.
- Transaction sırasında hesap satırı kilidinin iptali bekletmesi.
- Oturum iptalinde belirsiz/reddedilmiş ödeme kaydının korunması ve yeni girişle aynı kimlikle kurtarma.
- Giriş ekranına otomatik dönüş, korunan pencerenin kapanması, geçici kontrol hatası ve gecikmiş eski kontrol sonucu.
- Mevcut hesaplı şemaya yedekli geçiş ve tekrarlanabilirlik.
- Geri yükleme sonrası oturum iptali; audit hatası olsa da iptalin kalıcı olması.

Testler GUID adlı geçici LocalDB motorlarında yürütülür; gerçek hesaplara pasife alma/silme işlemi uygulanmaz.

Son tam doğrulama: **338 başarılı, 0 başarısız, 0 atlanan**, 15/15 alan. Önceki 316 teste 22 senaryo eklendi. Release derlemesi 0 hata ve 0 uyarıyla tamamlandı. Kanıt: `artifacts/analysis/master-20261025-075712-452/master.trx`, `summary.json` ve `RAPOR.md`. Geçici test motoru kaldırıldı.

## Yerel paket ve kullanıcı kılavuzu

Uygulama/kurulum aracı `artifacts/releases/session-revocation` klasörüne self-contained yayımlandı. Paket uygulama DLL'i ve 102 yükseltme betiği SHA-256 ile kaynak/derleme çıktısına eşleştirildi; kurulum aracının `--help` çalışması doğrulandı. Masaüstündeki `Muhasebe - Güncel.lnk` yeni pakete yönlendirildi; önceki kısayol `artifacts/analysis/session-revocation/shortcut-before.lnk` olarak korundu. Kanıt: aynı klasördeki `release-verification.json`.

Kullanıcı adımları `OTURUM_GUVENLIGI_DENEME_KILAVUZU.md` ve yazdırılabilir HTML sürümündedir. HTML kopyası masaüstüne `Muhasebe - Oturum Güvenliği Kılavuzu.html` adıyla kondu. Açık uygulama kapatılmadı ve yeni uygulama başlatılmadı. Bu çalışma GitHub'a push veya yeni GitHub Release yayını içermez.
