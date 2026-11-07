# AccountingApp merkezi API sunucusu

Bu paket Windows x64 sunucu içindir; sürüm 1.3.33-dev, API sözleşmesi 1.32, gerekli şema 137'dir. Şema yükseltmesi ayrıca uygulanır; paket canlı veritabanını otomatik değiştirmez. API konsolsuz WinExe olarak derlenir; sunucu logları ve yaşam döngüsü görev/başlatıcıdan yönetilir. Masaüstü kısayolunu `AccountingApp.Server.exe` dosyasına yönlendirmeyin.

- [Sunucu profili, HTTPS sertifikası ve kurulum](docs/MERKEZI_API_ALTYAPISI.md)
- [Masaüstü bağlantısı, oturum ve ödeme geçiş kuralları](docs/MASAUSTU_MERKEZI_API_BAGLANTISI.md)

`server.example.json` ve `database-profile.example.json` örnektir; gerçek bağlantı profili/TLS kurulumu gerekir. Başlangıçta veritabanı şeması/kapsamı yalnızca okunarak kontrol edilir. Bu paket mevcut şemayı otomatik yükseltmez ve masaüstünü otomatik bağlamaz.

30 Ekim 2026: kaynak derlemesi ve self-contained yayın tamamlandı. Kullanıcı isteğiyle test/çalışma zamanı denemeleri atlandı; canlı kurulum yapılmadı.

İstemci/API birlikte güncellenmeli; bu sürüm ücret/gider kuralları için şema 136 gerektirir. Canlı veritabanı yükseltmesi paket açılırken otomatik uygulanmaz. Başlatıcı hazır olma kontrollerinde paket manifestindeki `apiVersion` kullanılmalıdır. Ayrıntı: `docs/CALISMA_DONEMLERI.md`.

Kısıtlı ProjectUser rolü yalnız yetkili projelerini kullanır; merkez rollerinin genel erişimi sürer. Proje izin yönetimi yöneticidedir. Ayrıntı: `docs/PROJE_SORUMLUSU_VE_YETKILER.md`.

1.3.4-dev bütünleşik personel kartı mevcut şema 115 ile çalışır; yeni şema yükseltmesi yoktur. Kart genel merkez rollerine açıktır; ProjectUser için kapalıdır. Ad/IBAN kaydı bakiye veya defter değiştirmez. Ayrıntı: `docs/BUTUNLESIK_PERSONEL_KARTI.md`.

1.3.5-dev: yeni audit gövdeleri maskeli saklanır; eski gövdeler AuditLogSafe üzerinden maskeli okunur. Şema 116 hazırlanmıştır, canlıya uygulanmadı. Ayrıntı: `docs/ISLEM_GECMISI_VERI_MINIMIZASYONU.md`.

1.3.6-dev: POST employment/group-transition, sunucuda kişi/dönem sürümü ve tekrar kontrolü; aynı kişi için tek açık grup dönemi. Şema 117 hazırlanmıştır, uygulanmadı. Ayrıntı: `docs/TARIHLI_PERSONEL_GRUBU_GECISI.md`.

1.3.7-dev: UserInfo yeni okuma haklarını taşır; özet tutarları null olabilir, gizli personel sütunları null döner. Genel tablo/pano export her iki okuma hakkını ve export iznini ister. Şema 118 hazırlanmış, uygulanmamıştır. Ayrıntı: `docs/UCRET_VE_IBAN_GORUNTULEME_YETKILERI.md`.

1.3.8-dev: project-payments proje kuyruğu/prepare/decide uçları; yeni proje etiketli genel submit engeli ve atomik onay/ödeme. Şema 119 hazırlanmış, uygulanmamıştır. Ayrıntı: `docs/PROJE_ODEME_HAZIRLAMA_VE_ONAY.md`.

1.3.9-dev: schedule hafta/şablon/kişi/yayın/izin/kopyalama uçları, ayrı planlama ve izin onay proje hakları. Şema 120 hazırlanmış, uygulanmamıştır. Ayrıntı: `docs/VARDIYA_VE_IZIN_TAKVIMI.md`.

