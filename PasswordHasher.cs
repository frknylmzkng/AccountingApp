using System.Globalization;
using System.Security.Cryptography;

namespace WinFormsApp2
{
    internal static class PasswordHasher
    {
        public const int MaxPasswordLength = 1024;
        public const int MaxEncodedLength = 256;
        private const int Iterations = 600_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const string Algorithm = "pbkdf2-sha256";

        public static string Hash(string password)
        {
            ArgumentNullException.ThrowIfNull(password);
            if (password.Length > MaxPasswordLength)
                throw new ArgumentException("Şifre en fazla 1024 karakter olabilir.", nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            try
            {
                return $"${Algorithm}$v=1$i={Iterations.ToString(CultureInfo.InvariantCulture)}$" +
                    Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
            }
            finally { CryptographicOperations.ZeroMemory(hash); }
        }

        public static bool Verify(string? password, string? encodedHash)
        {
            if (password == null || password.Length > MaxPasswordLength ||
                string.IsNullOrEmpty(encodedHash) || encodedHash.Length > MaxEncodedLength)
                return false;

            string[] parts = encodedHash.Split('$');
            if (parts.Length != 6 || parts[0] != "" || parts[1] != Algorithm || parts[2] != "v=1" ||
                !parts[3].StartsWith("i=", StringComparison.Ordinal) ||
                !int.TryParse(parts[3].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) ||
                iterations < Iterations || iterations > 2_000_000)
                return false;

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[4]);
                expected = Convert.FromBase64String(parts[5]);
            }
            catch (FormatException) { return false; }
            if (salt.Length != SaltSize || expected.Length != HashSize)
                return false;

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
            try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
            finally { CryptographicOperations.ZeroMemory(actual); }
        }
    }
}
