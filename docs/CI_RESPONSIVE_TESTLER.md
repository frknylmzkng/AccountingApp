# Responsive testlerin GitHub Actions düzeltmesi

27 Ekim 2026. İlk toplu responsive gönderiminin [40547911821 numaralı çalışması](https://github.com/frknylmzkng/AccountingApp/actions/runs/40547911821), 609 testin 567'sini geçti; 42 yerleşim geçişi kontrolü başarısız oldu. Derleme ve veritabanı hazırlığı başarılıydı; Windows paketleme adımı test hatası nedeniyle çalışmadı.

## Neden

Başarısız kontrollerin tümü Yönetici, Ana Ekran, Genel Personel, Genel Durum, Ödemeler, Ayarlar ve Parola Değiştirme testlerinde geniş düzene ulaşılıp ulaşılmadığıyla ilgiliydi. Testler `Form.Size` üzerinden genişlik istiyordu; Windows/WinForms pencereyi çalışma ortamının `SystemInformation.MaxWindowTrackSize` sınırında tutuyordu. Küçük sanal ekranda istenen genişlik uygulanmadığından geniş düzen hiç sınanmıyordu.

Sorun yerelde de yeniden üretildi: 3000 × 1200 isteği normal boyut atamasında 1940 × 1100'e sınırlandı. Büyük `MaximumSize` vermek tek başına bunu çözmedi. Yerel kanıt `artifacts/analysis/ci-responsive/viewport-proof.json` dosyasındadır. Çerçeve davranışının resmi kaynağı: [.NET WinForms Form.cs](https://github.com/dotnet/winforms/blob/v10.0.13/src/System.Windows.Forms/System/Windows/Forms/Form.cs).

## Düzeltme

`tests/WinFormsApp2.RegressionTests/ResponsiveTestWindow.cs` yalnızca testlerin oluşturduğu saydam pencereleri boyutlandırır:

- Başlangıç içeriğini, menü/durum çubuğunu ve pencere çerçevesini hesaba katar.
- Önce normal `Form.Size` yolunu kullanır. İstek sınırlandıysa aynı test penceresine `SetWindowPos` ile boyut verir; konum, ön plan ve sıralamayı değiştirmez. `SWP_NOSENDCHANGING` izleme boyutu sınırlamasını bu test isteği için atlar; boyut/yerleşim bildirimleri sürer.
- Her istekte gerçek pencere boyutunun beklenen boyuta eşit olduğunu doğrular. Minimum pencere boyutları da denetlenir.
- 12 responsive test grubu ve ortak ekran çizim testi bu yardımcıyı kullanır. Sütun geçişi, taşma, seçim, parola, yetki veya işlem kilidi kontrolleri kaldırılmaz/zayıflatılmaz.
- Ek regresyon testi 2400 × 1200 pencerenin gerçek içerik boyutunu ve tekrar küçültülürken alan değerinin korunmasını denetler.

Uygulama kaynakları ve Windows masaüstünün çözünürlüğü/ayarları bu düzeltme için değiştirilmedi. Gerçek kullanıcı kaydı veya parola kullanılmadı.

## Doğrulama kayıtları

Yerel tam doğrulama: **610 test başarılı; 0 başarısız, 0 atlanan**. Release derlemesi 0 uyarı / 0 hata. Entegrasyon ve geri yükleme için kullanılan ayrı geçici LocalDB motoru temizlendi. Kanıt: `artifacts/analysis/master-20261027-202455-754/master.trx`.

İlk CI raporu, iş günlüğü ve sonraki doğrulama kayıtları yerelde `artifacts/analysis/ci-responsive/` klasöründedir. Bu dosyalar Git kapsamı dışındadır. Güncel test sonucu ve gönderim bilgisi [değişiklik günlüğünde](../CHANGELOG.md) yer alır.
