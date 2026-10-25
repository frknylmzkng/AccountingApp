# Parola değiştirme ve yöneticiyle sıfırlama

25 Ekim 2026. Yerel paket: `artifacts/releases/password-security/WinFormsApp2.exe`.

Sonraki güncellemede giriş ve mevcut parola doğrulamalarına hesap başına geçici kilit eklendi. Güncel kurallar [hatalı giriş kılavuzundadır](HATALI_GIRIS_SINIRLAMASI.md); aşağıdaki doğrulama kaydı parola özelliklerinin ilk eklendiği sürüme aittir.

## Kendi parolanı değiştirme

1. Güncel uygulamada kendi hesabınla giriş yap. Bütün roller bu işlemi yapabilir.
2. Üst menüde **Parolamı değiştir** seçeneğini aç.
3. Mevcut parolanı, yeni parolanı ve yeni parola tekrarını gir. Alanlar maskelidir.
4. **Parolayı değiştir** düğmesine bas. Başarıdan sonra giriş ekranına dönersin; yeni parolanla giriş yap.

Yeni parola 15–1024 karakter olmalı ve mevcut paroladan farklı olmalıdır. Boşluklar kırpılmaz; tekrar alanı birebir aynı olmalıdır. Uzun bir parola cümlesi kullanabilirsin. Eski kısa parolan, değiştirene kadar girişte kabul edilir. Yeni hesap ve ilk yönetici oluşturma da yeni uzunluk sınırını kullanır.

Yanlış mevcut parola, farklı tekrar veya kısa yeni parola kaydı değiştirmez. Bu durumlarda alanlar temizlenir; yeniden girmen gerekir. Ana ekrana dönmek veya çıkmak kayıt yapmaz.

## Yöneticiyle başka bir hesabı sıfırlama

1. Yönetici olarak giriş yapıp yönetici/hesap ekranını aç.
2. Listedeki hedef **etkin hesabın satırını** seç. Kendi hesabın için kendi parola değiştirme ekranını kullan.
3. **Parola sıfırla…** düğmesine bas. Pencerede hedef kullanıcı adını kontrol et.
4. Kendi yönetici parolanı girip **Geçici parola oluştur** düğmesine bas.
5. Başarılı işlemden sonra açılan pencerede geçici parolayı **Geçici parolayı göster** ile okuyabilirsin. Uygulama bunu kendiliğinden panoya veya dosyaya kopyalamaz.
6. Geçici parolayı hesap sahibine güvenli bir kanaldan ilet. Pencere kapatıldıktan sonra uygulama aynı parolayı tekrar göstermez. Kaybolursa yeniden sıfırla.

Geçici parola kriptografik rastgele üretilir, 32 karakterdir ve veritabanı saatine göre **30 dakika** geçerlidir. Sıfırlama eski parolayı ve hedef hesabın eski oturumlarını geçersiz kılar. Süresi dolarsa yönetici tekrar sıfırlamalıdır; hesabı etkinleştirmek süreyi uzatmaz.

Hedef hesap seçimden sonra değişmişse veya silinip aynı adla yeniden oluşturulmuşsa işlem reddedilir. Listeyi yenileyip hesabı yeniden seç. Pasif hesabı sıfırlamak için önce hesap durumunu kontrol et. Sıfırlama penceresindeki yönetici parolası yanlışsa hedef hesap değişmez.

## Hesap sahibinin geçici parolayla girişi

1. Kullanıcı adı ve geçici parolayla giriş yap.
2. Doğrudan parola değiştirme ekranı açılır. Geçici parolayı **Mevcut / geçici parola** alanına gir.
3. Kendi yeni parolanı ve tekrarını girip kaydet.
4. Giriş ekranında kendi yeni parolanla tekrar giriş yap. Geçici parola artık geçerli değildir.

Parola yenilenene kadar personel, ödeme, rapor, yönetici, ayar ve yedek ekranlarına erişim verilmez. Çıkış yapılabilir. Yenileme tamamlanmadan tekrar giriş gerekiyorsa geçici parola yalnızca kalan süresi içinde bu sınırlı ekranı açar; tek bir giriş denemesinden sonra tüketilmez. Süre dolarsa yeniden sıfırlama gerekir.

