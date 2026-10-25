# Muhasebe — Personel düzenleme deneme kılavuzu

25 Ekim 2026 · Güncel sürüm: personnel-concurrency

Bu kılavuzla normal personel düzenlemeyi, başka pencerede yapılan değişikliğin korunmasını ve eski taslak kontrolünü deneyebilirsin. Önce 1–5 numaralı bölümleri uygula; diğer denemeler isteğe bağlıdır. Ana deneme yaklaşık 10–15 dakika sürer.

## 1. Başlangıç ve deneme kaydı

1. Masaüstündeki **Muhasebe - Güncel** kısayolunu aç. Eski sürümün açık kopyalarını kullanma.
2. **Giriş hazır. Kullanıcı adınızı ve şifrenizi girin.** mesajını bekle. İlk açılışta veritabanı için yedek ve güncelleme hazırlığı yapılabilir.
3. Kendi hesabınla giriş yap. Düzenleme için **Yönetici** veya **Personel sorumlusu** yetkisi gerekir.
4. **GENEL PERSONEL İŞLEMLERİ** ekranına gir.
5. **TEMİZLE** veya **F2** ile yeni kayıt alanlarını temizle. Aşağıdaki gibi bir deneme kaydı oluştur ve **EKLE** düğmesine bas.

| Alan | Örnek deneme değeri |
| --- | --- |
| TC | 90000000001 — listede kullanılmıyorsa |
| ADI | DENEME |
| SOYADI | KONTROL |
| IBAN | TR000000000000000000000000 |
| VERİLECEK PARA | 1000,00 |

Bu TC ve IBAN yalnızca yerel deneme örnekleridir; gerçek kişi veya ödeme bilgisi olarak kullanılmamalıdır. TC zaten kayıtlıysa başka bir kullanılmayan 11 haneli deneme değeri seç. Gerçek personel kaydı yerine bu deneme satırını kullan.

**Beklenen sonuç:** Deneme kaydı listede görünür; tutar 1.000,00 TL'dir. Personel eklemek tek başına izin oluşturmaz. TC'yi bir yere not et; iki pencerede de aynı kaydı seçeceksin.

## 2. Normal düzenleme

1. Listeden deneme personelinin satırını seç. Alanların bu kayıtla dolduğunu gör.
2. Adını **DENEME NORMAL** yap; TC, IBAN ve tutarı değiştirme.
3. **DÜZENLE** düğmesine bas.
4. Listeden aynı personeli tekrar seç.

**Beklenen sonuç:** Ad **DENEME NORMAL** olur; IBAN ve 1.000,00 TL tutar korunur. Başarılı kayıttan sonra form alanlarının temizlenmesi normaldir; devam etmek için satırı yeniden seç.

## 3. İki pencereyi hazırlama

1. Açık uygulamaya **A penceresi** diyelim. Bu pencereyi açık bırak.
2. Masaüstündeki **Muhasebe - Güncel** kısayoluna yeniden çift tıkla. Görev çubuğu simgesi yalnızca mevcut pencereyi öne getirebilir; ikinci uygulamayı masaüstü kısayolundan aç.
3. İkinci uygulamada giriş yap; buna **B penceresi** diyelim. İki ayrı düzenleme yetkili hesabın varsa farklı hesaplar kullanabilirsin. Aynı hesabın iki ayrı penceresi de kontrolü denemek için yeterlidir.
4. İki pencerede de **GENEL PERSONEL İŞLEMLERİ** ekranını aç. Aynı veritabanı profilini kullan; ayarlardaki bağlantıyı değiştirme.
5. Her iki pencerede **F5**'e bas, ardından aynı deneme personelini seç.
6. İki pencerede de adın **DENEME NORMAL**, IBAN'ın başlangıç değeri ve tutarın 1.000,00 TL olduğunu kontrol et.

**Kritik nokta:** Sonraki bölümde A kaydettikten sonra B'de **F5'e basma ve satırı yeniden seçme**. B'nin eski bilgilerle kalması, denemenin asıl koşuludur.

## 4. Ana deneme: A'nın ad değişikliğini B ezemesin

| Sıra | Pencere | Yapılacak işlem |
| --- | --- | --- |
| 1 | A | Adı **DENEME A** yap ve **DÜZENLE**'ye bas. |
| 2 | B | Listeyi yenilemeden, IBAN'ı **TR111111111111111111111111** yap. |
| 3 | B | **DÜZENLE**'ye bas. |

