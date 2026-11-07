# Ödeme ve kişisel veri dışa aktarma yetkileri

29 Ekim 2026 · Yerel kaynak ve Windows paketi. Yayımlanan v1.2.0 bu değişiklikleri içermez.

**İzleyen canlı geçiş tamamlandı (29 Ekim 2026):** Kullanıcının onayıyla doğrulanmış yedek alındı ve yalnızca 108 uygulandı. Önceki iş verilerinin/hesap bilgilerinin içerik özetleri korundu; beklenen oturum yenilemesi, iki bit alanı ve kapalı varsayılanları doğrulandı. Masaüstündeki Muhasebe - Güncel kısayolu aşağıdaki 108 uyumlu pakete yönlendirildi. Denemeler atlandı, uygulama başlatılmadı. Özel yedek ve sonuçlar `artifacts/analysis/live-upgrade-108/` altında Git dışındadır. Aşağıdaki hazırlık notu yükseltmeden önceki durumu kaydeder.

Personel düzenleme rolü artık kendiliğinden ödeme veya dışa aktarma hakkı vermez. Yönetici rolü tüm yetkilere sahiptir; diğer hesaplarda iki bağımsız ek izin bulunur.

| İşlem | Gerekli yetki |
|---|---|
| Personel adı/soyadı/IBAN ve izin düzenleme, arşivleme | Yönetici veya Personel Yöneticisi rolü |
| Ödeme başlatma, tekrar gönderme ve sonuç onayı | Ödeme izni |
| İlk bakiye girme veya mevcut bakiyeyi değiştirme | Personel düzenleme ve ödeme izni birlikte |
| Excel/CSV, tablodan Ctrl+C/Ctrl+Insert, fiş/kapanış önizleme ve yazdırma/PDF | Kişisel veri dışa aktarma izni |
| Ödeme iptali ve hesap yetkisi yönetimi | Yönetici rolü |

Salt okunur rolündeki bir hesaba yalnızca ödeme izni verilebilir: ödeme yapabilir, personel kartını düzenleyemez. Yalnızca dışa aktarma izni verilen hesap rapor çıkarabilir; bu izin ödeme veya düzenleme hakkı vermez. Ödeme izni olmayan personel yöneticisi sıfır bakiyeli kart açabilir ve mevcut bakiyeyi değiştirmeden kişisel bilgileri düzenleyebilir.

## Yönetici kullanımı

1. Yönetici hesapları ekranında hesabı seçin.
2. **Ek yetkiler…** düğmesini açın.
3. Ödeme ve bakiye işlemleri ile kişisel veri dışa aktarma seçeneklerini ihtiyaca göre ayrı ayrı işaretleyin.
4. **Yetkileri kaydet** düğmesine basın. Değişen hesabın açık oturumları geçersiz olur; yeniden giriş gerekir.

Eski hesap seçimi, yeniden oluşturulan veya başka bir yönetici tarafından değiştirilmiş hesaba uygulanmaz. Hesabın özgün sürümü ve oturum kimliği transaction içinde doğrulanır. Yönetici hesabının bu iki yetkisi rolün parçasıdır; bu pencereden azaltılamaz. Yeni yönetici olmayan hesaplar iki ek izin olmadan açılır.

## Şema ve dağıtım

`108-separate-payment-export-permissions.sql`, hesap tablosuna varsayılanı kapalı `CanPay` ve `CanExportPersonalData` alanlarını ekler; eski oturum nesillerini yeniler. Mevcut yönetici olmayan hesaplar da açık izin verilene kadar ödeme ve dışa aktarma yapamaz. Önceden başlamış ödeme günlükleri silinmez; yetkili hesaba geçilerek aynı işlem kimliğiyle sonuç kurtarılır.

**Bu adımda canlı veritabanına 108 uygulanmadı.** Mevcut canlı şema 107 ve masaüstü kısayolu 107 uyumlu pakettedir. Yeni yerel paket `artifacts/releases/separate-permissions/` altında hazırlanmıştır. Yeni sürümün açılış hazırlığı bekleyen yükseltmeyi yedek alarak uygulayacağından, kontrollü yedek/yükseltme adımından önce eski kısayol değiştirilmedi ve paket açılmadı.

## Uygulanan korumalar ve sınırlar

Kontroller hem ekranlarda hem servislerde uygulanır. Her hassas işlemde rol, ek izinler, aktiflik, parola yenileme durumu ve oturum sürümü canlı veritabanından doğrulanır. Yetki güncellemesi, oturum iptali ve audit aynı transaction içindedir. Başlamış yetkili işlem tamamlanırken izin iptali hesap kilidini bekler; iptal tamamlandıktan sonra eski oturum yeni işlem yapamaz.

Dosya dışa aktarmada izin kontrolü dosya yayımlanana kadar; yazdırmada BeginPrint–EndPrint arasında korunur. Yazdırma önizlemesinden sonraki gerçek baskıda izin tekrar doğrulanır. Excel/CSV ve pano aktarımları audit kaydı üretir. Dosya sistemi/pano/yazıcı ile SQL transaction'ı ortak atomik işlem değildir; aktarılmış kopya yetki iptaliyle geri alınamaz.

Bu ayrım uygulama yetkileridir. Veritabanına doğrudan SQL erişimi olan kullanıcıları sınırlamaz. Görüntüleme yetkisi olan kişinin ekrandaki veriyi okumasını veya ekran görüntüsü almasını önlemez. IBAN görüntüleme maskelemesi, proje bazlı satır kapsamı ve ayrıntılı audit (hedef/eski-yeni/gerekçe) ayrı geliştirmelerdir.

## Doğrulama durumu

Release çözüm derlemesi: **0 uyarı, 0 hata**. Rol/izin birleşimleri, bakiyeden yetki aşımı, yalnızca ödeme/dışa aktarma hesabı, eski seçim, oturum iptali, sahte izin ve audit geri alımı için regresyon kaynakları eklendi. Kullanıcının isteğiyle testler ve ekran/veritabanı denemeleri çalıştırılmadı; önceki test sayıları bu değişikliklerin doğrulandığı anlamına gelmez.
