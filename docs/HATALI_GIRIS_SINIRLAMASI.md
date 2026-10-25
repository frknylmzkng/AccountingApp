# Hatalı giriş denemelerini sınırlama

25 Ekim 2026. Yerel paket: `artifacts/releases/login-throttle/WinFormsApp2.exe`.

## Davranış

| Kural | Sonuç |
| --- | --- |
| Aynı hesapta 15 dakika içinde 5 hatalı parola | Hesabın yeni girişleri 5 dakika durdurulur |
| Kilit sürerken doğru parola | Giriş kabul edilmez; sürenin dolması gerekir |
| Kilit sürerken başka hatalı deneme | Süre uzatılmaz |
| Kilit dolduktan sonra yeni hata | Yeni pencere ve 1 hata ile başlanır |
| Kilitlenmeden önce doğru parola | Hata sayacı temizlenir |
| 15 dakikalık hata penceresinin dolması | Sonraki hata yeni pencereyi başlatır |
| Uygulamayı kapatıp açmak, farklı pencere veya kullanıcı adı yazımı | Sayacı sıfırlamaz; denemeler aynı veritabanı hesabına bağlanır |
| Başka hesabın girişleri | Etkilenmez |

Ekranda bütün reddedilen girişler için aynı mesaj görünür: **“Giriş yapılamadı. Bilgilerinizi kontrol edin; art arda hatalı deneme yaptıysanız 5 dakika bekleyip yeniden deneyin.”** Mesaj, hesabın varlığını veya kilitli olup olmadığını açıklamaz. Bağlantı/kurulum hataları önceki gibi ayrı hata koduyla bildirilir; hatalı parola sayılmaz.

Kilit yalnızca yeni girişleri ve mevcut parolayla yeniden doğrulamayı sınırlar. Zaten açık ve geçerli oturumlar kapanmaz; personel ve ödeme işlemleri mevcut yetki kontrolleriyle çalışmayı sürdürür. Giriş denemeleri hesap satırını veya oturum sürümünü değiştirmez.

## Parola işlemleri ve kurtarma

**Parolamı değiştir** ekranındaki mevcut parola kontrolü ve yönetici sıfırlamasında girilen yönetici parolası aynı hesap sayacını kullanır. Böylece giriş ekranı dışında sınırsız parola tahmini yapılabilecek ikinci bir yol oluşmaz. Yanlış yeni parola tekrarı gibi form doğrulama hataları parola tahmini sayılmaz.

Kilitliyken mevcut parolan doğru olsa da bu yeniden doğrulamalar reddedilir. 5 dakika dolduktan sonra yeniden dene. Başka bir etkin yönetici, kendi parolasını doğrulayarak kilitli hesabı sıfırlayabilir. Başarılı sıfırlama hedef hesabın eski sayacını temizler; yeni geçici parola önceki gibi 30 dakika geçerlidir ve zorunlu parola değiştirme ekranını açar. Geçici parola da hatalı giriş sınırına tabidir.

Hesap silinip aynı adla yeniden oluşturulursa eski hatalar yeni hesaba taşınmaz. Onaylı yedek geri yükleme bütün oturum kimliklerini yenilediğinden giriş sayaçlarını da temizler.

## Deneme adımları

Denemeler için bir görüntüleyici hesabı kullan; ayrı yönetici oturumunu açık tut.

1. Deneme hesabının kullanıcı adıyla dört kez yanlış parola gir.
2. Beşinci denemede doğru parolayı kullan: giriş başarılı olmalı. Çıkış yap.
3. Aynı hesapta bu kez beş kez yanlış parola gir. Ardından doğru parolayla dene: giriş reddedilmeli.
4. Uygulamayı yeniden açıp doğru parolayla dene: aynı kilit sürmeli.
5. Başka bir hesapla girişin çalıştığını kontrol et.
6. Kilitli hesapta 5 dakika dolduktan sonra doğru parolayla giriş yap: başarılı olmalı. Kilit sırasında yapılan denemeler süreyi uzatmaz.
7. İkinci bir denemede hedef hesabı kilitle ve yönetici hesabından parolasını sıfırla. Yeni geçici parolayla yalnızca parola yenileme ekranı açılmalı; yeni parola belirlenince normal giriş çalışmalı.

