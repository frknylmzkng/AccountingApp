# Personel sayfalama ve SQL özetleri

30 Ekim 2026 · Yerel kaynak. Yayımlanmış v1.2.0 paketine dahil değildir.

**İzleyen canlı geçiş tamamlandı:** Kullanıcı onayıyla doğrulanmış yedek alındı ve yalnızca 111 uygulandı. Dört etkin indeksin anahtar, include ve filtre tanımları doğrulandı. Mevcut iş/finans/audit/hesap verilerinin, rowversion alanlarının ve oturum nesillerinin içerik özetleri korundu. Muhasebe - Güncel kısayolu 111 uyumlu sayfalama paketine yönlendirildi. Test/ekran/hız denemeleri yapılmadı; uygulama açılmadı. Özel yedek/kanıtlar `artifacts/analysis/live-upgrade-111/` altında Git dışındadır. Aşağıdaki dağıtım notu hazırlık aşamasının tarihsel durumudur.

## Kullanıcı deneyimi

Genel, Joker, Kişi Arama, Ödemeler personel listesi ve defteri, İzin Planlama ve personel arşivi sayfalıdır. Varsayılan 50 personel/hareket; seçenekler 25, 50, 100 ve 200'dür. Önceki/Sonraki, sayfa numarası/Git ve toplam kayıt bilgisi listenin altında bulunur. Sayfa değişince eski seçim temizlenir. Düzenleme/silme/ödeme/izin işlemlerindeki özgün kayıt sürümü kontrolleri korunur.

Kişi Arama'da Ara veya F5 filtreleri uygular ve ilk sayfaya döner. Sonraki/Önceki aynı uygulanmış filtrelerle devam eder; filtre kutusunu değiştirmek tek başına sorguyu değiştirmez. Ad/soyadda % ve _ gibi işaretler joker yerine gerçek karakter olarak aranır. TC sorgusu bigint parametresiyle yapılır; TC sütununa CONVERT uygulanmaz. Geçersiz TC araması sonuçları genişletmez.

Proje finans ekranı bütün personelleri ComboBox'a yüklemez. **Personel seç / ara** düğmesi aynı sayfalı aramayı açar; seçilen kişinin kalıcı kimliği ve sürümü alınır. Yenilemede yalnızca seçili TC sorgulanır; kart kimliği değişmiş veya arşivlenmişse seçim kaldırılır.

Bir personelin birden fazla güncel izin satırı varsa önce personel sayfası belirlenir, sonra o sayfadaki kişilerin izinleri birleştirilir. Kişinin izinleri sayfalar arasında bölünmez; görünen satır sayısı seçilen personel sayısından fazla olabilir. Ödeme/finans seçicileri gereksiz izin join'i yapmaz. Arşiv filtresi SQL'de uygulanır; toplam yalnızca o filtreyle eşleşen kişilerdir.

Toplu izin seçimi açık sayfayla sınırlıdır; sayfa değiştirirken önceki seçili kişiler arka planda tutulmaz. Veri düzeltme listesi yalnızca açık sayfayı denetler. Excel/CSV dışa aktarmadan önce yalnızca açık sayfanın aktarılacağı açıklanır; bu işlem tüm sonuçları dışa aktarma değildir.

## SQL hesapları

Genel Durum artık personel listesinin tamamını indirip bellekte gruplamaz. Genel/Joker kişi sayısı, toplam bakiye, ortalama ve bugün izinli kişi sayısı SQL'de hesaplanır. İzinli sayısı EXISTS ile kişi başına sayılır; iki izin satırı toplamı veya kişiyi iki kez artırmaz. Arşivlenmiş kartlar dışarıda kalır. Negatif avans bakiyeleri tutarlara aynen katılır.

Özet kartları tüm etkin personeli kapsar; izin uyarısı sayfası bu toplamları değiştirmez. Uyarılar geçmiş 30 gün / bugün / gelecek 30 gün aralığında SQL'de filtrelenip sıralanır, ayrıca sayfalanır. İş günü uygulamanın verdiği DateTime.Today değeridir; merkezi işletme saat dilimi ayarı bu adımın kapsamına dahil değildir. Mevcut DisplaySummary(DataTable) yalnızca tasarım/önizleme test kaynakları için korunmuştur; canlı ekran bunu çağırmaz.

Ödemeler defteri de sayfalıdır, en yeni hareket önce gelir. SQL pencere toplamı OFFSET'ten önce tüm hareketler üzerinde hesaplanır. Böylece ikinci sayfadaki yürüyen bakiye yalnızca o sayfanın toplamı olmaz. Ödeme fişi ve günlük kapanış zaten güncel SQL verilerinden üretilir; görünür defter sayfasına indirgenmez.

Sayfa sayımı ve satırlar aynı yetkili transaction içinde okunur; okuma sırasında aktiflik/rol/oturum denetimi korunur. NOLOCK veya kör izolasyon düşürmesi eklenmedi. Eski ListAsync/LedgerAsync imzaları uyumluluk ve mevcut test kaynakları için korunur; canlı ekranlar bu sınırsız yolları kullanmaz.

## İndeks ve dağıtım

111-personnel-query-indexes.sql etkin Genel/Joker listeleri için filtreli indeksler; izin kişi/tarih ve bitiş aralığı sorguları için indeksler hazırlar. İş verisi değiştirmez. Yeni indekslerin disk/yazma maliyeti vardır; sorgu planı/yük altında ölçüm yapılmadığından belirli bir hızlanma oranı iddia edilmez.

**111 canlı veritabanına bu adımda uygulanmadı.** Masaüstü kısayolu ve canlı şema 110'da kaldı. Yerel paket artifacts/releases/personnel-paging/ altındadır. Yeni paketin açılışı bekleyen yükseltmeyi uygulayabileceğinden kontrollü yedek/yükseltme öncesinde açılmadı.

Kullanıcının isteğiyle testler, ekran ve performans denemeleri çalıştırılmadı. Release çözüm derlemesi **0 uyarı / 0 hata**. Sayfa sınırları, çoklu izin, tüm-veri SQL özetleri, arşiv filtresi, typed TC ve yürüyen bakiye için regresyon kaynakları eklendi ve yalnızca derlendi.

Kalan sınırlar: çok derin OFFSET sayfaları ve COUNT/SUM taramaları veri büyüdükçe maliyetlidir; anahtar tabanlı gezinme/ölçüm gelecekte değerlendirilebilir. Aynı kişinin çok fazla izin satırı ayrıca geçmiş sayfalaması gerektirebilir. Kişi geçmişinin alt sekmeleri, proje listesi ve bazı eski uyumluluk API'leri sınırsızdır; finans belgelerinde mevcut en yeni 500 sınırı sürer. Bu değişiklik bunları veya rapor türü seçimi sorununu tamamen çözdüğünü iddia etmez.
