namespace App.Identity;

public sealed record TwoFactorSetupInfo(
    string Base32Secret,
    string OtpAuthUri,
    string QrCodePngBase64
);
