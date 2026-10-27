# İzin kaydetmede personel ve izin sürüm koruması

26 Ekim 2026. Genel Personel ve İzin Planlama için yerel geliştirme; önceki personel silme ve hesap hedefi korumaları korunur.

## İki ayrı kontrol

Personelin RecordVersion değeri, başka kullanıcının personeli değiştirmesini veya silip aynı TC ile yeniden oluşturmasını yakalar. İzin tarihleri değişirken personel satırı değişmeyebileceği için izin tablosunda ayrı LeaveVersion kullanılır. SQL Server izin satırı her güncellendiğinde bu sürümü yeniler; tarihler aynı değere dönse bile eski seçim geçerli olmaz.

Listeden özgün TC, personel sürümü ve izin sürümü birlikte alınır. Henüz izin yoksa null izin sürümü, seçim anında izin bulunmadığı anlamına gelir. Bu durumda kaydetme sırasında hâlâ izin bulunmamalıdır; başka kullanıcının eklediği izin güncellenmez. Eksik metadata, izin yokmuş gibi kabul edilmez.

Genel Personel'de seçili satır, İzin Planlama'da seçili tüm personelin anlık görüntüsü kullanılır. Toplu planın seçimleri ve tarihleri onaydan önce sabitlenir. Aynı kişinin aynı özgün sürümü birden fazla satırda seçilmişse bir kez işlenir; farklı izin kayıtları sessizce birleştirilmez. Önceki çoklu izin geçmişini topluca değiştirmeme koruması devam eder.

## Veritabanı işlemi

1. Rol ve güncel oturum doğrulanır; bütün istek sürümleri veritabanından önce kontrol edilip kopyalanır.
2. Personeller TC sırasıyla, özgün personel sürümü koşuluyla UPDLOCK/HOLDLOCK kullanılarak kilitlenir.
3. Her personelin izin satırları ve beklenen yokluk durumu aynı transaction içinde kilitlenerek doğrulanır.
4. Güncellemede TC ve LeaveVersion SQL koşulunun parçasıdır; yeni kayıt yalnızca beklenen yokluk hâlâ geçerliyse eklenir.
5. Tam bir izin satırı etkilenmelidir. Planın bir kaydı bile çatışırsa önceki personellerde yapılan ekleme/güncellemeler ve audit dahil planın tamamı geri alınır.

Reddedilen işlem personel bilgilerini, para bakiyesini veya ödeme defterini değiştirmez. Form alanları otomatik temizlenmez; kullanıcı bilgilerini kontrol edip F5 ile yeniler, güncel kaydı tekrar seçer. Eski sürüm otomatik olarak güncel sürümle eşleştirilip üzerine yazılmaz. Eski eşzamanlı PersonnelStore.SaveLeave yardımcısı da aynı sürümlü repository yolunu kullanır.

LeaveVersion iç metadata'dır; listelerde, kolon menüsünde ve Excel/CSV raporlarında gösterilmez. İzin ekleme/düzenleme personelin ad/IBAN/bakiye alanlarını değiştirmez.

## Şema ve dağıtım

Yeni ek yükseltme: `database/upgrades/105-leave-rowversion.sql`. Yalnızca dbo.izinler tablosuna SQL Server rowversion türünde LeaveVersion ekler ve mevcut aynı adlı sütunun uyumluluğunu doğrular. Eski 001/100/101/102/103/104 betikleri değiştirilmedi.

Mevcut yükseltme akışı uygulanmadan önce CHECKSUM yedeği alıp VERIFYONLY ile doğrular; DbUp betikleri transaction içinde uygular. Mevcut personel, izin tarihleri, hesap, oturum ve ödeme bilgileri korunur. Tekrar çalıştırıldığında uygulanmış betik yinelenmez. Uyumsuz sütun varsa yükseltme durur.

Bu geliştirmede yeni betik yalnızca ayrı test veritabanlarında uygulandı; **gerçek veritabanına henüz uygulanmadı**. Açık uygulama ve masaüstü kısayolu bu adımda değiştirilmedi. Yerel test paketi `artifacts/releases/leave-version/` içindedir. Gerçek ortama geçişte eski istemciler kapatılıp yeni paket kullanılmalıdır; yeni paketin standart giriş hazırlığı gereken yükseltmeyi yedek alarak uygular. Yeni şema uygulandıktan sonra eski sürüme dönmek için yalnızca eski EXE'yi açmak yeterli değildir.

## Elle deneme

Yalnızca deneme için oluşturulmuş personel ve iki ayrı uygulama oturumu kullanın.

