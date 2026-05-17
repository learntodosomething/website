using System;
using System.Security.Cryptography;
using System.Text;

public class PasswordManager
{
    private static Random random = new Random();

    // Ezt a metódust hozzáadhatja az osztályhoz, hogy hashelje a stringet
    public static string Hash(string value)
    {
        SHA256 sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
        StringBuilder builder = new();
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }
        return builder.ToString();
    }

    // Ezt a metódust hozzáadhatja az osztályhoz, hogy egyedi sót generáljon
    public static string GenerateSalt()
    {
        StringBuilder builder = new();
        for (int q = 0; q < 32; q++)
        {
            builder.Append(random.Next(16).ToString("x"));
        }

        return builder.ToString();
    }

    // Ezt a metódust hozzáadhatja az osztályhoz, hogy generálja a jelszó hash-t a sóval
    public static string GeneratePasswordHash(string password, out string salt)
    {
        salt = GenerateSalt();
        return Hash(password + salt);
    }

    // Ezt a metódust hozzáadhatja az osztályhoz, hogy ellenőrizze a jelszó helyességét
    public static bool Verify(string candidatePassword, string passwordSalt, string passwordHash)
    {
        var candidateHash = Hash(candidatePassword + passwordSalt);
        return candidateHash == passwordHash;
    }

    internal static string GeneratePasswordHash(string salt)
    {
        throw new NotImplementedException();
    }
}
