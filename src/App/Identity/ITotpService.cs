namespace App.Identity;

public interface ITotpService
{
    byte[] GenerateSecret();
    string EncodeBase32(byte[] secret);
    string BuildOtpAuthUri(byte[] secret, string accountLabel, string issuer);
    TotpVerificationResult Verify(byte[] secret, string code);
}

public abstract record TotpVerificationResult
{
    public sealed record Match(long Step) : TotpVerificationResult;
    public sealed record NoMatch : TotpVerificationResult;
}