1. A'da izinli bir personeli seçin. B'de aynı kişinin izin tarihlerini değiştirip kaydedin. A'da farklı tarihlerle İzni Kaydet'i deneyin: işlem reddedilmeli, B'nin tarihleri korunmalı.
2. A'da henüz izni olmayan bir personeli seçin. B'de bu kişiye izin ekleyin. A'nın eski izin ekleme isteği B'nin kaydını güncellememeli veya ikinci satır oluşturmamalı.
3. A eski seçimi korurken B'de personeli silip aynı TC ile yeniden oluşturun. A'daki izin isteği yeni personele yazmamalı.
4. İzin Planlama'da iki kişiyi seçin. Diğer oturumda ikinci kişinin iznini değiştirin. Eski plan onaylandığında ilk kişinin izni de değişmeden kalmalı.
5. F5 ile güncel listeyi alın, personeli yeniden seçin ve kaydedin: güncel sürümle işlem tamamlanmalı.

## Otomatik doğrulama

Gerçek veritabanından ayrı LocalDB motorunda eski seçim, ilk izin ekleme yarışı, izin satırını silip yeniden oluşturma, tarihleri aynı değere döndürme, personel değişikliği/ödeme, eşzamanlı izin kaydı, toplu çatışma ve audit hatasında rollback; metadata gizleme ve 105 yükseltmesinin tekrarlanabilirliği test edilir. Son tam test sonucu CHANGELOG'da belirtilir.

Son doğrulama: **490 başarılı, 0 başarısız, 0 atlanan**. Önceki 456 test korunup 34 yeni test durumu eklendi. Release derlemesi: 0 uyarı, 0 hata. Kanıt: `artifacts/analysis/master-20261026-162016-546/master.trx`. Geçici test motoru temizlendi.

## 26 Ekim 2026 — gerçek veritabanına uygulama ve ekran testleri

Kullanıcı onayıyla eski uygulama kapatıldı. Kullanılan bağlantı profili üzerinden mevcut veritabanının COPY_ONLY/CHECKSUM yedeği alındı; RESTORE VERIFYONLY WITH CHECKSUM başarılı. Yedek `artifacts/analysis/leave-live-ui/live-backups/` içinde `-pre105.bak` son ekli dosyadır. Ardından yalnızca 105 yükseltmesi uygulandı; izin tablosundaki LeaveVersion türü ve tek journal kaydı doğrulandı. Personel, izin, hesap, ödeme defteri, audit ve giriş sayacı tablolarının mevcut sütunları yükseltme öncesi/sonrası satır sayısı ve SHA256 özetleriyle karşılaştırıldı: bütün mevcut veriler aynı kaldı. Yukarıdaki “henüz uygulanmadı” açıklaması geliştirme aşamasının durumudur; bu dağıtımla 105 gerçek veritabanına uygulanmıştır.

Muhasebe - Güncel masaüstü kısayolu `artifacts/releases/leave-version/WinFormsApp2.exe` paketine yönlendirildi; bağlantı ayarı korundu ve giriş hazırlığı başarılı tamamlandı. Uygulama açık bırakıldı.

Yayımlanan DLL ile aynı SHA256 değerine sahip gerçek Genel Personel ve İzin Planlama ekranları, ayrı LocalDB motoru ve yalnızca sentetik personellerle denendi. Dört senaryonun her birinde eski seçim reddi ve F5 sonrası güncel seçimle başarılı kayıt olmak üzere **8 ekran kontrolü başarılı**:

- Aynı TC ile yeniden oluşturulan personel ve yeni izni eski seçimden etkilenmedi.
- Başka oturumun güncel izin tarihleri eski seçimle değiştirilmedi.
- Seçimden sonra ilk iznin başka oturumda eklenmesi ikinci satır oluşturmadı veya mevcut izni ezmedi.
- İki kişilik planda ikinci kişinin sürüm çatışması ilk kişinin yazmasını da geri aldı; audit ve ödeme defteri değişmedi. Yenilenen seçimle iki kişilik plan tek audit kaydıyla tamamlandı.

Test veritabanı ve yalnızca ona ait LocalDB motoru temizlendi. Ekran testleri gerçek kayıtları kullanmadı. Kanıtlar: `artifacts/analysis/leave-live-ui/live-upgrade.json`, `ui-results.json`, `deployment.json` ve `cleanup.txt`. Test DLL/paket SHA256: `23826A4BCECB55975EFDA673CD2D30801F9C32638A1C7CEDA389ECAA6BBB6C1C`. GitHub'a push veya yeni Release yapılmadı.
