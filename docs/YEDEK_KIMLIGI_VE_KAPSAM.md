# Yedek kimliği ve kapsam ayrımı

29 Ekim 2026. [F04 bulgusu](PROJE_DURUM_ANALIZI_2026_10_28.md): aynı adlı farklı veritabanlarının yedeklerinin karışmasını önleyen yerel kaynak değişikliği.

## Kimlik ve kapsam

SQL Server'ın yerleşik **FamilyGUID** değeri kalıcı veritabanı soy kimliği olarak kullanılır. Microsoft'a göre FamilyGUID geri yüklemede korunur; BindingID/database_guid ise değişebilir. [SQL Server yedek başlığı](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-headeronly-transact-sql?view=sql-server-ver17), [veritabanı kurtarma metaverisi](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-database-recovery-status-transact-sql).

Kapsam anahtarı; FamilyGUID, katalog adı, kaynak sunucu ve gerçek veri dosyası konumundan üretilen SHA-256'nın 128 bitlik bölümüdür. SQL Express'te sunucunun kendi bildirdiği ad kullanılır; aynı sunucuyu gösteren bağlantı takma adları ayrışmaz. LocalDB'de değişebilen dahili SQL örnek adı yerine bilgisayar, Windows kullanıcısı ve adlandırılmış LocalDB örneği kullanılır. Harf büyüklüğü normalize edilir. Farklı veritabanı kimliği, sunucu veya fiziksel dosya yolu ayrı kapsam üretir; aynı FamilyGUID ile klonlanan bağımsız kopyalar da ayrılır.

Yeni veritabanı tablosu veya migration gerekmez. Kimlik okunamıyorsa yalnızca ada dayalı yönteme geri dönülmez; işlem durur.

## Dosyalar ve doğrulama

Yeni ad biçimi `AccountingApp-v2-<kapsam>-<zaman>-<rastgele>.bak` olur. Kapsam SQL BACKUP NAME alanına da yazılır. Her dosyanın yanında `.backup.json` manifesti bulunur: biçim sürümü, FamilyGUID, kapsam, katalog, kaynak sunucu/dosya konumu, BackupSetGUID, dosya boyutu ve SHA-256 özeti. Manifest bir dijital imza değildir; karışan/eksik/değişen dosyaları belirlemek için kullanılır. SQL başlığının kimliği ayrıca doğrulanır.

- **Günlük kontrol ve durum:** yalnızca güncel kapsamın manifesti ve dosya özeti doğrulanan `.verified` işaretli yedekleri dikkate alır. Başka veritabanının bugünkü yedeği bu veritabanında günlük yedek alınmasını engellemez. Ayarlar kapsamı ve kalıcı kimliği gösterir.
- **Geri yükleme:** dosya adı/manifest/özet kontrolünden sonra SQL VERIFYONLY ve HEADERONLY çalışır. Katalog, FamilyGUID, gömülü kapsam, tek tam yedek olması ve BackupSetGUID eşleşmelidir. Eşleşmeme güvenlik yedeği, temizleme veya mevcut veritabanını değiştirme aşamasından önce reddedilir. Kaynak dosya, kontrol ile geri yükleme arasında değiştirilmemesi için salt okunur açık tutulur. Onay ekranı hedefin adını ve kimliğini gösterir.
- **Aynalama:** kaynak kimliği ve SQL başlığı doğrulanır; `.bak`, manifest ve doğrulama işareti birlikte yayımlanır. Hedefteki kapsamı belirsiz farklı bir dosyanın üzerine yazılmaz. Tamamlanmış kopya tekrar kullanılırken de bütünlük/kapsam kontrol edilir. Yerel klasörle aynı hedef, yerel yedeğin yaşını uzatmaz.
- **Temizleme:** sadece tam kapsam öneki, geçerli manifest ve doğru dosya özeti bulunan yedekler son 14 kayda dahil edilir. Silinen dosyanın manifesti, `.verified` ve `.mirror.json` yan dosyaları da temizlenir. Seçilen restore kaynağı korunur. Başka kapsamlı, eksik manifestli veya bozuk dosyalar otomatik silinmez.
- **Şema yükseltme güvenlik kopyası:** uygulama yedekleriyle aynı kimlik/manifest/başlık ve kapsamlı temizleme kurallarını kullanır.

Kendi kimliği doğrulanamayan dosyalar saklandığı için bir klasörde toplam dosya sayısı 14'ten fazla olabilir. Bütünlük kontrolü yedek içeriğinin okunmasını gerektirir; büyük yedeklerde disk okuması zaman alabilir.

## Eski yedekler ve taşıma

Eski `AccountingApp-<16 karakter>-...bak` dosyaları korunur. Yeni günlük yedek, aynalama veya temizleme bunları sahiplenmez. Eski bir `.verified` işareti kimlik kanıtı değildir. Kimlikli manifesti bulunmayan eski yedek normal geri yükleme ekranında kabul edilmez; dosya adını değiştirmek bu denetimi geçirmez.

**Bir yedeği elle taşırken `.bak` ile aynı adlı `.backup.json` dosyasını birlikte taşıyın.** Aynalama bu dosyaları otomatik taşır. `.verified` otomatik yedek takibi için kullanılır; manuel geri yükleme ayrıca SQL doğrulaması yapar.

Eski yedek kurtarma, farklı bilgisayara taşıma, katalog/dosya konumu değiştirme ve bağımsız klon oluşturma normal yerinde geri yüklemeden farklı işlemlerdir. Bu adımda bunları sessizce onaylayan bir geçiş eklenmedi. Kaynak ve hedefi ayrı doğrulayan bir taşıma/kurtarma akışı gerekir. Bu güvenlik ayrımı nedeniyle yeni sürüme geçerken eski yedek dosyalarını silmeyin.

## Derleme ve deneme durumu

Önceki kullanıcı talebi doğrultusunda test, ekran ve veritabanı denemeleri çalıştırılmadı. Kimlik/kapsam, yanlış SQL başlığı, yeniden adlandırılmış yabancı dosya, eski/bozuk manifest, kapsamlı temizleme ve restore öncesi ret için regresyon kaynakları eklendi; mevcut yedek testlerinin fixture'ları v2 manifestine uyarlandı. Bu testler henüz çalıştırılmadı; önceki başarılı test sayıları bu değişikliğe ait değildir.

Release derlemesi: **0 uyarı, 0 hata**. Gerçek veritabanında yedekleme, geri yükleme, tarama veya şema değişikliği yapılmadı. Yerel .NET 10.0.13 Windows paketi: `artifacts/releases/backup-scope-identity/WinFormsApp2.exe`. Masaüstü kısayolu/çalışan uygulama değiştirilmedi; GitHub'a gönderim/yayın yapılmadı.
