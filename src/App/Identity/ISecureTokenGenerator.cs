namespace App.Identity;

public interface ISecureTokenGenerator
{
    (string PlainToken, string HashedToken) Generate();
}
