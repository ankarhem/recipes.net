namespace App.Identity;

public abstract record TwoFactorResult
{
    public sealed record SetupPending(
        string Base32Secret,
        string OtpAuthUri,
        string QrCodePngBase64
    ) : TwoFactorResult;

    public sealed record Enabled(IReadOnlyList<string> RecoveryCodes) : TwoFactorResult;
    public sealed record Disabled : TwoFactorResult;
    public sealed record CodesRegenerated(IReadOnlyList<string> RecoveryCodes) : TwoFactorResult;
    public sealed record InvalidCode : TwoFactorResult;
    public sealed record AlreadyEnabled : TwoFactorResult;
    public sealed record NotEnabled : TwoFactorResult;
    public sealed record UserNotFound : TwoFactorResult;
}
