# Kullanım ve manuel test kılavuzu

Bu kılavuz, kaynak yeniden kullanım rehberini personel/izin/ödeme uygulamasına uyarlar. Stok, POS, müşteri puanı, tedarikçi ve lot modülleri eklenmez. Bunların işlem bütünlüğü, defter, hesaplama, tarih uyarısı ve taslak yöntemleri mevcut işlemlerde kullanılır.

## Giriş ve yetkiler

Masaüstündeki Muhasebe - Güncel kısayolunu açın. Giriş hazırlığı bağlantıyı kontrol eder, gerekiyorsa önce doğrulanmış şema yedeğini alır, DbUp yükseltmesini ve eski parola geçişini çalıştırır. Mevcut hesaplar Yönetici rolüyle korunur. Parolanızı değiştirmek veya yeniden paylaşmak gerekmez. Başarısız hazırlıkta yeniden deneyebilirsiniz; hata ayrıntıları yerine bir kod gösterilir.

Yönetici ekranında kullanıcı adı, maskeli parola ve rol seçilir. Roller: Yönetici (hesap, ayar, yedek, ödeme iptali dahil), Personel sorumlusu (personel, izin ve ödeme kaydı), Görüntüleyici (okuma, rapor ve önizleme). Servisler de yetkiyi kontrol eder. Son yönetici ve etkin hesabınız silinemez. Yetkiler yeni girişte yüklenir. Doğrudan SQL yetkisine sahip Windows kullanıcılarının erişimi ayrıca SQL Server/Windows izinleriyle yönetilir.

Deneme için iki geçici hesap açın: bir görüntüleyici, bir personel sorumlusu. Her biriyle girişte menüleri ve değiştirme düğmelerini kontrol edin; deneme sonunda yöneticiyle bu hesapları silebilirsiniz. Gerçek kullanıcı parolasını test betiğine yazmayın.

## Personel, izin ve ödeme

Yeni personel eklemek için alanları doldurup EKLE'ye basın. Otomatik izin eklenmemelidir. Listeden kayıt seçip adı veya tutarı değiştirdikten sonra DÜZENLE'yi kullanın. TC değiştirerek başka kayda yazma engellenir. Silme seçili kaydı ve Genel grubundaki ilişkili izinlerini tek işlemde siler; ödeme defteri kalır.

İzin kaydı için personeli seçin, tarihleri girin, İZNİ KAYDET'e basın. Aynı gün bir gündür. Bitiş başlangıçtan önceyse kayıt engellenir. İzin planı ekranında Ctrl ile birden fazla personel seçebilirsiniz; seçilen dönemin tamamı tek transaction ile kaydedilir. Bir personelde hata varsa planın tamamı geri alınır. Birden fazla eski izin dönemi olan personel sessizce topluca güncellenmez.

Ödemeler ekranında üst listeden personeli seçin, pozitif tutarı girin ve Ödeme kaydet'i onaylayın. Kalan tutarı aşan ödeme reddedilir. Aynı işlem kimliğinin yeniden denenmesi ikinci kez para düşürmez. Sonuç belirsiz bir bağlantı hatası yaşanırsa yeni işlem başlatmadan önce listeyi ve defteri kontrol edin. Alt defterde açılış, tutar farkları, ödeme, iptal ve kapanış satırlarını görebilirsiniz. Yönetici seçili ödemeyi yalnızca bir kez iptal edebilir. Silinmiş personele ait ödeme, etkin personel kaydı bulunmadan iptal edilmez.

Kalan tutarı başka bir ekranda ödeme yapıldıktan sonra eski bilgilerle düzenlemeyi deneyin: işlem engellenmeli, güncel bakiye korunmalıdır. Önce F5 ile yenileyin. Tutarın değiştirilmesi geçmiş ödeme tutarını değiştirmez; deftere fark kaydı ekler.

Özet ekranında toplam kalan tutar, personel başına ortalama ve izin uyarıları vardır. Son 30 günde biten izinler kırmızı; devam eden/yaklaşan izinler sarı ve metin durumuyla gösterilir. Yaklaşan dönem 30 gündür. Bu liste izin hakedişi hesaplamaz.

