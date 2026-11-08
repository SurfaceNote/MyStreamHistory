using System.Security.Cryptography;

namespace MyStreamHistory.AdminService.Api;

public static class AdminPassword
{
    private const int Iterations = 600_000;
    public static string Hash(string password)
    {
        if (password.Length < 16 || password.Length > 1024) throw new ArgumentException("Use a password of 16–1024 characters.");
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool ValidHash(string value)
    {
        var parts = value.Split('$');
        try
        {
            return parts.Length == 4 && parts[0] == "pbkdf2-sha256"
                && int.TryParse(parts[1], out var iterations) && iterations is >= Iterations and <= 2_000_000
                && Convert.FromBase64String(parts[2]).Length == 16 && Convert.FromBase64String(parts[3]).Length == 32;
        }
        catch (FormatException) { return false; }
    }

    public static bool Verify(string password, string encoded)
    {
        if (password.Length > 1024 || !ValidHash(encoded)) return false;
        var parts = encoded.Split('$');
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[2]), int.Parse(parts[1]), HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(parts[3]));
    }

    public static void RunGenerator()
    {
        Console.Write("Password (16+ characters, hidden): ");
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; }
            else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
        }
        Console.WriteLine();
        Console.WriteLine(Hash(password.ToString()));
    }
}
