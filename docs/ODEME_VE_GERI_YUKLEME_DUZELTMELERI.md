# Eski ödeme iptali ve geri yükleme kaynağı koruması

24 Ekim 2026. Bu değişiklikler mevcut veritabanı şemasını kullanır; yeni şema yükseltmesi gerektirmez.

## Eski ödeme iptali

Ödeme iptali, ödeme satırından sonra aynı TC ve personel grubuna ait bir `Closure` hareketi bulunuyorsa reddedilir. Böylece personel silinip aynı TC ile yeniden oluşturulduğunda önceki kaydın ödemesi yeni bakiyeye aktarılamaz. Kullanıcıya ödemenin kapanmış önceki personel kaydına ait olduğu açıklanır.

Kontrol, mevcut kişi → ödeme kilit sırası ve transaction içinde, bakiyeyi veya iptal durumunu değiştirmeden yapılır. Sıfır bakiyeli personelin silinmesi de kapanış hareketi yazdığı için korunur. Önceki kapanıştan sonra yeni kayıt üzerinde yapılan ödemeler iptal edilebilir; başka personelin kapanışı işlemi engellemez.

Bu koruma uygulamanın transaction içinde yazdığı kapanış geçmişini kullanır. Veritabanında uygulama dışında elle yapılan, kapanış hareketi bulunmayan silmeleri geriye dönük tanımlamaz. Kalıcı personel/çalışma dönemi kimliği ve arşiv modeli geliştirme analizindeki sonraki veri modeli adımıdır.

## Seçilen yedeğin korunması

Geri yükleme artık kaynak dosyayı, yedek başlığını ve dosya yapısını güvenlik yedeği oluşturmadan önce doğrular. Geçersiz kaynak mevcut yedekleri temizlemez ve veritabanını tek kullanıcı moduna almaz.

Doğrulanmış kaynak dosya, güvenlik yedeğinin saklama temizliğinden tam dosya yoluyla hariç tutulur. Kaynak ve güvenlik yedeği aynı klasördeyse, en eski kaynak da korunur. İlgili `.verified` işareti silinmez. Bunun için normal 14 dosyalık saklama sınırı o işlemde 15'e çıkabilir; sonraki normal yedeklemede olağan saklama politikası devam eder.

Doğrulama, güvenlik yedeği ve geri yükleme aynı süreç içi kilit altında yürür. Hata durumunda kilit bırakılır ve sonraki yedek alınabilir. Farklı uygulama süreçleri arasında genel bakım kilidi bu değişikliğin kapsamı değildir.

## Regresyon senaryoları

- Genel/Joker × kısmi/tam ödeme: silme ve yeniden kayıt sonrası eski ödeme iptali reddedilir; bakiye, defter ve audit değişmez.
- Genel/Joker: eski kayıt kapanmış olsa da yeni kaydın kendi ödemesi bir kez iptal edilebilir.
- Başka personelin kapanışı geçerli ödemenin iptalini engellemez.
- 14 yedek doluyken aynı klasördeki en eski yedekten gerçek izole geri yükleme tamamlanır; kaynak ve doğrulama işareti korunur, kaynak SHA-256 özeti değişmez.
- Geçersiz kaynakta güvenlik yedeği/temizleme yapılmaz, veriler korunur; ardından normal yedekleme çalışır.
- Dosya yolu normalizasyonu ve Windows büyük/küçük harf farkında korunan kaynak saklanır; diğer eski yedeklerin temizliği devam eder.

Hata öncesi hedefli doğrulamada 8 testten 6'sı başarısız, 2'si başarılı oldu. Düzeltme sonrası aynı 8 testin tamamı başarılıdır. Kanıt: `artifacts/analysis/payment-restore-safety/before-fix.trx` ve `after-fix.trx`.

Tam doğrulama: **270 başarılı, 0 başarısız, 0 atlanan**, 15/15 alan. Release derlemesi 0 hata ve 0 uyarıyla tamamlandı. Kanıt: `artifacts/analysis/master-20261024-172424-739/master.trx`, `summary.json` ve `RAPOR.md`. Önceki pakete göre 10 yeni regresyon senaryosu eklendi; geçici test motoru çalıştırma sonunda kaldırıldı.

Testler GUID adlı geçici LocalDB motorları ve geçici veritabanlarında yürütülür. Kullanıcının mevcut veritabanına geri yükleme yapılmaz.

## Yerel Release paketi

Yeni self-contained Windows paketi `artifacts/releases/payment-restore-safety` klasöründedir; uygulama ve veritabanı kurulum aracı birlikte yayımlandı. Paket uygulama DLL'i derleme çıktısıyla SHA-256 üzerinden eşleştirildi. Masaüstündeki `Muhasebe - Güncel.lnk` bu pakete yönlendirildi; önceki kısayol `artifacts/analysis/payment-restore-safety/shortcut-before.lnk` olarak saklandı. Kanıt: aynı klasördeki `release-verification.json`.

Açık uygulama kapatılmadı ve yeni bir uygulama başlatılmadı. Yeni sürüm, kullanıcı kısayolu yeniden açtığında çalışır. Bu çalışma GitHub'a push veya yeni GitHub Release yayını içermez.
