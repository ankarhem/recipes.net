using App.Identity;
using App.Identity.Ports;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Infrastructure.Identity;

public sealed class OtpNetTotpService(IOptions<TotpOptions> options) : ITotpService
{
    private readonly TotpOptions _options = options.Value;

    public byte[] GenerateSecret() => KeyGeneration.GenerateRandomKey();

    public string EncodeBase32(byte[] secret) => Base32Encoding.ToString(secret);

    public string BuildOtpAuthUri(byte[] secret, string accountLabel, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountLabel);
        var base32Secret = EncodeBase32(secret);

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={base32Secret}&issuer={encodedIssuer}&algorithm=SHA1&digits={_options.DigitCount}&period={_options.StepSeconds}";
    }

    public TotpVerificationResult Verify(byte[] secret, string code)
    {
        var totp = new Totp(
            secret,
            step: _options.StepSeconds,
            mode: OtpHashMode.Sha1,
            totpSize: _options.DigitCount
        );
        var ok = totp.VerifyTotp(
            code,
            out var matchedStep,
            new VerificationWindow(previous: _options.DriftWindow, future: _options.DriftWindow)
        );

        return ok
            ? new TotpVerificationResult.Match(matchedStep)
            : new TotpVerificationResult.NoMatch();
    }
}