**B'de beklenen mesaj:**

> Bu personel kaydı başka bir işlem tarafından değiştirilmiş veya yeniden oluşturulmuş. Girdiğiniz bilgiler korundu. Listeyi yenileyip güncel kaydı yeniden seçin.

Mesajı kapat. B'de yazdığın deneme IBAN'ının formda durduğunu kontrol et. İşlem reddedildiğinde girişlerin otomatik temizlenmemelidir.

Sonra **A penceresinde** F5'e basıp deneme kaydını yeniden seç:

- Ad **DENEME A** kalmalı.
- IBAN başlangıçtaki sıfırlı örnek değer olarak kalmalı; B'nin değişikliği kaydedilmemeli.
- Tutar 1.000,00 TL kalmalı.

Bu sonuç, eski ekrandaki kayıtla başka kullanıcının ad değişikliğinin ezilemediğini gösterir.

## 5. Çakışmadan sonra güncel kayıtla devam etme

1. B'deki yazdığın değeri gerekirse not et. **F5**'e bas ve deneme personelini yeniden seç.
2. Alanların güncel kayıtla dolduğunu gör: ad **DENEME A** olmalı. Yeniden satır seçmek, alanları güncel değerlerle doldurur.
3. IBAN'a **TR111111111111111111111111** değerini yeniden yaz.
4. **DÜZENLE**'ye bas.
5. A'da F5'e basıp aynı kaydı seç.

**Beklenen sonuç:** Bu kez kayıt başarılıdır. Ad **DENEME A** olarak korunur, IBAN yeni örnek değer olur, tutar değişmez. Uygulama çakışmaları kendiliğinden birleştirmez; güncel bilgiler üzerinden bilinçli düzenleme yapmanı ister.

## 6. Ters deneme: A'nın IBAN değişikliğini B ezemesin

1. A ve B'de F5'e basıp aynı deneme personelini seç.
2. A'da IBAN'ı **TR222222222222222222222222** yap ve DÜZENLE'ye bas.
3. B'de yenileme yapmadan soyadını **DENEME B** yap ve DÜZENLE'ye bas.
4. B'de çakışma mesajı çıkmalı; yazdığın soyadı formda kalmalı.
5. A'da F5 ile kaydı kontrol et.

**Beklenen sonuç:** A'nın yeni IBAN'ı korunur. B'nin soyadı değişikliği kaydedilmez; soyadı hâlâ **KONTROL**, tutar 1.000,00 TL'dir.

## 7. Yakın zamanda iki kaydetme

1. İki pencerede de F5'e basıp aynı personeli seç.
2. A'da adı **DENEME YARIS A**, B'de **DENEME YARIS B** yap.
3. İki pencerede de DÜZENLE'ye peş peşe bas; arada yenileme veya yeniden satır seçme yapma.

**Beklenen sonuç:** Yalnızca bir kayıt başarılı olur; diğer pencere çakışma mesajı verir. Hangisinin kazanacağına bağlı olarak listede iki addan biri görünür. İkisinin de başarılı olması beklenen davranış değildir.

## 8. Eski taslak başka kullanıcının değişikliğini ezemesin

1. A ve B'de F5'e basıp aynı deneme personelini seç.
2. B'de adı **DENEME TASLAK** yap, ancak DÜZENLE'ye basma.
3. B'de **F6** ile taslağı sakla.
4. A'da IBAN'ı **TR333333333333333333333333** yapıp DÜZENLE ile kaydet.
5. B'de **F5**'e bas. Bu adım önemlidir: liste A'nın güncel kaydını görmeli.
6. B'de **F7** ile eski taslağı yükle.

**Beklenen sonuç:** Taslağın kayıt sürümünün güncel listeyle eşleşmediğini bildiren bir uyarı görünür. Uyarıyı kapattığında taslak alanları yüklenir, fakat mevcut personel satırı düzenleme hedefi olarak seçili değildir.

Bu durumda DÜZENLE'ye basarsan önce listeden kayıt seçmen istenir; eski taslak doğrudan kaydı değiştiremez. A'da F5 ile kontrol ettiğinde yeni IBAN korunmalıdır. Devam etmek için güncel satırı seçip gerekli değişiklikleri yeniden kontrol et.

