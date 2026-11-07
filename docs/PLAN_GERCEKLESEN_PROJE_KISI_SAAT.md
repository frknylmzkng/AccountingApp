# Planlanan ve gerçekleşen proje kişi-saat karşılaştırması

6 Kasım 2026 · **1.3.25-dev / mevcut şema 132 / API 1.25**. Yeni migration yok.

Projenin yayımlanmış vardiya planını onaylı net çalışma süresiyle karşılaştırır. Taslak, iade ve bekleyen onaylar ayrı tutulur. Rapor ücret, maliyet veya hakediş kaydı üretmez; hesap ve onay kayıtlarını değiştirmez.

## Ekran ve kapsam

**Projelerim → Plan / gerçekleşen kişi-saat…** düğmesini açın. Merkez yöneticisi seçili projenin **Hakediş, avans ve proje maliyeti** ekranındaki aynı düğmeyi de kullanabilir. Proje okuma erişimi gerekir; para/IBAN görüntüleme yetkisi istenmez ve parasal alanlar okunmaz. CSV için ayrıca mevcut proje dışa aktarma yetkisi denetlenir.

Üstteki **İş günü / plan başlangıç günü** filtresinde başlangıç/bitiş günlerini işaretleyin; işaretsiz uç açıktır. Her iki gün dahildir. Kişi seçimi kalıcı PersonId kullanır; elle güncel ad, projedeki hesap snapshot'ında geçen ad veya kalıcı kişi numarası da aranabilir. Ad araması birden fazla kişiye uyabilir. Eski çalışma dönemleri ayrı EmploymentId satırlarında kalır.

**Filtreyi uygula** kapsamı yeniler ve sayfaları başa alır. Uygulanmamış filtre taslağı varken CSV kapalıdır. Yenile/F5 uygulanmış kapsamı yeniden okur. Bölümlerde 25/50/100/200 satır, önceki/sonraki ve sayfaya git kontrolleri bulunur. Özetler açık sayfadan değil bütün filtre kapsamından hesaplanır. Her yenilemenin veritabanı UTC veri zamanı ve oluşturan hesabı gösterilir.

### Bölümler

- **Proje özeti:** gerekli kadronun plan dakikası/kişi-saati, seçili aktif kişi-vardiya atamalarının planı, onaylı gerçekleşme ve fark. Bekleyen/taslak süre, vardiyasız çalışma, başka proje planından gelen süre, eksik puantaj ve eksik süre sayıları da gösterilir.
- **Günler:** plan ve gerçekleşmenin iş günü bazındaki toplamları. Planı olup gerçekleşmesi olmayan veya yalnız gerçekleşmesi olan günler kaybolmaz.
- **Kişi / çalışma dönemi:** aynı metrikler PersonId + EmploymentId + personel grubu bazında; ad güncel karttan gelir. TC, IBAN, ücret oranı veya genel bakiye alınmaz.
- **Plan ayrıntıları:** vardiya durumu, yerel aralık, mola, kadro ihtiyacı, tüm/seçili atama sayısı, eksik puantaj ve bağlı proje gerçekleşmesi. Taslak ve iptal planlar tarihçe olarak görülebilir; atanmış plan toplamına yalnız yayımlanmış plan girer.
- **Gerçekleşen kaynaklar:** ana puantaj veya proje parçası kimliği, iş günü, ana/proje onay durumları, yerel aralık, mola, ham net/ücret dakikası, sayılan onaylı/bekleyen/taslak dakika ve tam onaylı ücret dakikası. Kaynak saat dilimi ve süre politikası da görünür.
- **Eksik kayıt ve istisnalar:** yayımlanmış kişi planında etkin puantaj yokluğu, eksik/geçersiz süre, bekleyen/iade/çözümlenmemiş onay, ana puantajı tam onaylanmamış onaylı parça, tamamlanmamış etkin dağılım veya iptal plana bağlı onaylı gerçekleşme. Sayı tüm filtre sonuçlarını içerir; yalnız açık sayfa sayılmaz.

## Hesap kuralları

| Değer | Kural |
|---|---|
| Gerekli kadro kişi-saati | Yayımlanmış vardiyanın `(yerel süre − mola) × RequiredCount / 60` toplamı |
| Atanmış plan kişi-saati | Yayımlanmış, iptal edilmemiş kişi atamalarının kişi başı net plan dakikası toplamı / 60 |
| Proje onaylı gerçekleşme | Bu projeye ait etkin onaylı kaynakların UTC net dakikası / 60 |
| Fark | Proje onaylı gerçekleşme − atanmış plan; negatif değer otomatik devamsızlık kararı değildir |
| Bekleyen / taslak süre | Ana puantaj/proje onay durumuna göre ayrı net dakikalar; onaylı gerçekleşmeye eklenmez |
| Tam onaylı ücret süresi | Proje kaynağı onaylı **ve** ana puantaj tamamen Approved ise ücret dakikası; çalışma net süresinden ayrıdır |

