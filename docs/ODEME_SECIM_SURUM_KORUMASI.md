# Ödeme seçiminde kayıt sürümü koruması

28 Ekim 2026. [Güncel incelemenin](PROJE_DURUM_ANALIZI_2026_10_28.md) F01 bulgusu için yerel düzeltme. Mevcut RecordVersion sütunu kullanılır; yeni SQL migration veya gerçek veritabanı yükseltmesi gerekmez.

## Değişen davranış

Ödemeler ekranı ödeme onayını göstermeden önce seçilen satırın **özgün TC, grup ve RecordVersion** değerlerinin kopyasını alır. Sıralama, görünür hücre değişikliği veya onay penceresi sırasında listenin değişmesi bu hedefi değiştirmez. Sürümü olmayan seçimle ödeme başlatılmaz.

Hazırlama aşaması bu sürümün hâlâ güncel olduğunu transaction içinde doğrular. Personel silinip aynı TC/grupta yeniden oluşturulmuşsa veya ad/IBAN/bakiye değiştirilmişse istek hazırlanmaz; bekleyen ödeme dosyası ve ödeme hareketi oluşmaz. Ekran güncel listeyi getirir; tutar alanı korunur ve kullanıcı güncel kaydı yeniden seçer.

Hazırlanmış ödeme isteği, özgün sürümü DPAPI ile şifrelenen kalıcı günlüğünde taşır. Gönderimde personel kilidi, özgün sürüm ve kapanış sınırı tekrar doğrulanır. SQL UPDATE koşulu da TC + RecordVersion + yeterli bakiye ister. Hazırlamadan sonra değişen kayıt için bakiye, defter ve audit değiştirilmeden kesin ret döner. Sonucu onayladıktan sonra güncel seçimle ayrı ödeme başlatılabilir.

## Tekrar ödeme ve eski günlük uyumluluğu

Yeni günlük isteğinin FormatVersion değeri 2'dir. PersonVersion sekiz bayt olmak zorundadır. Bilinmeyen biçim veya eksik/bozuk sürüm sessizce tamamlanmaz; dosya korunur.

Eski sürümün günlük dosyasında bu iki alan yoktur. Bu biçim açıkça tanınır:

- Defterde aynı RequestId/TC/grup/tutarlı kayıt varsa **kaydedildi** veya **iptal edildi** sonucu okunur; tekrar ödeme yazılmaz.
- Defterde kayıt bulunamıyorsa eski, sürümsüz istek yeni bakiye azaltımı yapamaz. Kullanıcı **Bekleyen ödemeyi yeniden dene** ile sonucu kontrol ettiğinde açıklamalı ret görür. Ardından **Sonucu onayla / yeni ödeme** ile kapatıp listeyi yenileyerek güncel personeli seçer.
- Günlüğün okunamaması veya sunucuya erişilememesi kesin ret sayılmaz; mevcut belirsiz sonuç koruması sürer. Dosya elle silinmez veya farklı kimlikle değiştirilmez.

Yeni biçimde de daha önce kaydedilmiş bir istek, personelin sürümü sonradan değişse veya personel silinse bile defterden tanınır. Aynı RequestId yeniden bakiye düşmez. Servisin eski doğrudan PayAsync girişinden de yeni bir ödeme yazabilmek için seçim sürümü gerekir; kayıtlı sonucun okunması bu zorunluluktan ayrıdır.

Kullanıcıya ait şifreli ödeme günlüğü otomatik silinmez; sonuç onayıyla temizlenir. Eski ve yeni uygulamaları aynı veritabanında karışık sürümle çalıştırmak yerine güncelleme sırasında açık eski uygulamalar kapatılmalıdır.

## Deneme adımları

Bu senaryolar ayrı test veritabanındaki örnek personelle uygulanır.

1. A penceresinde Ödemeler'i açın ve personeli seçin.
2. B penceresinde aynı personeli silip aynı TC/grupta 250 TL kalan tutarla yeniden oluşturun.
3. A penceresinde 30 TL ödeme onaylayın. Eski seçimin değiştiği açıklanmalı; yeni kaydın bakiyesi **250 TL** kalmalı.
4. A'da güncel listeden personeli yeniden seçin ve 30 TL ödeyin. Bakiye **220 TL** olmalı; bir ödeme hareketi oluşmalı.
5. Sil-yeniden ekle yerine B'de ad veya IBAN değiştirildiğinde de eski seçim reddedilmeli.
6. Kaydedilmiş ödeme sonucu onaylanmadan ekran yeniden açıldığında aynı kayıt gösterilmeli; yeniden denemek ikinci ödeme oluşturmamalı.

