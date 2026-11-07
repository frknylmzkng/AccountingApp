using WinFormsApp2;

// Explicit operator-only tool; never registered as a startup task. No passwords
// in process arguments, environment variables, logs or redirected stdin.
Console.OutputEncoding=System.Text.Encoding.UTF8;
string Secret(string prompt)
{
    if(Console.IsInputRedirected)throw new InvalidOperationException("Parola yönlendirilmiş girdiden alınamaz.");
    Console.Write(prompt);var value=new System.Text.StringBuilder();for(;;){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace){if(value.Length>0)value.Length--;continue;}if(!char.IsControl(key.KeyChar)&&value.Length<256)value.Append(key.KeyChar);}Console.WriteLine();return value.ToString();
}
try
{
    string command=args.Length>0?args[0]:"help";
    switch(command)
    {
        case "export-keys" when args.Length==2:
            string password=Secret("Yeni anahtar dosyası parolası (16–256 karakter): ");if(password!=Secret("Parolayı tekrarlayın: "))throw new ArgumentException("Parolalar eşleşmiyor.");BackupVault.ExportKeys(args[1],password);Console.WriteLine("Parolalı anahtar kümesi dışa aktarıldı. Dosyayı ve parolayı yedekten ayrı yerde saklayın.");break;
        case "import-keys" when args.Length==2:BackupVault.ImportKeys(args[1],Secret("Anahtar dosyası parolası: "));Console.WriteLine("Anahtarlar birleştirildi; mevcut anahtarlar korundu.");break;
        case "rotate-key" when args.Length==1:Console.WriteLine("Yeni anahtar: "+BackupVault.RotateKey()+". Şimdi yeni anahtar kümesini dışa aktarın.");break;
        case "unseal" when args.Length==3:
            string target=Path.GetFullPath(args[2]);if(Directory.Exists(target)||File.Exists(target))throw new ArgumentException("Yeni bir kurtarma klasörü belirtin; mevcut klasör kullanılmaz.");BackupVault.RejectLinks(target);
            using(var opened=await BackupVault.OpenAsync(args[1],null,CancellationToken.None))
            {
                BackupVault.PrivateDirectory(target);string name=Path.GetFileName(opened.BackupPath!);File.Copy(opened.BackupPath!,Path.Combine(target,name),false);File.Copy(opened.BackupPath!+BackupScope.ManifestSuffix,Path.Combine(target,name)+BackupScope.ManifestSuffix,false);
                Console.WriteLine("Kimliği ve bütünlüğü doğrulanmış .bak ve manifest kurtarma klasörüne çıkarıldı. Veritabanı geri yüklenmedi. Açık dosyalar kişisel veri içerir; yalnız kontrollü bakımda kullanın ve ardından kaldırın.");
            }break;
        case "mirror" when args.Length==3:var policy=BackupVault.Policy(args[1]);BackupVault.SavePolicy(args[1],policy.Keep,args[2]);Console.WriteLine("Sunucu aynalama hedefi kaydedildi; dosya kopyalanmadı.");break;
        case "clean-stale" when args.Length==1:Console.WriteLine("Temizlenen eski ve boşta geçici klasör: "+BackupVault.CleanStaleStages());break;
        default:Console.WriteLine("AccountingApp.BackupRecovery\nexport-keys <yeni.abkey>\nimport-keys <yedek.abkey>\nrotate-key\nunseal <yedek.bak.abk> <yeni-kurtarma-klasörü>\nmirror <32-karakter-kapsam> <harici-klasör>\nclean-stale\nAPI'yi çalıştıran Windows hesabıyla sunucuda kullanın. Parola argüman olarak verilmez. Bu araç SQL geri yükleme veya prova yapmaz.");break;
    }
}
catch(Exception ex){AppLog.Failure(ex,"BackupVault.Operator");Console.Error.WriteLine("İşlem tamamlanamadı. Biçimi, parolayı, anahtar erişimini ve dosya/klasör izinlerini kontrol edin. Tür: "+ex.GetType().Name);Environment.ExitCode=1;}
