using App.Identity;
using App.Identity.Ports;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Identity;

public sealed class DataProtectionTotpSecretProtector : ITotpSecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionTotpSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Identity.Totp.v1");
    }

    public string Protect(byte[] plaintextSecret) =>
        _protector.Protect(Convert.ToBase64String(plaintextSecret));

    public byte[] Unprotect(string protectedSecret) =>
        Convert.FromBase64String(_protector.Unprotect(protectedSecret));
}