## 9. TC ve izin tarihleri korunsun

**TC kontrolü:** Seçili deneme kaydında TC alanını farklı bir değere çevirip DÜZENLE'ye bas. Düzenleme sırasında TC'nin değiştirilemeyeceği uyarısı beklenir. Ardından satırı yeniden seç.

**İzin kontrolü — yalnızca Genel personelde:**

1. Deneme personelini seç; başlangıcı **25.10.2026**, bitişi **27.10.2026** yap.
2. **İZNİ KAYDET** düğmesine bas.
3. Satırı yeniden seç, adını değiştir ve yalnızca DÜZENLE'ye bas.
4. Kaydı yeniden seçip izin tarihlerini kontrol et.

**Beklenen sonuç:** İzin tarihleri değişmemelidir. Bu örnek başlangıç ve bitiş dahil 3 gündür. Ad/IBAN düzenleme ile izin kaydetme ayrı işlemlerdir.

## 10. Joker ve kolon görünürlüğü

**Joker:** **JOKER PERSONEL İŞLEMLERİ** ekranında farklı bir deneme TC'siyle (örneğin kullanılmıyorsa **90000000002**) ikinci kayıt oluştur. 2–7 numaralı bölümlerdeki normal düzenleme ve çakışma denemelerini bu kayıt için de uygulayabilirsin. Joker ekranında izin denemesi yapma.

**Kolonlar ve Excel:** Üst araç çubuğundaki **Kolonlar** menüsünde teknik bir **RecordVersion** sütunu görünmemelidir. İstersen Excel çıktısı alıp bu teknik alanın rapora eklenmediğini kontrol edebilirsin. Excel aktarımı yalnızca seçili satırı değil, ekrandaki görünür listeyi aktarır; dosyayı kendi bilgisayarında sakla.

## 11. Denemeyi bitirme

1. İki pencerede de devam eden kayıt işlemi olmadığından emin ol.
2. Yalnızca deneme için oluşturduğun TC'leri listeden seçerek SİL ile kaldırabilirsin. Genel personeli silmek ona ait deneme izinlerini de kaldırır; silme onayını TC'yi kontrol ederek ver.
3. Deneme kayıtları etkin listeden kalkar; uygulamanın işlem geçmişi korunur.
4. B penceresinden **Çıkış** yapıp pencereyi kapat. A'yı günlük kullanım için açık bırakabilirsin.

## Sonuç kontrol listesi

- [ ] Normal personel düzenlemesi başarılı.
- [ ] A adı değiştirince B'nin eski kaydı reddediliyor.
- [ ] A IBAN'ı değiştirince B'nin eski kaydı reddediliyor.
- [ ] Reddedilen kayıtta B'nin girdiği alanlar hemen temizlenmiyor.
- [ ] F5 ve yeniden seçimden sonra bilinçli yeni düzenleme başarılı.
- [ ] İki eski sürümle peş peşe kayıtta yalnızca biri başarılı.
- [ ] Eski taslak güncel kayda otomatik bağlanamıyor.
- [ ] TC değişikliği engelleniyor; ad/IBAN düzenleme izin tarihlerini değiştirmiyor.
- [ ] Aynı koruma Joker personelde de çalışıyor.
- [ ] Teknik sürüm alanı kolon menüsünde ve Excel'de görünmüyor.

Beklenmeyen durumda hangi bölüm ve adımda olduğunu, görünen hata metnini ve hangi pencerenin önce kaydettiğini not et. Şifreni paylaşmana gerek yok. Çakışma beklerken iki pencere de başarılı olursa aynı kaydı ve aynı veritabanını kullandığını, B'de arada F5 veya yeniden seçim yapmadığını kontrol et.

## Kısayol tuşları

| Tuş | İşlev |
| --- | --- |
| F1 | Uygulama yardımı |
| F2 | Formu temizle / yeni kayıt |
| F5 | Listeyi yenile |
| F6 | Genel/Joker form taslağını sakla |
| F7 | Form taslağını yükle |
| Esc | Ana ekrana dön |

Bu kılavuz için senin adına personel eklenmedi, düzenlenmedi veya silinmedi. Deneme adımları, sen giriş yaptıktan sonra uygulayacağın işlemlerdir.
