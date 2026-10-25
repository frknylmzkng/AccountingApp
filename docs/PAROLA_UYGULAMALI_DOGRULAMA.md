# Parola özelliklerinin yayımlanan paketle doğrulanması

25 Ekim 2026. Paket: `artifacts/releases/password-security/WinFormsApp2.exe`.

Eski sürümden kalan arka plan süreci kapatıldı. Güncel uygulama açıldı; giriş hazırlığı başarıyla tamamlandı ve yönetici ana ekranı gözlendi. Son kontrolde yalnızca güncel sürümün 18960 numaralı süreci açıktı. Açılışta şema yükseltmesi için 08:45:32'de 3.854.336 baytlık güvenlik yedeği oluşturuldu.

## Yöntem

Paketin gerçek `WinFormsApp2.dll` dosyası ayrı bir deneme aracına kopyalandı; SHA-256 eşitliği çalıştırmadan önce ve sonra doğrulandı:

`78471D40C1C6C33F08C4B574C1424110AF189F48BA5A3961A596B0FE4CAAB344`

Rastgele adlandırılmış ayrı LocalDB motorunda yalnızca bu deneme için yönetici ve görüntüleyici hesapları oluşturuldu. Parola işlemleri yayımlanan AccountService üzerinden yürütüldü. Gerçek ApplicationContext, parola değiştirme formu, ekran yönlendirmesi ve 30 saniyelik oturum kontrolü de çalıştırıldı. Kimlik doğrulama penceresine otomatik giriş veya parola kaydetme düğmesine otomatik tıklama yapılmadı. Bu kayıt servis entegrasyonu ve pencere yaşam döngüsü doğrulamasıdır.

## Sonuçlar

**32 kontrol başarılı; hata bulunmadı.**

- Görüntüleyici kendi parola ekranını açabildi; parola alanları maskeliydi.
- Yanlış mevcut parola ve farklı tekrar reddedildi; başarısız işlem eski oturumu değiştirmedi.
- Başarılı değişimden sonra eski parola reddedildi, boşlukları korunmuş yeni parola kabul edildi; eski oturum geçersiz oldu.
- Yanlış yönetici parolasıyla sıfırlama reddedildi; hedef parolası korundu.
- Doğru yönetici doğrulamasıyla rastgele, 30 dakika geçerli geçici parola üretildi. Eski parola ve açık oturum geçersiz oldu.
- Gerçek zamanlayıcı açık test penceresini **29,31 saniye** sonra kapatıp giriş ekranına döndürdü.
- Eski hedef seçimiyle ikinci sıfırlama reddedildi.
- Geçici parolalı oturum doğrudan gerçek parola değiştirme ekranına yönlendi; diğer ekranlar ve veri okuma servisleri engellendi.
- Veritabanında geçici parolanın yalnızca doğrulanabilir hash'i bulundu; eski açık metin kolonu boş kaldı.
- Zorunlu değişim tamamlandıktan sonra geçici parola ve kısıtlı oturum reddedildi; yeni normal giriş ana ekranı açtı.
- Başarılı üç parola işlemi audit kayıtlarına işlendi.
- Süresi dolmuş geçici parola reddedildi; sıfırlayan yöneticinin parolası korundu.

Test veritabanı, test dosyaları ve ayrı LocalDB motoru temizlendi. Gerçek hesap parolaları değiştirilmedi. Uygulama kaynak kodunda ek düzeltme gerekmedi.

Makinece okunabilir sonuç: `artifacts/analysis/password-live-check/results.json`. Deneme aracı ve çalıştırma betiği aynı klasördedir; geçici test parolaları sonuç dosyasına kaydedilmez.

Kullanım adımları: [Parola değiştirme ve sıfırlama kılavuzu](PAROLA_DEGISTIRME_VE_SIFIRLAMA.md).
