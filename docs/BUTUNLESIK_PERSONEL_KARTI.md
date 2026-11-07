# Bütünleşik personel kartı

31 Ekim 2026 · Aşama 2 kaynak dilimi · **1.3.4-dev**, mevcut şema **115**, API sözleşmesi **1.4** (`/api/v1`).

Kullanıcı kurulu 1.3.3-dev sürümünün düzgün çalıştığını bildirdi. Bu çalışmada denemeler atlandı; canlı API, veritabanı ve kısayol değiştirilmedi.

## Açma ve görünüm

**İşlemler → Personel kartı ara…** menüsünden TC/ad/soyad ile arayın. Etkin ve arşivli Genel/Joker kartları aynı sayfalı listede bulunur. Bir satırı seçip **Seçili kartı aç…** kullanın. Genel Personel, Joker Personel ve Kişi Arama listelerinin sağ tık menüsünde de **Seçili personel kartını aç…** bulunur. Arşiv/geçmiş ekranında aynı kartı açan düğme vardır. Sağ tık işlemi vurgulanmış seçili satırı kullanır.

Kart kalıcı PersonId ile açılır. TC, grup ve personel kimliği bu ekranda değiştirilemez. Üstte ad/soyad, kart durumu, TC, **kartın güncel bakiyesi**, güncel çalışma döneminin tarihleri ve son kart/özet okuma zamanı görünür. Bilinmeyen eski dönem tarihleri bilinmiyor olarak kalır.

| Bölüm | İçerik ve kapsam |
|---|---|
| Çalışma dönemleri | Bütün dönemler; tarih, kapalı/açık ve eski aktarım bilgisi |
| Proje ve ekip atamaları | Tarihli proje/ekip ilişkileri ve iptaller; seçili dönem veya tümü |
| İzinler | Saklanan izin satırları; seçili dönem veya tümü |
| İzin değişiklikleri | Önceki/yeni tarihler ve kaydeden; seçili dönem veya tümü |
| Hakediş belgeleri | Personelle ilişkili hakedişler ve iptalleri; seçili dönem veya tümü |
| Ödeme ve defter | Ödeme, avans, düzeltme/dağıtım, kapanış ve iptal hareketleri; seçili dönem veya tümü |
| Arşiv olayları | Personelin tüm arşiv/geri alma olayları; eski olaylara dönem tahmin edilmez |
| İşlem geçmişi | Yalnız yöneticiye açık, PersonId ile bağlı eski/yeni değer ve gerekçeler; tüm dönemler |

Geçmiş dönemi kutusu değişince kart/özet yeniden okunur, eski sekme verileri temizlenir ve açık geçmiş sekmesi yüklenir. Diğer sekmeler açıldıkça yüklenir. Hareket listeleri 25/50/100/200 satırlık SQL sayfalaması kullanır; arşiv olayları ve audit dönem filtresinden bağımsızdır. Çalışma dönemleri küçük bir kişi kataloğu olarak topluca görünür. Proje/ekip ve kişi adları mevcut kayıtlardan gösterilir; tarihsel ad fotoğrafı üretilmez.

Kart başlığı, dönemler ve finans özeti tek okuma transaction'ında alınır. Sonraki sekme okumaları ayrı transaction'lardır; bütün sekmelerin aynı anda alınmış değişmez bir rapor olduğu iddia edilmez. Yenile düğmesi kart/özeti tekrar okur ve sekme sayfalarını sıfırlar. Sekmenin yüklenme zamanı sekme ipucunda bulunur.

## Bilgi ve izin düzenleme

**Ad / IBAN kaydet** yalnız ad, soyad ve IBAN'ı günceller. Bu işleme bakiye alanı gönderilmez; SQL güncellemesi PARA'ya dokunmaz ve sıfır tutarlı defter satırı oluşturmaz. PersonId, grup ve özgün kart sürümüyle doğrulanır. Başka kullanıcı arada bilgi, bakiye veya dönem değiştirdiyse eski kartla yazılmaz; güncel kart yeniden yüklenmelidir. Eski/yeni alan değerleri ve gerekçe mevcut audit sistemiyle aynı transaction içinde kaydedilir.

**Güncel dönem iznini düzenle…** yalnız etkin Genel personelde kullanılabilir. Seçili geçmiş filtresi eski dönem olsa da bu düğme açıkça **kartın güncel çalışma dönemini** düzenler. Kart ve izin sürümleri korunur; izin henüz yoksa yokluk bilgisi de snapshot'tır. Mevcut izin sayısı birden fazlaysa tek düğmeyle toplu ezme yapılmaz; düğme kapalı kalır. Yeni tarihlerin dönem dışına taşması veya başka kullanıcının izni değiştirmesi mevcut servis kontrolüne takılır. Önceki tarihler izin geçmişinde korunur.

**Çalışma dönemlerini yönet…** mevcut sürümlü dönem yönetimini açar. **Arşivle… / Arşivlemeyi geri al…** mevcut gerekçeli/sürümlü servisleri kullanır. Aynı dönemde arşivlemeyi geri alma ve yeni EmploymentId ile yeniden işe giriş ayrımı korunur; bakiye silinmez. Yeni işe girişte sıfır eski bakiye koşulu devam eder. Ayrıntı: [Çalışma dönemleri](CALISMA_DONEMLERI.md).

