# Kalıcı personel kimliği ve arşiv

**31 Ekim 2026 izleyen kaynak güncellemesi:** [Çalışma dönemleri](CALISMA_DONEMLERI.md), 1.3.2-dev / şema 114 / API 1.2 olarak hazırlandı. Aşağıdaki önceki sürüm/kapsam ifadeleri tarihsel teslimi anlatır; yeni sürüm henüz kurulmadı.

29 Ekim 2026. [F05 bulgusu](PROJE_DURUM_ANALIZI_2026_10_28.md) için yerel kaynak değişikliği.

## Kullanım

Genel/Joker Personel ekranlarında **Sil** yerine **Arşivle** bulunur. Personel etkin listeden çıkarılır; kart, ad/soyad, IBAN, kalan bakiye, mevcut izin satırları ve ödeme hareketleri korunur. Arşivleme tarihini ve yapan hesabı kaydeder. Arşivleme ödeme veya borç silme anlamına gelmez; kalan tutar sıfırlanmaz.

Listede **sağ tık → Personel arşivi ve geçmişi…** seçilir. Varsayılan görünüm arşivlenen kartlardır; **Etkin kayıtları da göster** ile etkin kartlar incelenebilir. Seçili kartın kalıcı kimliği, ödeme defteri, izin kayıtları ve arşiv/etkinleştirme olayları gösterilir. Kapat/Esc ile dönülür; ana liste yenilenir.

Yönetici/personel sorumlusu **Seçili kartı tekrar etkinleştir** ile aynı kartı, kimliği, izinleri ve bakiyeyi koruyarak etkinleştirebilir. Görüntüleyici geçmişi okuyabilir; değişiklik yapamaz. Arşivde veya etkin durumda bulunan TC için ikinci kart açılamaz. İşe geri dönen kişi aynı kart üzerinden etkinleştirilir. Grup değiştirme ve ayrı işe giriş/çıkış sözleşmeleri bu adımda eklenmedi.

Arşivlenen kişiler normal personel/arama/ödeme/izin planı ve Genel Durum'un etkin listelerinden çıkar. Arşivdeki bakiye geçmiş ekranında/defterde saklanır. Önceden gerçekleşmiş ödemeler arşivleme nedeniyle raporlardan silinmez.

## Kimlik ve geçiş

Yeni **106-personnel-identity-archive.sql** betiği merkezi PersonnelIdentities tablosu ile her karta değişmez GUID PersonId atar. TC tekil iş kuralı olarak korunur; eski TC anahtarları uyumluluk için sökülmez. SQL tetikleyicileri kimliğin/TC'nin değiştirilmesini ve iki grup arasında aynı TC ile ikinci kart oluşturulmasını engeller. Mevcut kartların bilinmeyen oluşturulma tarihleri uydurulmaz.

İzinler PersonId üzerinden bağlanır; kimlik/TC tutarlılığı birleşik foreign key ile korunur. Yeni ödeme/tahsis/kapanış/iptal hareketleri aynı kalıcı kimliği taşır. Geçmiş başka personelin güncel ayrıntılarından türetilmez. Fiş kimliği bilinen ödemede PersonId'yi de gösterir.

Önceki sürümlerde fiziksel silinen kartların kayıp ad/IBAN bilgileri geri getirilemez. Geçişte yalnızca mevcut kartın son Closure sonrasındaki ödeme segmenti bu karta bağlanır. Önceki kapanmış segmentlerin kimliği bilinmiyor olarak bırakılır; bütün defterde korunur, bugünkü karta tahminen bağlanmaz. Eski kimliksiz ödeme otomatik iptal edilemez.

Arşivleme kalan tutarı azaltmak yerine **sıfır tutarlı Closure** sınırı ekler. Bu sınır önceki dönemin otomatik ödeme iptalini ve eski ödeme isteklerini engeller. Yeni dönem ödemeleri normal şekilde kaydedilip iptal edilebilir. Arşiv/etkinleştirme olayında bakiye de saklanır.

Yeni bekleyen ödeme biçimi 3'tür; PersonId ve özgün sürüm zorunludur. Eski 0/2 biçimli dosyalar silinmez: kaydedilmiş sonuç RequestId ile okunabilir, kaydedilmemiş eski istek yeni ödeme oluşturamaz. Sonuç onayından sonra güncel kartla yeni istek başlatılır.

## İşlem güvenliği

Arşivleme ve etkinleştirme canlı oturumu/yetkiyi doğrular, özgün RecordVersion ile hedefi kilitler. Kart, kapanış, arşiv olayı ve audit aynı transaction içinde yazılır. Eski seçim/eşzamanlı değişiklik reddedilir; audit hatası tamamını geri alır. Arşivdeki karta düzenleme, ödeme ve izin kaydı yapılamaz. Eski DeleteAsync/PersonnelStore.Delete girişleri de artık arşivler; fiziksel personel/izin silmez.

Bu adım mevcut izin satırlarını arşiv sırasında korur. İzin tarihlerinin bütün eski düzenleme sürümleri, ayrıntılı ad/IBAN değişiklik audit'i ve ayrı işe giriş/çıkış dönemleri sonraki yol haritası işleridir.

## Yükseltme ve doğrulama

29 Ekim 2026, izleyen canlı işlem: kullanıcının açık onayıyla mevcut veritabanına yalnızca 106 uygulandı. Öncesinde COPY_ONLY/CHECKSUM yedeği alındı; SQL VERIFYONLY, v2 kimlik/manifest ve SHA-256 doğrulaması tamamlandı. Mevcut alanların içerik özetleri yükseltme öncesi/sonrası aynı kaldı; kimlik eşlemesi ve migration kaydı doğrulandı. Test/ekran denemesi yapılmadı. Özel yedek ve raporlar Git dışında `artifacts/analysis/live-upgrade-106/` altında saklanır. Masaüstü kısayolu 106 uyumlu yerel pakete yönlendirildi; uygulama başlatılmadı. Aşağıdaki hazırlık notu, yükseltme uygulanmadan önceki durumu kaydeder.

106 betiği hazırdır; önceki 100–105 betikleri değiştirilmedi. Gerçek veritabanına bu çalışma sırasında uygulanmadı. Güncel uygulamanın normal şema hazırlığı yükseltme öncesi doğrulanmış güvenlik yedeği oluşturur. 106 tamamlandığında açık eski oturumların güvenlik nesli yenilenir; eski uygulamalar yeniden yetkili işlem yapamaz. Geçişte eski uygulamalar kapatılmalıdır; eski sürüm 106 sonrasındaki şemayı desteklemez.

Önceki kullanıcı talebi doğrultusunda testler ve ekran/veritabanı denemeleri çalıştırılmadı. Kimliğin korunması, bakiye/izin/ödemelerin saklanması, rollback, eski seçim/ödeme isteği ve rol koruması için regresyon kaynakları eklendi; eski fiziksel silme beklentileri ve fixture'lar arşiv davranışına uyarlandı. Çalışma zamanı sonuçları henüz doğrulanmadı. Release derlemesi **0 uyarı / 0 hata**.

Yerel .NET 10.0.13 Windows paketi: `artifacts/releases/personnel-identity-archive/WinFormsApp2.exe`. Bağlantı ayarı korunur. Masaüstü kısayolu/çalışan uygulama değiştirilmedi; GitHub'a gönderim/yayın yapılmadı.
