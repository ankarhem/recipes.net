using Domain.Identity;

namespace App.Identity;

public interface ITwoFactorService
{
    Task<TwoFactorResult> SetupAsync(UserId userId);
    Task<TwoFactorResult> ConfirmAsync(UserId userId, string code);
    Task<TwoFactorResult> DisableAsync(UserId userId, string code);
    Task<TwoFactorResult> RegenerateRecoveryCodesAsync(UserId userId, string code);
}
