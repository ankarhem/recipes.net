using AwesomeAssertions;
using Xunit;
using DomainUser = Domain.Identity.User;

namespace App.UnitTests;

public class UserAggregateTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_NewUser_CreatesUnverifiedUserWithCurrentTimestamps()
    {
        var clock = new FakeClock(TestNow);
        var email = Domain.Identity.Email.Normalize(" Test@Example.COM ");
        var passwordHash = Domain.Identity.PasswordHash.From("hashed-password");

        var user = DomainUser.Register(email, passwordHash, clock);

        user.Id.Value.Should().NotBeEmpty();
        user.Email.Should().Be(email);
        user.Email.Value.Should().Be("test@example.com");
        user.PasswordHash.Should().Be(passwordHash);
        user.EmailVerified.Should().BeFalse();
        user.EmailVerifiedAt.Should().BeNull();
        user.CreatedAt.Should().Be(clock.UtcNow);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        user.EmailVerificationTokens.Should().BeEmpty();
        user.PasswordResetTokens.Should().BeEmpty();
    }

    [Fact]
    public void IssueEmailVerificationToken_AddsTokenAndUpdatesUpdatedAt()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var expiresAt = clock.UtcNow.AddHours(1);
        var hash = Domain.Identity.TokenHash.From("verification-hash");

        clock.Advance(TimeSpan.FromMinutes(5));
        var token = user.IssueEmailVerificationToken(hash, expiresAt, clock);

        user.EmailVerificationTokens.Should().ContainSingle().Which.Should().BeSameAs(token);
        token.UserId.Should().Be(user.Id);
        token.TokenHash.Should().Be(hash);
        token.ExpiresAt.Should().Be(expiresAt);
        token.CreatedAt.Should().Be(clock.UtcNow);
        token.ConsumedAt.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void IssueEmailVerificationToken_RemovesPreviousUnconsumedTokens()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var firstHash = Domain.Identity.TokenHash.From("first-verification-hash");
        var secondHash = Domain.Identity.TokenHash.From("second-verification-hash");

        user.IssueEmailVerificationToken(firstHash, clock.UtcNow.AddHours(1), clock);
        clock.Advance(TimeSpan.FromMinutes(1));
        var secondToken = user.IssueEmailVerificationToken(secondHash, clock.UtcNow.AddHours(1), clock);

        user.EmailVerificationTokens.Should().ContainSingle().Which.Should().BeSameAs(secondToken);
        user.EmailVerificationTokens.Should().NotContain(t => t.TokenHash == firstHash);
    }

    [Fact]
    public void IssueEmailVerificationToken_KeepsConsumedHistoricalTokens()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var consumedHash = Domain.Identity.TokenHash.From("consumed-verification-hash");
        var freshHash = Domain.Identity.TokenHash.From("fresh-verification-hash");
        var consumedToken = user.IssueEmailVerificationToken(
            consumedHash,
            clock.UtcNow.AddHours(1),
            clock
        );
        user.VerifyEmail(consumedHash, clock).Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(1));
        var freshToken = user.IssueEmailVerificationToken(freshHash, clock.UtcNow.AddHours(1), clock);

        user.EmailVerificationTokens.Should().HaveCount(2);
        user.EmailVerificationTokens.Should().Contain(consumedToken);
        user.EmailVerificationTokens.Should().Contain(freshToken);
        consumedToken.IsConsumed.Should().BeTrue();
        freshToken.IsConsumed.Should().BeFalse();
    }

    [Fact]
    public void VerifyEmail_ValidUnconsumedUnexpiredToken_ConsumesAndMarksVerified()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = Domain.Identity.TokenHash.From("verification-hash");
        var token = user.IssueEmailVerificationToken(hash, clock.UtcNow.AddHours(1), clock);

        clock.Advance(TimeSpan.FromMinutes(2));
        var verified = user.VerifyEmail(hash, clock);

        verified.Should().BeTrue();
        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(clock.UtcNow);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        token.ConsumedAt.Should().Be(clock.UtcNow);
        token.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public void VerifyEmail_UnknownHash_ReturnsFalseAndDoesNotMarkVerified()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var originalUpdatedAt = user.UpdatedAt;
        user.IssueEmailVerificationToken(
            Domain.Identity.TokenHash.From("verification-hash"),
            clock.UtcNow.AddHours(1),
            clock
        );

        clock.Advance(TimeSpan.FromMinutes(1));
        var verified = user.VerifyEmail(Domain.Identity.TokenHash.From("unknown-hash"), clock);

        verified.Should().BeFalse();
        user.EmailVerified.Should().BeFalse();
        user.EmailVerifiedAt.Should().BeNull();
        user.UpdatedAt.Should().NotBe(clock.UtcNow);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
        user.EmailVerificationTokens.Should().OnlyContain(t => !t.IsConsumed);
    }

    [Fact]
    public void VerifyEmail_AlreadyConsumed_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = Domain.Identity.TokenHash.From("verification-hash");
        user.IssueEmailVerificationToken(hash, clock.UtcNow.AddHours(1), clock);
        user.VerifyEmail(hash, clock).Should().BeTrue();
        var firstVerifiedAt = user.EmailVerifiedAt;

        clock.Advance(TimeSpan.FromMinutes(1));
        var verifiedAgain = user.VerifyEmail(hash, clock);

        verifiedAgain.Should().BeFalse();
        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(firstVerifiedAt);
    }

    [Fact]
    public void VerifyEmail_Expired_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = Domain.Identity.TokenHash.From("verification-hash");
        var token = user.IssueEmailVerificationToken(hash, clock.UtcNow.AddMinutes(1), clock);

        clock.Advance(TimeSpan.FromMinutes(2));
        var verified = user.VerifyEmail(hash, clock);

        verified.Should().BeFalse();
        user.EmailVerified.Should().BeFalse();
        user.EmailVerifiedAt.Should().BeNull();
        token.ConsumedAt.Should().BeNull();
    }

    [Fact]
    public void IssuePasswordResetToken_RemovesPreviousUnconsumedTokens()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var firstHash = Domain.Identity.TokenHash.From("first-reset-hash");
        var secondHash = Domain.Identity.TokenHash.From("second-reset-hash");

        user.IssuePasswordResetToken(firstHash, clock.UtcNow.AddHours(1), clock);
        clock.Advance(TimeSpan.FromMinutes(1));
        var secondToken = user.IssuePasswordResetToken(secondHash, clock.UtcNow.AddHours(1), clock);

        user.PasswordResetTokens.Should().ContainSingle().Which.Should().BeSameAs(secondToken);
        user.PasswordResetTokens.Should().NotContain(t => t.TokenHash == firstHash);
    }

    [Fact]
    public void IssuePasswordResetToken_KeepsConsumedHistoricalTokens()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var consumedHash = Domain.Identity.TokenHash.From("consumed-reset-hash");
        var freshHash = Domain.Identity.TokenHash.From("fresh-reset-hash");
        var consumedToken = user.IssuePasswordResetToken(
            consumedHash,
            clock.UtcNow.AddHours(1),
            clock
        );
        user.ResetPassword(consumedHash, Domain.Identity.PasswordHash.From("changed-hash"), clock)
            .Should()
            .BeTrue();

        clock.Advance(TimeSpan.FromMinutes(1));
        var freshToken = user.IssuePasswordResetToken(freshHash, clock.UtcNow.AddHours(1), clock);

        user.PasswordResetTokens.Should().HaveCount(2);
        user.PasswordResetTokens.Should().Contain(consumedToken);
        user.PasswordResetTokens.Should().Contain(freshToken);
        consumedToken.IsConsumed.Should().BeTrue();
        freshToken.IsConsumed.Should().BeFalse();
    }

    [Fact]
    public void ResetPassword_ValidToken_ConsumesTokenAndChangesPasswordHash()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var oldPasswordHash = user.PasswordHash;
        var newPasswordHash = Domain.Identity.PasswordHash.From("new-hash");
        var resetHash = Domain.Identity.TokenHash.From("reset-hash");
        var token = user.IssuePasswordResetToken(resetHash, clock.UtcNow.AddHours(1), clock);

        clock.Advance(TimeSpan.FromMinutes(3));
        var reset = user.ResetPassword(resetHash, newPasswordHash, clock);

        reset.Should().BeTrue();
        user.PasswordHash.Should().Be(newPasswordHash);
        user.PasswordHash.Should().NotBe(oldPasswordHash);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        token.ConsumedAt.Should().Be(clock.UtcNow);
        token.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public void ResetPassword_UnknownHash_ReturnsFalseAndDoesNotChangePassword()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var originalPasswordHash = user.PasswordHash;
        var originalUpdatedAt = user.UpdatedAt;
        user.IssuePasswordResetToken(
            Domain.Identity.TokenHash.From("reset-hash"),
            clock.UtcNow.AddHours(1),
            clock
        );

        clock.Advance(TimeSpan.FromMinutes(1));
        var reset = user.ResetPassword(
            Domain.Identity.TokenHash.From("unknown-hash"),
            Domain.Identity.PasswordHash.From("new-hash"),
            clock
        );

        reset.Should().BeFalse();
        user.PasswordHash.Should().Be(originalPasswordHash);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
        user.PasswordResetTokens.Should().OnlyContain(t => !t.IsConsumed);
    }

    [Fact]
    public void ResetPassword_AlreadyConsumed_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = Domain.Identity.TokenHash.From("reset-hash");
        var firstNewHash = Domain.Identity.PasswordHash.From("first-new-hash");
        user.IssuePasswordResetToken(hash, clock.UtcNow.AddHours(1), clock);
        user.ResetPassword(hash, firstNewHash, clock).Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(1));
        var resetAgain = user.ResetPassword(hash, Domain.Identity.PasswordHash.From("second-new-hash"), clock);

        resetAgain.Should().BeFalse();
        user.PasswordHash.Should().Be(firstNewHash);
    }

    [Fact]
    public void ResetPassword_Expired_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var originalPasswordHash = user.PasswordHash;
        var hash = Domain.Identity.TokenHash.From("reset-hash");
        var token = user.IssuePasswordResetToken(hash, clock.UtcNow.AddMinutes(1), clock);

        clock.Advance(TimeSpan.FromMinutes(2));
        var reset = user.ResetPassword(hash, Domain.Identity.PasswordHash.From("new-hash"), clock);

        reset.Should().BeFalse();
        user.PasswordHash.Should().Be(originalPasswordHash);
        token.ConsumedAt.Should().BeNull();
    }

    private static DomainUser CreateUser(FakeClock clock) =>
        DomainUser.Register(
            Domain.Identity.Email.Normalize("test@example.com"),
            Domain.Identity.PasswordHash.From("hashed-password"),
            clock
        );
}
