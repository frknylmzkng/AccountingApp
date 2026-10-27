# Pencere boyutuna uyarlanan arayüzler

27 Ekim 2026. Windows 2000 görünümü korunarak aşağıdaki 12 ekran/akış, dar pencereden tam ekrana kadar kullanılabilir alana uyarlanır. Parola Sıfırlama akışının geçici parola sonuç penceresi de kapsam içindedir.

| Ekran | Yerleşim | Önizleme ve deneme adımları |
|---|---|---|
| Ana ekran | İşlem kartları 1–4 sütun; dar alanda kaydırma | [Ana ekran](ANA_EKRAN_TAM_EKRAN_DUZENI.md) |
| Genel Personel | Bilgi/izin bölümleri yan yana veya alt alta; alanlar 1–3 sütun | [Genel Personel](GENEL_PERSONEL_RESPONSIVE.md) |
| Joker Personel | Ortak personel alanları 1–3 sütun; liste kalan alanı doldurur | [Joker Personel](JOKER_PERSONEL_RESPONSIVE.md) |
| Kişi Arama | Filtreler 1–3 sütun; genişleyen sonuç listesi | [Kişi Arama](KISI_ARAMA_RESPONSIVE.md) |
| Genel Durum | Sayım ve bakiye kartları genişliğe göre dizilir; izin listesi büyür | [Genel Durum](GENEL_DURUM_RESPONSIVE.md) |
| İletişim | Ortalanmış kartlar yan yana veya alt alta | [İletişim](ILETISIM_RESPONSIVE.md) |
| Yönetici | Yeni hesap/işlem bölümleri uyarlanır; liste kalan alanı doldurur | [Yönetici](YONETICI_RESPONSIVE.md) |
| Ödemeler | Ödeme/kapanış bölümleri ve iki liste pencereye göre dizilir | [Ödemeler](ODEMELER_RESPONSIVE.md) |
| Ayarlar | Bölümler iki/tek, işlem düğmeleri dört/iki sütun | [Ayarlar](AYARLAR_RESPONSIVE.md) |
| İzin Planlama | Tarihler yan yana/alt alta; personel listesi kalan alanı doldurur | [İzin Planlama](IZIN_PLANLAMA_RESPONSIVE.md) |
| Parola Değiştirme | Alanlar/kurallar birlikte ortalanır; işlem düğmesi görünür kalır | [Parola Değiştirme](PAROLA_DEGISTIRME_RESPONSIVE.md) |
| Parola Sıfırlama | Hesap/doğrulama bölümleri uyarlanır; sonuç penceresi de boyutlanabilir | [Parola Sıfırlama](PAROLA_SIFIRLAMA_RESPONSIVE.md) |

## Korunan davranışlar

Yeniden boyutlandırma aynı kontrolleri kullanır. Kaydedilmemiş alanlar, sıralanmış/çoklu seçimler, özgün kayıt sürümleri, ödeme durumu, parola maskeleri ve yetki kısıtları korunur. Boyut değişiminde sorgu, kayıt, ödeme veya parola işlemi çalışmaz. Kısa pencerelerde dikey kaydırma ve klavye odağı alanlara erişim sağlar; listeler kendi minimum okunabilir sütun genişliklerini korur.

## Doğrulama

27 Ekim toplu gönderim öncesinde güncel kaynakla `otomatik_test_master.ps1 -Full` tamamlandı: **609 test başarılı, 0 başarısız, 0 atlanan**. Release derlemesi 0 uyarı / 0 hata. Entegrasyon ve geri yükleme testleri ayrı geçici LocalDB motorunda çalıştı; motor temizlendi. Yerel kanıt: `artifacts/analysis/master-20261027-200150-194/master.trx`.

Son ekran adımında **364 veritabanısız test** başarılıdır. Ekranlar geliştirilirken yayımlanan yerel DLL'ler üzerinde toplam **294 görsel yerleşim kontrolü** yapılmıştır: ilk 10 ekran için 21'er, iki parola akışı için 42'şer kontrol. Her ekranın belgesinde kendi örnek boyutları, roller/temalar, test kapsamı ve yerel kanıt yolu bulunur. Bu sayı farklı geliştirme adımlarındaki kontrollerin toplamıdır.

Görseller örnek kayıtlarla üretilmiştir. Gerçek kişi, hesap veya parola kullanılmadı. Kontroller 96 DPI ortamındadır; farklı fiziksel DPI/monitör ayrıca denenmedi.

## Kaynak ve indirme paketi

Responsive kaynaklar, testler, görseller ve tarihli notlar bu depodadır. Önceki onaylı seçim/sürüm, güncel rapor ve yedek aynalama düzeltmeleri ayrı altyapı commit'inde; responsive arayüzler onu izleyen commit'te kaydedilir. Geliştirme tarihleri [değişiklik günlüğünde](../CHANGELOG.md) korunur; Git commit tarihleri gönderim hazırlığının gerçek tarihidir.

**v1.1.0 indirme paketi 26 Ekim arayüzünü içerir.** Bu responsive güncelleme için yeni Release ZIP'i henüz yayımlanmadı. Güncel kaynaklar README'deki derleme komutlarıyla çalıştırılabilir. Yerel paketler, uygulama/veritabanı dosyaları, kişisel bağlantı profilleri ve test çıktıları Git'e eklenmez.
