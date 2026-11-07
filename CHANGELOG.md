# Değişiklik günlüğü

## 2026-11-07 — Yetki, arayüz ve proje SQL regresyonları

Proje ve mali belge eklemeleri kimlikleri tetikleyicilerle uyumlu biçimde döndürür. Şema 138, ücret modülünden sonra proje maliyeti, hakediş ve tahsis hesaplarında kullanılan mali görünümün sütunlarını düzeltir. Ödemeler ekranında banka işlemi dar pencerelerde alt satıra geçer. Giriş ekranı beklenmeyen hata ayrıntılarını göstermez.

Regresyonlar ayrı ücret, IBAN, ödeme ve dışa aktarma yetkilerini, bağımsız proje avansı onayını ve güncel şema üzerinde işlem geri almayı denetler.

## 2026-11-07 — Şifreli yedekleme regresyonları

Yedekleme ve geri yükleme testleri şifreli `.bak.abk` arşivlerine ve geçici test kasasına geçirildi. Aynalama testleri mevcut farklı içerikli kopyanın korunduğunu ve işlem kaydı yazılamadığında yeniden deneme yapılabildiğini denetler. Saklama temizliği geri yükleme için seçilen şifreli arşivi hem güvenlik yedeği hem de şema yükseltmesi sırasında korur.

## 2026-11-07 — 1.3.33-dev kaynak teslimi

28 Ekim–7 Kasım 2026 arasında geliştirilen masaüstü istemcisi, merkezi API, SQL şema yükseltmeleri, dağıtım araçları ve kullanım belgeleri bu kaynak tesliminde bir araya getirildi. Aşağıdaki kayıtlar geliştirme ile yerel kurulum tarihlerini ayrı gösterir. [Pilot ve teslim kaydı](docs/PILOT_VE_TESLIM_1_3_33_DEV.md)

## 2026-11-07 — 1.3.33-dev yerel kurulumu

Doğrulanmış SQL yedeği sonrası .NET 10.0.14 içeren istemci ve API kuruldu. Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi. Şema 137 ve API 1.32 ile açılış doğrulandı. İşlevsel denemeler atlandı. [Kurulum kaydı](docs/1_3_33_DEV_KURULUMU.md)

## 2026-11-07 — Performans, erişilebilirlik ve güvenlik kaynak incelemesi

Paketle taşınan .NET çalışma zamanı Microsoft güvenlik güncellemesiyle 10.0.14 sürümüne, derleme SDK’sı 10.0.403 sürümüne alındı. [Microsoft güvenlik duyurusu](https://github.com/dotnet/announcements/issues/496)

1.3.33-dev sürümünde yedek seçimi ilk geçerli adayda durur ve sınırlı CSV sorguları pencere toplamı yerine satır sınırını kullanır. Aynalama hedefi üzerine yazmadan yayımlanır. Arşiv uzunluğu durum okumalarında doğrulanır. Rapor, kasa ve ortak sayfalama kontrollerine erişilebilir adlar ve klavye sırası düzenlemeleri eklendi. Dağıtılan beş projenin NuGet sorgusu kullanılan kaynaklarda açık raporlamadı. Şema 137 ve API sözleşmesi 1.32. İşlevsel denemeler atlandı. Kalan **1 ana iş paketi** pilot, açık kabul denemeleri ve teslimdir. [İnceleme ve pilot matrisi](docs/PERFORMANS_ERISILEBILIRLIK_GUVENLIK_2026_11_07.md).

## 2026-11-07 — 1.3.32-dev yerel kurulumu / değişmeyen şema 137

Özel erişimli klasöre kapsam kimlikli COPY_ONLY/CHECKSUM kurulum öncesi .bak alındı; SQL VERIFYONLY ve manifest/SHA-256 doğrulandı. Yedek standart şifresizdir; kasa denemesi yapılmadı. 61 tablonun içerik/satır özetleri ile 100–137 migration kimlikleri korundu; şema yükseltilmedi. İstemci/API/kısayol 1.3.32-dev / API 1.32 sürümüne geçirildi; profiller ve dosya/kayıt defteri tercihleri korundu. API mevcut gizli görevde loopback HTTPS üzerinde çalışır; geçici yedek görevi kaldırıldı. Denemeler atlandı, masaüstü/sihirbaz/kurtarma aracı açılmadı. Kalan **2 ana iş paketi**. [Kurulum kaydı](docs/1_3_32_DEV_KURULUMU.md).

## 2026-11-06 — Kurulum ve dağıtım sonlandırması (kaynak)

1.3.32-dev / şema 137 / API 1.32: ZIP/manifest/özet ve özel dosya/yol doğrulayıcısı; salt okunur sürüm/API/SQL kimliği bakım/geri dönüş planı; bağımlılık envanteri ve açık imza durumu eklendi. İsteğe bağlı CurrentUser Code Signing/SignTool/RFC3161 ve RequireSigned yolu hazır, bu teslim imzasızdır. Sihirbaz commit/sourceState/fileVersion/şema/API/runtime ve tüm kök dosyaları denetler. CI tam test kapısını korur; artifact/Release öncesi ikinci kontrol, etiket/sürüm eşlemesi ve eşzamanlılık politikası eklendi. Çok bilgisayar, bakım ve geri dönüş rehberi hazırlandı. SQL 100–137 ve kurulu 1.3.31-dev korundu. Denemeler, imzalama, Actions ve GitHub yayını yapılmadı. Kalan **2 ana iş paketi**. [Kapsam](docs/DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md).

## 2026-11-06 — 1.3.31-dev yerel kurulumu / değişmeyen şema 137

Kapsam kimlikli COPY_ONLY/CHECKSUM kurulum öncesi .bak yedeği, RESTORE VERIFYONLY ve manifest/SHA-256 ile doğrulandı; yedek klasörü yalnız mevcut Windows hesabı/SYSTEM erişimine alındı. Yedek şifresizdir; yeni şifreleme denemesi yapılmadı. 61 tablonun içerik/satır özetleri ve 100–137 migration kimlikleri korundu; şema yükseltmesi yapılmadı. İstemci/API/kısayol 1.3.31-dev / API 1.31 sürümüne geçirildi; profiller ile dosya/kayıt defteri tercihleri korundu. API mevcut gizli görevde loopback HTTPS üzerinde çalışır; geçici yedek görevi kaldırıldı. Kurtarma aracı kuruldu, çalıştırılmadı. Anahtar oluşturma veya güvenlik/işlev denemeleri yapılmadı; masaüstü açılmadı. Kalan **3 ana iş paketi**. [Kurulum kaydı](docs/1_3_31_DEV_KURULUMU.md).

## 2026-11-06 — Yedek ve destek güvenliği (kaynak)

1.3.31-dev / şema 137 / API 1.31: LocalDB uygulama/şema güvenlik yedekleri parçalı AES-256-GCM .bak.abk biçimine geçirildi. DPAPI CurrentUser anahtar kümesi ve özel NTFS geçici klasör, parolalı .abkey dışa/içe aktarımı, eski anahtarları koruyan döndürme ve elle kullanılan BackupRecovery aracı eklendi. Geri yükleme mevcut kapsam/kimlik/oturum iptal korumalarıyla şifreli kaynağı kabul eder; merkezi API geri yükleme veya anahtar uç noktası açmaz. Yönetici kasa ekranı, sunucuda oluşturma/tam doğrulama/aynalama, 2–90 saklama politikası ve anahtar dışa aktarımından önce temizlememe koruması vardır. Eski açık yedekler otomatik silinmez. Tanı ZIP'i izin verilen teknik alanlarla sınırlıdır; log/PII/anahtar/profil içermez, kendiliğinden gönderilmez. Derleme/paket kontrolü dışında denemeler atlandı; canlı 1.3.30-dev/137 ve mevcut yedekler/ACL/anahtarlar korunur. SQL 100–137 değiştirilmedi. Kalan **3 ana iş paketi**, sıradaki kurulum/dağıtım sonlandırması. [Kapsam ve açık doğrulamalar](docs/YEDEK_VE_DESTEK_GUVENLIGI.md).

## 2026-11-06 — 1.3.30-dev yerel kurulumu / şema 137

Kapsam kimlikli COPY_ONLY/CHECKSUM yedeği, RESTORE VERIFYONLY ve manifest/SHA-256 doğrulamasından sonra yalnız 137 uygulandı. Mevcut kayıt/bakiye ve hesap verileri ile 100–136 migration kimlikleri korundu. Boş ReportPresets tablosu, iki CHECK, bir yabancı anahtar ve iki indeks doğrulandı. İstemci/API/kısayol 1.3.30-dev / API 1.30 sürümüne geçirildi; profiller ve tercihler korundu. API loopback HTTPS ve gizli başlangıç görevinde çalışır; eski bellek oturumları yeniden başlatmayla geçersizdir. Geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı, masaüstü açılmadı. Kalan **4 ana iş paketi**, sıradaki yedek/destek güvenliği. [Kurulum kaydı](docs/1_3_30_DEV_KURULUMU.md).

## 2026-11-06 — Rapor merkezi genişlemesi (kaynak)

1.3.30-dev / hazırlanmış 137 / API 1.30: proje personel/ekip atamaları, ücret/gider kuralı, talebi, aktarımı ve bekleyen adımlar için ortak responsive rapor ekranı eklendi. Tarih/kişi/ekip filtreleri, SQL sayfalaması, çalışma dönemiyle kapsam koruması ve çoğalmayan mali satırlar; veritabanı/proje/kalıcı hesap kimliğine özel değişmez kayıtlı görünümler ve gerekçeli audit var. Mevcut kişi-saat/bütçe/mutabakat/kesin dönem ekranlarına bağlantı eklendi. Yeni büyük rapor akışı sunucuda tek serializable okumada en fazla 200.000 satırı 10.000'lik CSV parçaları ve kapsam/SHA-256 manifestiyle 32 MiB ZIP üretir. Canlı ücret/maliyet/export yetkisi teslim boyunca korunur; keyfi istemci tablosu, banka/IBAN/genel bakiye aktarımı yoktur. Ekip filtresi tarih aralığına uyan güncel geçerli atamadır; tarihsel ekip fotoğrafı veya ekipler arası maliyet dağıtımı değildir. 100–136 ve kurulu 1.3.29-dev / 136 korundu; 137 uygulanmadı, denemeler atlandı. [Kullanım ve sınırlar](docs/RAPOR_MERKEZI_GENISLEMESI.md). Kalan **4 ana iş paketi**, sıradaki yedek/destek güvenliği.

## 2026-11-06 — 1.3.29-dev yerel kurulumu / şema 136

Kapsam kimlikli COPY_ONLY/CHECKSUM yedeği doğrulandı ve yalnız 136 uygulandı. Mevcut kayıtlar/bakiyeler, mali kapanışlar ve uygulanmış 100–135 kimlikleri korundu; hesap oturumları yenilendi. Beş boş ücret/gider tablosu, on beş etkin tetikleyici, tablo bütünlük/tekillik korumaları ve finans/defter kaynak bağları doğrulandı. İstemci/API/kısayol 1.3.29-dev / API 1.29 sürümüne geçirildi; profiller ve tercihler korundu. Kural/talep oluşturulmadı, mali hareket yapılmadı; denemeler atlandı ve masaüstü açılmadı. Kalan **5 ana iş paketi**, sıradaki rapor merkezi genişlemesi. [Kurulum kaydı](docs/1_3_29_DEV_KURULUMU.md).

## 2026-11-06 — Ek ücret/gider kuralları (kaynak)

1.3.29-dev / hazırlanmış 136 / API 1.29: tarihli değişmez kural sürümü ve bağımsız karar, günlük/aylık temel, aktarılmış saatlik kaynağa yalnız ek fazla mesai farkı, ek ödeme ve doğrudan proje gideri talepleri eklendi. Açık birim/katsayı/bölen, sunucu önizlemesi ve değişmez hesap/hash saklanır; mevzuat/hak birimi varsayılmaz. Aynı kaynak belge, fazla mesai satırı ve çalışma dönemi temel kapsamı korunur; saatlik önizleme/SQL günlük-aylık çakışmasını reddeder. Hazırlama, bağımsız mali onay ve idempotent tek aktarım ayrıdır. Personel/çalışma dönemi/rowversion korunur; yalnız bakiye güncellenir. Yanlış aktarım bağımsız onaylı tam ters hareketle kapatılır; aktif tahsisler önce serbest bırakılır. İki mali kaydı birleştiren audit/makbuz ve eski dönem bağlantıları korunur, banka ödemesi oluşturulmaz. 100–135, canlı 1.3.28-dev/135 ve kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/EK_UCRET_VE_GIDER_KURALLARI.md). Kalan **5 ana iş paketi**, sıradaki rapor merkezi genişlemesi.

## 2026-11-06 — 1.3.28-dev yerel kurulumu / şema 135

Kapsam kimlikli COPY_ONLY/CHECKSUM yedeği doğrulandı. İlk uygulanmamış 135 taslağındaki ayrılmış SQL LineNo adı hatası transaction ile geri alındı; alan tanımı ve runtime SQL [LineNo] olarak düzeltilip paketler yeniden oluşturuldu. Doğrulanmış güncel yedek sonrası düzeltilmiş 135 uygulandı; 100–134, mevcut kayıtlar ve bakiyeler korundu, oturumlar yenilendi. İki boş kapanış tablosu, on bir etkin tetikleyici, sekiz CHECK, beş yabancı anahtar, dokuz indeks ve on yedi sütunlu kaynak görünümü doğrulandı. İstemci/API/kısayol 1.3.28-dev / API 1.28 sürümüne geçirildi; profiller ve tercihler korundu. Denemeler atlandı, masaüstü açılmadı. Kalan **6 ana iş paketi**, sıradaki ek ücret/gider kuralları. [Kurulum kaydı](docs/1_3_28_DEV_KURULUMU.md).

## 2026-11-06 — Kalıcı mali dönem kapanışı (kaynak)

1.3.28-dev / hazırlanmış 135 / API 1.28: proje bazlı tamamlanmış kayıt dönemi, ardışık kapanış zinciri, ilk açılış/sonraki devir, olay bazlı tek sahiplenme, önizleme ve idempotent atomik kesinleştirme eklendi. Değişmez snapshot/satır/hash ve önceki kapanış bağlantıları saklanır; mali düzeltme, iptal, tahsis ve banka bağlantısı değişiklikleri sonraki dönemde görünür. Kayıt zamanını kapalı döneme çekme ve kapalı ödeme gövdesini değiştirme engellenir; mevcut bağımsız mali onay/ödeme korumaları devam eder. Okumada maliyet+ödeme görünümü, CSV için ayrıca export yetkisi gerekir. Canlı veriyle yeniden hesaplanmayan sabit özet ve sayfalı kaynaklar ekranda ve CSV'de gösterilir. 100–134, canlı 1.3.27-dev/134 ve kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/KALICI_MALI_DONEM_KAPANISI.md). Kalan **6 ana iş paketi**, sıradaki ek ücret/gider kurallarıdır.

