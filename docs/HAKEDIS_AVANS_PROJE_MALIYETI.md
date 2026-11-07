# Hakediş, avans ve proje maliyeti

30 Ekim 2026 · Yerel geliştirme; yayımlanmış v1.2.0 paketine dahil değildir.

**Canlı geçiş tamamlandı:** Kullanıcı onayıyla mevcut veritabanının CHECKSUM/VERIFYONLY ve v2 manifestiyle doğrulanmış yedeği alındı; yalnızca 110 uygulandı. Önceki iş/audit/hesap verileri (beklenen oturum yenilenmesi dışında) korundu. Yeni finans tablolarının boşluğu, eski hareketlere proje/hakediş uydurulmadığı, bağlantı alanları ve etkin korumalar doğrulandı. Muhasebe - Güncel kısayolu 110 uyumlu yerel pakete yönlendirildi. Test/ekran denemesi yapılmadı, uygulama açılmadı. Özel yedek/kanıtlar Git dışındaki `artifacts/analysis/live-upgrade-110/` altındadır.

## Hesap modeli

Bu ilk sürüm elle onaylanmış işçilik hakedişini kaydeder. Henüz bulunmayan puantaj, ücret tarifesi, resmi bordro, vergi/SGK veya otomatik banka transferi hesaplanmaz.

| İşlem | Personel bakiyesi | Proje maliyeti | Para çıkışı |
|---|---|---|---|
| Hakediş belgesi | Artar | İşçilik maliyeti artar | Oluşmaz |
| Bakiye ödemesi | Azalır; bakiyeyi aşamaz | Değişmez | Ödeme olarak kaydedilir |
| Avans | Azalır; negatif olabilir | Değişmez | Ayrı avans olarak kaydedilir |
| Diğer proje gideri | Değişmez | Diğer maliyet artar | Bu modülde ödeme/banka gerçekleşmesi değildir |
| Belge iptali | Hakedişte özgün tutar geri alınır | İlgili maliyet çıkarılır | Önceki ödeme kendiliğinden iptal edilmez |

Örnek: sıfır bakiye üzerine 300 TL avans bakiye -300 yapar; sonra 1.000 TL hakediş girilirse bakiye 700, işçilik maliyeti 1.000, avans çıkışı 300 olur. Avans ikinci kez maliyet veya kesinti yazılmaz. Negatif bakiye, bu kayıt modelinde henüz karşılanmamış avans/fazla ödemeyi gösterir; resmi tahsilat veya hukuki kesinti kararı değildir.

Mevcut PARA ve defter satırları değiştirilmez. Eski bakiyeler geçmiş maaş/hakediş olarak yorumlanmaz; mevcut hareketlerin yeni sınıflandırması Unclassified olur. Geçmiş döneme tahakkuk girerken mevcut bakiyenin o tutarı zaten içerip içermediği işletme tarafından kontrol edilmelidir; uygulama geçmiş çalışma verisini çıkaramaz.

## Kullanım

**İşlemler → Hakediş, avans ve proje maliyeti** menüsünü açın.

1. Yönetici olarak Proje maliyetleri sekmesinde benzersiz kod ve adla proje oluşturun; üstteki Proje listesinden seçin.
2. Hakediş için üstte güncel personeli, belge sekmesinde Hakediş türünü seçin. Dönem başlangıç/bitiş, tutar, açıklama ve özgün belge numarası girin. Belgeyi onayla ve kaydet düğmesiyle gerekçe belirterek kaydedin.
3. Kira/malzeme gibi personel alacağı olmayan maliyet için Diğer proje gideri türünü seçin. Personel bakiyesi değişmez.
4. Avans sekmesinde proje ve personel seçip pozitif tutarı girin; gerekçe ve onay sonrası mevcut Ödemeler ekranına geçilir. Sonucu kontrol edip onaylayın. Sonuç belirsizse yeni ödeme başlatmayın; aynı günlük/işlem kimliğiyle tekrar deneyin.
5. Hatalı finans belgesini seçip gerekçeli iptal kullanın. Satır silinmez veya eski tutarı değiştirilmez. Hakediş iptali, daha önce ödeme yapılmışsa negatif bakiye oluşturabilir. Arşivlenmiş personel için önce mevcut kart aynı kimlikle etkinleştirilmelidir.

Yeni proje/hakediş/gider ve belge iptali yöneticiye aittir. Avans mevcut ayrı ödeme iznini kullanır. Görüntüleme mevcut Read, dışa aktarma mevcut ayrı dışa aktarma iznine bağlıdır. Proje bazlı satır erişimi bu adımda eklenmedi.

Proje maliyetleri hakediş + diğer gider toplamıdır; avans ve ödeme sütunları ayrı nakit hareketleridir, bu toplama katılmaz. Projesiz eski/normal ödemeler projelere keyfi dağıtılmaz. Diğer gider belgesi nakit ödemesi veya banka mutabakatı sayılmaz. İşletmenin aynı ekonomik olayı hem hakediş hem diğer gider olarak yeniden girmemesi gerekir.

## Koruma ve sınırlar

- Proje/tür/belge numarası benzersizdir. Aynı içeriğin tekrar gönderimi mevcut sonucu döndürür; farklı içerik reddedilir. İptal edilmiş belge aynı numarayla yeniden açılamaz. İstemci yeniden açıldıktan sonra da aynı numara kullanılmalıdır. Farklı belge numarası, yeni belge demektir.
- Hakediş kaydı, bakiye artırımı, bağlantılı defter hareketi ve ayrıntılı audit aynı transaction içindedir. Personelin kalıcı kimliği ve özgün kayıt sürümü doğrulanır. Belge iptali kendi sürümünü kontrol eder.
- Avans, mevcut şifreli ödeme günlüğü ve RequestId tekrar korumasını kullanır. Tür, proje, kişi, tutar ve gerekçe yeniden denemede korunur; aynı RequestId farklı tür/proje için kullanılamaz.
- Onaylı belgenin çekirdek alanları ve fiziksel silinmesi trigger ile korunur; yalnızca gerekçeli iptal bilgisi eklenir. Proje/belge/defter değişiklikleri ayrıntılı audit'e girer.
- Hakediş bu sürümde tutar bazındadır: puantaj kaynağı, miktar × tarife hesabı, hazırlayan/onaylayan iki kişi, dönem kilidi ve hakediş bazında PaymentAllocation henüz yoktur. Bakiye mahsubu kişi genelindedir; bir avansın belirli hakedişe/projeye tahsis edildiği iddia edilmez.
- Maliyet raporu proje boyunca toplamdır; belge tarih aralığı saklanır ama günlere otomatik dağıtım yapılmaz. Belgeler seçili proje için en yeni 500 kayıtla sınırlıdır. Eski bakiyenin nasıl oluştuğu ve sınıflandırılmamış manuel bakiye düzeltmeleri maliyete otomatik eklenmez.

## Şema ve paket

110-project-finance.sql; FinanceProjects/FinanceEntries tablolarını ve PaymentLedger ödeme türü/proje/belge bağlantılarını ekler. Eski oturumlar yenilenir. Mevcut tabloların verileri korunur, geçmiş hakediş uydurulmaz. Yerel paket artifacts/releases/project-finance/ altındadır.

Kullanıcı denemeleri atlamayı istedi: testler ve ekran denemeleri çalıştırılmadı. Release çözüm derlemesi 0 uyarı / 0 hata ile tamamlandı; regresyon kaynakları derlendi fakat çalıştırılmadı. Canlı 110 geçişinin sonuçları aşağıda ayrıca kaydedilir.
