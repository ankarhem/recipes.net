namespace App.Auth;

public static class TokenHasher
{
    public static string Hash(string plainToken)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(plainToken)
        );
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