Kaydedilmemiş personel alanları için dönem değiştirme, yenileme, alt işlem açma ve pencereyi kapatma sırasında mevcut uyarı kullanılır. Alt işlem pencereleri kapatılınca kart yenilenir. Liste üzerinden açılan kart kapanınca kaynak liste de yenilenir. Ödeme, avans, hakediş onayı/iptali ve proje ataması yazma işlemleri mevcut özel ekranlarında sürer; kart bu geçmişleri tek yerde okur ve ikinci bir para iş akışı eklemez.

## Finansın anlamı

Seçili kapsam özeti **iptal edilmemiş hakediş**, **iptal edilmemiş avans**, **iptal edilmemiş bakiye ödemesi** ve **eski/sınıflandırılmamış ödeme** tutarlarını ayrı gösterir. Hakediş, ödeme ve izin satırları birleştirilerek toplanmaz; çoklu izinler maliyet/ödemeyi şişirmez. Projenin kişiye bağlı olmayan diğer giderleri personele mal edilmez. Avans ve ödeme hakedişe ikinci kez eklenmez.

Üstteki bakiye her zaman güncel kart bakiyesidir; eski dönem filtresine göre geçmiş bakiye gibi sunulmaz. Defterdeki **Dönem içi net hareket TL**, o dönemin bilinen hareketlerinin kümülatif netidir ve sayfa kesilmeden önce SQL'de hesaplanır. Aktarılan eski dönemlerde eksik açılış hareketi bulunabileceği için bu alanın güncel bakiyeyi kanıtladığı iddia edilmez.

PersonId belirlenemeyen eski ödeme satırları TC veya zamana bakılarak karta bağlanmaz; genel defterde kalır. Kartın audit'i de yalnız PersonId ile bağlanabilen kayıtları gösterir; eski genel işlem loglarına hedef uydurulmaz. Ödeme tutarları uygulama kaydıdır; banka gerçekleşmesi doğrulaması eklenmedi.

## Yetki ve dışa aktarma

Tam kart mevcut **genel merkez rolleri** içindir. Proje kullanıcısı, doğrudan kart API'sine personel/dönem kimliği yazsa da GlobalRead kontrolünden geçemez; kendi Projelerim görünümünü kullanmaya devam eder. Kart, proje kullanıcısına başka proje, TC, IBAN veya genel bakiye açan bir geçit değildir. [Proje yetkileri](PROJE_SORUMLUSU_VE_YETKILER.md).

Bilgi/izin/arşiv/dönem işlemleri mevcut Edit; işlem geçmişi ayrıca Admin yetkisi gerektirir. Audit sekmesini gizlemek tek kontrol değildir: API ve servis sayfalama isteğinde yönetici yetkisi tekrar aranır. Ödeme ve dışa aktarma izinleri personel düzenleme iznine dönüşmez. İstemcinin gönderdiği dönem kimliği aynı PersonId ve gruba ait olmalıdır; başka kişiye ait dönem isteği reddedilir.

**Açık sayfayı CSV…** yalnız mevcut ayrı kişisel veri dışa aktarma izniyle çalışır; yüklenmiş görünür sütun/satırları kaydeder. Geçmiş sekmelerinde diğer sayfalar dahil değildir; dönem sekmesi tüm dönem listesidir. Arşiv/audit rapor kapsamı daima tüm dönemler olarak yazılır. Pano ve dosya yolları mevcut canlı yetki kontrolünü kullanır. Audit JSON hücresini çift tıklamak yönetici için biçimlendirilmiş salt okunur ayrıntı açar; bu ayrıntı kutusunda yerel kopyalama kısayolları kapalıdır.

## Teslim ve geçiş

Bu özellik mevcut tablolara dayanır. **Yeni şema betiği veya 116 yükseltmesi yok; gerekli şema 115'tir.** Yeni kaynak/paket **1.3.4-dev / API 1.4**. Kurulu **1.3.3-dev / API 1.3 / şema 115**, merkezi API görevi, bağlantı profili, tercihler ve masaüstü kısayolu değiştirilmedi.

Yeni sürüme geçişte istemci ve API birlikte güncellenmeli; aktif kurulumun API beklentisi manifestteki **1.4** olmalıdır. Bu çalışma kurulum/yeniden başlatma veya canlı veri değişikliği yapmadı. Unit, SQL, API, UI, eşzamanlılık ve yetki denemeleri kullanıcı isteğiyle atlandı. Release derlemesi ve paket üretimi, SQL sorguları veya ekran davranışlarının çalıştırılarak doğrulandığı anlamına gelmez. GitHub'a push/Release yayımlanmadı.

Aşama 2'nin bütünleşik kart dilimi kaynakta hazırdır; tarihli grup geçişi, daha ayrıntılı hassas alan politikası ve proje bazlı ödeme/puantaj onayı açık kalır. Bu not tüm aşamanın tamamlandığını belirtmez.