## Görünüm, klavye ve taslak

Araç çubuğu bütün korumalı ekranlarda ortaktır. Tema tercihi kullanıcıya özel JSON ve HKCU yedeğine yazılır. Kolonlar menüsünden görünürlük değiştirilebilir; kolonlar sürüklenebilir ve genişletilebilir. Düzen ekran kapanırken saklanır. Bozuk kolon ayarı listeyi açmayı engellemez. Standart Windows başlık çubuğundan pencere sürüklenebilir ve kapatılabilir.

| Tuş | İşlev |
|---|---|
| F1 | Kullanım yardımı |
| F2 | Destekleyen personel/arama ekranında temizle ve yeni giriş |
| F5 | Listeyi yenile |
| Delete | Metin alanı dışında seçili kaydı onayla sil |
| Esc | Ana ekran |
| F6 | Genel/Joker form taslağını sakla |
| F7 | Taslağı geri yükle |

Taslak Windows DPAPI CurrentUser ile şifrelenir; kullanıcı, veritabanı ve ekran kapsamına bağlıdır. Parola içermez. F7 veritabanına kayıt yapmaz. Hedef kayıt silinmişse eski satır indeksine yönelmez. Taslaktan sonra bakiye değiştiyse güncel tutar korunur ve bilgi verilir. Kaydetmeden önce alanları kontrol edin. Ekranlar arasında geçişte açık pencere sayısı bir kalmalı; çıkış boş giriş ekranına dönmelidir.

## Excel, CSV ve yazdırma

Excel/CSV araçları ekrandaki ilk listenin görünen kolonlarını ve geçerli satır sırasını dışa aktarır. Ödeme ekranında iki liste vardır; personel listesini dışa aktarır. Excel'de lacivert başlık, zebra satırlar, para biçimi ve ondalıklı kolonlarda SUM satırı bulunur. Kimlikler metin olarak korunur. CSV UTF-8 BOM, noktalı virgül ve çift tırnak kaçışı kullanır; formül gibi başlayan metinler etkisizleştirilir. Dışa aktarılan listelerde görünen kişisel veriler bulunur; dosyayı uygun konuma kaydedin.

Ödeme defterinden bir ödeme satırı seçip ödeme fişi önizlemesini açın. A4 veya 80 mm seçilebilir. TC/IBAN maskelidir; QR yalnızca işlem referansıdır. Logo için Ayarlar'da PNG seçilebilir. Günlük kapanışta gün ve beyan edilen toplam girilir; kayıt toplamıyla fark renkli gösterilir, iki imza alanı vardır. Önizlemeden yazdırma kullanıcı tarafından başlatılır. Fiziksel yazıcı çıktısını ayrıca kontrol edin; otomatik test yazıcıya iş göndermez.

## Bağlantı ve yedek

Ayarlar yalnızca yöneticiye açıktır. Başka bir JSON profili seçip kaydettiğinizde bağlantı ve temel şema doğrulanır, ardından oturum kapanır. Yeni girişte seçilen DB'nin hesapları kullanılır. Çalışan transaction ortasında bağlantı değiştirilmez. LocalDB dosya yolu mutlak, profil dosyasına göre göreli, ortam değişkenli veya `|DataDirectory|` tabanlı olabilir; DataDirectory Documents klasörüdür. SQL Express için FilePath boş, Name dolu olmalıdır; kimlik doğrulama Windows hesabıdır. Şifre veya `TrustServerCertificate=true` ayarı JSON'a eklenemez.

Bu bilgisayarda SQL Express servisi bulundu; gerçek deneme `-2146893019` sertifika güven hatası verdi. Yerel Express'te güvenilir TLS sertifikası kurulmadan bu profil kullanılamaz. Mevcut LocalDB profili etkilenmez. [Microsoft açıklaması](https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/connect/error-message-when-you-connect).

