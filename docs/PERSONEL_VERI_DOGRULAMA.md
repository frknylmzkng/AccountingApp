# Personel verisi ve IBAN doğrulaması

29 Ekim 2026. [F03 bulgusu](PROJE_DURUM_ANALIZI_2026_10_28.md) için yerel kaynak düzeltmesi.

## Yeni kayıt ve düzenleme

Desteklenen IBAN ülkesi Türkiye'dir. Yeni kayıt/düzenlemede TR ülke kodu, boşluksuz 26 karakter, iki kontrol rakamı, beş rakamlı ödeme hizmeti sağlayıcısı kodu, sıfır rezerv alanı ve 16 ASCII harf/rakamdan oluşan hesap alanı aranır. Kontrol rakamları MOD 97-10 ile doğrulanır. Kurallar [TCMB IBAN Tebliği, madde 4 ve ekleri](https://www.tcmb.gov.tr/wps/wcm/connect/EN/TCMB%2BEN/Bottom%2BMenu/IBAN/Communique) esas alınarak uygulandı.

Küçük ASCII harfler büyütülür; normal boşluk ve bölünemez boşluk kaldırılır. Noktalama, kontrol karakterleri, Unicode rakam/harf benzerleri ve desteklenmeyen ülke kodları açıklamalı hatayla reddedilir. Yanlış kontrol basamağı otomatik düzeltilmez. Sağlayıcı kayıt listesi veya gerçek banka hesabı sorgulanmaz; biçim doğrulaması hesap sahibini/hesabın açık olduğunu kanıtlamaz. Bu kapsam IBAN alanının yardım metninde belirtilir.

Ad ve soyadın başındaki/sonundaki boşluklar temizlenir; iç boşluklar, Türkçe harfler ve kullanıcının büyük/küçük harf seçimi korunur. Kontrol karakteri ve boş ya da 50 karakterden uzun ad/soyad reddedilir. Ekran ve servis aynı normalize edilmiş PersonnelDetails modelini kullanır. Asenkron PersonnelService ve eski PersonnelStore kayıt yolları doğrulanan modeli kaydeder; ham girişin doğrulamadan sonra saklanması engellenir.

TC, tutar, transaction, yetki ve seçim sürümü korumaları korunur. TC'nin kimlik türlerine ayrılması bu adımın kapsamında değildir.

## Eski kayıtları inceleme

Genel Personel ve Joker Personel listesinin üzerinde **sağ tık → Veri düzeltme listesi…** seçilir. Yüklenmiş listedeki sorunlu IBAN ve ad/soyad değerleri, TC ve düzeltme gerekçesiyle salt okunur gösterilir. Güncel kayıtlar için önce F5 kullanılır. Bir personelin birden fazla izin satırı varsa kişi bir defa gösterilir. Erişimde mevcut oturum tekrar doğrulanır.

Kontrol listesi ve satır hata açıklamaları veritabanını değiştirmez. Düzeltmek için ana listeden personel seçilip doğru bilgiyle Düzenle kullanılır; sürüm denetimi devam eder. Geçersiz eski IBAN'lı personelin bilgileri kaydedilmeden önce IBAN düzeltilmelidir. Önceki ödeme hareketleri ve ayrı izin/silme işlemleri otomatik veri temizliği yapmaz.

Gerçek veritabanında tarama, değişiklik veya migration yapılmadı; yeni SQL betiği gerekmez. Eski test verilerinin başarılı kayıt senaryolarındaki sahte IBAN'ları biçim olarak geçerli örneklerle değiştirildi. Bu örnekler gerçek ödeme talimatı değildir.

## Doğrulama ve paket

Kullanıcının talebiyle bu adımda otomatik testler, veritabanı ve ekran denemeleri çalıştırılmadı. Yalnızca Release derlemesi yapıldı: **0 uyarı, 0 hata**. Önceki 649 test sonucu bu değişikliğin test sonucu olarak kullanılamaz.

Yerel .NET 10.0.13 Windows x64 paketi: `artifacts/releases/personnel-data-validation/WinFormsApp2.exe`. Kişisel bağlantı ayarı önceki yerel paketten aynen taşınır. Masaüstü kısayolu ve çalışan uygulama değiştirilmez; GitHub'a gönderim/yayın yapılmaz.