## 2026-11-06 — 1.3.27-dev yerel kurulumu / şema 134

Kapsam kimlikli COPY_ONLY/CHECKSUM yedeği doğrulandı ve yalnız 134 uygulandı. Mevcut kayıtlar/bakiyeler korundu; hesap oturumları yenilendi. Dört yeni boş banka tablosu, dokuz etkin tetikleyici, yedi güvenilir CHECK, beş yabancı anahtar, on altı tablo indeksi ve ödeme aday indeksi doğrulandı. İstemci/API/kısayol 1.3.27-dev / API 1.27 sürümüne geçirildi; profiller/tercihler korundu. Denemeler atlandı; masaüstü açılmadı. Kalan kapsam **7 ana iş paketi**, sıradaki kalıcı mali dönem kapanışı. [Kurulum kaydı](docs/1_3_27_DEV_KURULUMU.md).

## 2026-11-06 — Banka CSV ve ödeme eşleştirme (kaynak)

1.3.27-dev / hazırlanmış 134 / API 1.27: TRY kaynak hesapları, açık CSV sütun/tarih/ondalık seçimi, salt okunur sunucu önizlemesi, hesap/işlem kimliğinde tekrar ve içerik çelişkisi kontrolü, idempotent atomik aktarım eklendi. Tam tutarlı mevcut ödeme adayları açık dekont/alıcı onayı ve gerekçeyle bağlanır; yeni ödeme/bakiye hareketi oluşmaz. Tekil aktif banka/ödeme bağlantısı, güncel önizleme, rowversion kontrollü gerekçeli kaldırma ve eşleşen ödeme iptalini engelleyen servis/SQL korumaları vardır. IBAN ve açıklama audit kopyalarına taşınmaz. Yönetici dışındaki proje yetkileri tüm ekstreyi açmaz. 100–133 ve canlı 1.3.26-dev/133 korundu; 134 uygulanmadı, denemeler atlandı. [Kullanım ve sınırlar](docs/BANKA_CSV_VE_ODEME_ESLESTIRME.md). Kalan kapsam **7 ana iş paketi**; sıradaki kalıcı mali dönem kapanışıdır.

## 2026-11-06 — 1.3.26-dev yerel kurulumu / şema 133

Kapsam kimlikli COPY_ONLY/CHECKSUM yedeği alınıp doğrulandı. Yalnız 133 uygulandı; eski kayıtlar ve bakiyeler korundu, hesap oturumları yenilendi. Bir yeni boş tablo, iki tetikleyici, üç CHECK, iki yabancı anahtar ve altı indeks doğrulandı. İstemci/API/kısayol 1.3.26-dev / API 1.26 sürümüne geçirildi. API gizli başlangıç göreviyle çalışıyor; bağlantı profilleri ve tercihler korundu. Denemeler atlandı; masaüstü açılmadı. [Kurulum kaydı](docs/1_3_26_DEV_KURULUMU.md).

## 2026-11-06 — Proje bütçesi ve maliyet sapması (kaynak)

1.3.26-dev / hazırlanmış 133 / API 1.26: işçilik/diğer gider bütçeleri, değişmez gerekçeli revizyon zinciri, önceki revizyon/proje sürümü ve tekrar istek koruması eklendi. Gerçekleşen maliyet düzeltilmiş geçerli hakediş ve giderden hesaplanır; nakit/tahsis tekrar eklenmez. Tanımsız/sıfır bütçe ayrımı, kategori/toplam sapma, kullanım, sayfalı geçmiş/belge ve yetkili CSV vardır. 100–132, canlı 1.3.25-dev/132 ve kısayol korundu; denemeler atlandı. [Kullanım](docs/PROJE_BUTCESI_VE_MALIYET_SAPMASI.md). Yol haritasındaki kalan açık kapsam [8 ana pakette](docs/KALAN_IS_PAKETLERI_2026_11_06.md) toplandı.

## 2026-11-06 — Konsol ve siyah masaüstü incelemesi

İnceleme anındaki System32 CMD başlığı Instagram Hub frontend sürecine bağlandı; diğer proje değiştirilmedi. Standart Winlogon Explorer/Userinit ayarları korundu; siyah masaüstünün kök nedeni kesinleşmedi. API görev eylemi gizli GUI betik konağına geçirildi; sonraki logonda yalnız Explorer yoksa açan 30 saniyelik koşullu görev eklendi. Çalışan API/Explorer durdurulmadı. Yeni API kaynak çıktısı WinExe oldu. Yeni görevleri çalıştırma/yeniden başlatma denemesi yapılmadı. [Kanıt ve sınırlar](docs/WINDOWS_BASLANGIC_VE_KONSOL_2026_11_06.md).

## 2026-11-06 — 1.3.25-dev yerel kurulumu, şema 132 korundu

Doğrulanmış yedek alındı; migration çalıştırılmadan veri, bakiye, oturum kimlikleri ve şema günlüğü korundu. İstemci/API/kısayol güncellendi; kullanıcı sürümü doğruladı. Denemeler atlandı. [Kurulum](docs/1_3_25_DEV_KURULUMU.md).

## 2026-11-06 — Planlanan / gerçekleşen proje kişi-saat (kaynak)

1.3.25-dev / mevcut 132 / API 1.25: proje kadro ihtiyacı, atanmış net plan, proje onaylı gerçekleşme/fark, bekleyen/taslak süre ve tam onaylı ücret dakikası karşılaştırması eklendi. Çok proje parçaları ana puantajla iki kez sayılmaz; kısmi proje onayı ile tam ana onay ayrılır. İş günü/kişi filtresi, SQL sayfalama, günlük/kişi/dönem/plan/kaynak/istisna ayrıntıları ve yetkili tüm bölüm CSV'si bulunur. Yeni para/hakediş veya migration üretilmez. 100–132 ve canlı 1.3.24-dev/132/kısayol korundu; denemeler atlandı. [Hesap ilkeleri ve kullanım](docs/PLAN_GERCEKLESEN_PROJE_KISI_SAAT.md).

## 2026-11-06 — 1.3.24-dev yerel kurulumu, şema 132 korundu

Doğrulanmış yedek alındı; migration olmadan veriler, bakiyeler, oturum kimlikleri ve şema günlüğü korundu. İstemci/API/kısayol güncellendi; kullanıcı sürümü doğruladı. Denemeler atlandı. [Kurulum](docs/1_3_24_DEV_KURULUMU.md).

## 2026-11-06 — Mali tarih/kişi filtreleri ve sayfalama (kaynak)

1.3.24-dev / mevcut 132 / API 1.24: tahsis ödeme/hakediş/geçmiş listeleri ve mutabakat bölümlerine kayıt tarihi/kişi filtresi ve SQL sayfalama eklendi. İstanbul gün sınırları, kalıcı kişi seçimi/hesap adı araması, bölüm başına toplam/sayfa, kapsam değişiminde seçim temizliği ve uygulanmamış filtrede yazma/dışa aktarma engeli bulunur. Parasal kapasite bütün aktif tahsisleri içerir; rapor özeti tüm filtre kapsamından hesaplanır. Tarih sınırını geçen tahsisler ayrı açıklanır. CSV aynı filtrede seçili bölümün tüm satırlarını yeniden üretir; 10.000 satır CSV sınırı korunur. Yeni migration yok; canlı 1.3.23-dev/132/kısayol korundu, denemeler atlandı. [Kapsam ve kullanım](docs/MALI_FILTRELER_VE_SAYFALAMA.md).

## 2026-11-06 — 1.3.23-dev yerel kurulumu, şema 132 korundu

Doğrulanmış yedek alındı; migration çalıştırılmadan veri, bakiye, oturum kimlikleri ve şema günlüğü korundu. İstemci/API/kısayol güncellendi; kullanıcı 1.3.23-dev'i doğruladı. Denemeler atlandı. [Kurulum](docs/1_3_23_DEV_KURULUMU.md).

## 2026-11-06 — Proje mali mutabakat raporu (kaynak)

1.3.23-dev / mevcut 132 / API 1.23: proje ve kişi/çalışma dönemi bazında ilk/düzeltilmiş hakediş, ödeme/avans, aktif tahsis, açık tutarlar ve net konum eklendi. Altı bölüm aynı Serializable transaction'da okunur; kaynaklar SQL'de ayrı özetlenir. Eksik bağlantı/kapsam, tahsis aşımı ve geçersiz kaynak istisnaları gösterilir. Yetkili seçili bölüm CSV'si sunucuda güncel veriden yeniden oluşturulur; kapsam/veri zamanı/aktör dosyada saklanır, dışa aktarma audit edilir. 10.000 satır/bölüm ve 32 MB dosya sınırı vardır. Yeni migration yok; 100–132 ve canlı 1.3.22-dev/132/kısayol korundu, denemeler atlandı. [Kullanım ve tutarların anlamları](docs/PROJE_MALI_MUTABAKAT_RAPORU.md).

## 2026-11-06 — 1.3.22-dev yerel kurulumu / şema 132

Doğrulanmış yedek sonrası 132 uygulandı; mevcut kayıtlar ve bakiyeler korundu. İki tahsis tablosu boş, yedi tetikleyici ve aktif görünüm doğrulandı. Otomatik tahsis/ödeme oluşturulmadı. İstemci/API/kısayol güncellendi; kullanıcı 1.3.22-dev'i doğruladı. Denemeler atlandı. [Kurulum](docs/1_3_22_DEV_KURULUMU.md).

## 2026-11-06 — Ödeme ve avans tahsisi (kaynak)

1.3.22-dev / hazırlanmış 132 / API 1.22: mevcut proje ödeme/avansını aynı kişi/çalışma döneminin hakediş belgelerine kısmi tahsis ve gerekçeli serbest bırakma eklendi. Yazma mevcut proje ödeme onay yetkisinde, okuma ücret/maliyet/ödeme kapsamındadır. Tahsis yeni ödeme, bakiye veya maliyet hareketi üretmez. Tüm aktif kaynak toplamları, güncel önizleme ve tekrar istek korunur; aktif tahsisli kaynak iptali ve tahsis altına inen mali düzeltme engellenir. İlk ekranlar en yeni 500 kayıtla sınırlıdır. 100–131 ve canlı 1.3.21-dev/131/kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/ODEME_VE_AVANS_TAHSISI.md).

## 2026-11-06 — 1.3.21-dev yerel kurulumu / şema 131

Doğrulanmış yedek sonrası 131 uygulandı; mevcut kayıtlar ve bakiyeler korundu. Üç yeni tablo boş, yedi tetikleyici ve iki görünüm doğrulandı. Otomatik düzeltme/ödeme üretilmedi. İstemci/API/kısayol güncellendi; kullanıcı 1.3.21-dev'i doğruladı. Denemeler atlandı. [Kurulum](docs/1_3_21_DEV_KURULUMU.md).

## 2026-11-06 — Bağlı bağımsız onaylı mali düzeltme ve ters hareket (kaynak)

1.3.21-dev / hazırlanmış 131 / API 1.21: aktarılmış asıl/ek hesabın kişi/çalışma dönemi satırı için yeni toplam tutarlı değişmez düzeltme, tarihsel kimlikli bağımsız mali onay, güncel bakiye/sürüm önizlemesi ve tek seferlik fark aktarımı eklendi. Yeni toplam sıfır tam ters harekettir; zincir ilk/sonraki bağlantı tekilliği ve eski tutar kontrolü korunur. Özgün finans/kaynak kayıtları değişmez. Defter/bakiye/makbuz/audit ortak transaction içindedir; proje maliyeti, kart, maliyet CSV ve proje ödeme limiti aktarılmış farkı içerir. Negatif bakiye borç olabilir; banka iadesi yapılmaz. 100–130 ve canlı 1.3.20-dev/130/kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/BAGLI_MALI_DUZELTME_VE_TERS_HAREKET.md).

## 2026-11-06 — 1.3.20-dev yerel kurulumu / şema 130

Doğrulanmış yedek sonrası 130 uygulandı; mevcut iş kayıtları ve bakiyeler korundu. Bağlı ek hesap nesneleri doğrulandı; otomatik ek hesap oluşturulmadı. İstemci/API/kısayol 1.3.20-dev'e geçirildi; kullanıcı sürümü doğruladı. Denemeler atlandı. [Kurulum](docs/1_3_20_DEV_KURULUMU.md).

## 2026-11-06 — Geç gelen çalışma için bağlı ek dönem hesabı (kaynak)

1.3.20-dev / hazırlanmış 130 / API 1.20: aktarılmış asıl hesapla aynı proje/döneme bağlı ek hesap önizleme ve kaydetme eklendi. Yalnız kullanılmamış onaylı kaynaklar alınır; asıl/ekler arasında kaynak tekilliği korunur. Ek hesap kendi bağımsız mali onayı ve tek seferlik aktarımını gerektirir. Asıl bağlantısı değişmez; geçmişte tür/asıl hesap numarası gösterilir. 100–129 betikleri ve canlı 1.3.19-dev/129/kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/EK_DONEM_HAKEDISI.md).

## 2026-11-06 — 1.3.19-dev yerel kurulumu / şema 129

Doğrulanmış yedek sonrası 129 uygulandı; mevcut veriler ve bakiyeler korunarak istemci/API/kısayol güncellendi. 129 ilk uygulaması SQL komut grubu nedeniyle geri alındı; GO sınırları düzeltilip paketler yeniden üretildi ve yükseltme tamamlandı. Üç yeni tablo boş, sekiz tetikleyici etkin; otomatik mali onay/aktarım yapılmadı. Bağımsız mali onay ve tek seferlik defter/bakiye aktarımı bu sürümde kuruldu. Kullanıcı 1.3.19-dev'i doğruladı; işlev denemeleri atlandı. [Kurulum](docs/1_3_19_DEV_KURULUMU.md).

## 2026-11-02 — Dönem hakedişinde mali onay ve aktarım (kaynak)

1.3.19-dev ile dönem hesabını hazırlayan ve mali onay veren hesaplar ayrıldı. Onaylı hesap, deftere ve personel bakiyelerine tek işlemde ve yalnız bir kez aktarılır. Bu akış için şema 129 ve API 1.19 hazırlandı. [Kullanım ve yetki ayrımı](docs/DONEM_HAKEDISI_MALI_ONAY_VE_AKTARIM.md)

