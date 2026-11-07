# Personelin çalışma dönemleri

**İzleyen kaynak dilimi:** [Proje sorumlusu ve proje bazlı yetkiler](PROJE_SORUMLUSU_VE_YETKILER.md), 1.3.3-dev / şema 115 / API 1.3 olarak hazırlandı; henüz kurulmadı. Aşağıdaki önceki kapsam açıklamaları tarihsel teslimleri anlatır.

**İzleyen kurulum:** Kullanıcı onayıyla 31 Ekim 2026'da [114 yükseltmesi ve 1.3.2-dev istemci/API kurulumu](1_3_2_DEV_KURULUMU.md) tamamlandı. Aşağıdaki uygulanmamış şema/korunan 1.3.1-dev ifadeleri kaynak tesliminin tarihsel kapsamıdır. İşlev denemeleri kurulumda da atlandı.

31 Ekim 2026 · Yol haritası Aşama 2 · Kaynak/paket **1.3.2-dev**, şema **114**, API sözleşmesi **1.2** (`/api/v1`).

Personel kimliği (`PersonId`) aynı kalır; yeniden işe giriş ayrı `EmploymentId` oluşturur. Yeni dönem başlangıç tarihiyle açılır. Mevcut Genel/Joker grubu korunur; gruplar arasında geçiş bu dilimde yoktur.

## Kullanım

Genel veya Joker Personel ekranında yeni kart eklerken işe giriş tarihi istenir. Arşivleme, ayrılış tarihini ve kart sürümünü denetleyerek güncel dönemi kapatır. Kalan borç veya alacak silinmez. Dönemi aşan izin, iptal edilmemiş proje ataması ya da iptal edilmemiş hakediş varsa önce ilgili kayıt düzeltilmelidir. Açık uçlu atamalar ayrılıştan önce gerekçeli iptal edilmelidir; otomatik tarih değiştirilmez.

**Personel arşivi ve geçmişi → bir kart seç → Çalışma dönemleri…** üzerinden bütün dönemler görülebilir. İşe giriş/ayrılış kutuları kartın güncel dönemini gösterir. Tarih düzeltmesi gerekçe, personel sürümü ve dönem sürümüyle kaydedilir. Başka kullanıcı arada kartı veya dönemi değiştirdiyse kayıt reddedilir; arşiv listesini yenileyip tekrar açın. Kaydetme sonrasında pencere kapanır ve arşiv listesi yenilenir. Önceki dönemlerin tarihleri ve kimlikleri dondurulur.

**Arşivlemeyi geri al (aynı dönem)** yanlışlıkla yapılan arşivlemeyi geri alır. Aynı EmploymentId, tarihler, izinler ve bakiye korunur. Ayrılış tarihi kendiliğinden silinmez. Gerekiyorsa güncel dönemin tarihleri ayrıca gerekçeyle düzeltilir. Bu işlem yeniden işe giriş değildir.

**Yeni çalışma dönemiyle işe başlat…** yalnız arşivlenmiş kartta kullanılabilir. Eski bakiyenin **tam sıfır** olması gerekir. Bakiye varsa arşivlemeyi aynı dönemde geri alın, mevcut yetkili finans/ödeme işlemleriyle eski dönemin hesabını tamamlayın ve tekrar arşivleyin. Yeni işe giriş, önceki bilinen ayrılış/izin/atama/hakediş tarihlerinden sonra olmalıdır. Yeni döneme borç aktarma veya otomatik bakiye sıfırlama yapılmaz. Yanıt belirsiz kalırsa listeyi yenileyin: eski kart sürümüyle aynı isteği tekrarlamak ikinci dönem açamaz.

Okuma mevcut Read yetkisini; tarih düzeltme, arşivleme ve işe giriş mevcut personel düzenleme yetkisini kullanır. Her yazmada canlı oturum/yetki kontrolü transaction içinde sürer. Finans işlemlerinin ayrı ödeme/yönetici ve dışa aktarma yetkileri korunur. Bu modül proje kapsamlı yetki ayrımı eklemez.

## Eski kayıtların aktarımı

