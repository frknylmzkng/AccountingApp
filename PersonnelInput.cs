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

            if (firstName.Trim().Length > 50 || lastName.Trim().Length > 50 || firstName.Any(char.IsControl) || lastName.Any(char.IsControl))
            {
                error = "Ad ve soyadı 1–50 karakter olarak, kontrol karakteri olmadan girin.";
                return false;
            }
            if (!IbanInput.TryNormalize(ibanText, out string iban, out error)) return false;

            if (!MoneyInput.TryParse(amountText, out decimal amount))
            {
                error = MoneyInput.ValidationMessage;
                return false;
            }

            person = new PersonnelDetails(tcNumber, firstName.Trim(), lastName.Trim(), iban, amount);
            return true;
        }

        internal static PersonnelDetails Normalize(PersonnelDetails person)
        {
            ArgumentNullException.ThrowIfNull(person);
            if (!TryCreate(person.Tc.ToString(CultureInfo.InvariantCulture), person.FirstName, person.LastName,
                person.Iban, MoneyInput.Format(person.Amount), out var normalized, out string error))
                throw new ArgumentException(error, nameof(person));
            return normalized;
        }
    }
}
