# İzin tarihleri değişiklik geçmişi

29 Ekim 2026. [F06 bulgusu](PROJE_DURUM_ANALIZI_2026_10_28.md) için yerel kaynak değişikliği. Mevcut 106 kimlik/arşiv yapısının üzerine **107-leave-history.sql** hazırlanmıştır.

## Kullanım ve davranış

Genel Personel'deki **İzni kaydet** ve toplu İzin Planlama aynı güncel izin satırını kullanmaya devam eder. Tarihler değiştirildiğinde ayrı LeaveHistory tablosuna önceki/yeni başlangıç-bitiş tarihleri, kalıcı PersonId, izin kaydı numarası ve sürümleri, UTC kayıt zamanı ve kaydeden hesap eklenir. Eski tarihler kaybolmaz. Aynı tarihler yeniden kaydedildiğinde sahte bir tarih değişikliği oluşturulmaz; eşzamanlı işlem koruması için LeaveVersion yine güncellenebilir.

Geçmiş, **Genel Personel listesi → sağ tık → Personel arşivi ve geçmişi → kart seç → İzin değişiklikleri** sekmesinden okunur. Güncel izinler ayrı sekmededir; en yeni değişiklik en üstte görünür. Eski/yeni gün sayıları başlangıç ve bitiş dahil takvim günü olarak gösterilir. Görüntüleyici rolü geçmişi okuyabilir, değiştiremez. Personel arşivlense veya yeniden etkinleştirilse de aynı kimliğe bağlı geçmiş saklanır.

Bu adım birden fazla yeni izin dönemi ekleyen takvim/başvuru/onay modülü değildir. Mevcut tek güncel kayıt düzenlemesi korunur; daha önce birden çok izin satırı bulunan personeli tek seçimle topluca değiştirme koruması devam eder.

## Saklama ve güvenlik

- SQL trigger'ı yeni izinleri, tarih değişikliklerini ve fiziksel silmeleri aynı transaction içinde yakalar. Uygulamanın bir yolunun geçmiş yazmayı unutması eski tarihleri kaybettirmez. Silinen izin numarasıyla birlikte eski tarihler geçmişte kalır; geçmiş izin tablosuna silmede zincirlenen bir foreign key ile bağlı değildir.
- Uygulama kaydında kullanıcı adı açıkça yazılır. Eski PersonnelStore yolu `legacy-store` olarak işaretlenir. Uygulama kullanıcısı bildirmeyen doğrudan SQL güncellemelerinde önceki kişinin adı tekrar kullanılmaz: kullanıcı bilinmiyor bırakılır, ayrıca SQL bağlantısının asıl hesabı kaydedilir.
- Güncelleme ve geçmiş aynı işlemde olduğundan audit hatası, iptal veya toplu kayıttaki çatışma geçmişi de geri alır. Başarısız/eski seçim için hayali geçmiş oluşmaz. Personel/izin sürüm kontrolleri devam eder.
- Geçmişte UPDATE/DELETE işlemleri SQL korumasıyla reddedilir; normal uygulamada geçmişi düzenleme veya silme yolu yoktur. İzin kaydının PersonId/TC/izin kimliği başka kayda yönlendirilemez. Bu, veritabanı yöneticisine karşı kriptografik bir denetim sistemi değildir.
- Geçmiş etkin personel listelerine, izin toplamına veya Excel toplamlarına ek satır olarak katılmaz. Özetler güncel izin verisini kullanır.

## Mevcut kayıtların aktarımı

107 uygulanırken mevcut izinler silinmez/değiştirilmez; o anda bilinen tarihleri **Aktarılan mevcut kayıt** olarak geçmişe alınır. Önceden üzerine yazılmış tarihler geri üretilemez. Bu satırların eski oluşturulma zamanı ve kaydeden kişisi bilinmediği için boş bırakılır; yeni tarihler uydurulmaz. Sonraki değişikliklerde gerçek SQL işlem zamanı ve bildirilen uygulama kullanıcısı kaydedilir.

Eksik veya ters mevcut tarih varsa yükseltme sessiz düzeltme yapmak yerine durur. 100–106 betikleri değiştirilmez. Şema hazırlığı yükseltme öncesinde doğrulanmış güvenlik yedeği alır.

## Doğrulama ve dağıtım

29 Ekim 2026, izleyen canlı işlem: kullanıcının açık onayıyla mevcut veritabanının COPY_ONLY/CHECKSUM yedeği alındı; SQL VERIFYONLY ve v2 manifest/SHA-256 doğrulamasından sonra yalnızca 107 uygulandı. Mevcut alanların içerik özetleri ve kalıcı kimlikler değişmedi; migration kaydı, geçmiş tablosu, etkin iki trigger ve kaydeden alanı doğrulandı. Mevcut izin satırı bulunmadığından başlangıç aktarımında kayıt oluşturulmadı. Test/ekran denemesi yapılmadı. Özel rapor ve yedekler Git dışındaki `artifacts/analysis/live-upgrade-107/` klasöründedir. Masaüstü kısayolu 107 uyumlu pakete yönlendirildi; uygulama başlatılmadı. Aşağıdaki notlar önceki kaynak hazırlığının durumudur.

Kullanıcı isteğiyle test ve ekran/veritabanı denemeleri çalıştırılmadı. Tarih zinciri, kaydeden hesap, aynı tarihlerin tekrar kaydı, eski seçim, audit/toplu rollback, doğrudan SQL değişikliği/silme, geçmişin değişmezliği ve arşiv/rol erişimi için regresyon kaynakları eklendi. Yalnızca Release derlemesi yapıldı: **0 uyarı, 0 hata**. Derleme SQL betiğinin çalışma zamanı denemesi değildir.

107 betiği ve .NET 10.0.13 Windows x64 yerel paketi `artifacts/releases/leave-history/` altında hazırdır. Gerçek veritabanına bu adımda 107 uygulanmadı; canlı veritabanı 106'da kalır. Masaüstü kısayolu ve çalışan uygulama değiştirilmedi. GitHub'a gönderim/yayın yapılmadı.