114, her mevcut kart için **tek “eski aktarım” dönemi** oluşturur. Önceki işe giriş, ayrılış, kayıt zamanı ve kayıt sahibi bilinmediğinden boş bırakılır; arşiv zamanı işe giriş/ayrılış tarihi sayılmaz. Önceden aynı kartla çalışılmış birden çok dönemi geriye dönük tahmin etmez. Eski dönemin bilinmeyen tarihleri gerekçeli tamamlanabilir; yalnız bu aktarım döneminde tarih boş bırakılabilir.

Mevcut izin satırlarına dönem kimliği eklenir. Ödeme defteri, hakediş, proje ataması ve izin değişikliği gövdeleri yeniden yazılmaz; `EmploymentRecordLinks` eklemeli bağlantı tablosu geçmiş kayıtları döneme bağlar. Bağlantılar güncellenemez veya silinemez. Önceden PersonId belirlenemeyen eski ödeme satırları bilinmeyen geçmiş olarak korunur; yeni kimlik uydurulmaz. Bu nedenle aktarılan dönemin defter hareketleri toplamı, kayıp eski hareketlerden dolayı kart bakiyesini tek başına kanıtlayan bir açılış bilançosu değildir.

Yeni izin, atama ve hakediş güncel dönemin aralığına uymalıdır. Yeni dönem açılınca eski izin satırları listeden/özetten ayrılır; arşiv geçmişinde dönem kimliğiyle görünür. Güncel izin silinmesi mevcut 107 geçmiş kaydını ve dönem bağlantısını üretir; önceki/arşivli dönemin izinleri değiştirilemez veya silinemez. İzin geçmişi ve dönem bağlantıları kalıcıdır.

Hakediş iptali, belgenin güncel döneme ait olduğunu doğrulamadan bakiyeyi değiştirmez. Eski ödeme iptalinin kapanış sınırı, RequestId ile aynı ödeme sonucunu bulma, personel sürümü ve tekrar ödeme günlüğü korunur. Tarih düzeltmesi ve yeniden işe giriş kart sürümünü de yeniler: eski ekranın yeni bir ödeme/izin/düzenleme yazması reddedilir. Daha önce kaydedilmiş ödeme isteğinin sonucunu sorgulamak yeni borç hareketi üretmez.

Personel ödeme geçmişi ve genel defterde dönem kimliği görünür; birikimli defter tutarı dönem bazında hesaplanır. Proje finans/atama listeleri de dönem kimliğini taşır. Proje maliyet raporları tüm dönemlerin gerçek hareketlerini toplamaya devam eder; yeni döneme geçiş geçmiş proje maliyetini ortadan kaldırmaz. Eski kalıcı günlük kapanışlar değiştirilmez.

## Kurulum ve doğrulama durumu

**114 hazırlanmıştır, canlı veritabanına uygulanmadı.** Bu bilgisayarda kullanıcı tarafından düzgün çalıştığı bildirilen **1.3.1-dev / 113** kurulumu, API görevi, bağlantı ayarları ve masaüstü kısayolu korundu. Yeni 1.3.2-dev istemci ve API paketleri kaynak ağacının geliştirme çıktılarıdır; GitHub Release değildir.

Geçişte doğrulanmış yedek alınmalı, istemciler ve API kapatılmalı, 114 transaction içinde uygulanmalı, istemci ve API birlikte güncellenmelidir. Yeni dönem satırları ve mevcut izin/kart rowversion değerleri oluşur; oturum nesilleri yenilenir. Mevcut başlatıcının hazır olma kontrolündeki eski **1.1** beklentisi de yeni paketin `apiVersion: 1.2` manifestiyle birlikte güncellenmelidir. Eski istemci/API ile karışık kurulum desteklenmez. Bu teslimde bu bakım işlemleri çalıştırılmadı.

İstek doğrultusunda testler, SQL/API/UI ve eşzamanlılık denemeleri çalıştırılmadı. Mevcut test verisi çağrılarına açık çalışma tarihleri ve izole veritabanı temizliğine yeni tablolar eklendi; bunlar çalıştırılmış test sonucu değildir. Release derlemesi ve paket üretimi SQL betiğinin veya çalışma zamanı davranışının doğrulandığı anlamına gelmez.

Aşama 2'de açık kalan işler: proje sorumlusu/proje kapsamlı yetkiler, bütünleşik personel kartı ve tarihli grup değişimi. Vardiya, puantaj, bordro ve ücret politikası sonraki dilimlerdir.