## 2026-11-02 — Tarihli tarife ve dönem hakedişi temeli (kaynak)

Kullanıcı 1.3.17-dev sürümünü doğruladı. 1.3.18-dev / hazırlanmış 128 / API 1.18: proje/çalışma dönemine çakışmayan tarihli saatlik TL tarifeleri, kaldırma/geçmiş, onaylı puantaj veya proje parçalarından önizleme ve mühürlü dönem hesabı eklendi. İş günü/oran/dakika/kişi adı/kaynak sürümleri snapshot olarak kalır; satır başına açık yuvarlama, kaynak tekilliği, önizleme sürümü ve idempotent hesap kimliği kullanılır. Kaynak/onaylar hesap sonrası kilitlenir. Deftere/bakiyeye aktarım, mali onay/düzeltme ve ödeme bu temel dilimde yoktur. 100–127 ve canlı 1.3.17-dev/127/kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/TARIHLI_TARIFE_VE_DONEM_HAKEDISI.md).

## 2026-11-02 — 1.3.17-dev yerel kurulumu, şema 127 korundu

Doğrulanmış yedek sonrası 1.3.17-dev istemci ve API 1.17 ayrı klasöre kuruldu; API görevi ve Muhasebe - Güncel kısayolu güncellendi. Migration uygulanmadı; mevcut veri/audit, güvenlik nesilleri, profiller/tercihler korundu. Geçici bakım görevi kaldırıldı; işlev denemeleri atlandı. [Kurulum kaydı](docs/1_3_17_DEV_KURULUMU.md).

## 2026-11-02 — Çok kişili toplu manuel puantaj (kaynak)

Kullanıcı 1.3.16-dev sürümünü doğruladı. 1.3.17-dev / mevcut 127 / API 1.17: yayınlanmış atamalardan çoklu seçim, vardiyasız kişi/dönem/atama ekleme, aynı güne ortak gerçekleşen alan uygulama, kişi bazında gözden geçirme ve tek önizleme eklendi. En fazla 200 yeni satır tek transaction/ortak makbuzla saklanır; hata tür/satır sırasıyla belirtilir. Özgün sürümler, tekrar/kalıcı vazgeçme, oturum/proje hakları ve şifreli taslak kurtarma korunur. Yeni şema betiği yok; 100–127 ve canlı 1.3.16-dev/127/kısayol korundu, denemeler atlandı. [Kullanım ve sınırlar](docs/TOPLU_MANUEL_PUANTAJ.md).

## 2026-11-02 — 127 ve 1.3.16-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeği sonrası yalnız 127 uygulandı. Mevcut iş/audit verileri korundu, oturum nesilleri yenilendi; vazgeçme makbuzu tablosu boş başladı. 1.3.16-dev / API 1.16 ayrı klasöre kuruldu; API görevi ve Muhasebe - Güncel kısayolu güncellendi. Profiller/tercihler korundu, geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı. [Kurulum kaydı](docs/1_3_16_DEV_KURULUMU.md).

## 2026-11-02 — Kalıcı puantaj taslak kurtarma (kaynak)

Kullanıcı 1.3.15-dev sürümünü doğruladı. 1.3.16-dev / hazırlanmış 127 / API 1.16: vardiyalı/vardiyasız puantaj, CSV önizleme ve proje/mola dağılımında şifreli otomatik/elle yerel taslak; açık ve sürüm kontrollü kurtarma; hesap/veritabanı/proje ayrımı ve dosya kilidi eklendi. Belirsiz gönderimden önce özgün istek dayanıklı saklanır; tekrar aynı kimlikle yapılır. Sunucu sonucunu kontrol etme ve kalıcı vazgeçme, gecikmiş aynı isteği SQL makbuzuyla engeller; kayıtlı iş kaydını geri almaz. Salt okunur ayrıntı ve canlı kapsam kontrolleri bulunur. 100–126 ve canlı 1.3.15-dev/126/kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/KALICI_PUANTAJ_TASLAK_KURTARMA.md).

## 2026-11-02 — 126 ve 1.3.15-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeği sonrası yalnız 126 uygulandı. Mevcut iş/audit verileri korundu, oturum nesilleri yenilendi; yeni eşleme/komut tabloları boş başladı. 1.3.15-dev / API 1.15 ayrı klasöre kuruldu; API görevi ve Muhasebe - Güncel kısayolu güncellendi. Profiller/tercihler korundu, geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı. [Kurulum kaydı](docs/1_3_15_DEV_KURULUMU.md).

## 2026-11-02 — Çalışan–hesap eşlemesi (kaynak)

Kullanıcı 1.3.14-dev sürümünü doğruladı. 1.3.15-dev / hazırlanmış 126 / API 1.15: yöneticiye sürümlü personel seçimiyle hesap eşleme/değiştirme/kaldırma ve hesap kapsamlı geçmiş eklendi. Etkin bağ tekil, geçmiş değişmez; kaldırılmış bağlar puantaj görev ayrımında kanıt kalır. Çalışan ve aynı kişiye eşlenmiş hazırlayan/olay katılımcısı hesapların onayı servis ve SQL sınırında engellenir. Eski seçim, atomik audit/oturum iptali, kalıcı komut makbuzu ve tekrar koruması bulunur. Eşleme veri erişimi vermez; mevcut onayları geriye yazmaz. 100–125 ve canlı 1.3.14-dev/125/kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/CALISAN_HESAP_ESLEMESI.md).

## 2026-11-02 — 125 ve 1.3.14-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeği sonrası yalnız 125 uygulandı. Mevcut veri/audit içerikleri korundu, oturum nesilleri yenilendi; üç boş onay tablosu ve yedi yeni tetikleyici doğrulandı. 1.3.14-dev / API 1.14 ayrı klasöre kuruldu; API görevi ve Muhasebe - Güncel kısayolu güncellendi. Profiller/tercihler korundu, geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı. [Kurulum kaydı](docs/1_3_14_DEV_KURULUMU.md).

## 2026-11-02 — Bağımsız proje parçası onayı (kaynak)

Kullanıcı 1.3.13-dev sürümünü doğruladı. 1.3.14-dev / hazırlanmış 125 / API 1.14: proje amirinin yalnız kendi parçası üzerinde onay/iadesi, sürümlü tur ve karar, değişmez karar olayları eklendi. Son onay ana kaydı tamamlar; tek iade turu kapatıp ana kaydı iade eder, diğer kararları değiştirmez. Proje süreleri güncel açık turdaki onaylı parçaları tek sayar. Görev ayrımı, kaynak/olay katılımcısı öz onay yasağı, eski tur/sürüm ve kalıcı komut korumaları sürer. Eski toplu onaylar korunur; yerel iade ve yeniden gönderimle yeni modele geçer. 100–124 ve canlı 1.3.13-dev/124/kısayol korundu, denemeler atlandı. [Kullanım ve sınırlar](docs/BAGIMSIZ_PROJE_PARCASI_ONAYI.md).

## 2026-11-02 — 124 ve 1.3.13-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 124 transaction içinde uygulandı. Eski iş verileri, puantaj/komut/ham olay/eşleme/audit içerikleri ve haklar korundu; oturum nesilleri yenilendi. Dağılım tabloları boş oluşturuldu; mevcut atamalar asıl kaldı, ek atamaya dönüşüm yapılmadı. Etkin dağılım tekilliği, kapalı ek atama varsayılanı ve koruma/audit kısıtları metadata üzerinden doğrulandı. 1.3.13-dev / API 1.13 ayrı klasöre kuruldu; API görevi ve Muhasebe - Güncel kısayolu güncellendi. Profiller/tercihler korundu, geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı; masaüstü açılmadı. [Kurulum kaydı](docs/1_3_13_DEV_KURULUMU.md).

## 2026-11-02 — Gün içi proje / mola dağılımı (kaynak)

Kullanıcı 1.3.12-dev sürümünü doğruladı. 1.3.13-dev / hazırlanmış 124 / API 1.13: asıl atamayı koruyan tarihli ek proje/ekip ataması; sürümlü, boşluksuz/çakışmasız zaman parçaları; mola ve ücret dakikasını ana kayıtla uzlaştıran dağılım eklendi. Hazırlama/kaldırma ve gönderim/onay/iade bütün ilgili projelerde canlı hak ister. Parça listesi proje kapsamıyla sınırlı; tam geçmiş bütün hedeflerde okuma ister. SQL özetleri onaylı parçaları ana süreyi tekrar saymadan toplar. Etkin dağılımda kaynak/ham olay düzenleme koruması, değişmez sürüm/iptal geçmişi, kalıcı komut ve audit eklendi. 100–123 ve canlı 1.3.12-dev/123/kısayol korundu; denemeler atlandı, finans defterine yazılmadı. [Kullanım ve sınırlar](docs/GUN_ICI_PROJE_VE_MOLA_DAGILIMI.md).

## 2026-11-02 — 123 ve 1.3.12-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 123 transaction içinde uygulandı. Eski iş verileri, puantaj/komut/audit içerikleri ve haklar korundu; oturum nesilleri yenilendi. Yeni ham olay/eşleme tabloları boş oluşturuldu; olay üretilmedi. Kaynak ve etkin eşleme tekillikleri, zaman hassasiyeti, koruma/audit trigger ve kısıtları metadata üzerinden doğrulandı. 1.3.12-dev / API 1.12 ayrı klasöre kuruldu; API görevi ve Muhasebe - Güncel kısayolu güncellendi. Profiller/tercihler korundu, geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı; masaüstü açılmadı. [Kurulum kaydı](docs/1_3_12_DEV_KURULUMU.md).

## 2026-11-02 — Ham giriş/çıkış ve kontrollü eşleme (kaynak)

Kullanıcı 1.3.11-dev sürümünü doğruladı. 1.3.12-dev / hazırlanmış 123 / API 1.12: değişmez ham olay, tek olay/tek kişilik CSV önizlemesi, projeler arasında kaynak olay tekilliği ve açık In-Out seçimiyle mevcut taslak/iade puantaja eşleme eklendi. Ham saniye/kesir korunur; EventMinuteFloor.v1 dönüşümü kullanıcı önizlemesinde açıkça kabul edilir. Aktif kaynak tekilliği, eski seçim/sürüm, kalıcı komut/transaction, gerekçeli yoksayma/kaldırma, saat temizliği ve audit korumaları eklendi. Olayı giren/eşleyen hesap bağlı puantajı onaylayamaz. 100–122 betikleri ve canlı 1.3.11-dev/122/kısayol korundu; denemeler atlandı, ücret/hakediş üretilmez. [Kullanım ve sınırlar](docs/HAM_OLAYLAR_VE_PUANTAJ_ESLEMESI.md).

## 2026-11-02 — 122 ve 1.3.11-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 122 uygulandı. İlk dinamik EXEC/QUOTENAME sözdizimi hatasında transaction geri alındı; henüz uygulanmamış betik düzeltildi, istemci/API paketleri yeniden üretildi ve veri özetleri değişmeden yükseltmeye devam edildi. Eski 100–121 betikleri, iş verileri, puantaj/komut/audit içerikleri ve haklar korundu; oturum nesilleri yenilendi. Nullable vardiya bağlantısı, filtreli tekillik ve NULL dahil kaynak koruması doğrulandı. 1.3.11-dev / API 1.11 ayrı klasöre kuruldu, API görevi ve Muhasebe - Güncel kısayolu güncellendi. Profiller/tercihler korundu; geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı, masaüstü açılmadı. [Kurulum kaydı](docs/1_3_11_DEV_KURULUMU.md).

## 2026-11-01 — Vardiyasız puantaj (kaynak)

Kullanıcı 1.3.10-dev sürümünü doğruladı. 1.3.11-dev / hazırlanmış 122 / API 1.11: tarihli proje/ekip atamasına bağlı vardiyasız gerçek çalışma taslağı, özgün kişi/roster sürümü ve dönem kontrolü eklendi. Mevcut gönderim/onay/iade/yeniden açma, farklı hesap ve kalıcı tekrar korumaları kullanılır. Haftalık okuma/özet/istisnalar vardiyasız kayıtları içerir; planlı vardiya, izin ve puantaj çakışmaları iki yönde korunur. Nullable vardiya bağlantısında eski planlı tekillik ve NULL dahil kaynak değişmezliği korundu. 100–121 betikleri ve kurulu 1.3.10-dev/121/kısayol korundu. Denemeler atlandı; ücret/hakediş üretilmez. [Kullanım ve sınırlar](docs/VARDIYASIZ_PUANTAJ.md).

## 2026-10-31 — 121 ve 1.3.10-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 121 transaction içinde uygulandı. Eski iş verileri, audit, takvim kayıtları, proje ödeme talepleri ve önceki yetkiler korundu; hesap oturumları yenilendi. Yeni puantaj tabloları boş, giriş/onay bitleri kapalı başladı. İstemci/API 1.3.10-dev / API 1.10 ayrı klasöre kuruldu; kalıcı Windows görevi ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Profiller, HTTPS sertifikası ve tercihler korundu, geçici bakım görevi kaldırıldı. Paket bütünlüğü, API başlangıcı ve son kurulum durumu doğrulandı. İşlev denemeleri atlandı; masaüstü açılmadı. [Kurulum kaydı](docs/1_3_10_DEV_KURULUMU.md).

## 2026-10-31 — Puantaj, istisnalar ve amir onayı (kaynak)

Kullanıcı 1.3.9-dev sürümünü doğruladı. 1.3.10-dev / hazırlanmış 121 / API 1.10: yayınlanmış vardiya kişi atamasına bağlı manuel ve önizlemeli CSV taslakları; ham saat/mola/net/ücretlendirilecek dakika ayrımı; istisnalar, farklı hesapla onay/iade/yeniden açma eklendi. CanEnterTimesheets ve CanApproveTimesheets ayrı kapalı proje haklarıdır. Kalıcı komut kimliği/özet, atomik toplu aktarım, sürüm kontrolü, ortak kişi kilidi, eski/yeni audit ve karşılıklı izin/atama/dönem korumaları eklendi. Saat içeren alanların kaydedilmemiş değişiklik takibi düzeltildi. 100–120 betikleri ve canlı 1.3.9-dev/120 kurulum/kısayol korundu. Denemeler atlandı; ücret/hakediş üretilmedi. [Kullanım ve açık kalan işler](docs/PUANTAJ_VE_AMIR_ONAYI.md).

