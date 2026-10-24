using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace WinFormsApp2
{
    internal static class PersonnelInput
    {
        public static bool TryCreate(string tcText, string firstName, string lastName,
            string ibanText, string amountText, [NotNullWhen(true)] out PersonnelDetails? person,
            out string error)
        {
            person = null;
            error = "";
            if (string.IsNullOrWhiteSpace(tcText) || string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(ibanText) ||
                string.IsNullOrWhiteSpace(amountText))
            {
                error = "Eksik veri!";
                return false;
            }

            string tc = tcText.Trim();
            if (tc.Length != 11 || !tc.All(c => c >= '0' && c <= '9') ||
                !long.TryParse(tc, NumberStyles.None, CultureInfo.InvariantCulture, out long tcNumber))
            {
                error = "TC 11 haneli ve sadece rakamlardan oluşmalıdır!";
                return false;
            }

            string iban = ibanText.ToUpperInvariant().Replace(" ", "");
            if (iban.Length < 16 || iban.Length > 34)
            {
                error = "Geçersiz IBAN!";
                return false;
            }

            if (!MoneyInput.TryParse(amountText, out decimal amount))
            {
                error = MoneyInput.ValidationMessage;
                return false;
            }

            person = new PersonnelDetails(tcNumber, firstName, lastName, iban, amount);
            return true;
        }
    }
}
