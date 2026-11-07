# Proje sorumlusu ve proje bazlı erişim

**İzleyen kurulum:** Kullanıcı onayıyla 31 Ekim 2026'da [115 yükseltmesi ve 1.3.3-dev istemci/API kurulumu](1_3_3_DEV_KURULUMU.md) tamamlandı. Aşağıdaki uygulanmamış şema/korunan 1.3.2-dev ifadeleri kaynak tesliminin tarihsel kapsamıdır. İşlev denemeleri kurulumda da atlandı.

31 Ekim 2026 · Aşama 2 kaynak dilimi · **1.3.3-dev**, şema **115**, API sözleşmesi **1.3** (`/api/v1`).

Kullanıcı, kurulu 1.3.2-dev sürümünün düzgün çalıştığını bildirdi. Bu kaynak çalışmasında canlı kurulum değiştirilmedi ve denemeler atlandı.

## Rol ve kapsam kararı

Mevcut **Yönetici**, **Personel sorumlusu (genel)** ve **Görüntüleyici (genel)** merkez rolleridir. Önceki genel erişimleri ve ayrı ödeme/dışa aktarma izinleri korunur; proje üyeliği verilmesi bu rolleri daraltan bir politika değildir. Özellikle genel görüntüleyici proje kullanıcıları için kullanılmamalıdır.

Yeni **Proje kullanıcısı (kısıtlı)** rolü yalnız etkin üyeliği olan projelere erişir. Proje A kullanıcısının Proje B kimliğini doğrudan API isteğine yazması erişim vermez. Kısıtlı hesap genel personel arama/listesi, çalışma dönemi geçmişi, genel bakiye/özet, ödeme defteri/sonuç sorgusu, günlük kapanışlar, audit, hesap/yedek yönetimi veya genel dışa aktarma yollarını kullanamaz. Oturum doğrulama ile genel veri okuma ayrı yetki kontrolleridir; parola değiştirme, otomatik kilit ve kilidi açma kullanılabilir.

| Proje izni | Verdiği erişim |
|---|---|
| Etkin üyelik | Proje bilgisi, ekipler, atama geçmişi ve projenin güncel çalışma dönemi personel havuzunu okuma |
| Proje sorumlusu | Görünür sorumlu kaydı; kendiliğinden düzenleme, maliyet veya ödeme yetkisi vermez |
| Ekip ve atama düzenleme | Aynı projede ekip oluşturma/düzenleme/pasife alma, havuzdaki personele tarihli atama ve gerekçeli atama iptali |
| Maliyet görüntüleme | Yalnız yetkili projenin maliyet toplamları ve hakediş/gider belgeleri |
| Proje raporu CSV alma | Seçili projenin atama raporu; maliyet belgeleri için ayrıca maliyet görüntüleme izni gerekir |

Proje kullanıcılarına ödeme, avans, hakediş/gider onayı/iptali, proje oluşturma veya proje meta bilgisi değiştirme yetkisi verilmez. Ödeme RequestId/tekrar koruması ve finans iş akışı bu dilimde değiştirilmedi. Proje bazlı ödeme hazırlama/onaylama ve puantaj onayı ileride ayrı iş akışlarıdır. Genel ödeme veya kişisel veri dışa aktarma izni bu role uygulama ve SQL check ile kapalıdır.

## Yönetici için kullanım

1. **Yönetici hesapları** ekranından yeni bir hesap açın; rolünü **Proje kullanıcısı (kısıtlı)** seçin. Mevcut merkez hesabının rolü sessizce değiştirilmez.
2. **İşlemler → Projeler, ekipler ve tarihli atamalar** ekranında projeyi seçip **Projeyi aç** düğmesine basın.
3. **Proje sorumlusu ve yetkiler…** ekranında etkin proje hesabını seçin; sorumluluk ve ek izinleri açıkça işaretleyip gerekçeyle kaydedin. Tüm kutular boş olsa da etkin üyelik proje okuma izni verir.
4. Mevcut yetki için satırı seçip **Seçili yetkiyi düzenle** kullanın. Hesap değiştirilemez; başka hesap için yeni yetki açılır. Bir projede en fazla bir etkin sorumlu bulunur. Sorumlu değişiminde önce eski sorumluluk işaretini kaldırın veya eski yetkiyi geri alın; ardından yeni hesabı sorumlu yapın. Sorumluluk işaretini kaldırmak üyeliği kaldırmaz.
5. Erişimi kaldırmak için **Seçili yetkiyi geri al** kullanın. Satır geçmişte kalır ve yeniden etkinleştirilemez; tekrar izin verilirse yeni GrantId oluşur.

Kısıtlı kullanıcıya başka bir projenin veya genel personel havuzunun verisi açılmaz. Personeli proje havuzuna ilk kez merkez yöneticisi atar. Kısıtlı kullanıcı, kendi projesine daha önce ataması olan kişinin **güncel çalışma dönemini** kullanabilir; iptal edilmiş geçmiş atamalar da havuz bağlantısıdır. Yeni işe giriş yeni EmploymentId oluşturduğunda eski bağlantı yeni döneme erişim vermez; merkez yeni dönemi ayrıca projeye tanıtır. Atama çakışması bütün projeler üzerinde denetlenmeye devam eder; başka projenin adı/ayrıntısı hata mesajına konmaz.

## Proje kullanıcısı için kullanım

