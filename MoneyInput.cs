using System.Globalization;
using System.Text.RegularExpressions;

namespace WinFormsApp2
{
    internal static class MoneyInput
    {
        private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("tr-TR");
        private static readonly Regex AmountPattern = new Regex(
            @"^[+-]?(?:[0-9]+(?:[.,][0-9]{1,2})?|[0-9]{1,3}(?:\.[0-9]{3})+,[0-9]{1,2}|[0-9]{1,3}(?:,[0-9]{3})+\.[0-9]{1,2})$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

        public const string ValidationMessage =
            "Geçersiz tutar! Örnek: 1500,50 veya 1500.50. " +
            "En fazla iki kuruş basamağı yazın. Binlik ayırıcı kullanıyorsanız kuruşu da yazın: 1.500,50.";

        public static bool TryParse(string? text, out decimal amount)
        {
            amount = 0;
            string input = text?.Trim() ?? "";
            if (input.Length == 0 || input.Length > 128 || !AmountPattern.IsMatch(input))
                return false;

            // Both separators are accepted only with explicitly grouped thousands.
            int decimalPosition = Math.Max(input.LastIndexOf(','), input.LastIndexOf('.'));
            if (decimalPosition >= 0)
            {
                string integerPart = input[..decimalPosition].Replace(",", "").Replace(".", "");
                input = integerPart + "." + input[(decimalPosition + 1)..];
            }

            if (!decimal.TryParse(input,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out amount))
                return false;

            // Decimal.TryParse can silently round a fractional amount near decimal.MaxValue.
            // Compare canonical text to ensure that even extreme inputs retain every cent.
            string[] parts = input.TrimStart('+', '-').Split('.');
            string whole = parts[0].TrimStart('0');
            if (whole.Length == 0) whole = "0";
            string fraction = parts.Length == 2 ? parts[1].PadRight(2, '0') : "00";
            string sign = input.StartsWith('-') && amount != 0 ? "-" : "";
            if (amount.ToString("F2", CultureInfo.InvariantCulture) != sign + whole + "." + fraction)
            {
                amount = 0;
                return false;
            }
            return true;
        }

        // Keep any existing extra precision visible rather than silently rounding it.
        public static string Format(decimal amount) =>
            amount.ToString("0.00##########################", DisplayCulture);
    }
}
