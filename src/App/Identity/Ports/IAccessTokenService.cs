namespace App.Identity.Ports;

public interface IAccessTokenService
{
    AccessToken Generate(Guid userId, string email);
}
