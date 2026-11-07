# 1.3.12-dev ve 123 şema kurulumu

2 Kasım 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci/API **1.3.12-dev**, dosya sürümü **1.3.12.0**, şema **123**, API **1.12**. [Ham olay ve kontrollü puantaj eşlemesi kullanım rehberi](HAM_OLAYLAR_VE_PUANTAJ_ESLEMESI.md).

## Yedek ve yükseltme

Açık masaüstü istemcisi ve bekleyen ödeme bulunmadı. Eski API görevi bakım için devre dışı bırakılıp API durduruldu. Aynı Windows kullanıcısının geçici bakım görevinde mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alındı. RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 123'ün beklediği ve uygulanmış 100–122 betiklerinin değişmediği kontrol edildi. İlave güvenlik kopyası oluşturularak 123 transaction içinde uygulandı. Betik/paket düzeltmesi gerekmedi.

Personel, izin, ödeme defteri, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemleri/bağlantıları, grup geçişleri, proje ödeme talepleri, proje hakları, takvim, puantaj ve kalıcı puantaj komut içeriklerinin önce/sonra özetleri aynı kaldı. Hesap oturum nesilleri ve ilgili hesap rowversion değerleri beklendiği gibi yenilendi; diğer eski satırların rowversion değerleri karşılaştırmaya dahil edildi. Parola hash'leri, roller ve mevcut haklar korundu.

AttendanceEvents ve AttendanceMatches tabloları boş oluşturuldu. Eski puantaj saatlerinden ham olay/eşleme üretilmedi. Beş etkin koruma/audit trigger'ı, dört adlandırılmış güvenilir CHECK, sekiz güvenilir foreign key ve yedi indeks metadata üzerinden doğrulandı. Üç etkin eşleme tekillik indeksi, projeler arasında kaynak/satır tekilliği, yedi kesir basamaklı iki zaman alanı ve genişletilmiş audit gizlilik fonksiyonu doğrulandı. Yeni tablolarda devre dışı/güvenilmeyen CHECK veya FK yok. Bunlar kullanıcı işlev senaryosu denemeleri değildir.

Doğrulanmış ön yedek `artifacts/analysis/live-upgrade-123/backups/AccountingApp-v2-<kapsam-kimliği>-20261102T0427454685154-pre123.bak` dosyasıdır. Aynı klasörde ilave güvenlik kopyası bulunur. Özel bakım kayıtları, yedek manifesti ve önce/sonra içerik özetleri `artifacts/analysis/live-upgrade-123/` altındadır; Git dışındadır.

## Paket, API ve kısayol

Paket kaynağı `artifacts/release-bundles/attendance-events-123-delivery/` klasörüdür. ZIP SHA-256 ve manifestteki dosya özetleri doğrulandı. İstemci/API `%LOCALAPPDATA%\Programs\AccountingApp\1.3.12-dev-20261102072758142` altında ayrı klasörlere kuruldu; eski sürüm dosyaları korundu.

Sunucu/istemci bağlantı profilleri, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance123 görevi kaldırıldı. MSSQLLocalDB zorlamadan durdurulup kalıcı AccountingApp Local API görevi yeni sürüme geçirildi. API 1.12 HTTPS başlangıcı ve beklenen süreç doğrulandı; adres https://localhost:7443/ ve yalnız IPv4/IPv6 loopback erişimi korundu. Görev Running durumunda.

**Muhasebe - Güncel** kısayolu yeni `desktop/payload/WinFormsApp2.exe` dosyasına yönlendirildi; hedef, çalışma klasörü ve simge ayarlandı. Hedef/çalışma klasörü tekrar okunarak doğrulandı. 123 eski oturumları geçersizleştirdiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir.

Proje üzerinden **Puantaj ve amir onayı… → Ham giriş / çıkış olayları…** kullanılabilir. Yeni yetki verilmedi; mevcut CanEnterTimesheets / CanApproveTimesheets ayrımı sürer. Ham olaylar ve eşlemeler ilk açılışta boştur; kişi/dönem/atama açıkça seçilerek manuel veya tek kişilik CSV alımı yapılır. Eşleme mevcut taslak/iade puantajda kullanıcı seçimiyle çalışır; otomatik yön, çıkış tahmini veya onay yapmaz. Ham saniye/kesir korunur; EventMinuteFloor.v1 puantaj dönüşümü önizlemede açıkça kabul edilir. Kaynak olayları giren/eşleyen hesap bağlı puantajı onaylayamaz. Ücret/hakediş üretilmez.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları `artifacts/analysis/install-1.3.12-dev/` altında; son durum `deployment.json` / `readback.json`, kurulu klasörde `installation.json` dosyasındadır. Masaüstü açılmadı. Manuel/CSV alımı, tekrar aktarım, eşleme/kaldırma/yoksayma, eski seçim, çakışma, onay ve UI denemeleri kullanıcı isteğiyle atlandı. Yalnız gerekli bakım aracı derlemesi, yedek/veri koruma, şema nesneleri, paket bütünlüğü, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. GitHub push veya Release yapılmadı.

Eski 122 bekleyen API yeni şemaya karşı çalıştırılmamalıdır. Eski dosyaların saklanması otomatik veritabanı geri dönüşü sağlamaz.
