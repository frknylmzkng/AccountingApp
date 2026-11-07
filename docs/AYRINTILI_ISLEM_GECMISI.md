# Ayrıntılı işlem geçmişi

29 Ekim 2026 · Yerel geliştirme. Yayımlanmış v1.2.0 paketine dahil değildir.

**İzleyen canlı geçiş tamamlandı (29 Ekim 2026):** Kullanıcı onayıyla doğrulanmış yedek alındı ve yalnızca 109 uygulandı. İlk uygulamadaki SQL blok sırası hatası transaction'ı geri aldı; önceki verilerin değişmediği doğrulandı. Betiğe gerekli GO sınırları eklendi, paket yeniden derlendi ve yeni doğrulanmış yedek sonrası yükseltme tamamlandı. İş verileri ve önceki audit içeriği korundu; 12 yeni audit alanı, altı etkin trigger, iki güvenilir JSON kısıtı ve kalıcı hesap kimlikleri doğrulandı. Oturumlar yenilendi; Muhasebe - Güncel kısayolu 109 uyumlu pakete yönlendirildi. Test/ekran denemeleri yapılmadı; uygulama açılmadı. Özel kanıtlar ve yedekler `artifacts/analysis/live-upgrade-109/` altında Git dışında tutulur. Aşağıdaki dağıtım notu hazırlık aşamasının tarihsel durumudur.

## Kaydedilen bilgiler

109 yükseltmesi mevcut AuditLog tablosunu genişletir. Personel (Genel/Joker), izin, ödeme defteri ve hesap kayıtlarının ekleme/değiştirme/silme işlemleri her kayıt için ayrı saklanır:

- SQL sunucusunun ürettiği UTC zaman, işlemi yapan hesap adı ve uygulamadan doğrulanmış hesabın kalıcı AccountId kimliği.
- Kayıt türü, özgün kayıt anahtarı, varsa kalıcı PersonId bağlantısı ve Insert/Update/Delete işlemi.
- Önceki ve yeni alan değerlerinin JSON görüntüsü. Eklemede önceki, silmede yeni görüntü boştur.
- Gerekçe, gerekçenin sağlanıp sağlanmadığı, uygulama/doğrudan SQL kaynağı ve SQL oturum hesabı.
- Aynı uygulama transaction'ındaki kart, bakiye, defter ve özet olaylarını bağlayan CorrelationId.

Genel/Joker kartlarında ad, soyad, TC, IBAN, bakiye ve arşiv durumu; izinlerde tarihler ve personel bağlantısı; defterde tutar, tür, RequestId ve iptal zamanı; hesaplarda kullanıcı adı, rol, aktiflik, ek izinler ve parola yenileme durumu saklanır. Parola değişiminde yalnızca CredentialChanged işareti kaydedilir. Parola, parola hash'i, geçici parola ve oturum sırları JSON görüntülerine alınmaz.

Hesaplara değişmez AccountId eklenir. Aynı kullanıcı adı silinip tekrar oluşturulduğunda hedef hesap kimliği farklıdır; canlı yetki kontrolünden geçen olaylarda yapan hesabın kimliği de korunur. Yükseltme eski oturumları geçersiz kılar; güncel istemciyle yeniden giriş gerekir. Önceki audit kayıtlarının bilinmeyen hedef/değer/gerekçe/kimlik bilgileri sonradan uydurulmaz; alanları NULL kalır.

## Kullanım

Personel ekleme/düzenleme/arşivleme/etkinleştirme, izin planı, yeni ödeme/iptal, hesap ekleme/silme/aktiflik/izin ve parola işlemlerinde gerekçe penceresi açılır. 5–500 karakter açıklama girin. Vazgeç işlemi kaydı değiştirmez. Bu alana parola veya başka bir sır yazmayın.

