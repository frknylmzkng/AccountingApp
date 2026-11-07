# 1.3.33-dev pilot ve teslim kaydı

Yayımlanan **v1.3.33 / şema 138** için güncel adımları ve doldurulabilir kabul tablosunu [gerçek kullanım pilotu ve kabul kılavuzunda](GERCEK_KULLANIM_PILOTU_VE_KABUL_V1_3_33.md) bulabilirsiniz. Bu sayfa geliştirme paketinin tarihsel teslim kaydıdır.

7 Kasım 2026 · Yerel Windows kurulumu · Şema 137 · API 1.32 · .NET 10.0.14

Kullanıcı, kurulu 1.3.33-dev sürümünün düzgün çalıştığını bildirdi. Teslim hazırlığında istemci ve API arşivlerinin dosya özetleri, kurulum kaydındaki kısayol hedefi ve kurulum öncesi yedeğin SHA-256 özeti doğrulandı. Kurulum sırasında 61 tablonun içerik özetleri eşleşmişti. Bu kayıtta pilot senaryoları **bekliyor** durumundadır. [Kurulum kaydı](1_3_33_DEV_KURULUMU.md)

| Teslim kalemi | Konum ve kullanım |
| --- | --- |
| Yerel uygulama | Masaüstündeki **Muhasebe - Güncel** kısayolu. API adresi `https://localhost:7443/`. |
| Kurulum dosyaları | `artifacts/release-bundles/static-review-137-final/` içindeki 1.3.33-dev masaüstü ve API ZIP’leri, manifestler ve `SHA256SUMS.txt`. Paketler imzasız geliştirme paketidir. |
| Bakım kanıtı | `artifacts/analysis/install-1.3.33-dev/completion.json` ve aynı klasördeki kurulum ve izin kayıtları. |
| Kurulum öncesi yedek | `artifacts/analysis/preinstall-backup-1.3.33/backups/` içindeki `*-pre-1.3.33.bak`. Yedek şifresizdir ve özel erişimli klasörde saklanır. Yanındaki manifest yedek kimliğini ve özeti taşır. |

## Pilot kapsamı ve operatör devri

Pilot açılışında sorumlu operatörü, gözden geçiren kişiyi, proje/ekibi ve tarih aralığını kaydedin. Kapsam için bir proje ve bir tam iş dönemi seçin. Mevcut iş takibini pilot boyunca sürdürün. Aynı döneme ait kayıtları karşılaştırırken ortak referans kullanın. Tutarları bir deftere ikinci kez kaydetmek yerine iki taraftaki kayıtları referansla eşleştirin.

| Rol | Eğitimde gösterilecek akış |
| --- | --- |
| Personel ve planlama operatörü | Personel ve çalışma dönemini seçme, ekip/iş günü filtresi, izin ve puantaj işlemleri, taslak kaydetme ve yenileme. [Puantaj ve amir onayı](PUANTAJ_VE_AMIR_ONAYI.md) |
| Proje sorumlusu | Atandığı projede puantajı inceleme, gerekçeli karar, bekleyen adımlar ve rapor kapsamı. [Rapor merkezi](RAPOR_MERKEZI_GENISLEMESI.md) |
| Mali operatör ve onaylayan | Kaynak saatten hakedişe iz sürme, hazırlama/onay ayrımı, ödeme sonucu belirsizse işlem referansını sorgulama, avans tahsisi ve banka eşleştirme. [Hakediş aktarımı](DONEM_HAKEDISI_MALI_ONAY_VE_AKTARIM.md) · [Ödeme ve tahsis](ODEME_VE_AVANS_TAHSISI.md) · [Banka eşleştirmesi](BANKA_CSV_VE_ODEME_ESLESTIRME.md) |
| Sistem ve yedek sorumlusu | Sürüm ve bağlantı kontrolü, şifreli yedek, ayrı anahtar saklama, izinli tanı ZIP’i ve kontrollü geri dönüş. [Yedek ve destek](YEDEK_VE_DESTEK_GUVENLIGI.md) · [Dağıtım ve geri dönüş](DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md) |

## Kabul kaydı

Her maddeye tarih, sorumlu, kullanılan sürüm, kanıt konumu, sonuç ve varsa takip maddesi ekleyin. Sonuç alanı **Bekliyor**, **Geçti** veya **Düzeltme gerekiyor** değerlerinden biri olmalıdır.

| Kabul alanı | Kaydedilecek sonuç | Durum |
| --- | --- | --- |
| Saat ve izin mutabakatı | Kişi, iş günü ve proje bazında onaylı net dakika. Gece vardiyası, eksik olay, mola, çok proje ve izin çakışması farklarının açıklaması. | Bekliyor |
| Mali mutabakat | Tarihli ücret, hakediş, avans, ödeme ve banka eşleşmesi için beklenen ve gerçekleşen tutarlar. Her fark için işlem referansı ve gerekçe. Aynı kaynak ve aynı ödeme referansı ikinci kayıt oluşturmamalı. [Proje mali mutabakat raporu](PROJE_MALI_MUTABAKAT_RAPORU.md) | Bekliyor |
| Kullanılabilirlik ve yetki | Klavye, ekran okuyucu, DPI, süre/bellek ve proje sınırı sonuçları. [Ayrıntılı kabul matrisi](PERFORMANS_ERISILEBILIRLIK_GUVENLIK_2026_11_07.md) | Bekliyor |
| Kurtarma ve geri dönüş | Ayrı hedefte şifreli yedek ve kurtarma anahtarı kullanılarak yapılan kurtarma kaydı. Kapsam, şema, yeni oturum, mali toplam ve işlem referansları karşılaştırılır. Geri dönüş öncesi bekleyen ödemeler ve yedekten sonraki hareketler kayda alınır. | Bekliyor |
| Dağıtım ve devir | Kullanılan topolojide kurulum kanıtı, CI/Release kaydı, paket özeti, varsa yayıncı doğrulaması ve rol eğitim kaydı. | Bekliyor |

Pilot sırasında veri kaybı, yinelenen mali hareket, yetki aşımı veya kurtarma hatası görülürse ilgili akış durdurulur ve iş kaydı açılır. Operatör işlem referansını, etkilenen kapsamı, sürümü ve varsa tanı kodunu kaydeder. Geri dönüş için doğrulanmış yedek ile uyumlu istemci/API çifti seçilir. Üretim veritabanına geri yükleme ayrı bir bakım işlemi olarak kararlaştırılır. Pilot sorumlusu, açıklanamayan farkları ve kritik hataları kapattıktan sonra kabul sonucunu kaydeder.

**Geliştirme ve teslim hazırlığı için kalan adım sayısı 0.** İsteğe göre atlanan gerçek kullanım ve kurtarma denemeleriyle saha kabulü **1 açık iş paketi** olarak izlenir.
