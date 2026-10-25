# Değişiklik günlüğü

Tarihler Türkiye saatine (Europe/Istanbul, UTC+03:00) göredir. Geliştirme günleri aşağıda belirtilir; Git commit tarihi değişikliklerin kaynak geçmişine kaydedildiği tarihtir. Geçmiş tarihlere ayrı commit üretilmedi.

## 25 Ekim 2026

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

## Son doğrulama ve yayın kapsamı

Tam yerel regresyon: **383 başarılı, 0 başarısız, 0 atlanan**. Uyarıları hata sayan Release derlemesi ve self-contained paket üretimi başarılı. Kanıt: artifacts/analysis/master-20261025-090245-255/. Geçici LocalDB motoru temizlendi; gerçek hesaplar test amacıyla değiştirilmedi.

Derleme çıktıları, yerel kanıtlar, gerçek veritabanları/yedekler, günlükler ve ödeme taslakları Git'e alınmaz. Kaynak push'u yeni GitHub Release yayımlamaz: mevcut **v1.0.0** Windows indirmesi 25 Ekim geliştirmelerini içermez. Son yerel paket artifacts/releases/login-throttle/ altındadır. Uzak GitHub Actions sonucu yerel testten ayrı değerlendirilir.
