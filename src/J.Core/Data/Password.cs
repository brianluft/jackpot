using System.Security.Cryptography;

namespace J.Core.Data;

public sealed class Password
{
    private static readonly char[] _allowedChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()-_=+[]{}<>?".ToCharArray();

    public string Value { get; }

    public Password(string value)
    {
        if (value.Length == 0)
            throw new ArgumentException("Password must not be empty.");
        Value = value;
    }

    public static Password New()
    {
        const int LENGTH = 32;

        using var rng = RandomNumberGenerator.Create();
        var passwordChars = new char[LENGTH];
        var randomBytes = new byte[LENGTH];

        // Fill the byte array with secure random values
        rng.GetBytes(randomBytes);

        for (int i = 0; i < passwordChars.Length; i++)
        {
            // Select a character from the allowedChars array based on the random byte
            passwordChars[i] = _allowedChars[randomBytes[i] % _allowedChars.Length];
        }

        return new(new string(passwordChars));
    }
}