1.3.10-dev: timesheets hafta/taslak toplu kayıt/durum ve CSV şablonu uçları; CanEnterTimesheets / CanApproveTimesheets, ayrı hesapla onay, sürüm ve kalıcı işlem kimliği koruması. 121 hazırlanmış, canlıya uygulanmamıştır. Ayrıntı: `docs/PUANTAJ_VE_AMIR_ONAYI.md`. Yukarıdaki eski sürüm kaynak notları tarihsel kapsamdır.

1.3.11-dev: POST timesheets/unplanned ve proje kapsamlı roster seçimi; mevcut onay/tekrar ve kaynak kimliği korumalarıyla vardiyasız çalışma. Şema 122 hazırlanmış, canlıya uygulanmamıştır. Ayrıntı: `docs/VARDIYASIZ_PUANTAJ.md`.

1.3.12-dev: attendance window/import/match/release/ignore uçları; değişmez ham kayıt, kaynak tekilliği, mevcut proje puantaj hakları ve kaynak katılımcılarından bağımsız onay. Şema 123 hazırlanmış, canlıya uygulanmamıştır. Ayrıntı: `docs/HAM_OLAYLAR_VE_PUANTAJ_ESLEMESI.md`.

1.3.13-dev: allocations targets/read/save/release uçları; AssignmentWrite.IsSupplementary, TimesheetWeek.Segments, tek sayımlı proje süreleri ve bütün hedeflerde puantaj yetkisi. Şema 124 hazırlanmış, canlıya uygulanmamıştır. Ayrıntı: `docs/GUN_ICI_PROJE_VE_MOLA_DAGILIMI.md`.

1.3.14-dev: allocations/segment-decision ve proje kapsamlı segments/{id}/decisions; tur/karar sürümleri, yerel amir yetkisi ve türetilen ana durum. Şema 125 hazırlanmış, canlıya uygulanmamıştır. Ayrıntı: `docs/BAGIMSIZ_PROJE_PARCASI_ONAYI.md`.

1.3.15-dev: yönetici açık hesap/personel eşlemesi yapabilir; geçmiş bağlar korunur, aynı çalışan/hazırlayan kimliğinin puantaj onayı engellenir. Eşleme yeni veri erişimi vermez. Ayrıntı: `docs/CALISAN_HESAP_ESLEMESI.md`.

1.3.16-dev: taslak içerikleri Windows profiline şifreli saklanır. Sunucu kendi proje kapsamını, özgün komut makbuzunu ve kalıcı vazgeçmeyi doğrular; arka plan gönderimi yapmaz. Ayrıntı: `docs/KALICI_PUANTAJ_TASLAK_KURTARMA.md`.

1.3.17-dev: en fazla 200 yeni vardiyalı/vardiyasız satır tek yetkili proje paketi, transaction ve makbuzla saklanır. Mevcut şema 127 yeterlidir. Kalıcı taslak/iptal korumaları sürer. Ayrıntı: `docs/TOPLU_MANUEL_PUANTAJ.md`.

1.3.18-dev: saatlik TL tarifesi ve mühürlü dönem hesap dökümü; onaylı kaynak/sürüm ve tekillik koruması. Bakiye, defter, bordro veya ödeme oluşturmaz. Ayrıntı: `docs/TARIHLI_TARIFE_VE_DONEM_HAKEDISI.md`.

Şifreli yedek, anahtar ve güvenli tanı paketi: [kullanım ve sınırlar](docs/YEDEK_VE_DESTEK_GUVENLIGI.md). AccountingApp.BackupRecovery.exe yalnız operatörün elle çalıştıracağı konsol aracıdır; otomatik başlangıca eklenmez.

Güncel doğrulama, imza modu ve çok bilgisayarlı bakım: [dağıtım rehberi](docs/DAGITIM_SONLANDIRMA_VE_COK_BILGISAYAR.md).