Yönetici oturumu açılınca ve açık kaldığı sürece saatlik kontrolde, son doğrulanmış yedek 24 saati geçtiyse yedek alınır. Varsayılan klasör `%LOCALAPPDATA%\AccountingApp\backups` olur. Şema öncesi yedekler bunun `schema-upgrades` altındadır. Yedekler SQL COPY_ONLY/CHECKSUM ve RESTORE VERIFYONLY ile doğrulanır. Son 14 yedek yalnızca bu veritabanına özgü hash öneki içinde tutulur; başka dosyalar silinmez. USB/OneDrive aynalama isteğe bağlıdır. SQL Express servis hesabının seçilen klasöre erişimi gerekir. SQL Server yedek yolu 259 karakteri geçemez. Uygulama kapalıyken görev çalışmaz.

Geri yükleme yönetici onayı ister ve mevcut verilerin yerini alır. Yalnızca aynı veritabanı adı ve uygulama yedek öneki kabul edilir; header/filelist ve checksum doğrulanır. Bağlantılar kesilmeden önce güvenlik yedeği alınır. Veri ve log dosyaları mevcut hedeflere geri yüklenir; işlem sonunda çok kullanıcılı mod geri açılır ve yeniden giriş gerekir. Devam eden restore iptal edilmez. Bu işlemi test etmek için **gerçek DB kullanmayın**: aşağıdaki -Full testi yalnızca kendi oluşturduğu GUID veritabanında çalışır.

## Geliştirme ve otomasyon

Models, Data ve Services katmanları mevcut WinForms ekranlarına bağlıdır. Eski senkron Store sınıfları eski şema regresyonu ve kurulum uyumluluğu için kalır; üretim ekranları async servisleri kullanır. Bir UnitOfWork tek işlem içindir; tekrar commit veya commit sonrası komut kabul etmez. DAL await'leri ConfigureAwait(false), UI await'leri normal bağlam kullanır. async void yalnızca olay işleyicilerindedir.

Taze kurulum `SchemaMigrations` journal'ı ile 001 temel şemayı kurar. Eski/yeni uygulama şemasına ek özellikler `__MigrationJournal` ile DbUp üzerinden uygulanır. Eski DB'ye gerçekte uygulanmamış 001 baseline kaydı yazılmaz. Betik adı SHA256 içerir; önceden uygulanmış betik değişirse veya DB daha yeni sürümdeyse yükseltme durur. Yeni değişiklik için yeni numaralı betik ekleyin. Başarısız yükseltme geri alınır; eski veriler korunur. İlk kurulum aracı mevcut yönetici parolasını sıfırlamaz.

```powershell
powershell.exe -NoProfile -File .\automation\otomatik_test_master.ps1 -NoDatabase
powershell.exe -NoProfile -File .\automation\otomatik_test_master.ps1
powershell.exe -NoProfile -File .\automation\otomatik_test_master.ps1 -Full
```

Master betiği restore/build/test yapar, gerçek TRX sonuçlarını 15 alana eşler, çalışmayan alanı başarılı saymaz ve rapor üretir. PS 5.1 için UTF-8 BOM kullanır. Boşluklu yollar tek argüman olarak aktarılır; NUnit çalışma klasörü sınırında yol normalize edilir. Temp temizliği yalnızca betiğin oluşturduğu, doğrulanmış GUID dizininde yapılır. Antivirüs, klasör istisnası veya powershell.exe izin listesi değiştirilmez.

CI, official actions/checkout, setup-dotnet ve upload-artifact ile Windows üzerinde aynı -Full betiğini ve .NET 10 paketlemeyi çalıştıracak şekilde hazırdır. Yerel başarı uzak GitHub çalıştırması sayılmaz. Kaynak Git kayıtları yereldir; uzak depoya otomatik push yapılmaz. Paketler için kilitli restore ve NuGet audit kullanılır. SDK/MSBuild seçimi SSMS'e bırakılmaz.

AuditLog aynı transaction içinde kullanıcı ve sabit işlem adını tutar; personel TC/IBAN/parola/açıklama audit metnine yazılmaz. NLog yalnızca sabit olay adı, istisna türü ve rastgele hata kodu tutar. Giriş günlükleri de istisna mesajı ve kimlik bilgisi içermez. Genel günlükler `%LOCALAPPDATA%\AccountingApp\logs`, giriş tanılama EXE yanındaki `logs` klasöründedir. Günlük yazma başarısızlığı girişin yeniden denenmesini engellemez.
