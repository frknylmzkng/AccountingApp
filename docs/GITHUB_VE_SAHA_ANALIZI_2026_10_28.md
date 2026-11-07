# GitHub karşılaştırması ve gerçek kullanım problemleri

28 Ekim 2026 · Hedef: vardiya ve proje bazlı çalışan ekiplerin takibi.

Bu belge, [mevcut kod bulgularından](PROJE_DURUM_ANALIZI_2026_10_28.md) ayrı bir ürün analizidir. Depoların README/kaynakları ve seçilmiş issue/tartışmaları incelendi; bu uygulamalar yerelde kurulup karşılaştırılmadı. Özellik varlığı güvenlik/kalite garantisi olarak yorumlanmadı. Issue'lar bildirilen senaryolardır; her birinin güncel sürümde hâlâ yaşandığı iddia edilmez. Senaryolardan uygulamamıza çıkarılan gereksinimler öneridir.

## 1. Benzer projelerden alınabilecek fikirler

| GitHub projesi | İncelenen yaklaşım | Bizim projeye uygun karşılığı | Sınır |
|---|---|---|---|
| [Frappe HRMS](https://github.com/frappe/hrms) | Personel, izin, vardiya, puantaj, bordro ve masraf süreçleri | Vardiya ataması → puantaj → onay → hakediş zinciri | Vergi/bordro hesaplarını doğrudan taşımak yerine işletmenin çalışma ve ücret kuralları belirlenmeli |
| [IceHrm](https://github.com/gamonoid/icehrm) | Personel kartı, organizasyon, ekip izin takvimi, mesai talebi, proje zaman çizelgesi, çalışan ekranı | Saha/ekip/amir, izin çakışması, çalışan talebi ve amir onayı | README kapsamı incelendi; sürümler/eklentiler arasında aynı paket kapsamı varsayılmadı |
| [ERPNext](https://github.com/frappe/erpnext) | Muhasebe, proje, maliyet ve operasyon kayıtlarını ilişkilendirme | Proje maliyet merkezi, hakediş/ödeme tahsisi, banka mutabakatı ve dönem kapanışı | Tam ERP kapsamı mevcut personel uygulamasından çok geniş |
| [Kimai](https://github.com/kimai/kimai) | Proje/aktivite bazlı süre, ücret oranları, takım yetkileri, bütçe ve raporlama | Personelin gününü projelere bölme; süre/bütçe/maliyet görünürlüğü | Puantajı kendiliğinden ücret veya yasal bordro kabul etmeme |
| [Akaunting](https://github.com/akaunting/akaunting) | Finans uygulamasını modüllerle genişletme | Ayrı ödeme, masraf ve rapor modülü sınırları | Bu incelemede bordro/banka eklentilerinin ücretsiz veya çekirdekte olduğu varsayılmadı |

Kaynaklar: [HRMS README](https://github.com/frappe/hrms/blob/develop/README.md), [IceHrm README](https://github.com/gamonoid/icehrm/blob/master/readme.md), [ERPNext README](https://github.com/frappe/erpnext/blob/develop/README.md), [Kimai README](https://github.com/kimai/kimai), [Akaunting README](https://github.com/akaunting/akaunting/blob/master/README.md).

Önerilen ürün yönü: **ekip planlama + gerçekleşen çalışma + proje maliyeti + kontrollü ödeme**. Mevcut WinForms yatırımını koruyarak bu iş akışları kurulabilir. Bu depolardan aynı ekranı veya aynı teknoloji yığınını kopyalamak hedef değildir; uygulama akışları bağımsız tasarlanır. Kaynak kod alınmasına karar verilirse ilgili dosya ve sürümün lisansı ayrıca değerlendirilir.

## 2. Gerçek projelerde görülen problemler ve bize etkisi

### S01 — Gece vardiyasının ertesi güne taşması

[HRMS #3298](https://github.com/frappe/hrms/issues/3298), gece vardiyasından sonraki sabah çıkışının sonraki günün vardiyasına bağlanmasıyla ilgili bir bildirimdir. Çıkarımımız: vardiya yalnızca “tarih + saat” alanı olmamalı; başlangıç/bitiş anı ve ShiftOccurrenceId ile tanımlanmalı.

Bizdeki kabul örneği: 28 Ekim 22:00 → 29 Ekim 06:00 çalışması tek vardiya örneğidir. 29 Ekim akşam ataması bunun çıkışını sahiplenemez. Ham giriş/çıkışlar korunur, otomatik eşleşme tereddütlüyse istisna kuyruğuna düşer. 8 saat örneği ara vermeden geçen süredir; ücretli süre mola ve onay kuralıyla ayrıca belirlenir.

### S02 — Eksik çıkışın ertesi günün girişini bozması

[HRMS #3859](https://github.com/frappe/hrms/issues/3859), önceki gün çıkış unutulunca yeni günün ilk kaydının çıkış sayılması sorununu bildiriyor. Çıkarımımız: “son hareket girişse yenisi çıkış” gibi genel bir anahtar kullanmamalıyız.

Bizde: vardiya bağlamı, açık oturum ve mantıklı süre sınırı kullanılır. Eksik çıkış “0 saat” veya 24 saat olarak sessizce ücretlendirilmez; amire uyarı ve gerekçeli düzeltme çıkar. Aynı cihaz olayı iki kez içeri alınırsa kaynak olay kimliğiyle tekilleştirilir.

### S03 — Tatil, ücretli/ücretsiz izin ve dönem sonu kesişimi

[HRMS #129](https://github.com/frappe/hrms/issues/129), tatiller ve kısmen ücretli izinlerin dönem hesabına etkisini anlatan tarihsel bir hata kaydıdır; sayfada kapalı görünür. [#2804 tartışması](https://github.com/frappe/hrms/discussions/2804) yarım gün çalışma/izin ilişkisinin ayrı durum olarak modellenmesini gösterir.

Bizde: önce takvim, izin türü, tam/yarım gün veya saat aralığı ve onay durumu tanımlanmalı. Mevcut başlangıç/bitiş dahil gün sayısı tarihsel kayıt olarak korunmalı. Onaylı izinle çakışan çalışma sessizce iki kez ücret üretmemeli; dönem sonuna taşan çalışma parçalanırken saat kaybolmamalı. Hukuki izin hakkı/bordro formülü bu örneklerden türetilmez.

### S04 — Banka satırı aynı ödemeyi tekrar üretmemeli

[ERPNext #54823](https://github.com/frappe/erpnext/issues/54823), mutabakatta mevcut ödeme kayıtlarının önceliklendirilmesini isteyen bir özellik talebidir. Bir banka hareketi ile birden fazla belge arasındaki ilişki ve yeni ödeme yaratılması riski tartışılır.

Bizde ilk sürüm banka CSV'sini ödeme göndermek için kullanmaz: dosya/hesap/kaynak satır kimliğiyle aktarır; mevcut ödemeye eşleştirme önerir; kullanıcı onaylar. Aynı dosya tekrar geldiğinde yeni satır/ödeme oluşmaz. Bir toplu banka çıkışını birden fazla personele tahsis etmek, tek ödemeyi birkaç banka hareketiyle kapatmak ve komisyonu ayrı sınıflamak mümkün olmalı. Birebir eşleşme dışındaki durumlar otomatik kesinleştirilmemeli.

### S05 — Yanlış şirket/proje bağlamının varsayılandan alınması

[ERPNext #55913](https://github.com/frappe/erpnext/issues/55913), toplu mutabakatta şirket alanının taşınmamasına ilişkin somut bir rapordur; sonraki yorumda düzeltildiği belirtilir. Bizim çıkarımımız proje/saha kimliğini kullanıcı varsayılanından sessizce tamamlamamaktır.

Bizde bir kullanıcı Proje A'yı görmeye yetkiliyken Proje B personeline toplu puantaj/ödeme yazamamalı. Ekran, istek, servis, SQL koşulu, dışa aktarma ve audit aynı ProjectId'yi doğrulamalı. Proje seçimi boşsa toplu işlem başlamamalı; değişince açık taslak ve seçimlerin bağlamı yeniden kontrol edilmeli.

### S06 — Ölçülen süre ile ücretlendirilen sürenin ayrılması

[HRMS #4088](https://github.com/frappe/hrms/issues/4088), vardiyadan erken girişin ücretli süre sayılıp sayılmamasını tartışan bir özellik talebidir. Bu, bütün işletmeler için tek doğru ücret kuralı olduğuna kanıt değildir.

Bizde ham 08:30 girişi korunur. Vardiya 09:00'da başlıyorsa ücret hesabı açıkça seçilmiş politika ve onaylı fazla mesaiye göre yapılır. Otomatik bir kesinti veya fazla mesai katsayısı varsayılmaz. Politika EffectiveFrom/EffectiveTo ile sürümlenir; aylar önce onaylanan tutar yeni oranla değişmez.

### S07 — Aynı kişinin gün içinde proje değiştirmesi

[Kimai'nin proje ve süre yaklaşımı](https://github.com/kimai/kimai), maliyeti tek personel bakiyesinden ayırmak için uygun örnektir. Bize özel saha örneği: kişi sabah 4 saat Proje A, öğleden sonra 3 saat Proje B'de çalışır. Bir saatlik ortak mola iki projeye birden yazılmaz; toplam gerçek süreyi aşan dağıtım reddedilir. Proje gideri ile personele olan borç ayrı görünür. Proje kârlılığı ancak proje geliri de güvenilir biçimde kaydedildiğinde raporlanır; ilk sürüm “işçilik maliyeti ve bütçe sapması” der.

## 3. Önerilen modüller

| Modül | Çözdüğü günlük problem | İlk kapsam | Öncelik / bağımlılık |
|---|---|---|---|
| Proje, saha ve ekip | Kimin nerede, hangi amire bağlı çalıştığının bilinmemesi | Proje kodu, durum, saha, ekip, sorumlu, tarihli personel ataması | İlk temel modül; kalıcı kişi/çalışma dönemi |
| Vardiya planlama | Çifte atama, eksik ekip ve izinli kişiye görev | Haftalık plan, gece vardiyası, kişi/saat çakışması, personel ihtiyacı, taslak/yayın | Proje ataması ve izin takvimi |
| Puantaj ve istisnalar | Kağıt/WhatsApp/Excel kayıtlarının dağınıklığı | Manuel günlük/toplu giriş, ara, gerçekleşen süre, eksik giriş/çıkış, amir onayı | Vardiya örneği, değişiklik geçmişi |
| İzin talep/onay takvimi | İzinli kişinin vardiyaya yazılması | Ayrı izin kayıtları, tür, talep/onay/ret/iptal, ekip takvimi | Eski izinlerin korunarak göçü |
| Hakediş ve avans | Bakiyenin nereden geldiğinin açıklanamaması | Onaylı süre × tarihli ücret oranı, düzeltme, avans ve ödeme tahsisi | Puantaj onayı, dönem ve para kuralları |
| Proje maliyeti/bütçe | Projenin işçilik yükünün görünmemesi | Onaylı saat ve tutar, planlanan/gerçekleşen, ekip/proje kırılımı | Hakediş ve maliyet dağıtımı |
| Masraf ve zimmet | Yemek/ulaşım gideri ve ekipman tesliminin unutulması | Gerekçeli masraf/onay; basit teslim-iade listesi | İkinci genişleme; ek dosya erişimi/saklama tasarımı |
| Banka CSV mutabakatı | Kaydedilen ödeme ile gerçek transferin ayrışması | Tekrarsız içe alma, eşleştirme önerisi, manuel onay, farklar | Ödeme kimliği, yöntem, referans, tutar tahsisi |
| Onay/denetim merkezi | Kimin hangi veriyi değiştirdiğinin bulunamaması | Bekleyen onaylar, gerekçe, hedef kayıt geçmişi, ayrı ödeme onayı | Ayrıntılı yetkiler ve audit |
| Operasyon raporları | Her rapor için Excel'i yeniden düzenlemek | Haftalık saha planı, eksik puantaj, hakediş, ödenen/kalan, proje maliyeti | Önce güvenilir kaynaklar, sonra kayıtlı filtreler |
| Çalışan/amir web ekranı | Sahadan merkezi kayıt/onay yapılamaması | Kendi vardiyasını görme, süre/izin talebi, takım onayı | Merkezi API ve proje kapsamlı yetki; ilk masaüstü MVP'sinden sonra |

En yüksek ilk değer: **proje/ekip + vardiya + puantaj onayı + hakediş**. Müşteri/tedarikçi cari, depo/lot, POS ve e-belge mevcut hedefe bağlı ilk paketler değildir; ayrı iş ihtiyacı oluşursa yeniden değerlendirilir.

## 4. İlk pilotun gerçek iş günü

1. Yönetici iki proje, iki saha, ekip amirleri ve çalışan atamalarını açar.
2. Amir bir haftalık vardiyayı taslak hazırlar; izin ve çifte atama uyarılarını çözerek yayınlar.
3. Gün sonunda gerçekleşen süreler girilir; eksik çıkış/geç kalma gibi istisnalar ayrı görünür.
4. Amir süreyi onaylar; kendi kaydını tek başına onaylayamaz. Muhasebe/finans kapsamındaki kullanıcı ücret ve hakedişi görür.
5. Hakediş dönemi, kullanılan ücret oranlarıyla dondurulur. Önceki avanslar ilgili hakedişe tahsis edilir; yeni dönem iki kez üretilmez.
6. Ödeme hazırlanır ve yetkili kişi onaylar; mevcut idempotent ödeme yolu kullanılır. Bu uygulama kaydı, banka transferinin gerçekleştiği anlamına gelmez.
7. Banka dosyası varsa mevcut ödemelerle eşleştirilir; kayıtsız/eksik/fazla tutarlar açık kalır.
8. Yönetici proje bazında planlanan/gerçekleşen saat ve maliyeti, ödeme sorumlusu hakediş/ödenen/kalan tutarı görür.

Pilot varsayımı: iki proje, bir gece vardiyası, kısmi proje değişimi, izinli çalışan, eksik çıkış ve avans içeren sentetik örnekler; ardından kullanıcı tarafından seçilecek gerçek bir dönemde paralel kontrol. Çalışan sayısı, kullanıcı/cihaz sayısı ve onay sorumluları henüz verilmediği için yük ve süre hedefleri [yol haritasında](GELISTIRME_YOL_HARITASI_2026_10_28.md) başlangıç varsayımıdır.
