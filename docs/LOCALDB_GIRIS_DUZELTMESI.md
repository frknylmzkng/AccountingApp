# LocalDB giriş başlangıcı düzeltmesi

Kullanıcının G-E299909F hata kodu, parola kontrolünden önce giriş hazırlığında oluştu. Günlükte SQL -1983577846 (0x89C5010A), Windows olay kaydında LocalDB başlatma hatası görüldü. Önceki paket testi çalışan bir motorla yapıldığı için masaüstündeki bu başlangıç hatasını yakalamamıştı.

Giriş hazırlığı artık ilk SQL bağlantısından önce Microsoft tarafından kurulmuş SqlLocalDB.exe aracını `start <instance>` komutuyla çağırır. Araç yalnızca Program Files altındaki SQL Server kurulumundan bulunur; uygulama klasörü ve PATH kullanılmaz. Shell kapalıdır, örnek adı ayrı argümandır, pencere gösterilmez ve zaman aşımı vardır. SQL Express ve paylaşılan LocalDB aliasları için özel motor başlatılmaz. Başlatma hataları anlaşılır ve hassas veri içermeyen mesajlarla gösterilir.

Bu akış stop/delete/create/restore komutlarını kullanmaz; mevcut veri, kullanıcı hesabı ve parola değiştirilmez. Soğuk başlangıç testi için motor, başka oturum yokken ayrıca güvenli biçimde durduruldu. Yeni uygulama masaüstünün Explorer nesnesi üzerinden açıldı; başlatan sürecin explorer.exe olduğu ve giriş hazırlığının ready durumuna geldiği doğrulandı. Parola girme veya oturum açma otomatikleştirilmedi.

Kanıtlar:

- `artifacts/analysis/localdb-desktop-cold-start.json`: motor kapalı, gerçek Explorer oturumu, başarılı giriş hazırlığı.
- `artifacts/analysis/test-results/localdb-startup-fix.trx`: 192 veritabanısız test başarılı; 7 yeni başlangıç testi dahil.
- Yeni paket: `artifacts/releases/localdb-startup-fix` (.NET 10.0.13 self-contained).
- Masaüstündeki Muhasebe - Güncel kısayolu yeni pakete yönlendirildi. Yeni giriş penceresi kullanıcı için açık bırakıldı.

Microsoft LocalDB başlatma ve yönetim açıklaması: https://github.com/MicrosoftDocs/sql-docs/blob/live/docs/connect/ado-net/sql/sqlclient-support-localdb.md

Tam testler artık her çalıştırmada AccountingAppTestEngine_GUID adlı ayrı bir LocalDB motoru oluşturur; testler bitince yalnızca bu motor durdurulup kaldırılır. Bu sayede test/komut oturumuyla masaüstündeki MSSQLLocalDB motoru karışmaz ve açık kullanıcı oturumu etkilenmez.

Son tam doğrulama: 260 başarılı, 0 başarısız, 0 atlanan; 15/15 alan. Kanıt: artifacts/analysis/master-20261024-103611-093/master.trx ve RAPOR.md. Test motoru çalıştırma sonunda güvenle durduruldu ve kaldırıldı. Yeni normal masaüstü uygulamasının Dashboard penceresine geçtiği de salt okunur süreç bilgisiyle gözlendi.
