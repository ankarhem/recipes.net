namespace App.Auth;

public interface IRefreshTokenGenerator
{
    (string PlainToken, string HashedToken) Generate();
}
