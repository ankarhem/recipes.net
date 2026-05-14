namespace App.Identity;

public interface IAccessTokenService
{
    AccessToken Generate(Guid userId, string email);
}