## 2026-10-31 — 120 ve 1.3.9-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 120 transaction içinde uygulandı; eski iş verileri, audit içerikleri, proje ödeme talepleri ve önceki yetkiler korundu, hesap oturumları yenilendi. Yeni takvim tabloları boş, planlama/onay bitleri kapalı başladı. İstemci/API 1.3.9-dev / API 1.9 ayrı klasöre kuruldu; kalıcı Windows görevi ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Profiller, HTTPS sertifikası ve tercihler korundu; geçici bakım görevi kaldırıldı. Paket bütünlüğü, API başlangıcı ve son kurulum durumu doğrulandı. İşlev denemeleri atlandı, masaüstü açılmadı. [Kurulum kaydı](docs/1_3_9_DEV_KURULUMU.md).

## 2026-10-31 — Vardiya ve izin takvimi (kaynak)

Kullanıcı 1.3.8-dev'i doğruladı. 1.3.9-dev / hazırlanmış 120 / API 1.9: haftalık proje/ekip görünümü, şablon snapshot'ı, gece vardiyası, taslak/yayın/iptal ve kapasite; atomik önizlemeli hafta kopyalama; ayrı izin talepleri/onay/ret/iptal eklendi. CanPlanShifts ve CanApproveLeaves ayrı, kapalı proje bitleridir. Kişi/dönem/roster/izin çakışmaları projeler arasında sunucuda korunur; eski izin, dönem ve roster yolları takvim korumalarına bağlandı. Takvim dakika politikaları ücret üretmez. Eski 100–119 ve canlı 1.3.8-dev/119 kurulum/kısayol korundu; denemeler atlandı. [Kullanım ve sınırlar](docs/VARDIYA_VE_IZIN_TAKVIMI.md).

## 2026-10-31 — 119 ve 1.3.8-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 119 transaction içinde uygulandı; eski iş verileri/audit ve mevcut ücret/IBAN hakları korundu, hesap oturumları yenilendi. Yeni proje ödeme bitleri kapalı, talep tablosu boş başladı; ödeme işlemi yapılmadı. İstemci/API 1.3.8-dev / API 1.8 ayrı klasöre kuruldu; kalıcı Windows görevi ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Profiller/tercihler korundu, geçici bakım görevi kaldırıldı. Denemeler atlandı; masaüstü açılmadı. [Kurulum kaydı](docs/1_3_8_DEV_KURULUMU.md).

## 2026-10-31 — Proje ödeme hazırlama / onay ayrımı (kaynak)

Kullanıcı 1.3.7-dev'i doğruladı. 1.3.8-dev / hazırlanmış 119 / API 1.8: bağımsız CanPreparePayments ve CanApprovePayments; SQL'de kalıcı dondurulmuş ödeme talepleri, başka hesapla onay/red ve tek transaction ödeme/karar/audit eklendi. Güncel proje/ücret/atama/personel dönemi, bakiye/proje hakedişi, hazırlayan güvenlik nesli ve tekrar kontrolleri korunur. Yeni proje etiketli ödeme genel submit ile akışı atlayamaz; eski kaydedilmiş sonuç kurtarması korunur. Yönetici ve Projelerim ekranına kuyruk eklendi. 100–118 betikleri ve canlı 1.3.7-dev/118 kurulum/kısayol korundu; denemeler atlandı. [Kapsam ve sınırlar](docs/PROJE_ODEME_HAZIRLAMA_VE_ONAY.md).

## 2026-10-31 — 118 ve 1.3.7-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 118 transaction içinde uygulandı; eski veri/audit ve grup geçişleri korundu, oturum nesilleri yenilendi. Yeni görüntüleme bitleri kapalı başladı; yönetici etkin tam erişimini sürdürür. İstemci/API 1.3.7-dev / API 1.7 ayrı klasöre kuruldu, kalıcı Windows görevi ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Profiller/tercihler korundu, geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı; masaüstü açılmadı. [Kurulum kaydı](docs/1_3_7_DEV_KURULUMU.md).

## 2026-10-31 — Ücret / IBAN görüntüleme yetkileri (kaynak)

Kullanıcı 1.3.6-dev'i doğruladı. 1.3.7-dev / hazırlanmış 118 / API 1.7: CanViewSalary ve CanViewIban ayrı ek hesap izinleri, kapalı non-admin varsayılanı, oturum iptali ve bit kontrolü eklendi. Personel/kart/geçmiş/özet/proje finans çıktıları API öncesinde filtrelenir; gizli alanlar metadata kaydında korunur. Ücret okuması ödeme, maliyet ve parasal raporlarda zorunlu; genel tablo/pano aktarımı yeniden adlandırmayla aşılmaması için iki okuma hakkı ve export ister. Taslak ve bekleyen ödeme görünümü korundu. 100–117 değiştirilmedi; canlı kurulum/kısayol korunarak denemeler atlandı. [Kapsam ve sınırlar](docs/UCRET_VE_IBAN_GORUNTULEME_YETKILERI.md).

## 2026-10-31 — 117 ve 1.3.6-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra yalnız 117 transaction içinde uygulandı; eski veri/audit içerikleri korundu, hesap oturumları yenilendi. Yeni grup geçişi tablosu boş başladı; personel grubu değiştirilmedi. İstemci/API 1.3.6-dev / API 1.6 ayrı klasöre kuruldu; kalıcı Windows görevi ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Profiller/tercihler korundu. Geçici bakım görevi kaldırıldı. İşlev denemeleri atlandı, masaüstü açılmadı. [Kurulum kaydı](docs/1_3_6_DEV_KURULUMU.md).

## 2026-10-31 — Tarihli personel grubu geçişi (kaynak)

Kullanıcı 1.3.5-dev'i doğruladı. 1.3.6-dev / hazırlanmış 117 / API 1.6: Genel ↔ Joker geçişi aynı PersonId, kapanan kaynak ve yeni hedef dönemiyle eklendi. Sıfır bakiye, tarihler, parasal geçmiş, kart/dönem sürümleri ve tekrar kimliği sunucuda korunur. Tek açık dönem ve eski grup dönemi korumaları, açık grup eşleşmeli kayıt bağlantıları ve kartta iki grup geçmişi/grup geçişleri sekmesi eklendi. Mevcut 100–116 betikleri değiştirilmedi. Canlı DB/kısayol korundu, denemeler atlandı. [Kapsam ve sınırlar](docs/TARIHLI_PERSONEL_GRUBU_GECISI.md).

## 2026-10-31 — 116 ve 1.3.5-dev yerel kurulumu

Doğrulanmış COPY_ONLY/CHECKSUM yedeğinden sonra 116 transaction içinde uygulandı; eski iş verileri/audit gövdeleri korundu, oturum nesilleri yenilendi. İlk uygulanmamış betikteki eksik GO nedeniyle geri alınan deneme düzeltildi; paketler yeniden üretildi. İstemci/API 1.3.5-dev ve API 1.5 kuruldu, kalıcı Windows görevi ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Profiller ve tercihler korundu. İşlev denemeleri atlandı; masaüstü açılmadı. [Kurulum ve yedek kaydı](docs/1_3_5_DEV_KURULUMU.md).

## 2026-10-31 — Audit veri minimizasyonu (kaynak)

1.3.4-dev kullanıcı tarafından doğrulandı. Aşama 2'nin hassas audit verisi dilimi: 1.3.5-dev / API 1.5 / hazırlanmış 116 şeması. Yeni audit gövdeleri SQL INSERT sınırında izin listesiyle maskelenir, alan değişimleri maskelemeden önce değerleri kopyalamadan çıkarılır. Eski audit değişmezliği korunur; her iki uygulama okuması güvenli görünümden gelir. Admin ve export kontrolleri korunur. Canlı DB/kurulum/kısayol değişmedi; denemeler atlandı. [Kapsam ve sınırlar](docs/ISLEM_GECMISI_VERI_MINIMIZASYONU.md).

## 2026-10-31 — 1.3.4-dev yerel kurulumu

Mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alınıp VERIFYONLY ve kapsam manifestiyle doğrulandı. Şema 115 değiştirilmeden 1.3.4-dev istemci ve API 1.4 ayrı klasöre kuruldu; API Windows görevi ve Muhasebe - Güncel kısayolu yeni pakete geçirildi. Bağlantı profilleri ve tercihler korundu. Paket bütünlüğü ve API başlangıcı kontrol edildi; işlev denemeleri atlandı, masaüstü açılmadı. [Kurulum kaydı](docs/1_3_4_DEV_KURULUMU.md).

Tarihler Türkiye saatine (Europe/Istanbul, UTC+03:00) göredir. Geliştirme günleri aşağıda belirtilir; Git commit tarihi değişikliklerin kaynak geçmişine kaydedildiği tarihtir. Geçmiş tarihlere ayrı commit üretilmedi.

## 31 Ekim 2026

1.3.4-dev kaynak dilimi: kalıcı PersonId ile açılan bütünleşik personel kartı eklendi. Çalışma dönemleri, dönem filtreli/paginalı atama, izin, izin değişikliği, hakediş ve ödeme defteri; tüm dönem arşiv olayları ve yöneticiye özel işlem geçmişi aynı ekranda toplandı. Ayrı sorgulu finans özeti ve dönem içi defter neti SQL'de hesaplanır. Ad/soyad/IBAN için bakiyeye/deftere dokunmayan sürümlü güncelleme, güncel izin düzenleme ve mevcut dönem/arşiv işlemlerine bağlantı eklendi. Genel/Joker, arama ve arşiv listelerinden kart açılır; menüde ortak kart araması vardır. Genel okuma/Edit/Admin/dışa aktarma ayrımı ve ProjectUser kapsam sınırı servis/API katmanında korunur. API 1.4, mevcut şema 115; yeni yükseltme betiği yok. Kurulu 1.3.3-dev değiştirilmedi, denemeler atlandı. [Kapsam ve kullanım](docs/BUTUNLESIK_PERSONEL_KARTI.md).

Kullanıcı 1.3.3-dev sürümünün düzgün çalıştığını bildirdi; bu çalışmada denemeler tekrarlanmadı.

Kullanıcı onayıyla doğrulanmış COPY_ONLY/CHECKSUM yedeği sonrası yalnız 115 proje yetkisi yükseltmesi uygulandı. Mevcut veri/audit içerik özetleri ve çalışma dönemi bağlantıları korundu, oturum nesilleri yenilendi. Yeni proje üyelik tablosu boş başlar; rol/sorumlu/izin ataması yapılmadı. 1.3.3-dev istemci/API paketleri manifest/SHA-256 doğrulamasıyla ayrı kullanıcı klasörüne kuruldu; aktif API protokolü 1.3 ve Muhasebe - Güncel kısayolu yeni sürüme geçirildi. LocalDB bakım sonrası zorlamadan durdurulup kalıcı API görevi bağlamında başlatıldı; HTTPS başlangıcı ve loopback süreç bağlaması doğrulandı. Bağlantı profilleri ve tercihler korundu. İşlev denemeleri atlandı; masaüstü açılmadı. [Kurulum kaydı](docs/1_3_3_DEV_KURULUMU.md).

1.3.3-dev kaynak dilimi: ayrı ProjectUser rolü, proje sorumlusu ve sürümlü AccountId/ProjectId üyelikleri eklendi. Kısıtlı rol için genel veri yolları kapatıldı; proje/ekip/atama ve maliyet sorguları servis/API katmanında kapsam kontrolü kullanır. Proje başına ekip/atama düzenleme, maliyet görüntüleme ve sunucuda üretilen CSV raporu izinleri ayrıldı. Yönetici için yetki ekranı, proje kullanıcısı için Projelerim ekranı eklendi. Eski hesap/yetki seçimi kontrolü, gerekçe/eski-yeni audit, tek etkin üyelik/tek sorumlu indeksleri ve kaldırılan yetki geçmişi koruması hazırlandı. Mevcut genel merkez rolleri ve ödeme tekrar koruması korundu; proje kullanıcısına ödeme yetkisi verilmedi. Şema 115 / API 1.3 kaynakta; canlı 1.3.2-dev/114 kurulumu değiştirilmedi. Denemeler atlandı. [Kapsam ve kullanım](docs/PROJE_SORUMLUSU_VE_YETKILER.md).

Kullanıcı 1.3.2-dev sürümünün düzgün çalıştığını bildirdi; bu çalışmada denemeler tekrarlanmadı.

Kullanıcı onayıyla doğrulanmış COPY_ONLY/CHECKSUM yedeği sonrası yalnız 114 çalışma dönemi yükseltmesi uygulandı. Eski veri/audit içerik özetleri korundu, oturum nesilleri yenilendi; mevcut bir kart bir bilinmeyen tarihli aktarım dönemine ve bir geçmiş bağlantısına bağlandı. 1.3.2-dev istemci/API paketleri manifest/SHA-256 doğrulamasıyla ayrı kullanıcı klasörüne kuruldu. Başlatıcı aktif kayıttaki API sürümünü (1.2) okuyacak şekilde güncellendi; ilk LocalDB başlangıç hatası zorlamasız örnek durdurma ve API görevi üzerinden yeniden başlatmayla çözüldü. HTTPS başlangıcı, loopback süreç bağlaması ve kısayol hedefi doğrulandı. Bağlantı profilleri ve tercihler korundu; işlev denemeleri atlandı, masaüstü açılmadı. [Kurulum kaydı](docs/1_3_2_DEV_KURULUMU.md).

1.3.2-dev kaynak dilimi: kalıcı PersonId altında ayrı EmploymentId ile işe giriş/ayrılış dönemleri, aynı dönemde arşivlemeyi geri alma ve sıfır bakiye koşullu yeni dönemle işe giriş eklendi. İzin/atama/hakediş tarihleri dönemle sınırlandırıldı; önceki hakedişin iptali yeni bakiyeyi değiştiremez. Eski kayıtlar tarih uydurulmadan aktarım dönemine bağlanır; geçmiş gövdeler ve kapanışlar yeniden yazılmaz. Gerekçeli/sürümlü tarih düzeltmesi, dönem bazlı defter ve API/masaüstü ekranı eklendi. Şema 114 ve API sözleşmesi 1.2 hazır; mevcut 1.3.1-dev/113 kurulumu değiştirilmedi. Denemeler atlandı. [Kullanım, geçiş ve sınırlar](docs/CALISMA_DONEMLERI.md).

Kullanıcı 1.3.1-dev sürümünün düzgün çalıştığını bildirdi; bu çalışma sırasında işlev denemeleri tekrarlanmadı.