Gerçek hesabını kilitlersen uygulamayı yeniden kurmana gerek yok; kilidin dolmasını bekle. Parolanı kimseyle paylaşmadan bu adımları deneyebilirsin.

## Uygulama

Yeni `104-login-throttle.sql` betiği ayrı `LoginThrottle` tablosunu ekler. Tabloda yalnızca hesap epoch kimliği, hata sayısı ve UTC zamanları tutulur; kullanıcı adı, açık parola veya parola hash'i bulunmaz. Önceden uygulanmış migration dosyaları değiştirilmedi. Yeni sürüm ilk giriş hazırlığında doğrulanmış yedek alıp betiği uygular.

Sayaç ve kilit kontrolleri veritabanı saatini kullanır. Aynı hesap için SQL oturumuna bağlı uygulama kilidi, farklı istemcilerden gelen doğrulamaları sıraya alır. PBKDF2 hesaplanırken SQL transaction'ı veya satır kilidi açık tutulmaz. İstemci iptali sırasında bağlantı havuzunda kilit bırakılmaması için oturum kilidi açıkça bırakılır; belirsiz bağlantı havuza geri verilmez. Başarılı girişte sayaç temizliği ve `Login.Success` audit kaydı aynı transaction içinde tamamlanır.

Bulunmayan, pasif, süresi geçmiş geçici parolalı ve kilitli hesaplarda da sahte bir hash ile parola hesaplaması yapılır; bu, erken çıkışlardan kaynaklanan süre farklarını azaltır. Mutlak zaman eşitliği iddiası yoktur. Reddedilen girişler için uygulama günlükleri yalnızca sabit `Login.Rejected` / `Login.Locked` olaylarını tutar; girilen kullanıcı adı ve parola yazılmaz.

Hesap başına sayaç, gözlem penceresi, süreli kilit ve parola kurtarmayla erişim sağlama tercihleri [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#login-throttling) ile desteklenir. 5 deneme / 15 dakika / 5 dakika değerleri bu uygulamanın yerel kullanımına yönelik uygulama kararıdır; OWASP'ın zorunlu sayıları değildir. Doğrudan SQL erişimi olan Windows kullanıcısının yetkileri SQL Server/Windows izinleriyle yönetilir.

## Güncelleme

Eski uygulama kopyalarını kapatıp **Muhasebe - Güncel** kısayolundan aç. Bütün istemciler aynı güncel sürümü kullanmalıdır. Bu çalışma sırasında gerçek hesaplara hatalı deneme yapılmadı veya parola değişikliği uygulanmadı; şema yükseltmesi sonraki açılışta çalışır. Yerel paket henüz GitHub Releases'e yayımlanmadı.

## Doğrulama

Tam regresyon **383 başarılı, 0 başarısız, 0 atlanan**; uyarıları hata sayan Release derlemesi başarılı. Önceki sürüme göre 19 yeni test durumu eklendi. Kanıt: `artifacts/analysis/master-20261025-090245-255/master.trx`, `summary.json` ve `RAPOR.md`. Ayrı geçici LocalDB motoru test sonunda kaldırıldı.

Yeni senaryolar bütün roller, beşinci hata sınırı, doğru parolayla sayaç temizleme, kalıcı sayaç ve kullanıcı adı varyasyonları, başka hesabın etkilenmemesi, kilit/gözlem penceresi sonu, 12 eşzamanlı deneme, eski boolean giriş yolunun korunması, geçici parola, parola işlemlerindeki ortak limit, sıfırlamayla kurtarma, silinip yeniden oluşturulan hesap, audit hatasında geri alma, iptal edilen kilit beklemesi ve şema yükseltmesinde verilerin/oturumların korunmasını kapsar.
