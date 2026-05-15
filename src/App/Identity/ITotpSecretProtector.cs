namespace App.Identity;

public interface ITotpSecretProtector
{
    string Protect(byte[] plaintextSecret);
    byte[] Unprotect(string protectedSecret);
}