Kullanıcı onayıyla mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alınıp VERIFYONLY ile doğrulandı; yalnız 113 proje/ekip/atama yükseltmesi uygulandı. Önceki iş/finans/audit verilerinin özetleri korundu, eski oturumlar yenilendi. Yeni tablolar boş; altı etkin trigger, dört güvenilir check, dört FK ve iki atama indeksi doğrulandı. 1.3.1-dev istemci/API paketleri SHA-256 manifestleriyle ayrı kullanıcı sürüm klasörüne kuruldu, otomatik başlangıç ve Muhasebe - Güncel kısayolu yeni sürüme geçirildi. İlk LocalDB başlangıç hatası sonrası başlatıcı HTTPS hazır olmayı bekleyecek ve API ömrü boyunca açık kalacak şekilde düzeltildi; görevde sınırlı yeniden deneme açıldı. Bağlantı/kapsam/sertifika ve tercihler korundu. İşlev/sihirbaz denemeleri atlandı, masaüstü açılmadı. [Kurulum ve bakım kaydı](docs/1_3_1_DEV_KURULUMU.md).

Yol haritası Aşama 2'nin ilk proje/ekip dilimi: proje saha/müşteri etiketi/plan tarihleri, sürümlü ekip düzenleme ve kalıcı PersonId ile tarihli asıl proje/ekip atamaları eklendi. Çakışma transaction/app-lock ile bütün projelerde denetlenir; eski personel/proje/ekip seçimi reddedilir. Gerekçeli iptal, silme/değiştirme koruması, eski-yeni audit ve API/masaüstü ekranı hazırlandı. 113 betiği kaynakta; canlı DB'ye uygulanmadı. Kaynak sürümü 1.3.1-dev, kurulu 1.3.0-dev korunuyor. Denemeler atlandı. [Kapsam ve açık kalan işler](docs/PROJE_EKIP_VE_TARIHLI_ATAMALAR.md).

Kullanıcı, kurulan 1.3.0-dev sürümünün düzgün çalıştığını bildirdi. Bu kullanıcı geri bildirimi kaydedildi; kurulum/işlev denemeleri bu çalışma sırasında tekrar yapılmadı.

Kullanıcı isteğiyle 1.3.0-dev istemci ve API ZIP'leri SHA-256/manifest doğrulaması sonrası kullanıcıya özel ayrı sürüm klasörüne kuruldu. Mevcut veritabanının COPY_ONLY/CHECKSUM yedeği doğrulandı; şema 112 ve kapsam korundu. Bağlantı/HTTPS sertifikası ve kullanıcı tercihleri değiştirilmedi; otomatik başlangıç aktif sürüm kaydını okuyacak şekilde yeni API'ye geçirildi. Muhasebe - Güncel kısayolu yeni istemciyi açar. İlk geçişlerde eski sürüme geri dönüş çalıştı; süreç kapanış beklemesi ve proxy kullanmayan localhost canlılık kontrolüyle geçiş tamamlandı. İşlev/kurulum sihirbazı denemeleri atlandı; masaüstü açılmadı. Özel yedek ve kurulum kanıtları Git dışındaki artifacts/analysis/install-1.3.0-dev/ altındadır. [Kurulum durumu](docs/1_3_0_DEV_KURULUMU.md).

## 30 Ekim 2026

F14 kaynak uygulaması: ortak Directory.Build.props ile 1.3.0-dev ürün/1.3.0.0 dosya sürümü; commit/kaynak durumu/şema metadatası ve Hakkında/API tanılama bilgisi eklendi. Sürümlü masaüstü+kurulum/API ZIP'leri, dosya manifesti, provenans ve SHA256 tek paketleme komutunda üretiliyor. Etiket/commit/temiz kaynak/sürüm kontrolü sonrası mevcut CI'den taslak Release hazırlığı eklendi; mevcut assets üzerine yazılmaz. Kullanıcı başına ayrı klasöre kuran, mevcut bağlantı/API/yeni LocalDB seçenekli ve eski tercih/kısayolu koruyan sihirbaz hazırlandı. Denemeler atlandı; canlı kurulum, kısayol, tercih veya DB değiştirilmedi. [Kullanım ve doğrulanmamış sınırlar](docs/SURUMLU_DAGITIM_VE_KURULUM.md).

Kullanıcı, bu bilgisayarda kurulan merkezi API bağlantısının düzgün çalıştığını bildirdi. Bu geri bildirim F13 yerel kullanım doğrulamasıdır; çok bilgisayar/çok worker veya ağ kesintisi denemesi yapılmış sayılmadı.

Kullanıcı isteğiyle API bu bilgisayara kuruldu: mevcut LocalDB korunarak açık `AllowUserLocalDb` seçeneğiyle yalnızca localhost Production dinleyicisi, mevcut kullanıcıya özel güvenilir HTTPS sertifikası ve Windows oturum açılış görevi hazırlandı. Şema değiştirilmeden doğrulanmış veritabanı yedeği alındı. API başlangıcına mevcut güvenli LocalDbRuntime başlatma adımı eklendi. Masaüstü tercihi API'ye geçirildi, Muhasebe - Güncel kısayolu yeni pakete yönlendirildi; önceki tercihler/kısayol yedeklendi. Sertifika doğrulaması açık HTTPS canlılık yanıtı ve yalnızca loopback bağlaması doğrulandı. Giriş/işlev denemeleri atlandı, masaüstü açılmadı. [Kurulum ve geri dönüş kayıtları](docs/YEREL_API_KURULUMU.md).

F13 masaüstü bağlantısı: girişte yerel/merkezi HTTPS API seçimi ve oturuma bağlı bellek içi HTTP adaptörü eklendi. Personel/izin/hesap/parola/ödeme/finans/kapanış/audit/rapor akışları sunucuya yönlendirildi. Canlı yetki ve kilit kontrolü, token iptali, API 1.1 kontrolü, veritabanı kapsam sabitlemesi ve aynı kalıcı RequestId ile ödeme kurtarma korunur. Ağ hatasında SQL'e dönüş veya otomatik ödeme tekrarı yoktur. Bekleyen ödeme/diğer uygulama örneği varken bağlantı değişimi engellenir. Sunucu rapor üretimi ve ayrı bakım kapsamı belgelendi. Denemeler atlandı; canlı DB, kısayol ve mevcut kullanıcı tercihi değiştirilmedi. [Kullanım ve geçiş sınırları](docs/MASAUSTU_MERKEZI_API_BAGLANTISI.md).

F13 ilk aşama, kullanıcı tercihiyle yalnızca merkezi API altyapısı: ayrı .NET sözleşmeleri ve Windows ASP.NET Core HTTPS sunucusu eklendi. Kimlik doğrulama/süreli hash'li token deposu, canlı hesap/sürüm/izin kontrolü, personel/izin/ödeme/proje finansı/kapanış/hesap uç noktaları mevcut transaction servislerini kullanır. Sunucu profili açık seçilir; şema yalnızca okunarak kontrol edilir, otomatik yükseltilmez. Windows x64 yayın çıktısı ve kurulum/sözleşme belgesi hazırlandı. Masaüstü HTTP bağlantısı, dağıtım ve çok cihaz denemeleri sonraki kapsamdadır. Canlı DB/kısayol değişmedi; denemeler atlandı. [Kapsam ve işletim sınırları](docs/MERKEZI_API_ALTYAPISI.md).

Kullanıcı uygulamayı kendisi açıp otomatik kilit ve kaydedilmemiş değişiklik uyarılarının düzgün çalıştığını bildirdi. Bu geri bildirim, önceki otomasyon başlatma hatasından ayrı kaydedildi; yüksek DPI/çoklu monitör saha doğrulaması yapılmış sayılmadı.

Kullanıcı isteğiyle Muhasebe - Güncel kısayolu otomatik kilit/değişiklik uyarıları paketine yönlendirildi; bağlantı ve paket hash'i korundu, önceki kısayol yedeklendi. Windows otomasyon araçları başlatılamadığı için uygulama açılamadı (kernel assets / os error 3). Denemeler yapılmadı.

F12 ilk bölüm: 10 dakika uygulama etkinliği yoksa otomatik kilit, manuel kilit ve aynı hesabın mevcut sınırlamalı parolasıyla yeniden doğrulama eklendi. Açık formlar korunur; gerçek kayıt işlemi iptal edilmez, UI devamı kilidi bekler. Kaydedilmemiş alanlar grup bazında takip edilir; gezinme/kapatma/seçim/sayfa/temizleme/yenileme uyarıları eklendi. Parolalar taslaktan çıkarıldı. SQL yükseltmesi gerekmez; canlı DB ve kısayol değiştirilmedi. Denemeler atlandı, Release 0 uyarı / 0 hata. [Kullanım ve doğrulama sınırları](docs/OTOMATIK_KILIT_VE_DEGISIKLIK_UYARISI.md).

Kullanıcı onayıyla mevcut veritabanının doğrulanmış v2 yedeği alındı; yalnızca 112 kalıcı kapanış yükseltmesi uygulandı. İş/finans/audit verileri korundu; beklenen oturum yenilemesi yapıldı. Boş kapanış tablosu, üç etkin koruma/audit trigger'ı, hash/JSON/tutar kısıtları, benzersiz gün, iptal kaynak bağlantısı ve indeksler doğrulandı. Eski hareketlere kaynak bağlantısı veya geçmiş kapanış uydurulmadı. Masaüstü kısayolu 112 uyumlu pakete yönlendirildi. Denemeler atlandı; uygulama başlatılmadı. Özel yedek/kanıtlar Git dışındaki `artifacts/analysis/live-upgrade-112/` altındadır.

F11: dışa aktarmada rapor/kapsam ve sütun seçimi eklendi; çok tablolu ekranlar ilk tabloya bağlanmıyor. Genel Durum tüm-veri özeti, güncel fiş/önizleme ve saklanan kapanış özeti/hareketleri ayrı raporlar. Excel kapsam bilgisi, UTC zaman koruması ve rapor seçiminin audit'i eklendi. 112 ile tamamlanmış gün için immutable snapshot, SHA-256, kapalı güne nakit hareketi koruması ve sonraki güne bağlı iptal modeli hazırlandı. Canlı DB yükseltilmedi; denemeler atlandı. Release 0 uyarı / 0 hata. [Kullanım ve sınırlar](docs/SECILEBILIR_RAPOR_VE_KALICI_KAPANIS.md).

Kullanıcı onayıyla canlı veritabanının CHECKSUM/VERIFYONLY ve v2 manifestiyle doğrulanmış yedeği alındı; yalnızca 111 personel sorgu indeksleri yükseltmesi uygulandı. Dört indeksin anahtar/include/filtre tanımları doğrulandı; tüm mevcut veri, rowversion ve oturum nesilleri korundu. Masaüstü kısayolu 111 uyumlu sayfalama paketine yönlendirildi. Denemeler/hız ölçümleri atlandı; uygulama açılmadı. Özel yedek/kanıtlar Git dışındaki `artifacts/analysis/live-upgrade-111/` altındadır.

F10: personel ekranları, arama, ödeme listesi/defteri, izin planı, arşiv ve finans personel seçimine SQL sayfalama eklendi. Çoklu izin sayfa sınırını bölmez; typed TC araması sütun CONVERT'ünü kaldırır. Genel Durum sayım/toplam/ortalama/izinli sayısını SQL'den alır; defter yürüyen bakiyesi sayfalamadan önce hesaplanır. Dışa aktarım açık sayfa kapsamını belirtir. 111 indeks betiği hazırlandı; canlı DB yükseltilmedi. Denemeler kullanıcı isteğiyle atlandı, Release 0 uyarı / 0 hata. [Kullanım ve sınırlar](docs/PERSONEL_SAYFALAMA_VE_SQL_OZETLERI.md).

Kullanıcı onayıyla canlı veritabanının doğrulanmış v2 yedeği alındı ve yalnızca 110 proje finansı yükseltmesi uygulandı. Önceki bakiyeler, ödemeler, kimlikler ve geçmiş içerikleri korundu; yeni tablolar boş başladı, eski defter verilerine sınıflandırma/proje uydurulmadı. Kısıtlar/audit korumaları ve şema kaydı doğrulandı. Masaüstü kısayolu 110 uyumlu pakete yönlendirildi. Test/ekran denemeleri atlandı; uygulama başlatılmadı. Özel kanıtlar Git dışındaki `artifacts/analysis/live-upgrade-110/` altındadır.

F09 ilk aşama: manuel hakediş, bağımsız diğer proje gideri, proje maliyet özeti ve ayrı avans türü eklendi. Hakediş/bakiye/defter/audit atomik; belge numarası tekrar korumalı, iptal sürüm kontrollü. Avans mevcut şifreli ödeme günlüğünü kullanır ve maliyete yeniden eklenmez. Eski bakiye/geçmiş yeniden sınıflandırılmaz. 110 betiği ve yerel paket hazırlandı; denemeler kullanıcı isteğiyle atlandı. [Kapsam ve kullanım](docs/HAKEDIS_AVANS_PROJE_MALIYETI.md).

## 29 Ekim 2026

Kullanıcı onayıyla canlı veritabanının doğrulanmış v2 yedeği alındı ve 109 ayrıntılı işlem geçmişi yükseltmesi uygulandı. İlk uygulamada yeni sütunlara bağlı kısıtların SQL blok sırası hatası işlemi geri aldırdı; veri özetlerinin değişmediği doğrulandı, GO sınırları düzeltildi ve yeni yedek sonrası yükseltme tamamlandı. Mevcut veri/audit içeriği korundu; altı etkin trigger, JSON kısıtları ve kalıcı hesap kimlikleri doğrulandı, eski oturumlar yenilendi. Masaüstü kısayolu 109 uyumlu pakete yönlendirildi. Denemeler atlandı, uygulama açılmadı. Özel kanıtlar/yedekler Git dışındaki `artifacts/analysis/live-upgrade-109/` altındadır.

Ayrıntılı işlem geçmişi: 109 betiği ile personel/izin/ödeme/hesap değişiklikleri kayıt bazında eski-yeni JSON, UTC zaman, aktör/kalıcı hesap kimliği, gerekçe, SQL kaynağı ve transaction bağıyla saklanıyor. İş ekranlarına gerekçe girişi ve yöneticiye sayfalı geçmiş ekranı eklendi. Parola/hash görüntülere alınmaz; ödeme gerekçesi bekleyen günlükte korunur. AuditLog UPDATE/DELETE korumalıdır. Canlı DB yükseltilmedi; denemeler kullanıcı isteğiyle atlandı. [Kapsam ve kullanım](docs/AYRINTILI_ISLEM_GECMISI.md).

