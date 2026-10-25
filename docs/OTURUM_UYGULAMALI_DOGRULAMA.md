# Oturum iptali — Yayımlanmış sürüm denemesi

25 Ekim 2026 · Paket: `artifacts/releases/session-revocation`.

Eski `personnel-concurrency` sürümündeki açık Joker penceresi normal pencere kapatma ile kapatıldı. Yeni `session-revocation` sürümü açıldı; gerçek uygulama penceresinde **Giriş hazır** durumu görüldü. Gerçek hesabın kullanıcı adı/şifresi benim tarafımdan girilmedi. Son kontrolde güncel uygulamanın ana ekranı (Dashboard) açıktı ve kullanım için orada bırakıldı.

## Deneme yöntemi

Yayımlanmış paketin DLL'i ayrı bir deneme dizinine kopyalandı; SHA-256 özeti asıl paketle eşleştirildi:

`44B6319045FF4ECF74C13CF77328FFB5B182A44E6FA6FFABFFFFA5532572B882`

Uygulama kodunu değiştirmeyen ayrı deneme programı, GUID adlı bağımsız LocalDB motoru ve geçici veritabanında bir yönetici ile bir personel sorumlusu oluşturdu. Parolalar yalnızca bellekte üretildi. Girişler, pasife alma ve etkinleştirme, paketin gerçek `AccountService` servisi üzerinden yapıldı. Ekran yaşam döngüsü yayımlanmış `AccountingApplicationContext` ve `SessionForm` bileşenleriyle iki test penceresinde doğrulandı. Giriş formuna otomatik parola yazılmadı.

İkinci oturumda paketin gerçek 30.000 ms zamanlayıcısı çalıştırıldı; hesap kontrolü yalnızca geçici veritabanına yönlendirildi. Böylece gerçek hesaplar kullanılmadan, zamanlayıcı ve giriş ekranına dönüş birlikte denendi.

## Sonuçlar

| Kontrol | Sonuç |
| --- | --- |
| İki geçerli oturumun açılması | Başarılı |
| Pasife alınan hesabın eski oturumundan personel yazma girişimi | Reddedildi |
| Reddedilen işlemde pencerenin kapanıp giriş ekranına dönmesi | Başarılı |
| Personel adının ve 100 TL bakiyenin korunması | Başarılı |
| Pasif hesabın giriş servisinden tekrar giriş yapması | Reddedildi |
| Etkinleştirme sonrasında eski oturumun geçerli hale gelmesi | Engellendi |
| Yeni girişle yeni oturum açılması | Başarılı |
| Boşta kalan pencerenin gerçek zamanlayıcıyla girişe dönmesi | **29,95 saniye** |
| Oturumun sona erdiği mesajının görünmesi | Başarılı |
| Geçici veritabanı, hesaplar ve dosyaların temizlenmesi | Başarılı |

Ham sonuçlar: `artifacts/analysis/session-live-check/results.json`.
Deneme programı: aynı klasördeki `harness/`.

Bu bir servis ve pencere yaşam döngüsü bütünleşme denemesidir. Hesap yönetimi düğmeleri yerine gerçek hesap servisleri çağrıldı; üretim veritabanına deneme hesabı veya personel eklenmedi. Deneme sonunda yalnızca göreve ait GUID adlı motor durdurulup kaldırıldı; kullanıcının normal LocalDB motoru durdurulmadı.
