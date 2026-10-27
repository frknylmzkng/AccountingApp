# Personel silme işleminde sürüm koruması

26 Ekim 2026. Genel ve Joker personel için yerel geliştirme.

## Davranış

Silme işlemi, seçilen satırın özgün TC ve RecordVersion değerini kullanır. Sürüm, silme onayı açılmadan önce alınır ve işlem boyunca aynı seçim korunur. Sıralama, gizli sütunlar veya ekranda sonradan değiştirilmiş değerler yeni bir silme hedefi oluşturmaz.

Servis eksik veya sekiz bayt olmayan sürümü veritabanını açmadan reddeder. Sürüm doğrulanmadan bağlı izinler silinmez. Aynı transaction içinde güncel personel satırı UPDLOCK/HOLDLOCK ile kilitlenir ve sürüm eşleşmesi aranır; DELETE koşulu da TC ve sürümü birlikte doğrular. Etkilenen satır sayısı tam bir olmalıdır.

Başka kullanıcı personelin adını, IBAN'ını veya bakiyesini değiştirdiyse; ödeme yapıldıysa; kayıt silindiyse veya aynı TC ile yeniden oluşturulduysa eski seçimle silme reddedilir. Genel grubundaki izinler, personel bakiyesi, ödeme defteri ve audit bu reddetme sırasında değişmez. Kullanıcının form alanları otomatik temizlenmez; kullanıcı F5 ile yenileyip güncel kaydı yeniden seçer ve yeniden silme onayı verir.

Geçerli sürümle silmede Genel personelin bağlı izinleri silinir, bakiye için tek Closure hareketi ve Person.Delete audit kaydı aynı transaction içinde yazılır. Ödeme geçmişi korunur. Audit veya veritabanı hatası olursa personel ve izin silme dahil tüm işlem geri alınır.

Eski eşzamanlı PersonnelStore.Delete yolu da sürümü zorunlu tutar; sürümsüz alternatif bırakılmadı. Bu değişiklik hesap silme veya izin düzenlemesine yeni bir sürüm kontrolü eklemez; bunlar analizdeki sonraki işlerdir.

## Şema ve dağıtım

Mevcut 101-personnel-rowversion yükseltmesindeki RecordVersion sütunları kullanılır. Yeni SQL betiği veya gerçek veritabanı yükseltmesi gerekmez.

Koruma güncellenen istemcide uygulanır. Eski v1.1.0 uygulama dosyaları bu düzeltmeyi içermez; tüm kullanılan uygulama örneklerinin yeni pakete geçirilmesi gerekir. İlk geliştirme sırasında açık uygulama veya masaüstü kısayolu değiştirilmedi; aşağıdaki uygulamalı denemede kullanıcı isteğiyle yeni paket açıldı ve Güncel kısayolu yönlendirildi. Yerel test paketi: `artifacts/releases/personnel-delete-version/`. GitHub'a push veya yeni Release yayımı bu adımda yapılmadı.

## Elle deneme

Gerçek kayıtlar yerine yalnızca bu deneme için oluşturulmuş personel kullanın. İki ayrı uygulama penceresi/oturumu açın.

1. A penceresinde deneme personelini seçin. B penceresinde aynı personelin adını veya IBAN'ını değiştirip kaydedin. A'da Sil'i onaylayın: işlem sürüm çatışmasıyla durmalı, B'nin değişikliği ve mevcut izinler korunmalı.
2. A eski seçimi korurken B'de kaydı silip aynı TC ile yeniden oluşturun. A'daki silme yeni personeli veya yeni izinlerini silmemeli.
3. A'da F5 ile güncel listeyi yükleyip deneme kaydını tekrar seçin. Silme onayından sonra yalnızca bu kayıt ve Genel grubundaysa izinleri silinmeli.
4. Aynı adımları Joker grubunda deneyin. Genel/Joker kapsamı karışmamalı.

## Otomatik doğrulama

Genel/Joker için eski sürüm, silip yeniden oluşturma, araya giren başka kullanıcı düzenlemesi/ödeme, geçerli silme, zaten silinmiş hedef, eşzamanlı düzenleme-silme ve audit hatasında geri alma senaryoları kalıcı regresyon testlerine eklendi. Eksik/bozuk sürümün veritabanından önce reddi ve eski eşzamanlı depo yolu da kapsanır. Son tam test sonucu ve kanıt klasörü CHANGELOG'da belirtilir. Testlerde ayrı geçici LocalDB motoru kullanılır; gerçek veritabanı kullanılmaz.

Son doğrulama: **414 başarılı, 0 başarısız, 0 atlanan**; Release 0 uyarı / 0 hata. Önceki 392 test korunarak 22 yeni test durumu eklendi. Kanıt: `artifacts/analysis/master-20261026-134118-955/master.trx`; test motoru temizliği başarılı.

## 26 Ekim 2026 uygulamalı ekran doğrulaması

Yerel paketin gerçek Genel/Joker ekranları, aynı DLL (SHA256: 73F8B0A3E1020C5E7D7F7CC55E5F9F4D9E1A8EC656E7BAFB09B813ED2D20D820) ve ayrı geçici LocalDB veritabanıyla açıldı. Her iki grupta eski satır seçildi; ikinci test oturumunun servisi kaydı silip aynı TC ile yeni kayıt oluşturdu. Gerçek Sil düğmesi ve onayı kullanıldığında sürüm çatışması görüldü. Yeni personel, Genel grubunun yeni izinleri, defter ve audit korunmuş olarak doğrulandı. F5 sonrası güncel satır seçilip Sil onaylandığında kayıt doğru silindi; tek Closure ve audit oluştu. **4 ekran kontrolü başarılı.** Gerçek personel kayıtları değiştirilmedi; test veritabanı ve motoru temizlendi. Kanıt: `artifacts/analysis/personnel-delete-ui/ui-results.json` ve `verification.json`.

Eski uygulama kapatıldı, güncel yerel paket açıldı. Masaüstündeki Muhasebe - Güncel kısayolu bu pakete yönlendirildi. Kullanıcının asıl uygulamadaki oturumu test sonunda korundu.
