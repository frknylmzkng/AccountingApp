# 1.3.1-dev ve 113 şema kurulumu

31 Ekim 2026 · Kullanıcı onayıyla yükseltildi; işlev denemeleri atlandı.

**Mevcut durum:** istemci/API **1.3.1-dev**, dosya sürümü **1.3.1.0**, şema **113**. Muhasebe - Güncel kısayolu yeni istemciyi açar. API **https://localhost:7443/** adresinde, yalnız IPv4/IPv6 loopback üzerinden çalışır. Mevcut Windows hesabı, HTTPS sertifikası, LocalDB, bağlantı profili ve veritabanı kapsam sabitlemesi korunmuştur.

Paketler SHA-256 ZIP ve dosya manifestleriyle doğrulanıp `%LOCALAPPDATA%\Programs\AccountingApp\1.3.1-dev-20261031080009513\` altına kuruldu. İstemci `desktop/payload/`, API `server/` klasöründedir. Eski 1.3.0-dev dosyaları silinmedi. [Yeni modülün kapsamı](PROJE_EKIP_VE_TARIHLI_ATAMALAR.md); ürün hâlâ geliştirme paketidir, GitHub'da kararlı Release yayımlanmadı.

## Yedek ve şema

Önce mevcut veritabanının COPY_ONLY/CHECKSUM yedeği alındı; VERIFYONLY, v2 kapsam manifesti ve dosya özeti doğrulandı. Açık masaüstü ve bekleyen ödeme bulunmadı. API otomatik başlangıcı bakım sırasında kapatılıp API durduruldu. Yedekten sonra veri değişmediği doğrulanarak yalnız 113 uygulandı. Şema aracı ayrıca korumalı güvenlik kopyası oluşturdu.

Önceki personel, izin, ödeme, finans, kapanış, kimlik ve eski audit içeriği özetleri yükseltmeden sonra aynı kaldı. Hesap oturum nesilleri beklendiği gibi değişti; yeni migration/audit kayıtları bu karşılaştırmada ayrı ele alındı. ProjectTeams ve PersonnelProjectAssignments boş başlar; eski projelerde tarih/saha/müşteri bilgisi uydurulmadı. Altı etkin koruma/audit trigger'ı, dört güvenilir check, dört güvenilir foreign key ve iki atama indeksi metadata üzerinden doğrulandı. Bu, iş senaryosu veya SQL davranış denemesi değildir.

## Başlangıç ve bakım

Windows oturumu açılışındaki **AccountingApp Local API** görevi yeni sürümü aktif kurulum kaydından seçer. İlk başlangıçlarda LocalDB motor hatası görüldü. Başlatıcı artık güvenilir HTTPS canlılığını proxy kullanmadan bekler, başarısız çocuk süreç başlangıcını görev başarısı saymaz ve yönlendirilmiş akışların/başlatma bağlamının API'den önce kapanmaması için API süreci bitene kadar açık kalır. Görevde birer dakika aralıkla en fazla üç başarısızlık yeniden denemesi ayarlandı. Görevin API çalışırken Running görünmesi normaldir. Bu düzenleme ile yeni API'nin canlılık yanıtı ve doğru loopback süreç bağlaması doğrulandı; Windows yeniden oturum açma provası yapılmadı.

Kullanıcı tercihleri dosya özetiyle korunmuştur. Eski oturumlar geçersizleştiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir. **İşlemler → Projeler, ekipler ve tarihli atamalar** yeni ekranıdır; bu kurulum sırasında açılmadı.

113 sonrasında eski 112 bekleyen API yeniden başlatılmamalıdır. Sorun giderme 113 uyumlu paket üzerinde yapılır; otomatik veritabanı geri yüklemesi yapılmadı. Bakımda masaüstü kapatılır ve görev durdurularak devre dışı bırakılır; API/başlatıcı süreçlerinin bittiği doğrulanır. Bakım tamamlanınca görev yeniden etkinleştirilir. Geri dönüş ayrı yedek/uyumluluk ve sonradan oluşan veri değerlendirmesi gerektirir.

Özel yedek, önce/sonra veri özetleri ve SQL doğrulama sonucu `artifacts/analysis/live-upgrade-113/` altındadır. Önceki kısayol, başlatıcı/görev, profil kopyaları ve kurulum kayıtları `artifacts/analysis/install-1.3.1-dev/` altında tutulur. Son durum `deployment.json`, kurulu sürüm kökünde `installation.json` dosyasıdır. Başarısız ilk başlangıç kayıtları tarihsel kanıt olarak bırakılmıştır; son başarılı kurulum kaydı esas alınır. Bu klasörler Git dışındadır.

Giriş, ödeme, proje/ekip/atama, çakışma, audit davranışı, diğer ekranlar ve kurulum sihirbazı denenmedi. Masaüstü uygulaması başlatılmadı; kullanıcı yeni kısayoldan açabilir. Yalnız paket/şema/veri koruma ve kurulum başlangıç kontrolleri yapıldı.
