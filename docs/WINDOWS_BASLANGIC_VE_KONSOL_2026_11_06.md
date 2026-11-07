# Windows başlangıcı, CMD ve siyah masaüstü incelemesi

6 Kasım 2026. Kullanıcı çalışırken komut penceresi açıldığını, Windows başlangıcında siyah masaüstüyle karşılaşıp Explorer'ı elle başlattığını bildirdi. Verilen pencere başlığı Windows System32 CMD olarak tarif edildi.

## Gözlenen durum

- İnceleme sırasında `C:\Windows\System32\cmd.exe` başlıklı görünür süreç, başka bir proje olan Instagram Hub'ın frontend başlatıcısıydı. Komut `cmd /k ... npm run dev` biçimindeydi; `/k` pencereyi açık tutar. Backend için de ayrı CMD süreci vardı. Bu bulgu yalnız inceleme anındaki pencereyi tanımlar; Windows açılışındaki pencerenin aynı süreç olduğu yeniden başlatılarak doğrulanmadı.
- Muhasebe API'si kullanıcı oturumunda yerel HTTPS üzerinden çalışıyordu. Zamanlanmış görev doğrudan gizli PowerShell başlatıyor, API ise konsol alt sistemli EXE olarak derleniyordu. Gizleme başlangıçta konsol oluşturulmasını tamamen önlemeyebilir.
- Winlogon Shell `explorer.exe`, Userinit standart `userinit.exe,` değerindeydi. İncelenen kullanıcı kabuk, CMD AutoRun ve Explorer yönlendirme anahtarlarında bunları değiştiren değer bulunmadı. İncelenen son yedi günlük en yeni 200 uygulama hata/hang kaydı içinde Explorer/Userinit/DWM eşleşmesi bulunmadı. Bu, Explorer'ın hiç hata vermediğini veya siyah masaüstünün nedeninin kanıtlandığını göstermez.
- `C:\Windows\system32.exe` adlı bir dosya bulunmadı; verilen başlık mevcut CMD penceresiyle eşleşti.

## Yapılan değişiklikler

1. **AccountingApp Local API** görev eylemi GUI Windows Script Host üzerinden `automation/Start-BackgroundScript.vbs` kullanacak şekilde güncellendi. PowerShell pencere oluşturulduktan sonra gizlemek yerine gizli başlatılır; görev görünürlüğü de Hidden olarak ayarlandı. Aynı kullanıcı, tetikleyici, mevcut API başlatma betiği, LocalDB profili ve yeniden başlatma ilkeleri korundu. Çalışan API durdurulmadı; yeni eylem sonraki görev çalıştırılmasında kullanılır.
2. Yeni API kaynak derlemesi `WinExe` yapıldı. 1.3.26-dev API yeni konsol penceresi ayırmaz; bu ikili değişikliği yeni paketin kurulmasından sonra geçerli olur.
3. **AccountingApp Desktop Recovery** adlı ayrı görev eklendi. Kullanıcı oturum açtıktan 30 saniye sonra, aynı oturumda **hiç explorer.exe yoksa** mevcut Windows Explorer'ı başlatır. Explorer zaten çalışıyorsa işlem yapmaz; Explorer'ı kapatmaz/yeniden başlatmaz. Winlogon/kabuk kayıtları değiştirilmedi. Explorer'ın çalışıp takılı kaldığı durumu onarmaz.

Görev kayıtları, önce/sonra XML ve tanı bilgileri `artifacts/analysis/startup-console-20261106/` altındadır. Kurtarma çalışırsa `desktop-recovery.jsonl` yazılır. Yeni kurtarma görevini kaldırmak geri alınabilir bir işlemdir; eski API görev tanımı `api-task-before.xml` olarak saklanmıştır. Diğer projenin dosyaları, süreçleri veya başlangıcı değiştirilmedi.

## Sınır

Derleme/bakım araçları çalışırken kısa süreli konsol penceresi yine görülebilir; bu değişiklik Codex araç konağını veya başka projelerin CMD başlatıcılarını değiştirmez. Bu teslimde oturum kapatma, yeniden başlatma veya yeni görevleri çalıştırma denemesi yapılmadı. Görev yapılandırması, host varlığı ve PowerShell sözdizimi kontrol edildi. Siyah masaüstünün kök nedeni kesinleştirilmedi ve Windows başlangıcının düzeldiği uygulamalı olarak doğrulanmadı; eklenen Explorer görevi yalnız koşullu kurtarmadır.
