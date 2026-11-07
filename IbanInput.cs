namespace WinFormsApp2;

internal static class IbanInput
{
    internal const string Help = "Türkiye IBAN'ı (TR, 26 karakter). Boşluklu ve küçük harfli giriş düzenlenir. Biçim kontrolü hesap sahibini veya hesabın açık olduğunu doğrulamaz.";

    internal static bool TryNormalize(string? text, out string iban, out string error)
    {
        iban = ""; error = "";
        if (string.IsNullOrWhiteSpace(text)) { error = "IBAN girin."; return false; }
        // Accept presentation spaces, not punctuation, control characters or
        // Unicode lookalikes that could silently become a different identifier.
        if (text.Any(c => c != ' ' && c != '\u00A0' && !(c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z')))
        { error = "IBAN yalnızca Latin harfleri, rakamlar ve ayırıcı boşluklar içerebilir."; return false; }
        string normalized = text.Replace(" ", "").Replace("\u00A0", "").ToUpperInvariant();
        if (!normalized.StartsWith("TR", StringComparison.Ordinal))
        { error = "Şu anda yalnızca Türkiye (TR) IBAN'ları destekleniyor."; return false; }
        if (normalized.Length != 26)
        { error = "Türkiye IBAN'ı boşluklar hariç 26 karakter olmalıdır."; return false; }
        if (!normalized.AsSpan(2, 7).ToArray().All(c => c is >= '0' and <= '9') || normalized[9] != '0')
        { error = "IBAN kontrol basamakları, ödeme hizmeti sağlayıcısı kodu veya rezerv alanı geçersiz."; return false; }
        int checkDigits = (normalized[2] - '0') * 10 + normalized[3] - '0';
        // ISO 7064 MOD 97-10, with the first four characters moved to the end.
        // The bounded remainder avoids numeric overflow even with alphabetic accounts.
        int remainder = 0;
        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[(i + 4) % normalized.Length];
            remainder = c is >= 'A' and <= 'Z'
                ? (remainder * 100 + c - 'A' + 10) % 97
                : (remainder * 10 + c - '0') % 97;
        }
        if (checkDigits is < 2 or > 98 || remainder != 1)
        { error = "IBAN kontrol basamakları geçersiz. Bankanızın verdiği IBAN'ı kontrol edin."; return false; }
        iban = normalized; return true;
    }
}