## Deneyebileceğin kontroller

Gerçek hesabını değiştirmek istemiyorsan ikinci bir yönetici oturumunu açık tutup yalnızca deneme hesabıyla çalış.

| Deneme | Beklenen sonuç |
| --- | --- |
| Görüntüleyici hesabıyla kendi parolasını değiştir | Kaydetme etkin; yeni parola ile giriş başarılı |
| Yanlış mevcut parola veya farklı parola tekrarı | İşlem reddedilir, eski parola geçerli kalır |
| Aynı parolayı yeniden seç | Reddedilir |
| Sıfırlamada yanlış yönetici parolası | Hedef hesap değişmez |
| Başka bir oturumu açık hedef hesabı sıfırla | Eski oturum en geç 30 saniyelik kontrolde girişe döner; veri işlemleri hemen reddedilir |
| Geçici parolayla giriş yap | Yalnızca parola yenileme ekranı açılır |
| Yeni parola belirledikten sonra geçici parolayı kullan | Giriş başarısız |
| Aynı hesabı yeniden sıfırla | Önceki geçici parola ve sınırlı oturum geçersiz olur |
| Hesap listesini veya raporunu aç | Parola, hash ve oturum belirteçleri görünmez |

## Teknik korumalar ve doğrulama

Parolalar PBKDF2-HMAC-SHA256 ile 600.000 yineleme, ayrı rastgele 16 bayt salt ve 32 bayt hash kullanılarak saklanır. Geçici parola 24 bayt kriptografik rastgele veriden oluşturulur; veritabanında yalnızca hash'i tutulur. Yeni parola, geçici parola, eski parola ve hash listelere, Excel/CSV'ye veya uygulama günlüklerine yazılmaz. Kullanıcı listesi yenileme gereğini ve geçici parola son tarihini gösterebilir.

Parola değişimi/sıfırlama ile audit kaydı aynı transaction içindedir; audit yazımı başarısızsa parola değişimi geri alınır. Hash hesaplanırken kilit açık tutulmaz; yazmadan önce oturum, yetki ve seçili hesap sürümü tekrar kontrol edilir. Eşzamanlı iki parola değişiminden yalnızca biri başarılı olur. Oturum sürümü ve epoch yenilenir; eski yetki veya geçici oturum yeniden kullanılamaz.

Bu akışta mevcut parolayı yeniden doğrulama, uzun parolaları ve boşlukları kabul etme tercihleri [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html) ile; rastgele, hash'lenmiş, süreli sıfırlama bilgisi ve oturum iptali tercihleri [OWASP Forgot Password Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html) ile desteklenir. Bu çalışma giriş denemesi sınırlaması veya sızmış parola listesi kontrolü eklemez; bunlar ayrıca ele alınmalıdır.

Tam regresyon: **364 başarılı, 0 başarısız, 0 atlanan**; Release derlemesi uyarıları hata sayarak başarılı. Kanıt: `artifacts/analysis/master-20261025-083517-979/master.trx` ve `summary.json`. Testler ayrı geçici LocalDB motorunda çalıştı; gerçek hesap parolaları değiştirilmedi. Kapsam: bütün roller, yanlış parola, tekrar doğrulama, süre sonu, art arda sıfırlama, eski oturum, eski hedef seçimi, yeniden oluşturulmuş hesap, audit hatasında rollback, eşzamanlı değişim, liste/rapor/günlük gizliliği ve sınırlı ekran yönlendirmesi.

## Güncelleme

Eski uygulama kopyalarını kapatıp güncel masaüstü kısayolundan yeniden aç. İlk giriş hazırlığı `103-password-change-reset.sql` şema yükseltmesinden önce doğrulanmış güvenlik yedeği alır. Mevcut hesaplar zorunlu parola değişimine alınmaz; veri ve mevcut parola hash'leri korunur. Bu çalışma sırasında gerçek veritabanına şema yükseltmesi veya parola değişimi uygulanmadı; hazırlık sonraki açılışta çalışır. Eski uygulama sürümlerini aynı veritabanıyla birlikte kullanma.

Kaynak değişikliklerinin tarihçesi [değişiklik günlüğündedir](../CHANGELOG.md). Eski v1.0.0 Release indirmesi bu geliştirmeleri içermez.
