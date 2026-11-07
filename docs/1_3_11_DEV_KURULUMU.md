# 1.3.11-dev ve 122 şema kurulumu

2 Kasım 2026 · Kullanıcı onayıyla kurulum; işlev denemeleri atlandı.

İstemci/API **1.3.11-dev**, dosya sürümü **1.3.11.0**, şema **122**, API **1.11**. [Vardiyasız puantaj kullanım rehberi](VARDIYASIZ_PUANTAJ.md).

## Yedek ve yükseltme

Açık istemci ve bekleyen ödeme bulunmadı. API otomatik başlangıç görevi bakım için devre dışı bırakıldı. Aynı Windows kullanıcısının geçici bakım görevinde mevcut veritabanının kimlikli COPY_ONLY/CHECKSUM yedeği alındı; RESTORE VERIFYONLY WITH CHECKSUM, kapsam manifesti ve dosya özeti doğrulandı. Yalnız 122'nin beklediği ve uygulanmış 100–121 betiklerinin değişmediği kontrol edildi. İlave güvenlik kopyaları oluşturuldu.

İlk uygulamada 122 betiğindeki dinamik EXEC ifadesinde QUOTENAME kullanımından kaynaklanan SQL sözdizimi hatası yükseltmeyi durdurdu; transaction geri alındı. Henüz uygulanmamış 122, SQL metnini önce değişkende oluşturup sp_executesql ile çalıştıracak şekilde düzeltildi. İstemci/API aynı düzeltilmiş betikle yeniden paketlendi. Önceki yedek ve içerik özetlerinin hâlâ geçerli olduğu kontrol edilerek yalnız 122 yeniden uygulandı. Eski migration dosyaları değiştirilmedi. İlk hata kayıtları saklandı; otomatik veritabanı geri yüklemesi yapılmadı.

Personel, izin, defter, finans, kapanış, kimlik, arşiv/izin/audit geçmişi, proje/ekip/atama, çalışma dönemleri/bağlantıları, grup geçişleri, proje ödeme talepleri, proje hakları, takvim kayıtları ve mevcut Timesheets/TimesheetCommands içeriklerinin önce/sonra özetleri aynı kaldı. Hesap oturum nesilleri ve ilgili hesap rowversion değerleri beklendiği gibi yenilendi; diğer eski satırların rowversion değerleri karşılaştırmaya dahil edildi. Parola hash'leri, roller ve mevcut yetkiler korundu. Sahte vardiya veya vardiyasız çalışma kaydı üretilmedi.

Nullable vardiya bağlantısı, planlı kayıt için filtreli tekil indeks, güvenilir vardiya foreign key'i ve NULL dahil kaynak değişmezliği koruması metadata üzerinden doğrulandı. Üç etkin koruma/audit trigger'ı, beş adlandırılmış güvenilir CHECK, altı güvenilir FK ve altı indeks bulundu. Devre dışı/güvenilmeyen CHECK veya FK yok. Mevcut audit gizlilik fonksiyonu korundu. Bunlar işlev senaryosu denemeleri değildir.

Doğrulanmış ön yedek `artifacts/analysis/live-upgrade-122/backups/AccountingApp-v2-<kapsam-kimliği>-20261102T0355228754653-pre122.bak` dosyasıdır. Aynı klasörde ilave güvenlik kopyaları bulunur. Özel bakım kayıtları, içerik özetleri ve ilk hata kayıtları `artifacts/analysis/live-upgrade-122/` altındadır; Git dışındadır.

## Paket, API ve kısayol

Kurulan düzeltilmiş paket kaynağı `artifacts/release-bundles/unplanned-timesheets-122-install-final/` klasörüdür. İlk `unplanned-timesheets-122-delivery` paketi kullanılmamalıdır; yerel olarak SUPERSEDED.txt ile işaretlendi. ZIP SHA-256 ve manifestteki dosya özetleri doğrulandı. İstemci/API `%LOCALAPPDATA%\Programs\AccountingApp\1.3.11-dev-20261102065743268` altında ayrı klasörlere kuruldu; eski sürüm dosyaları korundu.

Sunucu/istemci bağlantı profilleri, veritabanı profili/kapsam sabitlemesi, HTTPS sertifikası ve kullanıcı tercihleri korundu. Geçici AccountingApp Maintenance122 görevi kaldırıldı. MSSQLLocalDB zorlamadan durdurulup kalıcı AccountingApp Local API görevi yeni sürüme geçirildi. API 1.11 HTTPS başlangıcı ve beklenen süreç doğrulandı; adres https://localhost:7443/ ve yalnız IPv4/IPv6 loopback erişimi korundu. Görev Running durumunda.

**Muhasebe - Güncel** kısayolu yeni `desktop/payload/WinFormsApp2.exe` dosyasına yönlendirildi; hedef/çalışma klasörü tekrar okunarak doğrulandı. Simge ve sürüm açıklaması da güncellendi. 122 eski oturumları geçersizleştirdiğinden mevcut kullanıcı adı/parolayla yeniden giriş gerekir.

Proje üzerinden **Puantaj ve amir onayı… → Vardiyasız çalışma…** kullanılabilir. Mevcut CanEnterTimesheets ve CanApproveTimesheets ayrımı sürer; yeni hak verilmedi. Kişinin güncel dönemine ait tarihli proje/ekip ataması gerekir. Yönetici dahil hazırlayan / son düzenleyen / gönderen hesap kendi puantajını onaylayamaz. Yeni yol ücret veya hakediş üretmez.

Kurulum kayıtları ve önceki görev/kısayol/profil kopyaları `artifacts/analysis/install-1.3.11-dev/` altında; son durum `deployment.json` / `readback.json`, kurulu klasörde `installation.json` dosyasındadır. Masaüstü açılmadı. Vardiyasız giriş, eski seçim, tekrar gönderim, çakışma, onay/iade ve UI denemeleri kullanıcı isteğiyle atlandı. Yalnız gerekli derleme/paket bütünlüğü, yedek/veri koruma, şema nesneleri, ayar koruması, API başlangıcı ve kısayol kontrolleri yapıldı. GitHub push veya Release yapılmadı.

Eski 121 bekleyen API yeni şemaya karşı çalıştırılmamalıdır. Eski dosyaların saklanması otomatik veritabanı geri dönüşü sağlamaz.
