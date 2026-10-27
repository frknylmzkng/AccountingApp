# Hesap silme ve durum işlemlerinde eski seçim koruması

26 Ekim 2026. Yerel geliştirme; önceki personel silme koruması bu pakette korunur.

## Davranış

Yönetici ekranındaki Sil, Pasife al ve Etkinleştir işlemleri yalnızca kullanıcı adıyla çalışmaz. Seçilen satırın özgün kullanıcı adı, AccountVersion (SessionVersion) ve AccountEpoch (SessionEpoch) değerleri, onay penceresi açılmadan önce alınır. Sürüm dizisi kopyalanır; sıralama veya ekrandaki değerlerin değişmesi yeni bir hedef oluşturmaz.

Servis, eksik/bozuk hedefi veritabanını açmadan reddeder. Hesap yönetim kilidi ve yöneticinin güncel yetki doğrulaması korunur. Transaction içinde hedef satır UPDLOCK/HOLDLOCK ile okunur; kullanıcı adı, sürüm ve epoch birlikte eşleşmelidir. UPDATE/DELETE koşulunda aynı üç alan yeniden aranır ve tam bir satır etkilenmesi gerekir.

Hesap silinip aynı kullanıcı adıyla tekrar oluşturulduysa eski seçim reddedilir. Hedefin rolü, parolası veya durumu değiştiyse de eski seçimle işlem yapılamaz. İstenen durum zaten güncel durumla aynı olsa bile önce sürüm kontrolü yapılır; eski hedef sessizce geçerli sayılmaz.

Reddedilen işlem yeni hesabı, rolünü, parolasını, etkinliğini, oturum kimliğini, hatalı giriş sayacını veya audit kayıtlarını değiştirmez. Kullanıcı F5 ile listeyi yenileyip güncel hesabı yeniden seçer ve işlem için tekrar onay verir. Hata halinde alanlar veya seçim otomatik olarak yeni hesaba taşınmaz.

Kendi hesabını silme/pasife alma, son etkin ve parolası tamamlanmış yöneticiyi koruma ve pasif/eski oturumun işlem yapamaması kontrolleri korunur. İşlem audit'i başarısızsa hesap silme/durum değişikliği ve giriş sayacı temizliği birlikte geri alınır. Eski eşzamanlı AdminStore.Delete yardımcısı da kullanıcı oturumu ve AccountTarget alarak aynı yetkilendirilmiş servise yönlenir; kullanıcı adıyla sürümsüz silme yolu kaldırıldı.

## Şema ve dağıtım

Mevcut 102-account-session-version yükseltmesindeki alanlar kullanılır. Yeni SQL betiği veya gerçek veritabanı yükseltmesi gerekmez.

Yerel test paketi: `artifacts/releases/account-target-version/`. Bu paket personel silme korumasını da içerir. Açık uygulama, masaüstü kısayolu ve GitHub Release bu geliştirme adımında değiştirilmez; yeni istemci kullanılmaya başlandığında koruma devreye girer.

## Elle deneme

Yalnızca bu deneme için oluşturulan hesapları kullanın; mevcut gerçek hesapları silmeyin. İki yönetici uygulama oturumu açın.

1. A oturumunda geçici bir görüntüleyici/personel hesabını seçin.
2. B oturumunda bu hesabı güncel seçimle silin, aynı kullanıcı adıyla yeni bir hesap oluşturun.
3. A'da eski seçimi koruyarak Sil veya Pasife al işlemini onaylayın: seçim çatışması görünmeli, yeni hesap korunmalı.
4. A'da F5 ile listeyi yenileyip yeni hesabı seçin. Yeni onaydan sonra yalnızca seçilen güncel hesap üzerinde işlem yapılmalı.
5. Başka oturumda hedefin durumunu veya parolasını değiştirip A'daki eski seçimi deneyin: yine reddedilmeli.
6. Kendi yönetici hesabınızı silme/pasife alma denemesi engellenmeli. Geçerli pasife alma açık hedef oturumlarını iptal etmeye devam etmeli.

## Doğrulama kapsamı

Kalıcı testler: eksik/bozuk sürüm ve kimlik; sıralanmış/gizli sütunlu seçimde özgün hedef; yeniden oluşturulan hesapta eski sürüm ve eski epoch'un ayrı ayrı reddi; rol/parola/durum değişikliği; silinmiş hedef; geçerli silme/pasife alma/etkinleştirme; audit hatasında rollback; eşzamanlı hesap işlemleri; eski eşzamanlı silme yardımcısının güvenli servisi kullanması. Önceki personel silme, ödeme, parola, giriş sınırlaması ve oturum testleri de tam takımda çalıştırılır.

Testlerde gerçek veritabanı veya kullanıcı parolası kullanılmaz; ayrı geçici LocalDB motoru işlem sonunda temizlenir. Son tam test sonucu ve kanıt klasörü CHANGELOG'da kaydedilir.

Son doğrulama: **456 başarılı, 0 başarısız, 0 atlanan**; Release derlemesi 0 uyarı / 0 hata. Önceki 414 test korunarak hedef kimliği ve hesap yönetimi için 42 ek test durumu kapsandı; iki eski hesap silme testi yetkilendirilmiş yardımcıya taşındı. Kanıt: `artifacts/analysis/master-20261026-145710-633/master.trx`. Test motoru temizliği başarılı.
