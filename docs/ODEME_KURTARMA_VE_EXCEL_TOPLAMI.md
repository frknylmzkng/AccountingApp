# Belirsiz ödeme sonucunun kurtarılması ve Excel toplamı

25 Ekim 2026. Mevcut şema kullanılır; yeni veritabanı yükseltmesi gerekmez.

## Ödeme akışı

Ödeme ekranı artık işlem kimliğini her açılışta yenilemez. Personel, grup, tutar, işlem kimliği ve personelin son kapanış hareketi; bakiyeyi değiştiren komut gönderilmeden önce Windows kullanıcısına bağlı DPAPI ile şifrelenip atomik olarak saklanır. Geçici dosya diske flush edilir, ardından yerine taşınır. Saklama başarısız olursa ödeme gönderilmez.

Kayıt `%LOCALAPPDATA%\AccountingApp\payments` altındadır. Parola veya IBAN içermez; dosya adında kullanıcı adı veya TC yer almaz. Aynı Windows kullanıcısı ve veritabanı için ekranlar, uygulama yeniden başlatmaları ve farklı uygulama hesapları aynı bekleyen isteği görür. Bağlantı zaman aşımı gibi seçenekleri değiştirmek yeni bir kayıt alanı oluşturmaz. Eşzamanlı uygulama örnekleri dosya kilidiyle koordine edilir.

Ekran açıldığında bekleyen istek için ödeme defteri sorgulanır:

- **Kaydedilmiş:** Yeniden ödeme gönderilmez; kaydedilen sonuç gösterilir.
- **İptal edilmiş:** Ödemenin daha önce kaydedilip iptal edildiği gösterilir; yeniden ödeme yapılmaz.
- **Henüz doğrulanmamış:** Aynı kimlik, personel ve tutarla yeniden denenebilir. Yeni istek oluşturulmaz. Sunucu mevcut transaction ve tekil işlem kimliği korumasını uygular.
- **Kesin reddedilmiş:** Örneğin bakiye azalmış veya personel kaydı kapanmışsa, bakiye değiştirilmeden dönen ret sonucu saklanır. Bu sonuç onaylanıp farklı bir ödeme başlatılabilir.

Bağlantı, sorgu, commit yanıtı veya iptal hatası kesin ret sayılmaz. Bu durumlarda bekleyen dosya korunur. Dosya bozuksa veya okunamıyorsa yeni işlemle üzerine yazılmaz. Sonuç doğrulanmadan bekleyen istek temizlenemez.

Başarılı yanıt bile dosyayı otomatik silmez. Kullanıcı **Sonucu onayla / yeni ödeme** düğmesiyle sonucu kabul ettikten sonra sıradaki ödemeye geçer. Böylece kaydın yapılması ile kullanıcının sonucu görmesi arasındaki kapanma da kapsanır. **Ödeme sonucunu kontrol et** yalnızca sorgular; **Bekleyen ödemeyi yeniden dene** önce sonucu kontrol eder, gerekirse aynı isteği gönderir. Bekleyen isteğin personeli ve tutarı değiştirilemez.

Hazırlanmış isteğin son kapanış kimliği gönderim sırasında yeniden karşılaştırılır. İstekten sonra personel silinip aynı TC ile yeniden oluşturulmuşsa, eski istek yeni kaydın bakiyesinden ödeme yapamaz. Önceki sürümdeki eski ödeme iptali koruması da korunur.

İşlem sürerken araç çubuğu, gezinme, çıkış ve normal pencere kapatma engellenir. Zorla sonlandırma veya bağlantı kaybında diskteki kayıt üzerinden kurtarma yapılır. Kayıtların kullanıcı tarafından elle silinmesi, başka Windows hesabına geçilmesi veya başka bilgisayardaki bağımsız işlemler, aynı kullanıcı niyeti olarak otomatik eşleştirilemez; sunucu aynı işlem kimliğine karşı tekillik korumasını sürdürür.

## Excel toplamı