Kullanıcı onayıyla canlı veritabanının CHECKSUM/VERIFYONLY ve v2 kimlik manifestiyle doğrulanan yedeği alındı; yalnızca 108 yetki ayrımı yükseltmesi uygulandı. İş verileri ve hesap bilgileri korundu; oturum nesilleri beklendiği gibi yenilendi, iki ayrı izin alanı ve kapalı varsayılanları doğrulandı. Muhasebe - Güncel kısayolu 108 uyumlu yerel pakete yönlendirildi. Denemeler atlandı ve uygulama başlatılmadı. Özel yedek/kanıtlar `artifacts/analysis/live-upgrade-108/` altında Git dışında tutulur.

Ödeme ve kişisel veri dışa aktarma yetkileri personel düzenleme rolünden ayrıldı. Yönetici hesaplarına Ek yetkiler ekranı, canlı servis kontrolleri, yetki değişiminde oturum iptali, bakiye değişiminde ödeme izni ve Excel/CSV/pano/yazdırma korumaları eklendi. 108 yükseltmesi hazırlandı; canlı DB'ye uygulanmadı ve kısayol değiştirilmedi. Kullanıcı isteğiyle denemeler atlandı; çözüm Release derlemesi 0 uyarı / 0 hata. [Kullanım, dağıtım ve sınırlar](docs/AYRI_ODEME_VE_DISA_AKTARMA_YETKILERI.md).

Kullanıcı onayıyla canlı veritabanının doğrulanmış v2 yedeği alındı ve yalnızca 107 izin geçmişi yükseltmesi uygulandı. Mevcut veriler/kimlikler korundu; migration kaydı, geçmiş yapısı ve etkin korumalar doğrulandı. Muhasebe - Güncel kısayolu 107 uyumlu yerel pakete yönlendirildi. Test/ekran denemesi yapılmadı; uygulama başlatılmadı. Özel yedek/kanıtlar `artifacts/analysis/live-upgrade-107/` altında Git dışında tutulur.

İzin değişiklik geçmişi: 107 yükseltmesiyle mevcut izinlerin başlangıç görüntüsü ve yeni/tarih değişikliği/silme olayları için kalıcı PersonId bağlantılı LeaveHistory eklendi. Eski-yeni tarihler, sürümler, UTC zaman ve kaydeden saklanıyor; geçmiş UPDATE/DELETE'e kapalı. Genel/Toplu Plan ve eski kayıt yolu aynı transaction korumasını kullanıyor; arşiv ekranına İzin değişiklikleri sekmesi eklendi. Kullanıcı isteğiyle denemeler atlandı, Release derlemesi başarılı. 107 gerçek DB'ye uygulanmadı. [Kapsam ve kullanım](docs/IZIN_DEGISIKLIK_GECMISI.md).

Kullanıcı onayıyla canlı veritabanının doğrulanmış v2 güvenlik yedeği alındı ve yalnızca 106 personel kimliği/arşiv yükseltmesi uygulandı. Mevcut içerik özetleri, kimlik bağlantıları ve şema kaydı kontrol edildi; veriler korundu. Muhasebe - Güncel kısayolu 106 uyumlu yerel pakete yönlendirildi. Test/ekran denemesi yapılmadı, uygulama başlatılmadı. Özel yedek/kanıtlar Git dışındaki `artifacts/analysis/live-upgrade-106/` klasöründedir.

Personel kimliği ve arşivleme: 106 yükseltmesiyle değişmez PersonId, izin/ödeme bağlantıları ve arşiv olayları eklendi. Silme yerine kart/izin/ödeme/bakiyeyi koruyan arşivleme, aynı kimlikle tekrar etkinleştirme ve Genel/Joker sağ tık menüsünde geçmiş ekranı var. Yeni ödeme günlüğü kimlikli biçim 3; eski dosyalar yalnızca kayıtlı sonucu kurtarabilir. Gerçek DB yükseltilmedi. Önceki taleple testler atlandı; Release derlemesi başarılı. [Kullanım ve sınırlar](docs/PERSONEL_KIMLIGI_VE_ARSIV.md).

Yedek kapsamı artık yalnızca katalog adına bağlı değil: SQL FamilyGUID, kaynak sunucu ve fiziksel dosya konumu ile v2 kapsamı oluşturuluyor. SQL yedek başlığı ve SHA-256 manifesti restore/aynalamada birlikte doğrulanıyor; günlük kontrol, saklama temizliği ve şema güvenlik kopyaları aynı kapsamı kullanıyor. Eski/kimliği belirsiz dosyalar otomatik silinmiyor ve restore hedefi olarak kabul edilmiyor. Yeni migration gerekmez. Önceki kullanıcı talebiyle testler çalıştırılmadı; Release derlemesi 0 uyarı / 0 hata. [Kullanım ve eski yedek geçişi](docs/YEDEK_KIMLIGI_VE_KAPSAM.md).

Personel verisi doğrulaması: Türkiye IBAN'ı için 26 karakter/yapı/rezerv alanı ve MOD-97 kontrolü eklendi. Ad/soyad kenar boşlukları ve IBAN gösterimi normalize edilerek aynı model servis ve eski kayıt yolunda saklanıyor. Genel/Joker listelerine sağ tıkla açılan, mevcut kayıtları değiştirmeyen veri düzeltme listesi eklendi. Yeni SQL migration yok. Kullanıcı talebiyle test/ekran/DB denemeleri atlandı; yalnızca Release derlemesi 0 uyarı / 0 hata. [Kapsam ve kullanım](docs/PERSONEL_VERI_DOGRULAMA.md).

Yayımlanan yerel paket DLL'iyle, ayrı test veritabanında şema kontrolü/yükseltme hatası sonrası oturum koruması tekrar denendi: **46 kontrol başarılı**; tekrarlı restore ve şema onarımı eski oturumları geçerli kılmadı. Geçici DB/motor temizlendi. Windows otomasyon aracı başlatılamadığı için bu denemede gerçek uygulamayı kapatma/açma yapılamadı. [Paket denemesi ve sınırları](docs/GERI_YUKLEME_OTURUM_KORUMASI.md).

Tam doğrulama: **649 başarılı, 0 başarısız, 0 atlanan test**; Release 0 uyarı / 0 hata. Yeni üç geri yükleme hata vakası, tekrarlı restore ve eski yönetici/salt okunur oturumlarının servis erişimini kapsar. Geçici motor temizlendi. .NET 10.0.13 yerel test paketi `artifacts/releases/restore-session-revocation/` altında hazırlandı; bağlantı ayarı korundu, gerçek veritabanına dokunulmadı.

Geri yüklenen hesapların oturum nesilleri artık MULTI_USER ve şema kontrolünden önce, ayrı transaction ile yenileniyor. Uyumsuz şema, yükseltme geri alımı veya audit hatası eski oturumları yeniden geçerli kılamıyor. Aynı yedeğin tekrarlı geri yüklenmesi de yeni nesil üretir. Oturum iptali başarısızsa veritabanı OFFLINE kapatılır. SQL migration değiştirilmedi. [İşlem sırası, regresyonlar ve kapsam sınırları](docs/GERI_YUKLEME_OTURUM_KORUMASI.md).

## 28 Ekim 2026

Ödeme seçim koruması yerel paket DLL'iyle gerçek Ödemeler ekranında, yalnızca izole test veritabanında denendi: Genel/Joker eski seçim reddi, güncel ödeme, aynı RequestId tekrarı, yeniden ekran açma, sonuç onayı sonrası bilinçli yeni ödeme ve kayıp commit yanıtı kurtarma dahil **11 kontrol başarılı**. Geçici DB/motor/günlük temizlendi. Kullanıcı isteğiyle masaüstü kısayolu güncel yerel pakete yönlendirildi, bağlantı ayarı korundu ve uygulama girişe hazır açıldı. [Deneme sonuçları](docs/ODEME_SECIM_SURUM_KORUMASI.md).

Ödemeler ekranında özgün TC/grup/RecordVersion onaydan önce donduruluyor; hazırlama, gönderim ve SQL bakiye azaltımı aynı seçim sürümünü doğruluyor. Eski seçimin yeniden oluşturulan personeli etkilemesi engellendi. Yeni şifreli ödeme günlüğü sürümü taşır; eski sürümsüz istek yalnızca kayıtlı sonucu kurtarabilir, yeni ödeme yazamaz. Kaydedilmiş/iptal edilmiş işlem, personel sonradan değişse veya silinse de RequestId ile tanınır. Doğrudan servis yolu da yeni ödeme için sürüm ister. **646 test başarılı, 0 başarısız, 0 atlanan**; 36 yeni senaryo, Release 0 uyarı / 0 hata. Geçici test motoru temizlendi; yeni SQL yükseltmesi gerekmez. Yerel geliştirme; [davranış ve deneme adımları](docs/ODEME_SECIM_SURUM_KORUMASI.md).

