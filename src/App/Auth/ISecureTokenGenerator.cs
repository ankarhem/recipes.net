namespace App.Auth;

public interface ISecureTokenGenerator
{
    (string PlainToken, string HashedToken) Generate();
}