Ödeme gerekçesi, ilk gönderimden önce mevcut şifreli bekleyen ödeme günlüğüne yazılır. Yeniden deneme aynı gerekçeyi kullanır; kaydedilmiş ödemenin tekrar gönderimi yeni bakiye hareketi veya ayrıntılı değişiklik olayı oluşturmaz. Önceden oluşturulmuş gerekçesiz günlükler kurtarılabilir; eksik gerekçe sonradan icat edilmez.

Yönetici hesabıyla **Rapor → İşlem geçmişi…** menüsünü açın. En yeni 200 kayıt listelenir. Bir satırı seçince eski/yeni JSON, yapan hesap kimliği, gerekçe ve işlem bağı alt bölümde gösterilir. Daha eski 200 kayıt düğmesiyle anahtar tabanlı sayfalama yapılır; en güncel kayıtlar düğmesi başa döner. Zamanlar açıkça UTC olarak gösterilir.

## Transaction ve erişim

Ayrıntılar SQL trigger'larıyla kaydın kendi transaction'ında yazılır. Audit yazımı başarısız olursa iş değişikliği de geri alınır. Çoklu satır değişikliklerinde her satır yakalanır. İzlenen alanlarda değişiklik olmayan UPDATE ayrıntılı olay üretmez. Eski özet olayları da korunur; bunlar tek başına bir kayıt değişikliği değildir.

Uygulama işleminde gerekçe ve ilişkilendirme kimliği bağlantıya atanır; hesap kimliği canlı yetki doğrulamasından sonra atanır. Bağlantı kapanırken bağlam temizlenir; temizleme başarısızsa bağlantı havuzdan çıkarılır. Farklı işlemlerin hesap/gerekçe bilgileri paylaşılmaz.

Geçmiş ekranı yalnızca yönetici içindir ve her sayfa okumada canlı oturum kontrolü yapılır. Kopyalama mevcut dışa aktarma yetkisini kullanır. JSON görüntüleri kişisel veri içerir; normal log dosyalarına dökülmez.

AuditLog üzerinde UPDATE/DELETE koruma trigger'ı vardır. Bu, SQL yöneticisini veya trigger kapatabilen kullanıcıyı engelleyen harici bir kanıt sistemi değildir. Doğrudan SQL/eski yardımcı yollardan yapılan değişikliklerde doğrulanmış uygulama aktörü yoksa SQL hesabı ve DirectSql kaynağı kaydedilir; eksik gerekçe NotProvided olarak açıkça belirtilir. Böyle bir olaya kullanıcı gerekçesi atfedilmez.

Bu adım seçilen beş iş tablosunu kapsar; dosya/ayar değişikliklerinin eski-yeni görüntüsü, başarısız girişlerin ayrıntılı güvenlik olayları ve harici değiştirilemez audit arşivi ayrı işlerdir. Yedekten geri yükleme, diğer verilerle birlikte audit'i de yedeğin zamanına döndürür; bu geçmiş harici bir kesintisiz kayıt deposu değildir.

## Dağıtım ve doğrulama durumu

109-detailed-audit.sql hazırdır. **Bu adımda canlı veritabanına uygulanmadı.** Canlı şema ve masaüstü kısayolu 108 uyumlu pakette kaldı. Yeni yerel paket artifacts/releases/detailed-audit/ altında hazırlanır; kontrollü yedek/yükseltme öncesinde açılmamalıdır, çünkü açılış hazırlığı bekleyen şemayı uygular.

Kullanıcı isteğiyle testler, ekran denemeleri ve SQL betiğini çalıştıran denemeler atlandı. Çözümün Release derlemesi 0 uyarı / 0 hata ile tamamlandı. Eski/yeni değer, gerekçe, kimlik, audit hatasında geri alım, sırların dışarıda kalması, geçmiş koruması ve ödeme tekrarında çoğalmama için regresyon kaynakları eklendi; çalıştırılmadı. Derleme SQL trigger'larının çalışma doğrulaması değildir.
