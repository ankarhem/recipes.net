using Domain;
using Domain.Identity;
using Temporalio.Activities;

namespace App.Identity;

public sealed class TokenCleanupActivities(IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
{
    [Activity]
    public async Task DeleteEmailVerificationTokenAsync(string plainToken)
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;
        var hash = TokenHash.FromPlain(plainToken);
        var user = await users.GetByEmailVerificationTokenHashAsync(hash, ct);
        if (user is null)
        {
            return;
        }

        if (user.RemoveEmailVerificationToken(hash, clock))
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    [Activity]
    public async Task DeletePasswordResetTokenAsync(string plainToken)
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;
        var hash = TokenHash.FromPlain(plainToken);
        var user = await users.GetByPasswordResetTokenHashAsync(hash, ct);
        if (user is null)
        {
            return;
        }

        if (user.RemovePasswordResetToken(hash, clock))
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
