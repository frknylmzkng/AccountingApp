# Proje, ekip ve tarihli personel atamaları

**İzleyen kaynak dilimi:** [Proje sorumlusu ve proje bazlı yetkiler](PROJE_SORUMLUSU_VE_YETKILER.md), 1.3.3-dev / şema 115 / API 1.3 olarak hazırlandı; henüz kurulmadı. Aşağıdaki önceki kapsam açıklamaları tarihsel teslimleri anlatır.

**31 Ekim 2026 izleyen kaynak güncellemesi:** [Çalışma dönemleri](CALISMA_DONEMLERI.md), 1.3.2-dev / şema 114 / API 1.2 olarak hazırlandı. Aşağıdaki önceki sürüm/kapsam ifadeleri tarihsel teslimi anlatır; yeni sürüm henüz kurulmadı.

**İzleyen kurulum:** 31 Ekim 2026'da kullanıcı onayıyla [113 yükseltmesi ve 1.3.1-dev istemci/API kurulumu](1_3_1_DEV_KURULUMU.md) tamamlandı. Aşağıdaki uygulanmamış şema/korunan 1.3.0-dev ifadeleri önceki kaynak tesliminin tarihsel kapsamıdır. İşlev denemeleri kurulum sırasında da atlandı.

31 Ekim 2026 · Yol haritası Aşama 2'nin proje/ekip temeli · Kaynak sürümü **1.3.1-dev**, gerekli şema **113**.

F01–F14 sonrasında proje bazlı ekip takibinin ilk bağımsız iş dilimi eklendi. Mevcut kalıcı PersonId ve FinanceProjects kimlikleri kullanılır. Bu çalışma Aşama 2'nin tamamı, yeni Employment/işe giriş dönemi modeli veya vardiya modülü değildir.

## Kapsam

**İşlemler → Projeler, ekipler ve tarihli atamalar** ekranı yerel ve merkezi API modunda aynı servis kurallarını kullanır. Yeni proje kod/ad ile oluşturulur; listeden **Projeyi aç** seçildikten sonra ad, saha, isteğe bağlı müşteri etiketi, isteğe bağlı plan başlangıç/bitiş tarihleri ve etkinlik düzenlenir. Kod/kalıcı proje kimliği korunur. Müşteri etiketi cari hesap oluşturmaz.

Proje bazında benzersiz ekip kodu, ekip adı ve etkinlik durumu tutulur. Ekip kodu ve bağlı proje değiştirilemez; ekip ve proje fiziksel silinmez. Güncel/gelecek atamaları olan ekip veya proje pasife alınamaz; önce ilgili atamalar gerekçeli olarak düzenlenir. Proje aralığı, mevcut iptal edilmemiş atamaları dışarıda bırakacak biçimde daraltılamaz. Geçmiş sonlu atamaları olan proje/ekip pasife alınabilir; kayıtlar silinmez.

Atamalar kalıcı PersonId'ye bağlıdır. Personel arama/seçim penceresi mevcut sayfalı listeyi kullanır. Bir proje ve isteğe bağlı bir ekip için başlangıç ve isteğe bağlı bitiş günü seçilir. **Bitiş günü dahildir**. Boş bitiş açık uçludur; kapatılana/iptal edilene kadar başka asıl atamayla çakışır. Bu model **asıl proje/ekip üyeliğidir**; gün içinde farklı projelere saat dağıtımı değildir. Aynı günün iki farklı projeye asıl üyelik olarak atanması bu ilk sürümde reddedilir.

Yeni atama, bilinen proje plan aralığı içinde kalmalıdır. Proje veya ekip pasifse, ekip başka projeye aitse, personel arşivlenmişse veya seçim anından sonra personel/proje/ekip sürümü değişmişse kayıt reddedilir. Personel adı/IBAN değişikliği bile rowversion'ı değiştirdiğinden eski personel seçimi yenilenmelidir. Ekip değişiklikleri de atama öncesinde özgün ekip sürümüyle karşılaştırılır.

Asıl atamanın tarihleri ve hedefi yerinde değiştirilmez. Düzeltme, eski atamanın gerekçeli iptali ve yeni atama oluşturulmasıyla yapılır; önceki kayıt görünür kalır. İptal için özgün atama sürümü gerekir. SQL koruması silmeyi, sonradan hedef/tarih değiştirmeyi ve iptali geri almayı reddeder. İptal sonrası başka atama oluşturulması iki ayrı açık işlemdir; tek bir 'transfer' transaction'ı bu aşamada yoktur. Kullanıcı bunların arasında işlemi bırakırsa üyelik boşluğu oluşabilir.

## Eşzamanlılık, yetki ve audit

Atama sırasında PersonId başına transaction kilidi (`sp_getapplock`), proje/personel/ekip canlı kontrolleri ve Serializable SQL transaction birlikte kullanılır. Aynı kişinin farklı projelere çakışan iki isteği paralel gelse bile servis ikinci kaydı reddetmek üzere tasarlanmıştır. Tarih çakışması bütün projelerde ve ekiplerde kontrol edilir; yalnız açık ekranın satırlarına bakılmaz. Tarihler saat/saat dilimi içermeyen iş günleridir.

