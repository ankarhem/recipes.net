using App.Identity;

namespace Infrastructure.Identity;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private readonly Lazy<string> _dummyHash = new(() =>
        BCrypt.Net.BCrypt.HashPassword("timing-resistant-dummy", workFactor: 12)
    );

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hashedPassword)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public string DummyHash => _dummyHash.Value;
}
