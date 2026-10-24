# Yeniden kullanım rehberi uygulama takibi

Kapsam kararı (kullanıcı, 24 Ekim 2026): **Mevcut işlevlere uyarla.** Yeni bir perakende/stok uygulaması kurulmayacak; rehberin yöntemleri personel, izin ve ödeme takibine uygulanacak. Kaynak rehberin kopyası `yeniden-kullanim-kaynak-rehberi.txt` dosyasındadır. Her başlık aşağıda izlenir; tamamlanmamış satır tamamlandı sayılmaz.

Mevcut .NET 10, PBKDF2-HMAC-SHA256/600.000 yineleme, hash göçü, parametrik SQL ve veri bütünlüğü regresyonları korunur. Rehberdeki eski sürümler, SHA-1 kullanan eski PBKDF2 kurucu örneği, açık metne geri düşme, UI await'lerinde ConfigureAwait(false) ve antivirüs istisnaları aynen kopyalanmaz. Kullanıcı arayüzü await'leri UI bağlamına döner; DAL/servis await'leri bağlam yakalamaz. Güvenlik yazılımı ayarları değiştirilmez.

| Rehber maddesi | Bu projedeki karşılığı | Durum / kanıt |
|---|---|---|
| 1.1 | Güçlü PBKDF2, salt, sabit zamanlı kontrol, kontrollü eski parola göçü | Mevcut; son regresyonda tekrar doğrulanacak |
| 1.2 | Parametreli sorgular, güvenli tanımlayıcılar, transaction ve kilitler | Mevcut; yeni DAL işlemleri de kapsanacak |
| 1.3 | Yönetici / personel sorumlusu / görüntüleyici; servis ve ekran izinleri | Bekliyor |
| 2.1 | DbUp, gömülü sürümlü betikler, otomatik güvenli şema yükseltme | Bekliyor |
| 2.2 | Geriye uyumlu JSON + LocalDB / SQL Express profilleri, DataDirectory, kontrollü profil geçişi | Bekliyor |
| 3.1 | Modeller, repository, bir işlem için UnitOfWork; commit yoksa rollback | Bekliyor |
| 3.2 | Gerçek async SQL, cancellation, kaynak temizliği, yanıt veren UI | Bekliyor |
| 3.3 | Koşullu/atomik bakiye düşümü ve eşzamanlı ödeme koruması | Bekliyor |
| 4.1 | Ortak tema, modern grid, pencere sürükleme, durum çubuğu, erişilebilir klavye akışları | Bekliyor |
| 4.2 | Kullanıcıya özel kalıcı açık/koyu tema | Bekliyor |
| 4.3 | Kolon genişliği/sırası/görünürlüğünü saklama | Bekliyor |
| 4.4 | .NET TextBox.PlaceholderText ile yer tutucular | Bekliyor |
| 5.1 | ClosedXML, biçimli tutar raporu, SUM satırı, güvenli dosya açma | Bekliyor |
| 5.2 | UTF-8 BOM, noktalı virgül, doğru CSV kaçışları ve formül koruması | Bekliyor |
| 5.3 | A4 / 80 mm personel ödeme fişi, logo/QR, izin/ödeme bilgileri | Bekliyor |
| 5.4 | POS Z raporu yerine günlük ödeme kapanış raporu ve çift imza | Bekliyor |
| 6.1 | Ayrı decimal hesaplama servisi; personel tutar toplamı/ağırlıklı ortalama | Bekliyor |
| 6.2 | Transaction'a bağlı denetim kaydı + kişisel veri içermeyen NLog | Bekliyor |
| 6.3 | 24 saat aralıklı yedekleme, 14 yedek, isteğe bağlı aynalama, çift ayar kaynağı, doğrulanmış restore | Bekliyor |
| 6.4 | Müşteri puanı yerine saf ve sınanmış izin günü/tutar hesaplamaları; hayali izin hakedişi üretilmez | Bekliyor |
| 6.5 | Tedarikçi carisi yerine personel ödeme defteri ve yürüyen bakiye | Bekliyor |
| 6.6 | Lot/FEFO yerine tarih sıralı izin uyarıları ve atomik izin planı onayı | Bekliyor |
| 6.7 | Sepet yerine personel/izin formu taslaklarını F6/F7 ile güvenle bekletme/geri yükleme | Bekliyor |
| 7.1 | Güncel NUnit; izole DB testleri ve DB'siz test filtresi | Mevcut; yeni testlerle genişletilecek |
| 7.2 | .NET 10 Windows GitHub Actions, TRX ve Release artifact'leri | Bekliyor |
| 7.3 | Master test scripti, 15 uyarlanmış entegrasyon alanı, rapor, -Full izole restore testi | Bekliyor |
| 8.1–8.6 | Proje, DB helper, oturum, ayarlar, global hata kaydı, tema | Korumalı modern uyarlama; P0 |
| 8.7–8.10 | POCO, repository/UoW, servis, migration | P1 |
| 8.11–8.14 | POS/borç/mal kabul yerine ödeme akışı, bakiye, hesaplamalar, audit/yedek | P1–P2 |
| 8.15–8.18 | Grid/export, yazdırma, CI, otomasyon ve manuel kılavuz | P2–P3 |
| 9 P0→P3 | Sıra: güvenlik/tema temeli → veri/mimari → rapor/iş akışları → otomasyon | Bu sıra izlenecek |
| 10.1 / 10.9 | İzole test çalışma klasörü; antivirüs kapatma/istisna yok | Mevcut yaklaşım korunacak |
| 10.2 | PowerShell 5.1 betiklerinde UTF-8 BOM, Türkçe round-trip testi | Mevcut; yeni dosyalarda da uygulanacak |
| 10.3 | Yerel/pinlenmiş dotnet SDK; SSMS MSBuild seçilmez | Mevcut |
| 10.4 | Boşluklu yollarda güvenli argüman aktarımı | Mevcut; yeni otomasyon testi |
| 10.5 | Geçerli dotnet test / TRX komutları; geçersiz vstest /nologo yok | Mevcut |
| 10.6 | Ayarlar/DataDirectory ilk DB erişiminden önce hazırlanır | Bekliyor |
| 10.7 | Dispose/DisposeAsync ile tamamlanmamış işlemi geri alma | Bekliyor |
| 10.8 | Koşullu güncelleme + etkilenen satır sayısı | Bekliyor |
| 10.10 | Kaynakta .pfx/büyük JPG envanteri; bağlı kaynakları bozmadan temizlik | Tarama: temizlenecek dosya bulunmadı |
| 10.11 | Her aşama derleme/test/smoke ve yerel sürüm kaydı; uzak depoya otomatik gönderim yok | Başlangıç kaydı hazırlanıyor |
| 10.12 | async void yalnızca olay işleyicileri; iç API'ler Task, UI thread kontrolü | Bekliyor |

CI dosyası hazırlamak, uzak GitHub çalıştırması yapılmış sayılmaz. SQL Express profilinin bağlantı testi yerel kurulum varsa yapılır; kurulu olmayan servis veya fiziksel yazıcıyla yapılan testler otomatik başarı olarak raporlanmaz. Gerçek kullanıcı veritabanına restore testi uygulanmaz.
