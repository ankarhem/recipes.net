using System.Security.Cryptography;
using App.Auth;

namespace Infrastructure.Auth;

public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public (string PlainToken, string HashedToken) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plainToken = Convert.ToBase64String(bytes);
        var hashedToken = TokenHasher.Hash(plainToken);

        return (plainToken, hashedToken);
    }
}
