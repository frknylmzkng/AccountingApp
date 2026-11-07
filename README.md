# AccountingApp

**Güncel sürüm v1.3.33**, Windows x64 için iki ayrı paketle yayımlandı. Her iki paket .NET 10.0.14 çalışma zamanını içerir. Merkezi kullanımda istemci ve sunucu için aynı sürümü kullanın. Sunucu **şema 138** ve **API 1.32** gerektirir. [Sürüm sayfası](https://github.com/frknylmzkng/AccountingApp/releases/tag/v1.3.33) · [Değişiklik günlüğü](CHANGELOG.md)

## Windows paketlerini indir ve kur

- [Windows istemcisi (v1.3.33, x64)](https://github.com/frknylmzkng/AccountingApp/releases/download/v1.3.33/AccountingApp-v1.3.33-desktop-win-x64.zip)
- [Merkezi API sunucusu (v1.3.33, x64)](https://github.com/frknylmzkng/AccountingApp/releases/download/v1.3.33/AccountingApp-v1.3.33-server-win-x64.zip)
- [SHA-256 dosya özetleri](https://github.com/frknylmzkng/AccountingApp/releases/download/v1.3.33/SHA256SUMS.txt)

Paketler imzasızdır. PowerShell’de `Get-FileHash <paket.zip> -Algorithm SHA256` komutunda dosya adını belirterek sonucu yayımlanan özetle karşılaştırabilirsiniz. [Yayın doğrulaması](https://github.com/frknylmzkng/AccountingApp/actions/runs/42359399523/attempts/2)

### Windows istemcisi

1. Önceki Muhasebe pencerelerini kapatın. Mevcut yerel veritabanı şema 138’den eskiyse doğrulanmış yedek alın ve yükseltmeyi ayrı bakım adımında tamamlayın. [Veritabanı ve bakım adımları](docs/DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md#gerçek-bakım-sırası)
2. İstemci ZIP’inin tamamını yeni bir klasöre çıkarın ve kökteki `AccountingApp.Setup.exe` dosyasını açın. Güncellemede **Mevcut bağlantıyı koru (güncelleme)** seçeneğini kullanın. Yeni bir sunucuya bağlanmak için **Merkezi HTTPS API'ye bağlan** seçeneğini seçin ve adresini girin.
3. İlk kez yerel veritabanı kuruyorsanız SQL Server Express LocalDB kurulu olmalıdır. **Yeni yerel veritabanı ve ilk yönetici** seçeneğinde yönetici hesabını belirleyin. **Devam** ve **Kur** adımlarını tamamlayıp **Muhasebe - Güncel** kısayolundan giriş yapın.

### Merkezi API sunucusu

1. API ZIP’inin tamamını sunucuda yeni bir klasöre çıkarın. Mevcut veritabanını yükseltiyorsanız önce istemcileri ve API’yi kapatın, doğrulanmış yedek alın ve şema 138’i ayrı bakım adımında uygulayın.
2. `server.example.json` dosyasından `server.json`, `database-profile.example.json` dosyasından SQL bağlantı profilinizi oluşturun. `AccountingApi:DatabaseProfilePath` alanını bu profile yönlendirin. `ListenUrl`, `AllowedHosts` ve istemcilerin güvendiği HTTPS sertifikasını yapılandırın. Tek bilgisayarda LocalDB için `AllowUserLocalDb` değerini `true` yapın ve aynı Windows hesabıyla `https://localhost:7443` kullanın. Çok bilgisayarlı kurulumda sunucuda SQL Express veya SQL Server kullanın. [Sunucu ve TLS yapılandırması](docs/DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md#topolojiler)
3. Paket klasöründe `AccountingApp.Server.exe` dosyasını çalıştırın. Sunucu açıldıktan sonra istemci kurulumunda aynı HTTPS adresini girin.

## Gerçek kullanım pilotu ve kabul

Pilot hazırlığı, günlük iş akışı, ayrı ortamda yapılacak denemeler ve doldurulabilir kabul tablosu [v1.3.33 pilot ve kabul kılavuzunda](docs/GERCEK_KULLANIM_PILOTU_VE_KABUL_V1_3_33.md) yer alır.

## Geliştirme geçmişi

7 Kasım 2026’da 1.3.33-dev yerel teslimi hazırlandı. Daha sonra başarılı Actions çalışmasındaki istemci ve API paketleri şema 138 ile yerel olarak kuruldu. Gerçek kullanım pilotu ve saha kabulü bekliyor. [Pilot ve teslim kaydı](docs/PILOT_VE_TESLIM_1_3_33_DEV.md)

6 Kasım 2026 güncel kaynak: [kurulum ve dağıtım sonlandırması](docs/DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md), **1.3.32-dev / şema 137 / API 1.32**. Tam ZIP/manifest doğrulama, salt okunur bakım/geri dönüş planı, bağımlılık envanteri, isteğe bağlı Authenticode ve kurulum kimlik kontrolleri eklendi. Çok bilgisayarlı bakım rehberi hazır. CI tam test kapısı korunur; denemeler/Actions yapılmadı. Doğrulanmış kurulum öncesi yedek sonrası 1.3.32-dev istemci/API/kısayol kurulumu tamamlandı; şema 137 değişmedi, paket imzasızdır. [Kalan 2 ana iş paketi](docs/KALAN_IS_PAKETLERI_2026_11_06.md). Aşağıdaki kayıtlar tarihseldir.

6 Kasım 2026 güncel kaynak: [yedek ve destek güvenliği](docs/YEDEK_VE_DESTEK_GUVENLIGI.md), **1.3.31-dev / şema 137 / API 1.31**. Yeni yedekler şifreli arşivdir; DPAPI anahtar kümesi, parolalı anahtar dışa/içe aktarımı, anahtar döndürme, kapsamlı saklama/aynalama, kurtarma durumu ve kişisel veri/log içermeyen tanı ZIP'i eklendi. Kurtarma aracı yalnız elle çalışır. Yeni SQL yükseltmesi yok; doğrulanmış kurulum öncesi yedek sonrası 1.3.31-dev istemci/API/kısayol kurulumu tamamlandı. Denemeler atlandı. [Kalan 3 ana iş paketi](docs/KALAN_IS_PAKETLERI_2026_11_06.md). Aşağıdaki geliştirme/kurulum kayıtları tarihseldir.

6 Kasım 2026 güncel kaynak: [proje rapor merkezi](docs/RAPOR_MERKEZI_GENISLEMESI.md), **1.3.30-dev / hazırlanmış 137 / API 1.30**. Ekip/tarih/kişi filtreleri, hesaba özel kayıtlı görünümler, bekleyen ücret/gider işleri, kural/talep/aktarım kırılımları ve parçalı CSV/ZIP aktarımı eklendi. Mevcut proje raporları ortak ekrandan açılır. Doğrulanmış yedek sonrası 137 ve 1.3.30-dev yerel kurulumu tamamlandı; denemeler atlandı. [Kalan 4 ana iş paketi](docs/KALAN_IS_PAKETLERI_2026_11_06.md). Aşağıdaki geliştirme ve kurulum kayıtları tarihseldir.

.NET 10 Windows Forms uygulaması: personel, izin ve kalan ödeme tutarı takibi. Rehberdeki yöntemler mevcut işlevlere uyarlandı. Ayrıntılı madde eşlemesi `docs/REHBER_UYGULAMA_TAKIBI.md`, kullanım ve sınama adımları `docs/KULLANIM_VE_TEST.md` içindedir.

**Neler, ne zaman değişti?** [Tarihli değişiklik günlüğü](CHANGELOG.md) geliştirmeleri, şema yükseltmelerini ve test sonuçlarını listeler.

Bu bilgisayarda **7 Kasım 2026 tarihinde 1.3.33-dev** kuruldu. İstemci ve API .NET 10.0.14 taşır. Muhasebe - Güncel kısayolu yeni sürüme bağlıdır. Kurulum yedeği doğrulandı ve işlevsel denemeler atlandı. [Kurulum kaydı](docs/1_3_33_DEV_KURULUMU.md).

6 Kasım 2026 güncel kaynak: [ek ücret/gider kuralları](docs/EK_UCRET_VE_GIDER_KURALLARI.md), **1.3.29-dev / uygulanmış 136 / API 1.29**. Tarihli ve bağımsız onaylı kural sürümleri, günlük/aylık temel, aktarılmış saatlik kaynağa ek fazla mesai farkı, ek ödeme ve doğrudan gider talepleri eklendi. Hazırlama/onay/tek seferlik aktarım ayrıdır; bağımsız onaylı tam ters hareket ve saatlik/temel ücret çakışma korumaları vardır. Mevzuat katsayısı veya hak birimi varsayılmaz. Doğrulanmış yedek sonrası 136 ve 1.3.29-dev kurulumu tamamlandı; denemeler atlandı. [Kalan 5 ana iş paketi](docs/KALAN_IS_PAKETLERI_2026_11_06.md).

Önceki geliştirme: [kalıcı mali dönem kapanışı](docs/KALICI_MALI_DONEM_KAPANISI.md), 1.3.28-dev içinde kuruldu ve kullanıcı tarafından doğrulandı.

Önceki geliştirme: [banka CSV ve ödeme eşleştirme](docs/BANKA_CSV_VE_ODEME_ESLESTIRME.md), 1.3.27-dev içinde kuruldu ve kullanıcı tarafından doğrulandı.

Önceki geliştirme: [proje bütçesi ve maliyet sapması](docs/PROJE_BUTCESI_VE_MALIYET_SAPMASI.md), 1.3.26-dev içinde kuruldu ve kullanıcı tarafından doğrulandı. Windows başlangıç görevleri ayrıca gizli başlatma ve koşullu Explorer kurtarma için güncellendi; [bulgular ve sınırlar](docs/WINDOWS_BASLANGIC_VE_KONSOL_2026_11_06.md).

Önceki geliştirme: [planlanan / gerçekleşen proje kişi-saat karşılaştırması](docs/PLAN_GERCEKLESEN_PROJE_KISI_SAAT.md), 1.3.25-dev içinde kuruldu.

Önceki geliştirme: [mali filtreler ve sayfalama](docs/MALI_FILTRELER_VE_SAYFALAMA.md), 1.3.24-dev içinde kuruldu.

Önceki geliştirme: [proje mali mutabakat raporu](docs/PROJE_MALI_MUTABAKAT_RAPORU.md), 1.3.23-dev içinde kuruldu.

Önceki geliştirme: [ödeme ve avans tahsisi](docs/ODEME_VE_AVANS_TAHSISI.md), 1.3.22-dev içinde kuruldu.

Önceki geliştirme: [bağlı bağımsız onaylı mali düzeltme ve ters hareket](docs/BAGLI_MALI_DUZELTME_VE_TERS_HAREKET.md), 1.3.21-dev içinde kuruldu.

Önceki geliştirme: [geç gelen çalışma için bağlı ek dönem hesabı](docs/EK_DONEM_HAKEDISI.md), 1.3.20-dev içinde kuruldu; yalnız kullanılmamış onaylı kaynakları tüketir.

Önceki geliştirme: [tarihli tarife ve hesap temeli](docs/TARIHLI_TARIFE_VE_DONEM_HAKEDISI.md) üzerine [bağımsız mali onay ve tek seferlik aktarım](docs/DONEM_HAKEDISI_MALI_ONAY_VE_AKTARIM.md) eklendi; 1.3.19-dev içinde kuruldu.

2 Kasım 2026 önceki kaynak ve kurulum: [çok kişili toplu manuel puantaj](docs/TOPLU_MANUEL_PUANTAJ.md), **1.3.17-dev / 127 / API 1.17** olarak kuruldu. En fazla 200 yeni vardiyalı/vardiyasız satır, açık gözden geçirme, ortak transaction/makbuz ve kalıcı taslak korumalarıyla hazırlanır.

2 Kasım 2026 önceki kaynak ve kurulum: [kalıcı puantaj taslak kurtarma](docs/KALICI_PUANTAJ_TASLAK_KURTARMA.md), **1.3.16-dev / 127 / API 1.16** olarak kuruldu. Şifreli yerel taslak ve belirsiz gönderim kurtarma bulunur; vazgeçilen istek kalıcı sunucu makbuzuyla gecikmiş yazmaya karşı korunur.

2 Kasım 2026 önceki kaynak ve kurulum: [çalışan–hesap eşlemesi](docs/CALISAN_HESAP_ESLEMESI.md), **1.3.15-dev / 126 / API 1.15** olarak kuruldu. Yönetici açık personel eşlemesi yapar; geçmiş kimlik kanıtı korunur ve puantaj öz onayında kullanılır. Eşleme erişim yetkisi vermez.

2 Kasım 2026 önceki kaynak ve kurulum: [bağımsız proje parçası onayı](docs/BAGIMSIZ_PROJE_PARCASI_ONAYI.md), **1.3.14-dev / 125 / API 1.14** olarak kuruldu. Amir yalnız kendi projesinin parçasını onaylar/iade eder; ana durum güncel turdan türetilir. Kısmi proje onayları tek sayılır; iade turu kapatır, kararlar değişmez geçmişte kalır. Eski toplu onaylar yeniden açılmadan bağımsız karar gibi gösterilmez.

2 Kasım 2026 önceki kaynak ve kurulum: [gün içi çok proje zaman parçaları ve mola dağıtımı](docs/GUN_ICI_PROJE_VE_MOLA_DAGILIMI.md) eklendi. Kaynak/paket **1.3.13-dev**, hazırlanmış şema **124**, API **1.13**. Tarihli ek proje/ekip ataması, tam aralığı boşluksuz kapsayan parçalar, açık mola/ücret dakikası dağılımı, bütün ilgili proje haklarını isteyen tek onay paketi ve çift saymayan proje süre özetleri bulunur. Doğrulanmış yedek sonrası 124 uygulandı; 1.3.13-dev istemci/API kuruldu ve kısayol yeni pakete geçirildi. Denemeler atlandı. Parasal hakediş/ödeme üretilmez.

2 Kasım 2026 önceki kaynak ve kurulum: [ham giriş/çıkış olayları ve kontrollü puantaj eşlemesi](docs/HAM_OLAYLAR_VE_PUANTAJ_ESLEMESI.md) eklendi. Kaynak/paket **1.3.12-dev**, hazırlanmış şema **123**, API **1.12**. Tek olay / CSV önizlemesi, değişmez kaynak kayıtları, açık In-Out seçimi, saat dönüşümü önizlemesi, gerekçeli yoksayma/kaldırma ve kaynak katılımcıları için farklı hesapla onay bulunur. Doğrulanmış yedek sonrası 123 uygulandı; 1.3.12-dev istemci/API kuruldu ve kısayol yeni pakete geçirildi. Denemeler atlandı.

1 Kasım 2026 önceki kaynak ve kurulum: [vardiyasız puantaj](docs/VARDIYASIZ_PUANTAJ.md) eklendi. Kaynak/paket **1.3.11-dev**, hazırlanmış şema **122**, API **1.11**. Tarihli proje/ekip ataması olan kişinin vardiya planı olmadan gerçekleşen çalışması, mevcut sürüm/tekrar/çakışma ve farklı hesapla onay korumalarıyla kaydedilir. 2 Kasım 2026'da doğrulanmış yedek sonrası düzeltilen 122 uygulandı; istemci/API ve kısayol 1.3.11-dev sürümüne geçirildi. Denemeler atlandı; ücret/hakediş üretilmez.

31 Ekim 2026 önceki kaynak ve kurulum: [puantaj, istisnalar ve amir onayı](docs/PUANTAJ_VE_AMIR_ONAYI.md) eklendi. Kaynak/paket **1.3.10-dev**, hazırlanmış şema **121**, API **1.10**. Yayınlanmış kişi vardiyasına manuel / önizlemeli CSV taslak girişi, gerçekleşen mola/net dakika, ayrı ücretlendirilecek süre, istisna filtresi ve farklı hesapla onay/iade/yeniden açma bulunur. Doğrulanmış yedek sonrası 121 uygulandı; 1.3.10-dev istemci/API kuruldu ve kısayol yeni pakete geçirildi. Denemeler atlandı; ücret/hakediş üretilmez.

31 Ekim 2026 önceki kaynak ve kurulum: [vardiya ve izin takvimi](docs/VARDIYA_VE_IZIN_TAKVIMI.md), **1.3.9-dev / şema 120 / API 1.9** ile bu bilgisayara kuruldu. Haftalık proje/ekip planı, şablon, gece vardiyası, yayın/iptal, kapasite, önizlemeli hafta kopyalama ve izin talep/onay/ret/iptal akışı bulunur. Kişi/dönem/proje ve eski izin çakışma kontrolleri sunucudadır.

1.3.4-dev ile eklenen [bütünleşik personel kartı](docs/BUTUNLESIK_PERSONEL_KARTI.md) 1.3.5-dev içinde kullanılabilir.

Bu bilgisayarda **31 Ekim 2026'da [1.3.0-dev istemci ve API kurulumu](docs/1_3_0_DEV_KURULUMU.md)** tamamlandı; Muhasebe - Güncel kısayolu yeni istemciye yönlendirildi. Mevcut veri/ayarlar korundu; işlev denemeleri atlandı. Bu yerel geliştirme kurulumu, GitHub'daki v1.2.0 indirme bağlantısını veya kararlı Release durumunu değiştirmez.

30 Ekim 2026 yerel kaynak: [sürümlü paketleme, Hakkında ve Windows kurulum sihirbazı](docs/SURUMLU_DAGITIM_VE_KURULUM.md) eklendi. Geliştirme sürümü 1.3.0-dev'dir; v1.3.0 henüz yayımlanmadı. `automation/Build-WindowsRelease.ps1`, EXE sürümünü doğrulayarak ZIP/manifest/SHA256 üretir; etiket CI'si Release taslağı hazırlamak üzere düzenlendi. İşlev ve kurulum denemeleri atlandı, çalışan API/kısayol değiştirilmedi.

Bu bilgisayarda 30 Ekim 2026'da [yerel HTTPS API kurulumu](docs/YEREL_API_KURULUMU.md) yapıldı; masaüstü tercihi ve Muhasebe - Güncel kısayolu yeni istemciye geçirildi. Bu makineye özel kurulum yalnızca localhost erişimi sağlar; yayımlanmış v1.2.0 paketinin kapsamı değişmez.

30 Ekim 2026, yerel geliştirme: [merkezi API altyapısı](docs/MERKEZI_API_ALTYAPISI.md) ve [masaüstü bağlantısı](docs/MASAUSTU_MERKEZI_API_BAGLANTISI.md) eklendi. Giriş ekranından yerel/API seçimi yapılır; HTTPS, sunucuda oturum/yetki kontrolü ve kalıcı ödeme günlüğü kullanılır. İlk sunucu Windows içindir. Bu çalışma yayımlanmış v1.2.0 paketine dahil değildir; mevcut tercih/kısayol ve canlı veritabanı değiştirilmedi, denemeler atlandı.

Tam doğrulama (26 Ekim 2026): **392 test başarılı, 0 başarısız, 0 atlanan**. Release derlemesi 0 uyarı / 0 hata ile tamamlandı; entegrasyon ve geri yükleme testleri ayrı, geçici LocalDB motorunda çalıştı.

Arayüz güncellemesi (26 Ekim 2026): [Windows 2000 görünümü, yenilenen ekranlar ve görseller](docs/WINDOWS_2000_DENEME.md). Giriş, ana ekran, personel, arama, özet, hesap yönetimi, ödeme, ayarlar, izin ve parola pencereleri düzenlendi. Bu klasik görünüm **v1.2.0 Windows paketinde** korunur.

Responsive arayüz güncellemesi (27 Ekim 2026): ana ekran ve diğer 11 ekran/akış pencere boyutuna uyarlandı; sütunlar, bölümler, listeler ve işlem düğmeleri kullanılabilir alana göre yerleşir. Girilmiş bilgiler, seçimler ve güvenlik kontrolleri korunur. [Ekranlar, önizlemeler ve deneme adımları](docs/RESPONSIVE_ARAYUZLER.md). Bu değişiklikler **28 Ekim 2026'da yayımlanan v1.2.0 Windows paketine** dahildir.

Güncel kaynak doğrulaması (27 Ekim 2026): **610 test başarılı, 0 başarısız, 0 atlanan**; Release derlemesi 0 uyarı / 0 hata. Entegrasyon ve geri yükleme testleri gerçek kullanıcı veritabanından ayrı, testin oluşturduğu geçici LocalDB motorunda tamamlandı; motor temizlendi. [CI ortamındaki ekran boyutu sınırlamasının düzeltmesi](docs/CI_RESPONSIVE_TESTLER.md), geniş yerleşimlerin küçük sanal ekranlarda da gerçekten sınanmasını sağlar.

Yeni özellikler:

Yerel geliştirme (henüz v1.2.0 paketinde değil): 10 dakika uygulama etkinliği olmadığında otomatik kilit ve kaydedilmemiş alanlar için grup bazlı uyarılar. Yeni SQL yükseltmesi gerekmez; mevcut 112 şeması kullanılır. [Kullanım ve sınırlar](docs/OTOMATIK_KILIT_VE_DEGISIKLIK_UYARISI.md).

Yerel geliştirme (henüz v1.2.0 paketinde değil): dışa aktarmada rapor/kapsam ve sütun seçimi; tamamlanmış günleri değişmez içerikle saklayan kesin kapanış ekranı. Bu bilgisayarda 30 Ekim 2026'da doğrulanmış yedek sonrası 112 uygulandı; masaüstü kısayolu uyumlu pakete yönlendirildi, denemeler atlandı. [Rapor seçimi ve kapanış kullanımı](docs/SECILEBILIR_RAPOR_VE_KALICI_KAPANIS.md).

Yerel geliştirme (henüz v1.2.0 paketinde değil): personel ekranlarında sayfalama ve aramalı finans personel seçimi; Genel Durum hesapları SQL tarafında. Bu bilgisayarda 30 Ekim 2026'da doğrulanmış yedek sonrası 111 destekleyici indeks yükseltmesi uygulandı; masaüstü kısayolu uyumlu pakete yönlendirildi. Denemeler/hız ölçümleri atlandı. [Kullanım ve performans sınırları](docs/PERSONEL_SAYFALAMA_VE_SQL_OZETLERI.md).

Yerel geliştirme (henüz v1.2.0 paketinde değil): proje bazlı manuel hakediş, diğer gider ve maliyet özeti; maliyete ikinci kez eklenmeyen ayrı avans ödemesi. 110 yükseltmesi gerekir. Bu bilgisayarda 30 Ekim 2026'da doğrulanmış yedek sonrası 110 uygulandı; masaüstü kısayolu uyumlu pakete yönlendirildi, denemeler atlandı. [Kullanım ve hesap sınırları](docs/HAKEDIS_AVANS_PROJE_MALIYETI.md).

Yerel geliştirme (henüz v1.2.0 paketinde değil): kayıt bazlı eski/yeni değerler, yapan hesap, UTC zaman ve gerekçeyi saklayan işlem geçmişi; yöneticiler için Rapor menüsünde geçmiş ekranı. 109 şema yükseltmesi gerekir. Bu bilgisayarda 29 Ekim 2026'da doğrulanmış yedek sonrası 109 uygulandı, masaüstü kısayolu uyumlu yerel pakete yönlendirildi; denemeler atlandı. [Kapsam ve kullanım](docs/AYRINTILI_ISLEM_GECMISI.md).

Yerel geliştirme (henüz v1.2.0 paketinde değil): ödeme ve kişisel veri dışa aktarma yetkileri personel düzenlemeden ayrıldı; yönetici hesabın ek izinlerini ayrı yönetebilir. 108 şema yükseltmesi gerektirir. Bu bilgisayarda 29 Ekim 2026'da doğrulanmış yedek sonrası 108 uygulandı ve masaüstü kısayolu uyumlu yerel pakete yönlendirildi; denemeler atlandı. [Kullanım ve geçiş](docs/AYRI_ODEME_VE_DISA_AKTARMA_YETKILERI.md).

Yerel geliştirme (henüz v1.2.0 paketinde değil): izin tarihleri değiştirilirken önceki/yeni değerleri saklayan geçmiş ve arşiv ekranında İzin değişiklikleri sekmesi eklendi. 107 şema yükseltmesi gerektirir. [Kullanım ve geçiş](docs/IZIN_DEGISIKLIK_GECMISI.md).

Yerel geliştirme (henüz v1.2.0 paketinde değil): personel silme yerine arşivleme, kalıcı kimlik ve geçmiş ekranı eklendi. 106 şema yükseltmesi gerektirir. [Kullanım ve geçiş](docs/PERSONEL_KIMLIGI_VE_ARSIV.md).

- Yönetici, personel sorumlusu ve görüntüleyici rolleri; servislerde yetki denetimi ve son yönetici koruması.
- Gerçek async veri erişimi, cancellation, repository ve bir işlem için UnitOfWork; commit yoksa rollback.
- Transaction'a bağlı audit, ödeme defteri ve atomik bakiye düşümü; tekrarlanan işlem kimliği ikinci ödeme oluşturmaz. Ödeme iptalini yönetici yapabilir.
- Açık/koyu tema, kolon düzeni, üç bölümlü durum çubuğu ve klavye kısayolları; şifrelenmiş personel/izin taslakları.
- Excel/CSV, A4 ve 80 mm ödeme fişi, QR referansı, günlük ödeme kapanışı, tarih sıralı ve renkli izin uyarıları, toplu izin planı.
- Yükseltmeden önce doğrulanmış yedek; yönetici oturumunda günlük yedek, son 14 kayıt, isteğe bağlı aynalama ve onaylı geri yükleme.

**Yerel kaynak değişikliği (henüz yayımlanan v1.2.0 paketinde değil):** Yedekler veritabanı kimliği ve kaynak kapsamına ayrılır. Yeni yedeği elle taşırken `.bak` ile yanındaki `.backup.json` dosyasını birlikte taşıyın. Eski kimliksiz yedekler otomatik silinmez veya normal geri yüklemede kabul edilmez. [Yedek kimliği, kapsam ve eski yedekler](docs/YEDEK_KIMLIGI_VE_KAPSAM.md).

PBKDF2-HMAC-SHA256, 600.000 yineleme, rastgele 16 bayt salt ve 32 bayt hash korunur. Eski parolalar kontrollü transaction ile dönüştürülür; bozuk hash açık metne geri düşmez. Kullanıcı listesinde parola veya hash okunmaz. Parola alanları maskelidir. Parola boşlukları korunur; yeni hesaplarda ve parola değişiminde sınır 15–1024 karakterdir. Mevcut kısa parolalar değiştirilmelerine kadar girişte geçerlidir.

Her kullanıcı üst menüdeki **Parolamı değiştir** ile mevcut parolasını doğrulayarak yeni parola belirleyebilir. Yönetici, hesap listesinden başka bir etkin hesabı seçip **Parola sıfırla…** ile kendi parolasını tekrar doğrular. Uygulama 30 dakika geçerli rastgele geçici parola üretir; bu hesap kendi parolasını belirleyene kadar diğer ekranlara erişemez. Parola değişimi ve sıfırlama eski oturumları geçersiz kılar. Ayrıntılar ve deneme adımları: [parola güvenliği kılavuzu](docs/PAROLA_DEGISTIRME_VE_SIFIRLAMA.md). Bu özellikler v1.2.0’dan beri Windows paketinde yer alır.

Hatalı giriş sınırı: aynı hesapta 15 dakika içinde 5 yanlış parola, 5 dakikalık geçici kilit oluşturur. Sayaç veritabanında tutulur; yeniden başlatma veya farklı pencereler sınırı aşamaz. Açık oturumlar etkilenmez. Mevcut parolayla yeniden doğrulamalar da aynı bütçeyi kullanır. [Kullanım ve deneme kılavuzu](docs/HATALI_GIRIS_SINIRLAMASI.md).

Para girişinde `1500,50`, `1500.50`, `1.500,50` ve `1,500.50` aynı tutardır. En fazla iki ondalık kabul edilir; `1.500`/`1,500` belirsiz olduğu için reddedilir. Önceki negatif tutar davranışı korunur; pozitif olmayan ödeme yapılamaz. Önceden yanlış kaydedilmiş tutarlar otomatik değiştirilmez.

Personel EKLE otomatik izin oluşturmaz. DÜZENLE izin tarihlerini değiştirmez. İZNİ KAYDET yalnızca seçili personelin izin dönemini kaydeder. Birden fazla eski izin dönemi varsa topluca değiştirme reddedilir. İzin hesabı başlangıç ve bitiş dahil takvim günüdür. Hayali izin hakedişi veya yasal izin kuralı üretilmez.

Personel tutarı **kalan/verilecek tutardır**. Yeni şema mevcut tutarları açılış bakiyesine aktarır. Düzenleme farkı deftere eklenir; ödeme tutarı atomik azaltır; iptal geri artırır. Arada ödeme yapıldıysa eski ekrandan kalan tutarı ezerek düzenleme engellenir. Personel silinse bile ödeme defteri korunur.

Ekranlar tek ApplicationContext ile yönetilir. Geçişte önceki pencere dispose edilir; çıkış kullanıcı ve rolü temizler. Bekleyen iş iptal edilir; kapalı ekrana gelen giriş sonucu yeni oturum açamaz.

Derleme ve test:

```powershell
.\dotnet.cmd restore WinFormsApp2.sln --locked-mode
.\dotnet.cmd build WinFormsApp2.sln -c Release --no-restore -p:TreatWarningsAsErrors=true
powershell.exe -NoProfile -File .\automation\otomatik_test_master.ps1 -Full
```

`-NoDatabase` yalnızca DB'siz testleri çalıştırır. Varsayılan master çalıştırma restore testini dışarıda bırakır; `-Full` yalnızca testin oluşturduğu GUID adlı veritabanında restore yapar. Gerçek Muhasebe2.mdf test hedefi olmaz. Raporlar `artifacts/analysis/master-*/` altında JSON, Markdown ve TRX olarak üretilir. Testler ayrı geçici klasör kullanır; antivirüs ayarlarına dokunulmaz.

SDK `global.json` ile 10.0.402 sürümüne sabitlenmiştir. `dotnet.cmd` varsa `artifacts/tools/dotnet` içindeki SDK'yı, yoksa sistemdeki dotnet.exe'yi kullanır. Paket kilitleri kaynak denetimindedir. `.github/workflows/ci.yml`, Windows üzerinde aynı tam testleri ve self-contained paketlemeyi tanımlar; uzak GitHub çalıştırması yerel testten ayrı bir işlemdir.

Paket üretimi:

```powershell
.\dotnet.cmd publish WinFormsApp2.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -p:TreatWarningsAsErrors=true -o artifacts\releases\v1.2.0
.\dotnet.cmd publish tools\DatabaseSetup\AccountingApp.DatabaseSetup.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:PublishTrimmed=false -p:RestoreLockedMode=true -p:TreatWarningsAsErrors=true -o artifacts\releases\v1.2.0
```
