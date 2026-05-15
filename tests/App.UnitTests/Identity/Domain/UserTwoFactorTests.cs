using AwesomeAssertions;
using Domain.Identity;
using Xunit;

namespace App.UnitTests;

public class UserTwoFactorTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StartTwoFactorSetup_WhenNotEnabled_CreatesPendingCredential()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var secret = EncryptedTotpSecret.From("encrypted-secret");

        // Act
        clock.Advance(TimeSpan.FromMinutes(5));
        user.StartTwoFactorSetup(secret, clock);

        // Assert
        user.Totp.Should().NotBeNull();
        user.Totp!.Id.Should().NotBeEmpty();
        user.Totp.UserId.Should().Be(user.Id);
        user.Totp.EncryptedSecret.Should().Be(secret);
        user.Totp.IsVerified.Should().BeFalse();
        user.Totp.LastUsedStep.Should().BeNull();
        user.Totp.CreatedAt.Should().Be(clock.UtcNow);
        user.Totp.VerifiedAt.Should().BeNull();
        user.Totp.UpdatedAt.Should().Be(clock.UtcNow);
        user.HasTwoFactorEnabled.Should().BeFalse();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void StartTwoFactorSetup_WhenPendingExists_ReplacesIt()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var firstSecret = EncryptedTotpSecret.From("first-encrypted-secret");
        var secondSecret = EncryptedTotpSecret.From("second-encrypted-secret");
        user.StartTwoFactorSetup(firstSecret, clock);
        var firstCredentialId = user.Totp!.Id;

        // Act
        clock.Advance(TimeSpan.FromMinutes(5));
        user.StartTwoFactorSetup(secondSecret, clock);

        // Assert
        user.Totp.Should().NotBeNull();
        user.Totp!.Id.Should().NotBe(firstCredentialId);
        user.Totp.EncryptedSecret.Should().Be(secondSecret);
        user.Totp.IsVerified.Should().BeFalse();
        user.Totp.CreatedAt.Should().Be(clock.UtcNow);
        user.HasTwoFactorEnabled.Should().BeFalse();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void StartTwoFactorSetup_WhenAlreadyEnabled_Throws()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var secret = EncryptedTotpSecret.From("replacement-secret");

        // Act
        var act = () => user.StartTwoFactorSetup(secret, clock);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Two-factor authentication is already enabled.");
    }

    [Fact]
    public void ConfirmTwoFactor_WhenNoPending_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(5));
        var confirmed = user.ConfirmTwoFactor(100, CreateRecoveryCodeHashes("code-1"), clock);

        // Assert
        confirmed.Should().BeFalse();
        user.Totp.Should().BeNull();
        user.RecoveryCodes.Should().BeEmpty();
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void ConfirmTwoFactor_WhenAlreadyVerified_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var originalVerifiedAt = user.Totp!.VerifiedAt;
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(5));
        var confirmed = user.ConfirmTwoFactor(101, CreateRecoveryCodeHashes("new-code"), clock);

        // Assert
        confirmed.Should().BeFalse();
        user.Totp.VerifiedAt.Should().Be(originalVerifiedAt);
        user.Totp.LastUsedStep.Should().Be(100);
        user.RecoveryCodes.Should().HaveCount(2);
        user.RecoveryCodes.Should().NotContain(c => c.CodeHash.Value == "new-code");
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void ConfirmTwoFactor_WhenPending_SetsVerifiedAndAddsRecoveryCodes()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hashes = CreateRecoveryCodeHashes("code-1", "code-2", "code-3");
        user.StartTwoFactorSetup(EncryptedTotpSecret.From("encrypted-secret"), clock);

        // Act
        clock.Advance(TimeSpan.FromMinutes(5));
        var confirmed = user.ConfirmTwoFactor(123, hashes, clock);

        // Assert
        confirmed.Should().BeTrue();
        user.HasTwoFactorEnabled.Should().BeTrue();
        user.Totp.Should().NotBeNull();
        user.Totp!.IsVerified.Should().BeTrue();
        user.Totp.VerifiedAt.Should().Be(clock.UtcNow);
        user.Totp.LastUsedStep.Should().Be(123);
        user.Totp.UpdatedAt.Should().Be(clock.UtcNow);
        user.RecoveryCodes.Should().HaveCount(3);
        user.RecoveryCodes.Select(c => c.CodeHash).Should().BeEquivalentTo(hashes);
        user.RecoveryCodes.Should().OnlyContain(c => c.UserId == user.Id && c.CreatedAt == clock.UtcNow);
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void IssueTwoFactorChallenge_ReplacesUnconsumedChallenges()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var firstHash = TokenHash.From("first-challenge-hash");
        var secondHash = TokenHash.From("second-challenge-hash");
        user.IssueTwoFactorChallenge(firstHash, clock.UtcNow.AddMinutes(5), clock);

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        user.IssueTwoFactorChallenge(secondHash, clock.UtcNow.AddMinutes(5), clock);

        // Assert
        user.TwoFactorChallenges.Should().ContainSingle();
        user.TwoFactorChallenges.Should().NotContain(c => c.TokenHash == firstHash);
        var challenge = user.TwoFactorChallenges.Single();
        challenge.TokenHash.Should().Be(secondHash);
        challenge.UserId.Should().Be(user.Id);
        challenge.ExpiresAt.Should().Be(clock.UtcNow.AddMinutes(5));
        challenge.CreatedAt.Should().Be(clock.UtcNow);
        challenge.IsActive(clock.UtcNow).Should().BeTrue();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void ConsumeTwoFactorChallenge_WhenActive_ReturnsTrueAndConsumes()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = TokenHash.From("challenge-hash");
        user.IssueTwoFactorChallenge(hash, clock.UtcNow.AddMinutes(5), clock);
        var challenge = user.TwoFactorChallenges.Single();

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var consumed = user.ConsumeTwoFactorChallenge(hash, clock);

        // Assert
        consumed.Should().BeTrue();
        challenge.IsConsumed.Should().BeTrue();
        challenge.ConsumedAt.Should().Be(clock.UtcNow);
        challenge.IsActive(clock.UtcNow).Should().BeFalse();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void ConsumeTwoFactorChallenge_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = TokenHash.From("challenge-hash");
        user.IssueTwoFactorChallenge(hash, clock.UtcNow.AddMinutes(1), clock);
        var challenge = user.TwoFactorChallenges.Single();
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(2));
        var consumed = user.ConsumeTwoFactorChallenge(hash, clock);

        // Assert
        consumed.Should().BeFalse();
        challenge.IsExpired(clock.UtcNow).Should().BeTrue();
        challenge.ConsumedAt.Should().BeNull();
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void ConsumeTwoFactorChallenge_WhenConsumed_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = TokenHash.From("challenge-hash");
        user.IssueTwoFactorChallenge(hash, clock.UtcNow.AddMinutes(5), clock);
        user.ConsumeTwoFactorChallenge(hash, clock).Should().BeTrue();
        var challenge = user.TwoFactorChallenges.Single();
        var originalConsumedAt = challenge.ConsumedAt;
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var consumedAgain = user.ConsumeTwoFactorChallenge(hash, clock);

        // Assert
        consumedAgain.Should().BeFalse();
        challenge.ConsumedAt.Should().Be(originalConsumedAt);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void ConsumeTwoFactorChallenge_WhenUnknown_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        user.IssueTwoFactorChallenge(TokenHash.From("challenge-hash"), clock.UtcNow.AddMinutes(5), clock);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var consumed = user.ConsumeTwoFactorChallenge(TokenHash.From("unknown-hash"), clock);

        // Assert
        consumed.Should().BeFalse();
        user.TwoFactorChallenges.Should().OnlyContain(c => !c.IsConsumed);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void VerifyAndAdvanceTotp_WhenNotEnabled_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var verified = user.VerifyAndAdvanceTotp(101, clock);

        // Assert
        verified.Should().BeFalse();
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void VerifyAndAdvanceTotp_WhenStepIsReplay_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var verified = user.VerifyAndAdvanceTotp(100, clock);

        // Assert
        verified.Should().BeFalse();
        user.Totp!.LastUsedStep.Should().Be(100);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void VerifyAndAdvanceTotp_WhenStepIsBeforeReplay_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var verified = user.VerifyAndAdvanceTotp(99, clock);

        // Assert
        verified.Should().BeFalse();
        user.Totp!.LastUsedStep.Should().Be(100);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void VerifyAndAdvanceTotp_WhenStepIsNew_ReturnsTrueAndAdvances()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var verified = user.VerifyAndAdvanceTotp(101, clock);

        // Assert
        verified.Should().BeTrue();
        user.Totp!.LastUsedStep.Should().Be(101);
        user.Totp.UpdatedAt.Should().Be(clock.UtcNow);
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void ConsumeRecoveryCode_WhenUnknown_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var consumed = user.ConsumeRecoveryCode(Guid.NewGuid(), clock);

        // Assert
        consumed.Should().BeFalse();
        user.RecoveryCodes.Should().OnlyContain(c => !c.IsConsumed);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void ConsumeRecoveryCode_WhenAlreadyConsumed_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var code = user.RecoveryCodes.First();
        user.ConsumeRecoveryCode(code.Id, clock).Should().BeTrue();
        var originalConsumedAt = code.ConsumedAt;
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var consumedAgain = user.ConsumeRecoveryCode(code.Id, clock);

        // Assert
        consumedAgain.Should().BeFalse();
        code.ConsumedAt.Should().Be(originalConsumedAt);
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void ConsumeRecoveryCode_WhenValid_ReturnsTrueAndMarksConsumed()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var code = user.RecoveryCodes.First();

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var consumed = user.ConsumeRecoveryCode(code.Id, clock);

        // Assert
        consumed.Should().BeTrue();
        code.IsConsumed.Should().BeTrue();
        code.ConsumedAt.Should().Be(clock.UtcNow);
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void DisableTwoFactor_WhenNotEnabled_Throws()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);

        // Act
        var act = () => user.DisableTwoFactor(clock);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Two-factor authentication is not enabled.");
    }

    [Fact]
    public void DisableTwoFactor_WhenEnabled_ClearsTotpAndCodes()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        user.IssueTwoFactorChallenge(TokenHash.From("challenge-hash"), clock.UtcNow.AddMinutes(5), clock);

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        user.DisableTwoFactor(clock);

        // Assert
        user.HasTwoFactorEnabled.Should().BeFalse();
        user.Totp.Should().BeNull();
        user.RecoveryCodes.Should().BeEmpty();
        user.TwoFactorChallenges.Should().ContainSingle();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void RegenerateRecoveryCodes_WhenNotEnabled_Throws()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hashes = CreateRecoveryCodeHashes("new-code");

        // Act
        var act = () => user.RegenerateRecoveryCodes(hashes, clock);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Two-factor authentication is not enabled.");
    }

    [Fact]
    public void RegenerateRecoveryCodes_WhenEnabled_ReplacesCodes()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateEnabledUser(clock);
        var oldCodeIds = user.RecoveryCodes.Select(c => c.Id).ToArray();
        var newHashes = CreateRecoveryCodeHashes("new-code-1", "new-code-2", "new-code-3");

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        user.RegenerateRecoveryCodes(newHashes, clock);

        // Assert
        user.RecoveryCodes.Should().HaveCount(3);
        user.RecoveryCodes.Select(c => c.Id).Should().NotIntersectWith(oldCodeIds);
        user.RecoveryCodes.Select(c => c.CodeHash).Should().BeEquivalentTo(newHashes);
        user.RecoveryCodes.Should().OnlyContain(c => c.UserId == user.Id && c.CreatedAt == clock.UtcNow);
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void RemoveTwoFactorChallenge_WhenPresent_ReturnsTrueAndRemoves()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        var hash = TokenHash.From("challenge-hash");
        user.IssueTwoFactorChallenge(hash, clock.UtcNow.AddMinutes(5), clock);

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var removed = user.RemoveTwoFactorChallenge(hash, clock);

        // Assert
        removed.Should().BeTrue();
        user.TwoFactorChallenges.Should().BeEmpty();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void RemoveTwoFactorChallenge_WhenUnknown_ReturnsFalse()
    {
        // Arrange
        var clock = new FakeClock(TestNow);
        var user = CreateUser(clock);
        user.IssueTwoFactorChallenge(TokenHash.From("challenge-hash"), clock.UtcNow.AddMinutes(5), clock);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        clock.Advance(TimeSpan.FromMinutes(1));
        var removed = user.RemoveTwoFactorChallenge(TokenHash.From("unknown-hash"), clock);

        // Assert
        removed.Should().BeFalse();
        user.TwoFactorChallenges.Should().ContainSingle();
        user.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    private static User CreateEnabledUser(FakeClock clock)
    {
        var user = CreateUser(clock);
        user.StartTwoFactorSetup(EncryptedTotpSecret.From("encrypted-secret"), clock);
        user.ConfirmTwoFactor(100, CreateRecoveryCodeHashes("code-1", "code-2"), clock)
            .Should()
            .BeTrue();
        return user;
    }

    private static RecoveryCodeHash[] CreateRecoveryCodeHashes(params string[] values) =>
        values.Select(RecoveryCodeHash.From).ToArray();

    private static User CreateUser(FakeClock clock) =>
        User.Register(Email.Normalize("test@example.com"), PasswordHash.From("hashed-password"), clock);
}
