# İşlem geçmişinde hassas veri minimizasyonu

31 Ekim 2026 · Kaynak/paket 1.3.5-dev · Şema 116 · API 1.5.

Kurulum güncellemesi: [116 / 1.3.5-dev yerel kurulumu](1_3_5_DEV_KURULUMU.md) kullanıcı onayıyla tamamlandı; yedek doğrulandı, denemeler atlandı. Aşağıdaki ilk kaynak teslimi notları tarihsel durumdur.

Analizdeki hassas alanların ham audit kopyalarını önleme dilimi uygulanmıştır. Kullanıcının 1.3.4-dev sürümünün çalıştığını bildirmesinden sonra hazırlanmıştır. Canlı kurulum ve veritabanı bu adımda değiştirilmedi; denemeler atlandı.

## Yeni kayıtlar

116 betiği AuditLog tablosuna ChangedFields JSON alanını, AuditSafeJson ve AuditChangedFields fonksiyonlarını, MinimizeAuditInsert trigger'ını ve AuditLogSafe görünümünü ekler. Uygulanmış eski migration dosyaları değiştirilmez. Yeni audit INSERT işlemleri, uygulama/varlık trigger'ı/doğrudan SQL kaynağı ayrılmadan aynı transaction içinde minimizasyondan geçer. Trigger çok satırlı INSERT'i satır bazında işler; hata bütün iş işlemini geri aldırır.

TC, IBAN, ad, soyad, serbest metin açıklama/referans, proje/ekip adları ve izinli alan listesinde olmayan dolu değerler **[MASKELENDİ]** olarak saklanır. Boş/null değerler null kalır. İç içe nesne ve dizilerin gövdesi kopyalanmaz; tamamı maskelenir. Tanımsız yeni alanlar varsayılan olarak maskelenir. Parola veya hash alanları yanlışlıkla bir payload'a eklenirse izin listesinde olmadığından maskelenir; mevcut hesap trigger'ı bunları zaten üretmez.

Kimlik GUID'leri, tarih/durum/yetki alanları ve finansal tutarların eski/yeni değerleri izin listesiyle tutulur. Bu, ücret/mali tutarlara yeni bir görüntüleme yetkisi getirmez: audit sadece yöneticiye açıktır. İşlemi yapan hesap, zaman, kayıt kimliği, gerekçe ve korelasyon metadatası korunur.

**ChangedFields**, maskelemeden önceki JSON değerlerinden SQL tarafında hesaplanan değişen alan adlarıdır; çağıranın gönderdiği ChangedFields kabul edilmez. Örneğin IBAN değiştiğinde iki değer de maskeli görünür, değişen alanlar IBAN'ı içerir. Ham değer veya geri tahmin edilebilir değer hash'i tutulmaz. İlk eklemede mevcut alanlar, silmede kaldırılan alanlar listelenir; ayrıntısız tarihsel olayda alan listesi null kalır. Sıra anlam taşımaz.

## Eski kayıtları okuma

Mevcut audit satırları güncellenmez/silinmez; değişmezlik koruması kaldırılmaz. Genel işlem geçmişi ve personel kartının işlem geçmişi artık AuditLogSafe üzerinden okur. Eski OldValues/NewValues da aynı SQL fonksiyonuyla maskelenir, alan değişimi eski özgün gövdeden hesaplanır. Böylece API yanıtı, ekran, CSV/Excel dışa aktarma ve detay görünümü aynı maskeli veriyle çalışır. Ekranda gizlemekle yetinilmez.

Audit yönetici kontrolü ve kişisel veri dışa aktarma kontrolü korunur. ProjectUser'a audit erişimi açılmaz. İşlem geçmişi detay metninde doğrudan klavye/bağlam menüsü kopyası kapalıdır; yetkili tablo dışa aktarma yolu kullanılabilir.

## Sınırlar ve kurulum

116 yalnız yeni audit gövdelerinin saklanma politikasını değiştirir. Eski ham audit satırları, eski yedekler ve asıl personel/finans kayıtları yerinde kalır; SQL/veritabanı dosyasına doğrudan erişimi olan kişiye karşı bir şifreleme veya SQL erişim yetkisi sınırı değildir. İzinli tarih/tutar/teknik kimlik alanları ve aktör/SQL hesabı bilgileri maskelenmez. Kullanıcının serbest gerekçe metnine kişisel veri yazması otomatik metin taramasıyla temizlenmez; gerekçeye TC/IBAN/parola yazılmamalıdır. Bu çalışma tüm uygulamayı kapsayan hassas alan yetki matrisi değildir.

116 yükseltmesi önce doğrulanmış yedek gerektirir ve oturum nesillerini yeniler. 1.3.5-dev istemci/API birlikte kurulmalı; API şemayı yalnız doğrular, otomatik yükseltmez. Eski 115 bekleyen uygulama yeni şemaya uyumlu varsayılmaz. Bu teslimde yükseltme uygulanmadı, çalışan 1.3.4-dev/115 ve masaüstü kısayolu değiştirilmedi.

Release derlemesi ve paket manifest/dosya özetleri kontrol edilir. SQL migration, API, kullanıcı arayüzü, eşzamanlılık, performans ve işlev denemeleri kullanıcı isteğiyle çalıştırılmadı. SQL betiği hazırlanmıştır; veritabanında yürütülmüş olduğu iddia edilmez.
