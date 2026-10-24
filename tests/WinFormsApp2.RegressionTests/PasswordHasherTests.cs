using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture]
public class PasswordHasherTests
{
    // Independently generated with Python hashlib.pbkdf2_hmac, salt=bytes(range(16)).
    private const string KnownHash = "$pbkdf2-sha256$v=1$i=600000$AAECAwQFBgcICQoLDA0ODw==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=";

    [Test]
    public void VerifiesAnIndependentPbkdf2Sha256Vector()
    {
        Assert.That(PasswordHasher.Verify("known test password", KnownHash), Is.True);
        Assert.That(PasswordHasher.Verify("wrong password", KnownHash), Is.False);
    }

    [Test]
    public void EqualPasswordsUseDifferentRandomSalts()
    {
        const string password = "Unique test password 123!";
        string first = PasswordHasher.Hash(password);
        string second = PasswordHasher.Hash(password);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first.Split('$')[4], Is.Not.EqualTo(second.Split('$')[4]));
            Assert.That(first, Does.Not.Contain(password));
            Assert.That(first.Length, Is.LessThanOrEqualTo(PasswordHasher.MaxEncodedLength));
            Assert.That(first, Does.StartWith("$pbkdf2-sha256$v=1$i=600000$"));
            Assert.That(PasswordHasher.Verify(password, first), Is.True);
            Assert.That(PasswordHasher.Verify(password, second), Is.True);
        });
    }

    [TestCase("Türkçe Şifre 🔐")]
    [TestCase("  password with spaces  ")]
    [TestCase("пароль密碼")]
    [TestCase("password\0with-null")]
    [TestCase("")]
    public void HashPreservesEveryPasswordCharacter(string password)
    {
        Assert.That(PasswordHasher.Verify(password, PasswordHasher.Hash(password)), Is.True);
    }

    [TestCase("Password 123", "password 123")]
    [TestCase("  secret  ", "secret")]
    [TestCase("é", "e\u0301")]
    public void ComparisonDoesNotTrimChangeCaseOrNormalizeUnicode(string password, string other)
    {
        Assert.That(PasswordHasher.Verify(other, PasswordHasher.Hash(password)), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("plaintext-password")]
    [TestCase("$pbkdf2-sha256$v=1$i=600000$bad$bad")]
    [TestCase("$pbkdf2-sha256$v=2$i=600000$AAECAwQFBgcICQoLDA0ODw==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=")]
    [TestCase("$pbkdf2-sha1$v=1$i=600000$AAECAwQFBgcICQoLDA0ODw==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=")]
    [TestCase("$pbkdf2-sha256$v=1$i=1$AAECAwQFBgcICQoLDA0ODw==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=")]
    [TestCase("$pbkdf2-sha256$v=1$i=2147483647$AAECAwQFBgcICQoLDA0ODw==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=")]
    [TestCase("$pbkdf2-sha256$v=1$i=-600000$AAECAwQFBgcICQoLDA0ODw==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=")]
    [TestCase("$pbkdf2-sha256$v=1$i=600000$AA==$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=")]
    [TestCase("$pbkdf2-sha256$v=1$i=600000$AAECAwQFBgcICQoLDA0ODw==$AA==")]
    [TestCase("$pbkdf2-sha256$v=1$i=600000$!!!!!!!!!!!!!!!!!!!!!!!!$2S3oep6NgFXoYFMdZRiawxaY88Cpw8Vd6S5VAD0R42Y=")]
    public void MalformedUnsupportedOrExcessiveHashesAreRejected(string? hash)
    {
        Assert.That(PasswordHasher.Verify("known test password", hash), Is.False);
    }

    [Test]
    public void TamperingWithSaltOrDigestInvalidatesThePassword()
    {
        Assert.That(PasswordHasher.Verify("known test password", KnownHash.Replace("AAECAw", "AQECAw")), Is.False);
        Assert.That(PasswordHasher.Verify("known test password", KnownHash.Replace("2S3oep", "3S3oep")), Is.False);
    }

    [Test]
    public void NullAndOverlongInputsAreHandledWithoutDerivingAKey()
    {
        Assert.That(PasswordHasher.Verify(null, KnownHash), Is.False);
        Assert.That(PasswordHasher.Verify(new string('x', 1025), KnownHash), Is.False);
        Assert.That(PasswordHasher.Verify("secret", new string('x', 257)), Is.False);
        Assert.Throws<ArgumentException>(() => PasswordHasher.Hash(new string('x', 1025)));
    }

    [Test]
    public void MaximumSupportedPasswordRoundTrips()
    {
        string password = new string('x', PasswordHasher.MaxPasswordLength);
        Assert.That(PasswordHasher.Verify(password, PasswordHasher.Hash(password)), Is.True);
    }
}