İzinlerle birleşen listede bir personelin birden fazla satırı bulunabilir. İzin satırları dışa aktarımda korunur; `PARA` toplamı TC ve grup bazında tekilleştirilir. Eşit bakiyeli iki farklı personel ayrı ayrı toplanır. Aynı TC'nin farklı gruplardaki eski kayıtları da ayrı sayılır.

Kolon başlıkları değiştirilse veya TC/grup kolonları gizlense bile kaynak alan bilgisi kullanılır. Gizli TC/grup alanları Excel dosyasına eklenmez. Yinelenen bakiye bulunan raporlarda ayrı hesaplama sayfası yalnızca rapor anındaki tekil bakiye tutarlarını saklar; toplam bu değerlerden hesaplanır. Böylece Excel'de izin satırlarını sonradan sıralamak toplamı değiştirmez. Toplam hücresindeki açıklama bunun rapor anındaki personel toplamı olduğunu belirtir.

Diğer decimal kolonların normal toplamı ve CSV satırları değişmez. Aynı personel için farklı bakiye değerleri bulunursa yanlış toplam yayımlamak yerine rapor reddedilir ve listenin yenilenmesi istenir.

## Doğrulanan senaryolar

- Commit başarılı olduktan sonra yanıt kaybı, yeniden ekran açma ve tekrar deneme: tek ödeme, tek bakiye azaltımı ve tek audit kaydı.
- Commit öncesi kesinti: aynı kimlik ve parametrelerle kurtarma; belirsiz sonuç erken onaylanamaz.
- Başarılı yanıt sonrası onay öncesi kapanma; onaydan sonra aynı tutarda bilinçli yeni ödeme.
- Eşzamanlı iki başlangıç, şifreli dosyanın bozulması veya yazılamaması ve sorgu bağlantısının kesilmesi.
- Bekleyen istekten sonra personelin yeniden oluşturulması veya bakiyenin azalması; kesin ret sonrası yeni ödeme yapılabilmesi.
- Sunucuda eşzamanlı tekrarlar ve daha önce iptal edilmiş ödemenin tanınması.
- İşlem sırasında araç çubuğu, gezinme, çıkış ve pencere kapatma koruması.
- Gerçek SQL çoklu izin çıktısı; eşit tutarlı farklı personeller; gizli TC ve yerelleştirilmiş başlıklar; Excel'de sonradan sıralama; boş/genel raporlar.

Testler geçici klasörlerde ve GUID adlı izole LocalDB motorlarında yürütülür. Üretim veritabanına ödeme veya geri yükleme yapılmaz.

Son tam doğrulama: **292 başarılı, 0 başarısız, 0 atlanan**, 15/15 alan. Önceki 270 teste 22 senaryo eklendi. Release derlemesi 0 hata ve 0 uyarıyla tamamlandı. Kanıt: `artifacts/analysis/master-20261025-071235-847/master.trx`, `summary.json` ve `RAPOR.md`. Geçici test motoru kaldırıldı.

Excel sıralama regresyonu ek korumadan önce başarısız oldu: beklenen 3.000 TL yerine 2.000 TL hesaplanıyordu. Kanıt: `artifacts/analysis/payment-recovery-and-excel/sort-before.trx`. Son tam pakette bu test de başarılıdır.

## Yerel paket

Self-contained Windows uygulaması ve kurulum aracı `artifacts/releases/payment-recovery-excel` klasörüne yayımlandı. Uygulama DLL'i derleme çıktısıyla SHA-256 üzerinden eşleştirildi; gerekli runtime ve şifreleme dosyaları doğrulandı. `Muhasebe - Güncel.lnk` bu sürüme yönlendirildi; eski kısayol `artifacts/analysis/payment-recovery-and-excel/shortcut-before.lnk` olarak korundu. Ayrıntılar aynı klasördeki `release-verification.json` dosyasındadır.

Açık uygulama kapatılmadı ve yeni uygulama başlatılmadı. Kısayol yeniden açıldığında güncel sürüm çalışır. Bu çalışma GitHub'a push veya yeni GitHub Release yayını içermez.
