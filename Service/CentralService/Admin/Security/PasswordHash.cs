using System.Security.Cryptography;
using System.Text;

namespace CentralService.Admin.Security;

public static class PasswordHash
{
    private const string Algorithm = "pbkdf2_sha256";
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int DefaultIterations = 100_000;

    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("密码不能为空", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Hash(password, salt, DefaultIterations);

        return $"{Algorithm}${DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4)
        {
            return false;
        }

        var algorithm = parts[0];
        if (!string.Equals(algorithm, Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Hash(password, salt, iterations);
        return FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Hash(string password, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeySizeBytes);
    }

    private static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}

