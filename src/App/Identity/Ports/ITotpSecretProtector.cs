namespace App.Identity.Ports;

public interface ITotpSecretProtector
{
    string Protect(byte[] plaintextSecret);
    byte[] Unprotect(string protectedSecret);
}
