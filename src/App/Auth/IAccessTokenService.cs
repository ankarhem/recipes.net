namespace App.Auth;

public interface IAccessTokenService
{
    AccessToken Generate(Guid userId, string email);
}