Hazırlama–gönderim arası kesinti, eski günlük dosyası ve eşzamanlı gönderim senaryoları otomatik testlerle sınanır; bunları gerçek ödeme üzerinde bağlantı keserek denemek gerekmez.

## Kapsam

Bu düzeltme ödeme seçimine odaklanır. F02 geri yükleme hata güvenliği ve F03 IBAN doğrulaması ayrı sonraki işlerdir. GitHub'daki v1.2.0 ZIP'i bu yerel düzeltmeyle değiştirilmez.

## Doğrulama

Tam `otomatik_test_master.ps1 -Full` çalışması **646 başarılı, 0 başarısız, 0 atlanan test** ile tamamlandı. Önceki 610 teste 36 senaryo eklendi. Release derlemesi **0 uyarı / 0 hata**; entegrasyon ve geri yükleme testlerinin GUID adlı geçici LocalDB motoru temizlendi. Gerçek veritabanı test hedefi olmadı.

Yeni kapsam: iki personel grubunda hazırlamadan önce ve hazırlama–gönderim arasında yeniden oluşturma/ad/IBAN/bakiye değişimi; değişen/silinen personelden sonra kayıtlı isteği tanıma; eski günlük biçimi; sürümsüz doğrudan servis çağrısını engelleme; sıralanmış özgün seçimi dondurma ve bozuk sürüm bilgisi.

Kanıt: `artifacts/analysis/master-20261028-132650-032/master.trx`, `summary.json`. Önceki hatanın 250 TL → 220 TL kanıtı `artifacts/analysis/review-20261028/probe-results.json` içindedir; yeni testler aynı yeniden oluşturma durumunda eski seçimi reddeder ve 250 TL'yi korur.

Güncel yerel paket: `artifacts/releases/payment-selection-version/`. Masaüstü kısayolu ve açık uygulama bu geliştirme adımında değiştirilmez; kullanıcı isteğiyle yeni paket ayrıca açılabilir.

## Uygulamalı ekran doğrulaması — 28 Ekim 2026

Kullanıcı isteğiyle yayımlanan yerel paketin DLL'i kullanılarak gerçek PaymentsForm ekranı ayrı bir GUID test veritabanında açıldı. Test aracı DLL'i değiştirmedi; SHA256 değeri paketle aynı: `788078D7ECEC48666E2E2E00B340ADC98F0D217384AEC9CBD673A025BD8B7499`. Sentetik oturum/kayıtlar kullanıldı; gerçek kullanıcı parolası girilmedi ve gerçek personele ödeme yapılmadı.

**11 ekran/işlem kontrolü başarılı:**

- Genel ve Joker için eski satır seçiliyken kayıt silinip aynı TC/grupta 250 TL ile yeniden oluşturuldu. Ekrandan 30 TL ödeme onayı reddedildi; bakiye 250 TL, defter ve audit değişmeden kaldı.
- Her iki grupta güncel seçimle 30 TL ödeme bir kez kaydedildi; bakiye 220 TL oldu. Kayıtlı sonuçta yeniden ödeme düğmesi kapalıydı.
- Aynı RequestId iki kez daha gönderildi; ikinci bakiye düşümü/ödeme/audit oluşmadı.
- Gerçek Ödemeler ekranı kapatılıp yeniden açıldı; kalıcı günlükten aynı RequestId ve kaydedilmiş sonuç okundu, yeniden ödeme yapılmadı.
- Sonuç onayından sonra bilinçli yeni 10 TL ödeme ayrı RequestId ile kaydedildi; bakiye 210 TL oldu.
- Ayrı senaryoda commit sonrası yanıt kaybı taklit edildi. Gerçek ekran yeniden açılınca 30 TL kaydı bulundu; 100 TL başlangıç bakiyesi 70 TL kaldı, ikinci ödeme oluşmadı.

Geçici veritabanı düşürüldü; test motoru, sentetik bekleyen ödeme ve boş kilit dosyası temizlendi. Masaüstündeki **Muhasebe - Güncel** kısayolu bu yerel pakete yönlendirildi. Önceki kısayol yedeklendi ve mevcut appsettings.json özeti korunarak normal uygulama açıldı; giriş hazırlığı `ready` durumuyla doğrulandı.

Kanıtlar: `artifacts/analysis/payment-selection-live-ui/ui-results.json`, `cleanup.txt`, `deployment.json`, `verification.json`. Bu adımda uygulama kaynak kodu yeniden değiştirilmedi; önceki 646 testlik tam doğrulamaya ek olarak yayımlanan DLL üzerinde uygulamalı ekran kontrolü yapıldı.