Yeni modülün yazma/iptal işlemleri **yöneticiye** açıktır; sunucu rolü her transaction'da canlı doğrular. Okuma mevcut uygulamadaki Read iznini kullanır. Dışa aktarma/pano için mevcut ayrı izin kontrolleri korunur. Bu, proje bazlı erişim sınırlaması değildir: Read yetkisi olan kullanıcılar bu aşamada tüm projeleri görebilir. Proje sorumlusu veya Proje A/Proje B amiri ayrımı henüz uygulanmadı.

Proje/ekip/atama değişikliklerinin eski-yeni değerleri, kalıcı kayıt kimliği, aktör/hesap kimliği, UTC zaman, gerekçe, kaynak ve transaction korelasyonu audit'e aynı transaction'da yazılır. Ekip/proje adları tarihsel atama gridinde güncel adlarıyla gösterilir; önceki ad audit'te bulunur. Atama kalıcı personel kimliğine bağlı olsa da ad/soyad da gridde mevcut karttan gösterilir. Bordro niteliğinde tarihsel personel kartı görüntüsü olduğu iddia edilmez.

Personel arşivleme mevcut atamaları otomatik iptal etmez; geçmiş korunur. Yeni atama arşivli karta yapılamaz. Arşiv/yeniden etkinleştirmede açık gelecek atamaların yönetici tarafından ayrıca ele alınması gerekir. İşe giriş/ayrılış ve ayrı EmploymentId henüz bu modele bağlanmadı.

## Finans ve eski veri

113, FinanceProjects'e nullable saha/müşteri etiketi/plan tarihleri ekler; yeni ProjectTeams ve PersonnelProjectAssignments tablolarını oluşturur. Eski projelere tarih veya personel ataması uydurulmaz. Yeni tablolar boş başlar. Eski hakediş, avans, ödeme, bakiye ve kalıcı kapanışlar yeniden sınıflandırılmaz. Plan tarihleri bu aşamada atama doğrulamasındadır; mevcut finans belgelerinin tarihlerini yeniden yorumlamaz veya otomatik maliyet/hakediş üretmez.

Ekip ve atama FK'leri ile proje-ekip tutarlılığı korunur. Personel sürümü üzerinden TC+grup+PersonId eşleşmesi yeniden doğrulanır. Atama kaydında para, IBAN veya ücret yoktur. Önceki ödeme günlüğü ve RequestId tekrar koruması değiştirilmedi. Yeni atama yazmasında ayrı bir istemci RequestId günlüğü yoktur; yanıt belirsizse liste/audit yenilenir. Aynı aralıkta yeniden tıklamak çakışma kontrolüne takılır; kör otomatik tekrar yapılmaz.

## Ekran ve sınırlar

Proje/ekip listeleri kod sırasıyla ilk 500 kaydı gösterir; atama listesi 1–200 boyutlu SQL sayfalaması kullanır ve iptal edilmiş kayıtları da içerir. Çok büyük proje kataloğu için aramalı sayfalama sonraki genişletmedir. Yeni proje oluşturulduktan sonra ayrıntıları düzenlemek için listeden açılır. Projeyi açma ve ekip/personel seçimi açık düğmelerle yapılır; yalnız gridde farklı satıra tıklamak işlem hedefini değiştirmez. Kaydedilmemiş değişiklik uyarıları ve boş bırakılabilir tarih kutularının null/tarih ayrımı korunur.

Henüz eklenmeyenler: bağımsız Employment dönemleri, sorumlu hesap ataması/proje kapsamlı izinler, vardiya şablonu/takvimi, ekip kapasitesi, izinle çakışma, puantaj/onay, saat bazlı çok proje dağıtımı ve tarihli ücret politikası. Personel çalışma dönemleri ile proje erişim ayrımı Aşama 2'de açık kalır; bu ilk dilim tüm yol haritasının tamamlandığı anlamına gelmez.

## Uygulama ve doğrulama durumu

Kaynak geliştirme paketleri **1.3.1-dev** olarak hazırlanır; bu bilgisayarda çalışan **1.3.0-dev** uygulaması/sunucusu değiştirilmedi. **113 canlı veritabanına uygulanmadı**. Önce mevcut veritabanı yedeklenmeli, API ve istemciler kapatılmalı, 113 ayrı onaylı bakım işlemiyle uygulanmalı, ardından istemci/API birlikte güncellenmelidir. API şema uyumsuzsa başlamaz; kaynak sürümünü eski 112 veritabanına otomatik bağlayıp yeni modülü kullanmayın. Yükseltme oturum nesillerini yeniler; tekrar giriş gerekir.

Kullanıcının isteğiyle unit/SQL/API/UI ve eşzamanlılık denemeleri çalıştırılmadı. Release derlemesi ve yayın çıktısı çalışma zamanı/SQL betiği doğrulamasının yerine geçmez. Bu teslimde canlı SQL bağlantısı veya veri değişikliği yapılmadı. GitHub'a push/Release yayımlanmadı.
