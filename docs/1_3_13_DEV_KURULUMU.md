# 1.3.13-dev ve 124 şema kurulumu

2 Kasım 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci/API **1.3.13-dev**, dosya sürümü **1.3.13.0**, şema **124**, API **1.13**. [Gün içi proje ve mola dağılımı kullanım rehberi](GUN_ICI_PROJE_VE_MOLA_DAGILIMI.md).

## Yedek ve yükseltme

Açık masaüstü istemcisi ve bekleyen ödeme bulunmadı. Eski API görevi bakım için devre dışı bırakılıp API durduruldu. Aynı Windows kullanıcısının geçici bakım görevinde mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alındı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 124'ün beklediği ve uygulanmış 100–123 betiklerinin değişmediği kontrol edildi. İlave güvenlik kopyası oluşturularak 124 transaction içinde uygulandı. Betik/paket düzeltmesi gerekmedi.

Personel, izin, ödeme defteri, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemleri/bağlantıları, grup geçişleri, proje ödeme talepleri, proje hakları, takvim, puantaj/komut, ham olay ve eşleme içeriklerinin önce/sonra özetleri aynı kaldı. Hesap oturum nesilleri ve ilgili hesap rowversion değerleri beklendiği gibi yenilendi; diğer eski satırların rowversion değerleri karşılaştırmaya dahil edildi. Parola hash'leri, roller ve mevcut haklar korundu.

TimesheetAllocationSets ve TimesheetSegments tabloları boş oluşturuldu. Eski çalışmalardan dağılım/parça üretilmedi. Mevcut atamalar IsSupplementary=0 ile asıl kaldı; ek atamaya dönüşüm yapılmadı. Altı etkin koruma/audit trigger'ı, beş adlandırılmış güvenilir CHECK, beş güvenilir foreign key ve beş indeks metadata üzerinden doğrulandı. Tek etkin dağılım indeksi, ek atama alanının kapalı varsayılanı ve genişletilmiş audit gizlilik fonksiyonu doğrulandı. Yeni tablolarda devre dışı/güvenilmeyen CHECK veya FK yok. Bunlar kullanıcı işlev senaryosu denemeleri değildir.

Doğrulanmış ön yedek `artifacts/analysis/live-upgrade-124/backups/AccountingApp-v2-<kapsam-kimliği>-20261102T0541363840832-pre124.bak` dosyasıdır. Aynı klasörde ilave güvenlik kopyası bulunur. Özel bakım kayıtları, manifest ve önce/sonra içerik özetleri `artifacts/analysis/live-upgrade-124/` altındadır; Git dışındadır.

## Paket, API ve kısayol

Paket kaynağı `artifacts/release-bundles/work-segments-124-delivery/` klasörüdür. ZIP SHA-256 ve manifestteki dosya özetleri doğrulandı. İstemci/API `%LOCALAPPDATA%\Programs\AccountingApp\1.3.13-dev-20261102084147983` altında ayrı klasörlere kuruldu; eski sürüm dosyaları korundu.

Sunucu/istemci bağlantı profilleri, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance124 görevi kaldırıldı. MSSQLLocalDB zorlamadan durdurulup kalıcı AccountingApp Local API görevi yeni sürüme geçirildi. API 1.13 HTTPS başlangıcı ve beklenen süreç doğrulandı; adres https://localhost:7443/ ve yalnız IPv4/IPv6 loopback erişimi korundu. Görev Running durumunda.

**Muhasebe - Güncel** kısayolu yeni `desktop/payload/WinFormsApp2.exe` dosyasına yönlendirildi; hedef, çalışma klasörü ve simge ayarlandı. Hedef/çalışma klasörü tekrar okunarak doğrulandı. 124 eski oturumları geçersizleştirdiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir.

Yeni akış **Puantaj ve amir onayı… → Proje / mola dağılımı…** üzerinden açılır. Gerekirse kişi için başka projede **Gün içi ek proje/ekip ataması** hazırlanır; mevcut asıl atama korunur. Ana taslak/iade puantajın saat, mola ve ücret dakikası tamamlanmış olmalı. Dağılım bütün aralığı boşluksuz/çakışmasız kapsar ve toplamlarla uzlaşır. Hazırlama/kaldırma ve gönderim/onay/iade bütün ilgili proje haklarını ister. Proje süre özetleri parçaları kullanır; bölünmüş ana süreyi tekrar saymaz. Ücret/hakediş veya ödeme üretilmez.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları `artifacts/analysis/install-1.3.13-dev/` altında; son durum `deployment.json` / `readback.json`, kurulu klasörde `installation.json` dosyasındadır. Masaüstü açılmadı. Ek atama, dağılım/önizleme/kaldırma, eski seçim, eşzamanlılık, proje yetkileri, mola/toplam kontrolü ve UI denemeleri kullanıcı isteğiyle atlandı. Yalnız gerekli bakım aracı derlemesi, yedek/veri koruma, şema nesneleri, paket bütünlüğü, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. GitHub push veya Release yapılmadı.

Eski 123 bekleyen API yeni şemaya karşı çalıştırılmamalıdır. Eski dosyaların saklanması otomatik veritabanı geri dönüşü sağlamaz.
