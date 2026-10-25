# Muhasebe — Oturum güvenliği deneme kılavuzu

25 Ekim 2026 · Güncel sürüm: session-revocation

Bu deneme, pasife alınan veya silinen hesabın açık uygulamada işlem yapamadığını gösterir. Mevcut yöneticini silme; yalnızca bu deneme için açacağın hesabı kullan.

## 1. İki güncel pencere hazırla

1. Eski sürümün bütün açık kopyalarını kapat. Masaüstündeki **Muhasebe - Güncel** kısayolunu aç; giriş hazırlığı tamamlanınca yönetici hesabınla giriş yap. Bu pencereye **A** diyelim.
2. **YÖNETİCİ BİLGİLERİ İŞLEMLERİ** ekranına gir. Kullanılmayan bir kullanıcı adı (örneğin **deneme-oturum**) ve kendi belirlediğin şifreyle **Personel sorumlusu** yetkisinde deneme hesabı ekle.
3. Masaüstü kısayolunu yeniden çift tıklayıp ikinci uygulamayı aç. Burada deneme hesabıyla giriş yap; bu pencereye **B** diyelim.
4. B'de **GENEL PERSONEL İŞLEMLERİ** ekranını aç. İki pencere de aynı veritabanı profilini kullanmalı.

## 2. Pasife alma ve açık oturum kontrolü

1. A'daki hesap listesinden yalnızca **deneme-oturum** hesabını seç.
2. **Pasife al** düğmesine basıp hesabın adını kontrol ederek onayla. Listedeki **Etkin** işareti kalkmalı.
3. B'de **F5**'e bas.

**Beklenen sonuç:** B yeniden giriş ekranına döner. Hesabın veya yetkinin değişmesi nedeniyle oturumun sona erdiği mesajı görünür. Pasif hesapla aynı şifreyi kullanarak tekrar giriş yapamazsın. Giriş reddi genel kullanıcı adı/şifre mesajı olarak gösterilir.

F5'e basmadan beklemeyi de deneyebilirsin: boşta kalan B penceresi yaklaşık 30 saniye içindeki kontrolde giriş ekranına dönmeli. Süren bir işlem varsa önce tamamlanması beklenebilir.

## 3. Yeniden etkinleştirme

1. A'da aynı hesabı seç, **Etkinleştir** düğmesine bas ve onayla.
2. B'nin açık personel ekranına kendiliğinden dönmediğini gör. Yeniden giriş yapmak gerekir.
3. B'de deneme hesabının kullanıcı adı ve şifresiyle giriş yap. Artık ekranları normal kullanabilmelisin.

**Beklenen sonuç:** Yeni giriş başarılıdır. Pasife alınmadan önceki oturum yeniden canlanmaz.

## 4. Yazma işleminin engellenmesini deneme — isteğe bağlı

1. B'de yalnızca deneme için oluşturduğun bir personel kaydını seç. Ad alanına bir deneme değişikliği yaz; henüz DÜZENLE'ye basma.
2. A'da deneme hesabını tekrar pasife al.
3. B henüz giriş ekranına dönmediyse hemen **DÜZENLE**'ye bas. Zaten giriş ekranına dönmüşse oturum iptali gerçekleşmiştir.

**Beklenen sonuç:** Değişiklik kaydedilmez; B giriş ekranına döner. A'da personel listesini yenileyerek adın değişmediğini kontrol edebilirsin. Bu adım için gerçek personel yerine önceki personel deneme kılavuzundaki deneme kaydını kullan.

## 5. Aynı kullanıcı adıyla yeniden oluşturma — isteğe bağlı

1. A'da deneme hesabını etkinleştir; B'de onunla yeniden giriş yap ve Genel ekranını açık bırak.
2. A'da yalnızca deneme hesabını **SİL** ile kaldır.
3. Aynı kullanıcı adıyla tekrar Personel sorumlusu hesabı oluştur. Şifreyi aynı seçmen de mümkündür.
4. B hâlâ eski ekrandaysa F5'e bas.

**Beklenen sonuç:** Eski B oturumu giriş ekranına döner. Kullanıcı adı aynı olsa bile yeniden oluşturulan hesap eski oturumla kullanılamaz; yeni hesabın bilgileriyle tekrar giriş gerekir.

## 6. Bitirme ve kontrol listesi

Deneme bittiğinde A'dan yalnızca deneme hesabını silip B penceresini kapatabilirsin. Gerçek yönetici hesabını değiştirme. İşlem geçmişi korunur.

- [ ] Pasife alınan hesap yeniden giriş yapamıyor.
- [ ] Açık B penceresinde F5 yeni giriş istiyor.
- [ ] Boşta kalan pencere yaklaşık 30 saniyelik kontrolde kapanıyor.
- [ ] Etkinleştirme sonrasında yeni giriş gerekiyor ve başarılı oluyor.
- [ ] Pasif hesabın eski ekranından personel değişikliği kaydedilemiyor.
- [ ] Aynı kullanıcı adıyla yeniden oluşturmak eski oturuma yetki vermiyor.

Beklenmeyen durumda hangi adımda olduğunu ve mesajı not et; şifreni paylaşmana gerek yok. Bu kılavuz için senin adına hesap oluşturulmadı, pasife alınmadı veya silinmedi. Parola değiştirme/sıfırlama ve hatalı giriş sınırlama ekranları bu değişikliğin kapsamında değildir.
