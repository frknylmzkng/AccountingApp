# Çalışan–hesap eşlemesi ve puantaj görev ayrımı

İzleyen durum: [126 / 1.3.15-dev kurulumu](1_3_15_DEV_KURULUMU.md) tamamlandı ve kullanıcı doğruladı. 1.3.16-dev kaynakta [kalıcı puantaj taslak kurtarma](KALICI_PUANTAJ_TASLAK_KURTARMA.md) eklendi. Aşağıdaki kaynak teslimi metni tarihsel kapsamdır.

2 Kasım 2026 · Kaynak/paket **1.3.15-dev** · Hazırlanan şema **126** · API **1.15**.

Kullanıcı 1.3.14-dev sürümünün çalıştığını doğruladı. Bu adım yönetici tarafından açık hesap–personel eşlemesini ve bu kimlik kanıtına dayanan puantaj onay korumasını ekler. Çalışan web/self servis ekranı değildir. Kurulu 1.3.14-dev / API 1.14 / şema 125, canlı veritabanı ve masaüstü kısayolu bu kaynak tesliminde değiştirilmedi. Denemeler kullanıcı isteğiyle atlandı.

## Kullanım

1. Yönetici ekranında hesabı seçip **Personel eşlemesi…** düğmesini açın. Etkin bağ ve o hesabın önceki bağları gösterilir.
2. **Personel seç / değiştir…** ile mevcut sayfalı personel aramasından kişiyi seçin. Seçim ad benzerliğiyle otomatik yapılmaz. Onay ekranında hesap, eski kişi, seçilen kişi/grup ve TC'yi kontrol edin.
3. **Kaydet** ve gerekçe ile kaydedin. Kişi değiştiyse eski bağ gerekçesiyle kaldırılır, yeni bağ aynı transaction içinde eklenir. Hedef hesabın açık oturumları sonlanır. Kendi hesabınızı eşlerseniz yeniden giriş gerekir.
4. **Etkin bağı kaldır…** yalnız etkin ilişkiyi kaldırır; geçmiş silinmez. Başka kişiye geçmek için ayrıca kaldırmak gerekmez; yeni kişiyi seçebilirsiniz.

Bir hesapta en fazla bir etkin bağ olabilir. Bir çalışana birden fazla hesap bağlanabilir. Bağ, çalışma dönemine veya Genel/Joker etiketine değil kalıcı PersonId'ye aittir; yeniden işe giriş ve grup geçişinde aynı kişiyi temsil eder. Yeni bağ için personel arşivde olmamalı ve seçimdeki sürümü güncel olmalıdır. Eski bağın görüntülenmesi arşivleme yüzünden kaybolmaz.

## Onay güvenliği

Dağıtılmamış puantaj ve bağımsız proje parçaları için aşağıdaki yeni kontrol uygulanır:

- Onaylayan hesap çalışanın PersonId'sine şimdi veya geçmişte bağlandıysa kendi çalışma kaydını onaylayamaz.
- Onaylayan ve hazırlayan, son düzenleyen, gönderen, dağılımı hazırlayan veya açık turu gönderen hesapların ortak bir personel eşlemesi varsa onay reddedilir.
- Etkin ham olay eşlemesinde eşleyen veya giriş/çıkış olayını aktaran hesaplarla aynı kimlik kanıtı varsa onay reddedilir.

Mevcut aynı AccountId kontrolü de sürer; eşlenmemiş hesaplarda eski koruma kaybolmaz. İade, hatalı hazırlığı geri çevirebilmek için bu öz onay yasağına tabi değildir; canlı proje onay yetkisi ve sürüm kontrolü yine zorunludur. Başka projelerin adları, saatleri veya eşleme ayrıntıları proje amirine bu kontrol üzerinden verilmez; listede yalnız kimlik/görev çakışması bilgisi görünür. Yetkiler değişmez: eşleme ücret/IBAN, dışa aktarma, proje veya personel düzenleme hakkı vermez.

Eşlemeler bütün personel/hesaplar için açıkça yapılmalıdır. Sistem eşlenmemiş iki hesabın gerçekte aynı kişiye ait olduğunu bilemez; kullanıcı adı, TC veya ad üzerinden tahmin yapmaz. Yanlış eşlemeyi düzeltirken geçmiş bağlar da kanıt olarak kalır; bunun sonucunda temkinli bir onay reddi devam edebilir. Bağı kaldırmak geçmiş kimlik kanıtını kaldırma veya onay yasağını aşma yolu değildir.

Önceden verilmiş kararlar, saat toplamları ve onay geçmişi eşleme oluşturulunca geri yazılmaz. Yeni onay istekleri o anda mevcut bütün eşleme geçmişiyle değerlendirilir. Eski onayları yeni kimlik bilgisiyle topluca yeniden inceleme, ödeme/izin süreçlerinde bu kimliği kullanma ve çalışan self servis kapsamı bu teslimin dışındadır.

## Eski seçim, tekrar ve işlem geçmişi

Hesap hedefi özgün SessionVersion/SessionEpoch ile; personel hedefi PersonId/grup/RecordVersion ile; kaldırılan bağ LinkId/RecordVersion ile doğrulanır. Silinip aynı kullanıcı adıyla açılan hesap eski AccountId'yi veya bağları devralmaz. Aynı TC ile farklı kişi oluşturulması da eski PersonId bağını taşımaz. Hesap silinse bile geçmiş bağlar, puantaj hazırlayan eski hesap kimliğinin kanıtı olarak saklanır.

Değişiklik, oturum iptali, audit ve kalıcı komut makbuzu aynı transaction içindedir. Aynı CommandId/aktör/içerik tekrarı yeni işlem oluşturmaz; farklı içerikle aynı kimlik reddedilir. Yanıt belirsizse açık onay ekranındaki **Aynı isteği yeniden gönder** kullanılabilir. Yeniden giriş gerekirse hesap listesini yenileyip etkin bağı kontrol edin; önceki istek kaydedildiyse hesap/bağ sürümü değişmiştir. Kalıcı çevrimdışı eşleme kuyruğu bu adımda yoktur.

Eşleme değişikliği ve puantaj onayları aynı transaction uygulama kapısını kullanır. SQL benzersiz indeksleri etkin bağ tekilliğini sağlar. Bağ gövdesi ve kaldırılmış bağ değiştirilemez, geçmiş fiziksel silinemez. Gerekçe, aktör ve UTC zamanlar saklanır; yönetici geçmişi hesap kapsamındadır. Audit eski/yeni gövdelerinde hassas metin maskelemesi sürer; LinkId, PersonId ve hesap kimlikleri korunur.

## Şema ve dağıtım

`126-account-person-identity.sql` iki boş tablo, tarihsel kimlik sorgu fonksiyonları, puantaj/parça onay korumaları ve audit ekler. 100–125 betikleri değiştirilmedi. Eski hesaplara otomatik bağ oluşturulmaz; mevcut personel, çalışma ve ödeme kayıtları değiştirilmez. Uygulama geçişinde eski oturumlar sonlandırılır. İstemci ve API birlikte dağıtılmalıdır; API başlangıcı canlı şemayı kendiliğinden yükseltmez.

Bu kaynak tesliminde SQL yükseltmesi uygulanmadı; giriş, eşleme, onay, ağ kesintisi, eşzamanlılık ve ekran denemeleri yapılmadı. Derleme ve paket bütünlüğü kontrolü çalışma zamanı davranışının doğrulanması değildir. Aşama 4'te sonraki işler kalıcı taslak kurtarma ve çok kişili toplu manuel giriş; çalışan self servis ayrı kapsamdır.