Girişten sonra **Projelerim** açılır. Genel ana ekran yerine yalnız atanmış projeler listelenir. Üstte proje ve sorumlu hesap gösterilir. **Proje personeli** bölümünde kişi, ekip ve tarihler seçilip izin varsa atama kaydedilir. **Atamalar** bölümünden geçmiş okunur ve yetkili iptal yapılır. **Ekipler** bölümünden izin varsa ekip işlemleri yapılır.

Personel havuzu TC, IBAN veya genel bakiye içermez. Atama ve finans yanıtlarında TC kısıtlı rol için sunucuda boş bırakılır; yalnız ekranda gizleme yapılmaz. Personel kimliği ve sürümü sunucuya işlem hedefi olarak gider, gerçek TC sunucuda proje havuzu doğrulanarak bulunur. Serbest açıklama alanlarına merkez tarafından yazılan kişisel bilgileri otomatik tanıyıp temizleyen bir DLP sistemi eklenmedi.

**Maliyetler → Maliyetleri yükle** yalnız maliyet izniyle çalışır; belge listesi en yeni 500 kayıttır. CSV raporu ekranın istemciden gönderdiği keyfi bir tabloyu kaydetmez: sunucu proje kimliği ve sabit rapor türünden veriyi yeniden üretir. Atama raporu TC/IBAN/genel bakiye içermez. Maliyet CSV'si hakediş/gider belgelerini iptal durumlarıyla içerir; nakit ödeme mutabakatı değildir. En fazla 10.000 satır ve 32 MB desteklenir, sınır aşılırsa rapor eksiltilerek verilmez. Formül benzeri CSV metinleri mevcut CSV korumasından geçer.

Kısıtlı hesapta genel Excel/CSV, yazdırma ve pano yolları açılmaz; bu dilimde proje raporu için özel **CSV** düğmeleri vardır. Önceden görülen/indirilen verilerin geri çağrıldığı iddia edilmez. Rapor yetkisi kaldırılması, halen yetkilendirilmiş aktarımın bitmesini bekler; yeni istek canlı yetkiyi tekrar kontrol eder. Ekrandaki düğmeler bir önceki yüklemenin izinlerini gösterebilir; sunucu güncel izin olmadan işlemi yapmaz. Güncel düğmeler için **Yenile** kullanın.

## Kimlik, sürüm ve geçmiş

Yetki, kullanıcı adına değil kalıcı **AccountId + ProjectId** çiftine bağlıdır. Silinen hesabın aynı adla yeniden açılması eski yetkiyi devralmaz. Yetki kaydı saklanır; silinmiş/pasif hesabın eski yetkisi yönetici tarafından geri alınabilir. Hesabı pasife almak oturumunu geçersiz kılar, üyeliğini silmez; aynı hesabı yeniden etkinleştirince kaldırılmamış proje izinleri tekrar geçerli olur.

Yetki verme/düzenlemede proje sürümü, hesap kimliği/sürümü/oturum nesli ve mevcut yetki satırının sürümü kontrol edilir. Geri almada proje ve yetki sürümü gerekir. Eski seçimin yeni hesaba veya yeni yetki satırına uygulanması önlenir. Bir hesap/proje için tek etkin üyelik ve proje başına tek etkin sorumlu SQL unique indeksleriyle de korunur. Üyelik değişiklikleri eski/yeni değer, hesap/aktör kimliği, UTC zaman, gerekçe ve korelasyonla audit'e aynı transaction içinde yazılır.

Hesap ve proje yetkisi servis transaction'ında canlı doğrulanır; JWT/istemci önbelleğine bakılarak kalıcı izin verilmez. Üyelik geri alınması yeni veri istekleri ve yazmalarda geçerlidir; ayrıca kullanıcının token'ını iptal etmek gerekmez. Proje kullanıcıları yönetici hesap listesine veya proje izin yönetimine ulaşamaz; sorumlu olmak başkasına yetki verme hakkı sağlamaz.

## Geçiş ve teslim durumu

**115 hazırlanmıştır, uygulanmadı.** Yeni ProjectAccountAccess tablosu boş başlar; mevcut projelere sorumlu/üyelik veya tarih uydurulmaz. Eski finans, izin, Employment, atama, ödeme ve kapanış gövdeleri yeniden yazılmaz. Yükseltme hesap oturum nesillerini yeniler. Kurulu **1.3.2-dev / 114 / API 1.2**, veritabanı, API görevi ve masaüstü kısayolu bu kaynak çalışmasında değiştirilmedi.

Geçiş için doğrulanmış yedek, istemci/API bakım duruşu, 115 yükseltmesi ve **1.3.3-dev istemci/API'nin birlikte kurulması** gerekir. Başlatıcıdaki aktif protokol beklentisi yeni manifestin `apiVersion: 1.3` değeriyle güncellenmelidir. Yeni kaynak paketi eski 114 şemayla karışık kullanılmamalıdır.

İstek doğrultusunda unit, SQL, API, UI, çoklu kullanıcı, yetki sızması ve eşzamanlılık denemeleri çalıştırılmadı. Derleme/paket üretimi, bu davranışların çalıştırılarak doğrulandığı anlamına gelmez. GitHub'a push veya Release yayımlanmadı.

Bu dilim Aşama 2'nin tamamı değildir: bütünleşik personel kartı, tarihli grup geçişi, vardiya/puantaj, proje bazlı ödeme/onay iş akışları ve daha ayrıntılı hassas alan politikaları açık kalır.
