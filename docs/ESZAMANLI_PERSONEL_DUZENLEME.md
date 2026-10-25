# Eşzamanlı personel düzenleme koruması

25 Ekim 2026.

## Davranış

Genel ve Joker personel kayıtlarında SQL Server'ın otomatik ürettiği sekiz baytlık `RecordVersion` (`rowversion`) kullanılır. Liste açıldığındaki sürüm, seçilen satırın özgün verisinden alınır. Güncelleme, hem TC hem bu sürüm eşleşiyorsa yapılır. Ad, soyad, IBAN veya bakiye değişmişse eski ekrandan gönderilen kayıt reddedilir. Alan sonradan eski değerine dönse bile sürüm yenilenir; yalnızca bakiye karşılaştırmasına dayanan eski koruma artık kullanılmaz.

Kontrol SQL `UPDATE` koşulunun parçasıdır. İki kullanıcı aynı sürümle aynı anda kaydettiğinde yalnızca birisi başarılı olur. Eski sürümle başarısız işlem personel verisini, bakiyeyi, ödeme defterini ve audit kayıtlarını değiştirmez. Kayıt silinip aynı TC ile tamamen aynı bilgilerle yeniden oluşturulsa da eski sürüm geçerli olmaz.

Çakışma mesajı kaydın başka bir işlem tarafından değiştirildiğini açıklar. Başarısız işlem form alanlarını temizlemez. Kullanıcı kendi girişlerini kontrol edip F5 ile listeyi yenileyebilir, güncel kaydı yeniden seçerek değişikliklerini yeniden uygulayabilir. Otomatik birleştirme veya zorla üzerine yazma yapılmaz. Güncel kayıt seçildiğinde alanların güncel değerlerle doldurulması mevcut davranıştır.

Güncelleme için sürüm zorunludur. Eksik/bozuk sürüm, veritabanına yazma yapılmadan reddedilir. Eski eşzamanlı `PersonnelStore.Update` yolu da sürüm koşulunu zorunlu tutar. Çalışan bütün uygulama örneklerinin bu güncel sürüme geçirilmesi gerekir; önceden açık eski uygulamalar yeni istemci kontrolünü uygulamaz.

## Taslaklar ve raporlar

Seçilmiş kayda ait şifreli taslak, özgün kayıt sürümünü de saklar. Taslak sürümü listede bulunan kayıtla eşleşmiyorsa taslak alanları yüklenir, ancak mevcut personel seçilmez. Böylece eski taslak içeriği yeni sürümle eşleştirilip başkasının değişikliklerinin üzerine yazılamaz. Sürüm bilgisi içermeyen eski taslaklar da otomatik düzenleme hedefi oluşturmaz. Aynı sürümle eşleşen taslaklar normal biçimde kullanılabilir; veritabanı daha sonra değişmişse son SQL kontrolü yine kaydı korur.

Sürüm alanı listelerde ve kolon seçim menüsünde gösterilmez. Kaydedilmiş kolon tercihleri bu alanı açamaz. Excel/CSV aktarımı hem ekran görüntüsünden hem doğrudan veri tablosundan yapıldığında bu iç alanı dışarı vermez. Çoklu izin satırlarında personel sürümü aynıdır; izin geçmişi güncelleme işleminden etkilenmez.

## Şema geçişi

Yeni `database/upgrades/101-personnel-rowversion.sql` betiği yalnızca iki personel tablosuna otomatik sürüm sütunu ekler. Daha önce uygulanmış `001-initial.sql` ve `100-roles-audit-ledger.sql` değiştirilmedi; mevcut şema hash'leri korunur.

Uygulamanın ilk giriş hazırlığı veya kurulum aracının `--upgrade` işlemi mevcut yükseltme akışı üzerinden önce `COPY_ONLY/CHECKSUM` yedeği alır ve `RESTORE VERIFYONLY` ile doğrular. Ardından ek sütunları transaction içinde uygular. Mevcut personel, izin, bakiye, hesap ve hareket kayıtları korunur. Tekrar açılışta uygulanmış betik yeniden çalıştırılmaz. Uyumsuz aynı adlı sütun varsa yükseltme durur; kısmi DDL değişiklikleri geri alınır.

Eski şemalı bir yedek geri yüklendiğinde takip eden şema yükseltmesinin yedeği de kullanıcının seçtiği güvenlik klasörüne alınır. Geri yüklemenin kaynak dosyası bu aşamadaki saklama temizliğinde de korunur; önceki seçili yedek koruması sürer.

Bu çalışma sırasında üretim veritabanına bağlanılıp yükseltme uygulanmadı. Mevcut veri üzerinde geçiş, yeni sürüm ilk kez giriş hazırlığı yaptığında yedekli akışla gerçekleşir.

## Doğrulama

- Genel/Joker için ad veya IBAN değişikliği sonrasında eski sürümle kaydın reddedilmesi; kazanan kullanıcının verilerinin, bakiye ve defterin korunması.
- İki eşzamanlı düzenlemede tek kazanan ve tek güncelleme audit kaydı.
- Aynı TC ile yeniden oluşturulan kaydın eski sürümle değiştirilememesi.
- Güncel sürümle normal düzenleme ve çoklu izin kayıtlarının korunması.
- Eksik/bozuk sürümün veritabanına erişmeden reddi; özgün sürümün ve döndürülen byte dizisinin korunması.
- Eski, bozuk veya farklı kayda ait taslağın güncel kayda bağlanamaması; sıralama sonrası doğru taslağın doğru kişiyi seçmesi.
- Sürümün liste, tercihler, Excel ve CSV'de gizlenmesi.
- Mevcut verili şemaya yedekli geçiş, yeniden çalıştırmanın veriyi/sürümü değiştirmemesi, hatalı geçişte DDL rollback.
- En eski yedekten eski şemayı geri yükleme ve ardından sürüm yükseltmesi sırasında kaynak dosyanın korunması.

Son tam doğrulama: **316 başarılı, 0 başarısız, 0 atlanan**, 15/15 alan. Önceki 292 teste 24 senaryo eklendi. Release derlemesi 0 hata ve 0 uyarıyla tamamlandı. Kanıt: `artifacts/analysis/master-20261025-072545-740/master.trx`, `summary.json`, `RAPOR.md`. Testler GUID adlı geçici LocalDB motorlarında yürütüldü; test motoru kaldırıldı.

## Yerel paket

Uygulama ve kurulum aracı `artifacts/releases/personnel-concurrency` klasörüne self-contained olarak yayımlandı. Uygulama DLL'i ve yeni yükseltme betiği kaynak/derleme çıktısıyla SHA-256 üzerinden eşleştirildi; paket kurulum aracının `--help` çalışması doğrulandı. Masaüstündeki `Muhasebe - Güncel.lnk` yeni pakete yönlendirildi; önceki kısayol `artifacts/analysis/personnel-concurrency/shortcut-before.lnk` olarak saklandı. Kanıt: aynı klasördeki `release-verification.json`.

Açık uygulama kapatılmadı ve yeni uygulama başlatılmadı. Güncel sürümü kullanmak için eski açık kopyalar kapatılıp kısayol yeniden açılmalı. Bu çalışma GitHub'a push veya yeni GitHub Release yayını içermez.