Responsive arayüzleri ve önceki kayıt/sürüm, rapor ve yedek aynalama düzeltmelerini içeren **v1.2.0 Windows x64 paketi** GitHub Releases'te yayımlandı. Paket, `0e58031` commit'inin 610 test başarılı / 0 başarısız Actions çalışmasından alındı; SHA256 özeti aynı kaldı. ZIP ve SHA256SUMS.txt dosyalarına herkese açık erişim doğrulandı. README'deki indirme/sürüm/özet bağlantıları ve paketleme komutları v1.2.0'a güncellendi; güncelleme, ilk kurulum, kısayol ve bütünlük doğrulama adımları açıklandı. [Sürüm notları](https://github.com/frknylmzkng/AccountingApp/releases/tag/v1.2.0).

## 27 Ekim 2026

GitHub Actions'taki responsive test hataları düzeltildi. İlk çalışmada 42 kontrol, sanal ekranın pencere izleme boyutu sınırı yüzünden geniş düzene ulaşamıyordu. Testlere ortak, gerçek boyutu doğrulayan pencere yardımcısı ve büyük boyuttan küçülme regresyon testi eklendi; uygulama kodu veya test beklentileri gevşetilmedi. Güncel yerel tam paket: **610 başarılı, 0 başarısız, 0 atlanan test**, Release 0 uyarı / 0 hata; geçici LocalDB motoru temizlendi. Kanıt: artifacts/analysis/master-20261027-202455-754/. [Neden ve düzeltme](docs/CI_RESPONSIVE_TESTLER.md).

Responsive arayüzlerin toplu kaynak gönderimi hazırlandı: 12 ekran/akış, regresyon testleri, örnek verili önizlemeler ve tarihli belgeler toplandı. Önceki onaylı sürüm/rapor/aynalama düzeltmeleri ayrı altyapı commit'inde tutuldu. Güncel kaynakla tam doğrulama: **609 test başarılı, 0 başarısız, 0 atlanan**, Release 0 uyarı / 0 hata; entegrasyon ve geri yükleme testlerinin geçici LocalDB motoru temizlendi. Yerel paketler, kişisel bağlantı ayarları ve kullanıcı veritabanı Git kapsamı dışında kaldı. [Ekranlar ve gönderim kapsamı](docs/RESPONSIVE_ARAYUZLER.md). Mevcut v1.1.0 Release ZIP'i değiştirilmedi.

Parola Sıfırlama ve Geçici Parola sonuç pencereleri pencereye uyarlanıyor: yeniden boyutlandırma/büyütme, yan yana/alt alta bölümler, satır kıran uzun hesap adları ve açıklamalar, ortalanmış içerik ve daima görünür işlem düğmeleri eklendi. Yönetici parolası/karakter seçimi, maskeleme, özgün hedef hesap, işlem kilitleri; sonuç penceresinin süre/salt okunurluk/gösterme tercihi korunuyor. **364 veritabanısız test**, üç temada boşta/kilitli sıfırlama ve maskeli/gösterilen örnek sonuç için **936 boyutlandırma durumu**, yayımlanan DLL ile **42 görsel kontrol** başarılı. Gerçek parola sıfırlanmadı. Paket: artifacts/releases/password-reset-adaptive/. [Önizlemeler ve deneme adımları](docs/PAROLA_SIFIRLAMA_RESPONSIVE.md).

Parola Değiştirme pencereye uyarlanıyor: bilgi/kurallar bölümleri yan yana veya alt alta; alan açıklamaları sola/üste geçiyor. Form ve işlem düğmesi birlikte ortalanıyor; kısa pencerede alanlar kayarken düğme görünür kalıyor. Bölüm geçişleri, satır kıran açıklama ve doğal Tab sırası eklendi. Girilen parolalar/boşluklar/karakter seçimi, maskeleme, uzunluk sınırı, geçici parola kısıtı ve işlem kilidi korunuyor. **352 veritabanısız test**, üç rol × iki oturum türünde boşta/kilitli **624 boyutlandırma durumu** ve yayımlanan DLL ile **42 görsel kontrol** başarılı. Gerçek parola değiştirilmedi. Paket: artifacts/releases/password-change-adaptive/. [Önizlemeler ve deneme adımları](docs/PAROLA_DEGISTIRME_RESPONSIVE.md).

İzin Planlama pencereye uyarlanıyor: tarihler iki/tek sütuna, süre bilgisi aynı/ayrı sıraya geçiyor; sabit boyutlu onay düğmesi, satır kıran açıklama, bölüm geçişleri ve dikey kaydırma eklendi. Personel listesi kalan yüksekliği ve genişliği paylaşıyor. Girilmiş tarihler, sıralanmış çoklu seçim, tekil kişi sayımı, özgün personel/izin sürümleri ve rol kısıtları korunuyor. **346 veritabanısız test**, üç rol × üç tarih durumunda **468 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Gerçek izin kaydı oluşturulmadı. Paket: artifacts/releases/leave-plan-adaptive/. [Önizlemeler ve deneme adımları](docs/IZIN_PLANLAMA_RESPONSIVE.md).

Ayarlar pencereye uyarlanıyor: bağlantı, yedekleme, görünüm ve durum bölümleri iki/tek sütuna; alan açıklamaları sola/üste, tema seçenekleri yan yana/alt alta geçiyor. İşlem düğmeleri dört/iki sütunda, üst bölüm geçişleri sabit; uzun durum metni değiştiğinde yeniden ölçülüyor. Kaydedilmemiş yollar, tema seçimleri, durum bilgisi ve etkin/pasif kontroller korunuyor. **337 veritabanısız test**, yönetici oturumunda üç tema × üç durum metniyle **468 boyutlandırma durumu**, iki yetkisiz erişim kontrolü ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Gerçek ayar/yedek değiştirilmedi. Paket: artifacts/releases/settings-adaptive/. [Önizlemeler ve deneme adımları](docs/AYARLAR_RESPONSIVE.md).

Ödemeler pencereye uyarlanıyor: ödeme/günlük kapanış bölümleri genişliğe göre yan yana veya alt alta; personel/hesap defteri çok geniş alanda yan yana yerleşiyor. Sabit bölüm geçişleri, satır kıran sonuç açıklaması, dikey kaydırma ve alanı paylaşan tablo sütunları eklendi. Girilen tutarlar, tarih/fiş seçeneği, özgün personel/ödeme seçimi ve belirsiz ödeme kilitleri korunuyor. **326 veritabanısız test**, üç rol × beş ödeme durumunda **780 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Gerçek ödeme/iptal/yazdırma yapılmadı. Paket: artifacts/releases/payments-adaptive/. [Önizlemeler ve deneme adımları](docs/ODEMELER_RESPONSIVE.md).

Yönetici pencereye uyarlanıyor: yeni hesap alanları 1–3 sütuna; hesap ekleme/işlem bölümleri yan yana veya alt alta geçiyor. Sabit bölüm geçişleri, düğme yükseklikleri, dikey kaydırma ve alanı paylaşan hesap sütunları eklendi. Girilmiş bilgiler, parola maskesi, özgün hesap seçimi/sürümü ve yetki korumaları sürüyor. **311 veritabanısız test**, yönetici oturumunda yeni hesabın üç yetki seçeneğiyle **156 boyutlandırma durumu**, iki yetkisiz erişim kontrolü ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/admin-adaptive/. [Önizlemeler ve deneme adımları](docs/YONETICI_RESPONSIVE.md).

İletişim pencereye uyarlanıyor: kartlar geniş alanda yan yana, dar alanda alt alta; sınırlı içerik genişliği, ortalama, sabit düğme yüksekliği ve dikey kaydırma eklendi. Telefonlar salt okunur/seçilebilir kalıyor; kopyalama başarı/hata bilgisi satır kırabilen alt alanda da gösteriliyor. **306 veritabanısız test**, üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Kopyalama testleri yerel taklit hedefle yapıldı; gerçek panoya dokunulmadı. Paket: artifacts/releases/contact-adaptive/. [Önizlemeler ve deneme adımları](docs/ILETISIM_RESPONSIVE.md).

Genel Durum pencereye uyarlanıyor: sayım kartları dört/iki sütuna, bakiye kartları yan yana/alt alta düzene geçiyor; sabit bölüm geçişleri, satır kıran izin açıklaması ve alanı paylaşan tablo sütunları eklendi. Büyük tutarlarda metin boyutu ayarlanıyor; toplamlar, hassasiyet, izin satırları ve özgün seçim korunuyor. **303 veritabanısız test**, üç rolde **156 boyutlandırma durumu**, uç tutar/tema için **30 durum** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/summary-adaptive/. [Önizlemeler ve deneme adımları](docs/GENEL_DURUM_RESPONSIVE.md).

Kişi Arama pencereye uyarlanıyor: filtreler 1–3 sütuna geçiyor; sabit yüksekliğe sahip arama/temizleme düğmeleri, bölüm geçişleri, dikey kaydırma ve alanı paylaşan sonuç sütunları eklendi. Boyut değişiminde filtreler, sonuç tablosu ve özgün seçim/sürüm korunuyor; yeni sorgu çalışmıyor. Enter/F2/F5 ve salt okunur rol/oturum akışı sürüyor. **299 veritabanısız test**, üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/search-adaptive/. [Önizlemeler ve deneme adımları](docs/KISI_ARAMA_RESPONSIVE.md).

Joker Personel pencereye uyarlanıyor: alanlar 1–3 sütuna, dar alanda işlem düğmeleri iki sıraya geçiyor; sabit bölüm geçişleri, dikey kaydırma ve alanı paylaşan tablo sütunları eklendi. Genel Personel ile ortak PersonnelEditor kullanılıyor; özgün TC/sürüm, sıralanmış seçim, kaydedilmemiş alanlar ve rol korumaları korunuyor. **296 veritabanısız test**, Joker için üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/joker-adaptive/. [Önizlemeler ve deneme adımları](docs/JOKER_PERSONEL_RESPONSIVE.md).

Genel Personel ekranı pencereye uyarlanıyor: bilgi/izin bölümleri geniş alanda yan yana, dar alanda alt alta; personel alanları 1–3 sütun, dar alanda iki sıralı işlem düğmeleri. Sabit bölüm geçişleri, dikey kaydırma ve alanı paylaşan tablo sütunları eklendi. Yeniden boyutlandırmada kaydedilmemiş alanlar, izin tarihleri, özgün seçim/sürümler ve rol korumaları sürüyor. **293 veritabanısız test**, yeni üç rolde **156 boyutlandırma durumu** ve yayımlanan DLL ile **21 görsel kontrol** başarılı. Paket: artifacts/releases/general-adaptive/. [Önizlemeler ve deneme adımları](docs/GENEL_PERSONEL_RESPONSIVE.md).

Ana ekran gerçek uyarlanabilir yerleşime geçirildi: işlem kartları kullanılabilir alana göre 1–4 sütuna geçiyor; dar alanda ağaç menü gizleniyor, dikey kaydırma bütün işlemleri erişilebilir tutuyor. Başlık/çıkış sabit, sığan içerik ortalanmış; roller ve klavye erişimi korundu. **290 veritabanısız test**, 282 boyutlandırma durumu ve yayımlanan yerel DLL ile **21 rol/boyut görsel kontrolü** başarılı. Yeni paket: artifacts/releases/dashboard-adaptive/. [Önizlemeler ve deneme adımları](docs/ANA_EKRAN_TAM_EKRAN_DUZENI.md).

Ana ekranın büyük pencere düzeni yenilendi: sol menü ve işlem grupları ortak bir alanda ortalanıyor; gruplar pencereyle genişliyor, eşit sütunlar/aralıklar ve sabit 40 piksel mantıksal düğme yüksekliği kullanılıyor. Geniş ekranlarda kontrol alanı sınırlandırıldı; küçük pencere ve mevcut rol/klavye/gezinti davranışı korundu. 287 veritabanısız test ve 12 rol/boyut yerleşim kontrolü başarılı; Release 0 uyarı / 0 hata. Gerçek veriler ve açık uygulama değiştirilmedi. [Önizlemeler ve ayrıntılar](docs/ANA_EKRAN_TAM_EKRAN_DUZENI.md). Kanıt: artifacts/analysis/master-20261027-083049-321/ ve artifacts/analysis/dashboard-responsive/.
## 26 Ekim 2026

Kullanıcı isteğiyle güncel yerel aynalama paketi açıldı ve Muhasebe - Güncel kısayolu yönlendirildi. Gerçek Ayarlar formunda, ayrı veritabanı ve test klasörleriyle 6 kontrol başarılı: başarısız aynalamada yerel yedek korunması, düğmeden yeniden deneme, otomatik hata/onarım, durum bilgisinin F5 ile güncellenmesi ve yinelenen yedek/audit oluşmaması. Tek ana yedek, değişmeyen yerel doğrulama zamanı ve kaynak/hedef SHA256 eşitliği doğrulandı. Otomatik üretim kontrol yolu doğrudan çağrıldı; fiziksel USB çıkarılmadı. Gerçek kayıtlar ve kullanıcı tercihleri kullanılmadı/değiştirilmedi. Geçici veritabanı/motor temizlendi; güncel uygulama açık bırakıldı. Kanıt: artifacts/analysis/mirror-live-ui/. [Ayrıntılar](docs/YEDEK_AYNALAMA_YENIDEN_DENEME.md).

Başarısız yedek aynalaması, 24 saatlik yerel yedek kontrolünden bağımsız yeniden deneniyor. Eksik/bozuk hedef aynı doğrulanmış kaynakla onarılıyor; kalıcı aynalama durumu, SHA256 doğrulaması ve atomik kopyalama eklendi. Yerel başarı/aynalama audit'i ayrıldı; Ayarlar'a durum ve Aynalamayı yeniden dene düğmesi, otomatik kontrole doğru kısmi başarı bildirimi eklendi. Aynalanan eski dosya saklama temizliğinde korunuyor. **532 test başarılı, 0 başarısız, 0 atlanan**; 23 test durumu eklendi, Release 0 uyarı / 0 hata. Yeni SQL betiği yok; gerçek veritabanı, açık uygulama ve kısayol değiştirilmedi. [Ayrıntılar ve deneme adımları](docs/YEDEK_AYNALAMA_YENIDEN_DENEME.md). Kanıt: artifacts/analysis/master-20261026-195505-188/.

Güncel yerel rapor paketi açıldı ve Muhasebe - Güncel kısayolu yönlendirildi. Gerçek Ödemeler formunda F5 yapılmadan, ayrı test veritabanında 6 fiş/kapanış raporu (A4/80 mm) ve veri değişmeme kontrolü başarılı. Kapanış toplamları 45 → 65 → 20 TL olarak güncellendi; 30 TL eski fiş seçimi iptal durumunu gösterdi. Geçici test ortamları temizlendi. Standart modal fiş önizlemesi gözlendi; otomasyonun pencere hedefleme kısıtı sonrası kalan önizlemeler aynı paket çizimiyle modeless test penceresinde doğrulandı. Gerçek kayıtlar kullanılmadı, fiziksel çıktı gönderilmedi. Ayrıntı ve kanıt: docs/FIS_VE_KAPANIS_GUNCEL_VERI.md, artifacts/analysis/payment-report-live-ui/.

Ödeme fişi RequestId ile yeniden okunuyor; günlük kapanış açık ekrandaki eski tablodan değil seçilen günün güncel veritabanı verisinden üretiliyor. Yetki/veri/zaman aynı Serializable transaction içinde; raporda veri zamanı ve oluşturan kullanıcı yer alıyor. Sonradan iptalin özgün günün güncel toplamından çıkarılması açıkça belirtiliyor. Geçmiş fişe güncel veya yeniden oluşturulan personelin IBAN/bakiye/izin bilgileri eklenmiyor. **509 test başarılı, 0 başarısız, 0 atlanan**, Release 0 uyarı / 0 hata; 19 rapor testi eklendi. Yeni şema yok; gerçek veritabanı ve açık uygulama değiştirilmedi. [Ayrıntılar ve deneme adımları](docs/FIS_VE_KAPANIS_GUNCEL_VERI.md). Kanıt: `artifacts/analysis/master-20261026-171519-136/`.

Kullanıcı onayıyla mevcut veritabanının CHECKSUM yedeği alınıp VERIFYONLY ile doğrulandı ve yalnızca 105 izin sürümü yükseltmesi uygulandı. Mevcut verilerin özet karşılaştırması aynı kaldı. Muhasebe - Güncel kısayolu yeni yerel pakete yönlendirilip uygulama açıldı. Gerçek Genel Personel/İzin Planlama ekranlarında ayrı veritabanıyla 4 senaryoda eski seçim reddi ve güncel seçimle kaydetme dahil **8 ekran kontrolü başarılı**; geçici veritabanı/motor temizlendi. Kanıt: artifacts/analysis/leave-live-ui/. [Ayrıntılar](docs/IZIN_SURUM_KORUMASI.md).

İzin kaydetmeye personel RecordVersion + ayrı LeaveVersion kontrolü eklendi. Genel Personel ve toplu İzin Planlama özgün seçim sürümlerini taşır; beklenen izin yokluğu da doğrulanır. Yeniden oluşturulan personel veya başka kullanıcının izin değişikliği eski seçimle etkilenmez; tek çatışmada bütün plan/audit geri alınır. Eski eşzamanlı izin yardımcısı aynı sürümlü yolu kullanır, iç sürüm raporlardan gizlenir. Ek `105-leave-rowversion.sql` betiği hazırlanıp ayrı test veritabanında doğrulandı; eski betikler ve gerçek veritabanı değiştirilmedi. **490 test başarılı, 0 başarısız, 0 atlanan**; Release 0 uyarı / 0 hata. Test motoru temizlendi. [Ayrıntılar ve deneme adımları](docs/IZIN_SURUM_KORUMASI.md). Kanıt: `artifacts/analysis/master-20261026-162016-546/`.

Hesap silme/pasife alma/etkinleştirme işlemlerine özgün kullanıcı adı + SessionVersion + SessionEpoch kontrolü eklendi. Seçim onaydan önce alınır; hedef transaction içinde kilitlenerek ve SQL yazma koşulunda doğrulanır. Yeniden oluşturulan veya rol/parola/durumu değişen hesapta eski seçim reddedilir; giriş sayacı ve audit korunur. Eski AdminStore.Delete aynı güvenli servise yönlendirildi. Kendi hesabı/son yönetici ve oturum iptali korumaları sürdürüldü. **456 test başarılı, 0 başarısız, 0 atlanan**; Release 0 uyarı / 0 hata. Ayrı test motoru temizlendi; gerçek hesaplar kullanılmadı. Yeni SQL betiği yok. Yerel geliştirme; [ayrıntılar ve deneme adımları](docs/HESAP_SECIM_SURUM_KORUMASI.md). Kanıt: `artifacts/analysis/master-20261026-145710-633/`.

Personel silme koruması yayımlanan yerel DLL ile gerçek Genel/Joker ekranlarında, yalıtılmış veritabanında denendi: eski seçim reddi ve yenilenmiş seçimle silme dahil **4 ekran kontrolü başarılı**. Geçici test veritabanı/motoru temizlendi; gerçek personel verisi değiştirilmedi. Kullanıcı isteğiyle eski uygulama kapatıldı, yerel test paketi açıldı ve Muhasebe - Güncel kısayolu yönlendirildi.

Personel silme için özgün RecordVersion kontrolü eklendi: Genel/Joker seçim sürümü onaydan önce alınır, izin/defter değişmeden önce transaction içinde kilitlenerek doğrulanır ve DELETE koşulunda tekrar aranır. Yeniden oluşturulan veya başka kullanıcı/ödeme tarafından değiştirilen kayıtta eski seçim reddedilir; izinler, bakiye, defter ve audit korunur. Eski PersonnelStore.Delete yolu da sürümü zorunlu tutar. **414 test başarılı, 0 başarısız, 0 atlanan**; Release derlemesi 0 uyarı / 0 hata. Ayrı test motoru temizlendi; gerçek veritabanı kullanılmadı. Yeni SQL betiği yok. Yerel geliştirme; [ayrıntılar ve deneme adımları](docs/PERSONEL_SILME_SURUM_KORUMASI.md). Kanıt: `artifacts/analysis/master-20261026-134118-955/`.

Windows 2000 arayüz geliştirmeleri tek kaynak güncellemesinde toplandı: klasik/açık/koyu tema, ortak menüler ve klavye erişimi; giriş, ana ekran, Genel/Joker Personel, Kişi Arama, Genel Durum, İletişim, Yönetici, Ödemeler, Ayarlar, İzin Planlama, Parola Değiştirme/Sıfırlama ve geçici parola pencerelerinin ölçü ve yerleşimleri. Önceki geliştirme notları aşağıda tarihleriyle korunur. README ve örnek verili ekran görselleri güncellendi.

Tam doğrulama: **392 test başarılı, 0 başarısız, 0 atlanan**; Release derlemesi **0 uyarı / 0 hata**. Entegrasyon ve geri yükleme kontrolleri gerçek veritabanından ayrı geçici LocalDB motorunda çalıştı; motor temizlendi. Kanıt: `artifacts/analysis/master-20261026-025032-473/`. Bu gönderim kaynak kodu ve belgeleri günceller; mevcut v1.0.0 Release paketi değiştirilmez.

## 25 Ekim 2026

Parola Sıfırlama ekranı ve geçici parola sonucu düzenlendi: kompakt sabit pencereler, hedef hesap başlığı, ayrı doğrulama/parola bölümü ve hizalı düğmeler. Vazgeç/Esc desteği eklendi; vazgeçildiğinde yönetici parolası temizlenir. Maskeleme, süre ve mevcut güvenlik akışı korunur. 100 görünüm/parola/oturum kontrolü başarılı; gerçek hesap sıfırlanmadı. Geliştirme yereldir.

Parola Değiştirme ekranı düzenlendi: kompakt başlangıç ölçüsü, üstte oturum açıklaması, hizalı üç maskeli alan, altta parola kuralları ve sağda değiştirme düğmesi. Enter, mevcut/geçici parola akışı ve işlem sonrası temizleme korunur. Normal/küçük/büyük pencere ve mevcut parola/oturum kısıtları dahil 99 kontrol başarılı; gerçek parola değiştirilmedi. Geliştirme yereldir.

İzin Planlama ekranı düzenlendi: üstte hizalı başlangıç/bitiş, gün sayısı, seçili kişi sayısı ve onay; altta geniş Genel personel/izin listesi. Bir kişinin tekrarlanan izin satırları sayımı şişirmez, özgün TC kullanılır. Mevcut çoklu seçim ve toplu kaydetme/onay akışı korunur. Normal/küçük/büyük pencere dahil 108 veritabanısız kontrol başarılı; gerçek izin kaydı oluşturulmadı. Geliştirme yereldir.

Ayarlar ekranı düzenlendi: ayrı bağlantı, yedekleme ve görünüm/fiş bölümleri; hizalı dosya/klasör alanları ve seçim düğmeleri; altta kaydetme/yedekleme/geri yükleme işlemleri. Normal/küçük/büyük pencere, tema seçimi ve mevcut profil/yedek/oturum korumaları dahil 108 veritabanısız kontrol başarılı. Gerçek veritabanına yedekleme/geri yükleme uygulanmadı; geliştirme yereldir.

Ödemeler ekranı düzenlendi: solda tutar/ödeme/sonuç kontrolü, sağda günlük kapanış ve fiş ayarları; altta ayrı personel ve hesap defteri listeleri. Geniş IBAN, okunabilir Türkçe başlıklar ve sağa hizalı tutarlar. Görünür kolonlar değişse de fiş/iptal için özgün ödeme kimliği korunur. Küçük/normal/büyük pencere görünümü ve çift ödeme/kayıt seçimi/oturum korumaları dahil 100 veritabanısız kontrol başarılı; geliştirme yereldir.

Yönetici ekranı düzenlendi: solda yeni hesap alanları, sağda seçili hesabın etkinleştirme/pasife alma/parola sıfırlama/silme işlemleri, altta tam genişlikte hesap listesi. Parola maskesi ve hesap/oturum korumaları korunur; okunabilir kolon ölçüleri, Türkçe yetkiler ve UTC bitiş saati gösterimi eklendi. 90 görünüm/kayıt seçimi/oturum kontrolü ve son değişiklikle 17 görünüm kontrolü başarılı; geliştirme yereldir.

İletişim ekranı düzenlendi: kompakt başlangıç ölçüsü, yan yana kişi kartları, büyük ve seçilebilir telefon alanları, yerel panoya kopyalama düğmeleri. Mevcut iletişim bilgileri korunur; belge görselinde örnek bilgiler kullanılır. 90 görünüm/kayıt seçimi/oturum geçişi kontrolü başarılı; geliştirme yereldir.

Genel Durum açılışındaki turuncu geçiş giderildi: eski arka plan ve çerçevesiz başlangıç ayarı kaldırıldı, tema pencere gösterilmeden uygulanıyor. 71 görünüm/ekran geçişi kontrolü başarılı; düzeltme yereldir.

Genel Durum yerleşimi yenilendi: üstte dört kişi sayacı, ayrı ödeme toplamı ve ortalama kutuları, altta geniş izin takibi. Çoklu izin kayıtlarında kişi sayısı/ödeme toplamının şişmediği doğrulandı. 95 görünüm/özet/rapor/ekran geçişi kontrolü başarılı; geliştirme yereldir.

Kişi Arama ekranı düzenlendi: üstte hizalı TC/ad/soyad filtreleri, sağda arama ve filtre temizleme düğmeleri, altta tam genişlikte sonuçlar. Enter ile arama ve sonuç sayısı başlığı eklendi. 88 görünüm/kayıt seçimi/ekran geçişi kontrolü başarılı; geliştirme yereldir.

Joker Personel düzenlendi: üstte hizalı alanlar ve geniş IBAN, altta tam genişlikte personel listesi; işlem düğmeleri tek sırada. Boş izin kolonları gösterilmez; görünür kolonlar değişse de sıralama sonrası özgün kişi/sürüm bilgisi korunur. 45 görünüm/kayıt seçimi kontrolü başarılı; değişiklik yereldir.

Genel Personel ekranı yeniden yerleştirildi: üstte personel bilgileri ve ayrı izin bölümü, altta tam genişlikte liste; geniş IBAN alanı, hizalı işlem düğmeleri ve okunabilir kolon başlıkları. 44 görünüm/kayıt seçimi kontrolü başarılı. Bu geliştirme yereldir.

Ana ekranın başlangıç ölçüsü ve yerleşimi düzenlendi: genişletilmiş Bölümler ağacı, Personel ve izin / Ödeme ve rapor / Hesap ve destek grupları, eşit yükseklikte düğmeler ve alt sağda çıkış. Büyük pencerede düğmelerin gereksiz büyümesi engellendi. 70 görünüm/oturum ve son hizalama için 15 görünüm kontrolü başarılı; geliştirme yereldir.

Girişin mavi dış alanı ve iç içe pencere görünümü kaldırıldı; sabit pencere doğrudan 620 × 400 boyutlu giriş içeriğine oturtuldu. Büyütme kapalı kaldı. 23 görünüm/giriş hazırlığı kontrolü başarılı; güncelleme yereldir.

Kullanıcı geri bildirimiyle girişin büyütülmesi kapatıldı: sabit boyutlu pencere, devre dışı büyütme düğmesi ve normal açılış. Önceki tam ekran tasarımı yerine bu davranış seçildi; diğer ekranlar büyütülebilir. 23 görünüm/giriş hazırlığı kontrolü başarılı; değişiklik yereldir.

Windows 2000 denemesinde tam ekran giriş iyileştirildi: DPI'ya uygun, boyutu sınırlandırılmış giriş kartı mavi çalışma alanının merkezinde tutulur. 243 veritabanısız test başarılı; normal/dar/tam ekran yerleşimleri kontrol edildi. Bu arayüz değişikliği yereldir.

**Yerel arayüz denemesi — Windows 2000:** Klasik renkler ve kontroller; giriş/ana ekran ile Genel/Joker yerleşimleri yenilendi. Dosya, İşlemler, Rapor, Görünüm ve Yardım menüleri; role uygun ağaç kısayolları ve tema seçimi eklendi. 387 test başarılı. Ayrı deneme paketi ve kısayolu hazır; bu arayüz denemesi henüz GitHub'a push edilmedi. [Görseller ve deneme adımları](docs/WINDOWS_2000_DENEME.md).

| Değişiklik | Yapılanlar | Doğrulama |
| --- | --- | --- |
| Hatalı giriş sınırı | Hesap başına 15 dakikada 5 hata için 5 dakika geçici kilit; kalıcı SQL sayacı; eşzamanlı deneme ve iptal koruması. Parola işlemleri aynı bütçeyi kullanır. Açık oturumlar etkilenmez; yönetici sıfırlaması hedefin kilidini temizler. | 383 test başarılı; 19 yeni test durumu |
| Parola değiştirme/sıfırlama | Her rol kendi parolasını doğrulayarak değiştirebilir. Yönetici yeniden doğrulamayla 30 dakika geçerli rastgele geçici parola oluşturur. Kullanıcı yeni parolasını belirlemeden diğer ekranlara erişemez. Yeni parolalar 15–1024 karakter; yalnızca hash saklanır. Eski oturumlar ve eski hedef seçimi reddedilir. | 364 test başarılı; yayımlanan paketle ayrı ortamda 32 uygulamalı kontrol |
| Hesap durumu/oturum iptali | Pasife alma ve etkinleştirme; kendi hesabı/son yönetici koruması; servislerde güncel yetki ve oturum kontrolü. Açık ekranlar 30 saniyelik kontrolle girişe döner. Yedekten dönüş eski oturumu canlandırmaz. | 338 test başarılı; yayımlanan paketle oturum ve zamanlayıcı denemesi |
| Eşzamanlı personel düzenleme | Genel/Joker kayıtlarının bütün alanlarında rowversion kontrolü. Eski ekran başka kullanıcının ad, soyad, IBAN veya bakiye değişikliğini ezmez. Özgün satır seçimi, taslak koruması; iç sürümler listelerden/raporlardan gizli. | 316 test başarılı; kullanıcı denemesi başarılı |
| Belirsiz ödeme sonucu | Ödeme gönderilmeden önce işlem kimliği ve hedef bağlamı DPAPI ile kalıcılaştırılır. Sonuç belirsizse aynı işlem incelenir/yeniden denenir. İkinci ödeme ve farklı uygulama örneklerinin çakışması önlenir. | 292 test başarılı |
| Excel toplamı | Çoklu izin satırlarının aynı personelin tutarını tekrar tekrar toplaması düzeltildi. | 292 test takımında doğrulandı |

Teknik raporlar ve kullanıcı kılavuzları:

- [Hatalı giriş sınırı](docs/HATALI_GIRIS_SINIRLAMASI.md)
- [Parola işlemleri](docs/PAROLA_DEGISTIRME_VE_SIFIRLAMA.md) · [uygulamalı doğrulama](docs/PAROLA_UYGULAMALI_DOGRULAMA.md)
- [Oturum iptali](docs/OTURUM_IPTALI_VE_HESAP_DURUMU.md) · [deneme kılavuzu](docs/OTURUM_GUVENLIGI_DENEME_KILAVUZU.md) · [uygulamalı doğrulama](docs/OTURUM_UYGULAMALI_DOGRULAMA.md)
- [Eşzamanlı düzenleme](docs/ESZAMANLI_PERSONEL_DUZENLEME.md) · [personel deneme kılavuzu](docs/PERSONEL_DENEME_KILAVUZU.md)
- [Ödeme kurtarma ve Excel](docs/ODEME_KURTARMA_VE_EXCEL_TOPLAMI.md)

## 24 Ekim 2026

- Eski personel kaydının ödeme iptalinin aynı TC ile yeniden oluşturulan kaydın bakiyesini etkilemesi engellendi.
- Geri yüklemede seçilen yedeğin temizlik/yükseltme sırasında silinmesi önlendi.
- Bu düzeltmeler **270 test** ile doğrulandı; yeni şema gerektirmez. [Teknik rapor](docs/ODEME_VE_GERI_YUKLEME_DUZELTMELERI.md).
- Daha önceki temel sürümde .NET 10, merkezi bağlantı ayarları, kurulum aracı, parola hash'i, ekran/çıkış yönetimi ve personel/izin/ödeme düzeltmeleri kaydedildi.
- Rehber mevcut işlevlere uyarlandı: roller, servis yetkileri, transaction/audit, ödeme defteri, raporlar, şifreli taslaklar, yedekleme ve şema yükseltmesi. LocalDB başlangıcı, izole test motoru, Windows CI, self-contained paket ve README kurulum/indirme adımları eklendi.
- Önceki Git kayıtları: [03a9dcf](https://github.com/frknylmzkng/AccountingApp/commit/03a9dcf), [305b3de](https://github.com/frknylmzkng/AccountingApp/commit/305b3de), [95fd89d](https://github.com/frknylmzkng/AccountingApp/commit/95fd89d), [6d4c451](https://github.com/frknylmzkng/AccountingApp/commit/6d4c451). [Rehber uygulama takibi](docs/REHBER_UYGULAMA_TAKIBI.md).

## Şema yükseltmeleri

| Betik | Tarih | Amaç |
| --- | --- | --- |
| 101-personnel-rowversion.sql | 25 Ekim 2026 | Eşzamanlı personel düzenleme koruması |
| 102-account-session-version.sql | 25 Ekim 2026 | Hesap durumu, oturum sürümü ve epoch |
| 103-password-change-reset.sql | 25 Ekim 2026 | Zorunlu parola yenileme ve geçici parola süresi |
| 104-login-throttle.sql | 25 Ekim 2026 | Ayrı kalıcı hatalı giriş sayacı |

Önceden uygulanmış 001 ve 100 betikleri değiştirilmedi. İlk giriş hazırlığı eksik yükseltmelerden önce doğrulanmış yedek alır. Bütün istemciler güncel sürümü kullanmalıdır.

## 25 Ekim 2026 doğrulaması ve o tarihteki yayın kapsamı

Tam yerel regresyon: **383 başarılı, 0 başarısız, 0 atlanan**. Uyarıları hata sayan Release derlemesi ve self-contained paket üretimi başarılı. Kanıt: artifacts/analysis/master-20261025-090245-255/. Geçici LocalDB motoru temizlendi; gerçek hesaplar test amacıyla değiştirilmedi.

Derleme çıktıları, yerel kanıtlar, gerçek veritabanları/yedekler, günlükler ve ödeme taslakları Git'e alınmaz. Kaynak push'u yeni GitHub Release yayımlamaz: mevcut **v1.0.0** Windows indirmesi 25 Ekim geliştirmelerini içermez. Son yerel paket artifacts/releases/login-throttle/ altındadır. Uzak GitHub Actions sonucu yerel testten ayrı değerlendirilir.