Saatler toplam tam dakikadan türetilip iki ondalık gösterilir; satırlardaki yuvarlanmış saatleri toplayarak yeni toplam üretilmez. Plan `LocalPlannedMinutes.v1` yerel net süreye, gerçekleşme kaynak UTC net süreye dayanır; saat dilimi farkları otomatik eşitlenmez. Plan aralığı/mola ve gerçekleşen aralık/mola değişmişse fark oluşması normaldir.

Kadro ihtiyacı ve boş kontenjan proje toplamı olduğundan **kişi filtresi uygulanınca boş gösterilir**; seçili kişinin ihtiyaç kapasitesi olarak sunulmaz. Projedeki yayımlanmış vardiya sayısı tarih kapsamının proje bilgisidir. Atama sayısı kişi-vardiya satırı sayısıdır; aynı kişinin farklı vardiyaları ayrı atamalardır. Farklı kişi sayısı plan/kaynaklarda görülen kişilerden gelir; bu alan çalışmış kişi sayısı veya işe devam kararı değildir.

## Çok proje, gece ve onay

Etkin zaman dağılımı olmayan ana puantaj kendi projesinde bir kez sayılır. Etkin dağılım varsa ana puantaj toplamı yerine yalnız ilgili projenin etkin, mühürlü parçaları kullanılır. Kaldırılmış dağılım/parçalar sayılmaz. Aynı projede birden fazla ayrık parça varsa net süreleri toplanır; ana süre tekrar eklenmez.

Bağımsız proje parçası açık/hazır onay turunda Approved ise, diğer projenin kararı beklenirken de **proje onaylı net gerçekleşme** sayılır. Ana puantaj hâlâ Submitted ise **tam onaylı ücret süresine girmez** ve istisna notu oluşur. Bu ayrım mevcut operasyonel puantaj özetiyle tutarlıdır; hakediş hesap temelinin tam ana onay şartı değiştirilmez. Eski toplu parça onayında ana Approved durumu kullanılır. İade/kapalı tur/taslak geçmiş onayı yeniden geçerli kılmaz.

Başka proje planından bu projeye dağıtılmış çalışma ayrı gösterilir; diğer projenin adı, plan kimliği veya kişi listesi rapora açılmaz. Vardiyasız onaylı çalışma ayrıca görünür. Gece vardiyası plan başlangıç gününde, gerçekleşme mevcut Timesheets.WorkDate gününde tutulur; gece yarısında otomatik bölünmez. Kaynak kayıt tarihi veya banka tarihi bu filtrenin yerine kullanılmaz.

Örneğin 8 saatlik aktif kişi planı, bu projede 6 saat proje onaylı net çalışma ve 1 saat bekleyen onay varsa karşılaştırma **8 → 6, fark −2 saat** gösterir; bekleyen 1 saat ayrıca görünür. Bu kişi-saat farkı kendiliğinden ücret kesintisi, fazla mesai veya devamsızlık kararı oluşturmaz. İzin süreleri çalışılmış süre gibi eklenmez.

## CSV, sınırlar ve teslim

Seçili bölümün CSV'si aynı filtrede sunucuda güncel veriden yeniden oluşturulur; yalnız açık sayfa aktarılmaz. Proje, filtre, veri zamanı, aktör ve hesap ilkesi dosyada saklanır. Seçili bölüm 10.000 satırı veya dosya 32 MB'ı aşarsa filtre daraltılmalıdır. Ekran SQL sayfalamasıyla daha geniş sonuçları gezebilir. Dışa aktarma gerekçeli audit olayıdır; proje ve export yetkisi teslim bitene kadar korunur.

Tüm rapor Serializable transaction içinde okunur; veri zamanı bölümler tamamlandıktan sonra veritabanından alınır. Bu geçmiş bir tarih itibarıyla durum rekonstrüksiyonu veya değişmez dönem kapanışı değildir. Kayıtların güncel plan/onay hali raporlanır. Eksik kayıtlar için otomatik süre, mola, ücret veya onay uydurulmaz.

Yeni API: `POST /project-work-hours/browse` (proje, tarih/kişi filtresi, bölüm bazlı sayfa istekleri). CSV mevcut ProjectExport üzerinden `Workforce.Summary|Days|People|Shifts|Actual|Issues` seçimi ve Filter ile üretilir. İstemci/API birlikte 1.3.25-dev / API 1.25 kullanılmalıdır; şema 132 korunur.

Bu kaynak tesliminde canlı **1.3.24-dev / şema 132 / API 1.24**, veritabanı, bağlantılar ve kısayol değiştirilmedi. 100–132 betikleri korundu. Kullanıcı isteğiyle SQL, işlev, arayüz, eşzamanlılık ve rapor senaryo denemeleri atlandı. Derleme ve paket bütünlüğü kontrolü çalışma zamanı doğrulaması değildir.
