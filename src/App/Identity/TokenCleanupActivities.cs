using Domain;
using Domain.Identity;
using Temporalio.Activities;

namespace App.Identity;

public sealed class TokenCleanupActivities(IUserRepository users, IClock clock)
{
    [Activity]
    public async Task DeleteEmailVerificationTokenAsync(string plainToken)
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;
        var hash = TokenHash.From(TokenHasher.Hash(plainToken));
        var user = await users.GetByEmailVerificationTokenHashAsync(hash, ct);
        if (user is null)
        {
            return;
        }

        if (user.RemoveEmailVerificationToken(hash, clock))
        {
            await users.SaveChangesAsync(ct);
        }
    }

    [Activity]
    public async Task DeletePasswordResetTokenAsync(string plainToken)
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;
        var hash = TokenHash.From(TokenHasher.Hash(plainToken));
        var user = await users.GetByPasswordResetTokenHashAsync(hash, ct);
        if (user is null)
        {
            return;
        }

        if (user.RemovePasswordResetToken(hash, clock))
        {
            await users.SaveChangesAsync(ct);
        }
    }
}
