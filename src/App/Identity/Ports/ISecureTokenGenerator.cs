namespace App.Identity.Ports;

public interface ISecureTokenGenerator
{
    (string PlainToken, string HashedToken) Generate();
}
