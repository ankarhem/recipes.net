using AwesomeAssertions;
using Xunit;

namespace App.UnitTests;

public class UserSessionAggregateTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_CreatesActiveSessionWithCorrectTimestamps()
    {
        var clock = new FakeClock(TestNow);
        var userId = Domain.User.UserId.New();
        var hash = Domain.User.TokenHash.From("session-hash");
        var expiresAt = clock.UtcNow.AddDays(7);

        var session = Domain.User.UserSession.Issue(userId, hash, expiresAt, clock);

        session.Id.Should().NotBeEmpty();
        session.UserId.Should().Be(userId);
        session.TokenHash.Should().Be(hash);
        session.ExpiresAt.Should().Be(expiresAt);
        session.CreatedAt.Should().Be(clock.UtcNow);
        session.RevokedAt.Should().BeNull();
        session.IsRevoked.Should().BeFalse();
        session.IsActive(clock.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsActive_FreshSession_ReturnsTrue()
    {
        var clock = new FakeClock(TestNow);
        var session = CreateSession(clock);

        session.IsActive(clock.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsActive_AfterExpiry_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var session = CreateSession(clock, expiresAt: clock.UtcNow.AddMinutes(1));

        clock.Advance(TimeSpan.FromMinutes(2));

        session.IsActive(clock.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsActive_AfterRevoke_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var session = CreateSession(clock);

        session.TryRevoke(clock).Should().BeTrue();

        session.IsActive(clock.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void TryRevoke_NotPreviouslyRevoked_SetsRevokedAtAndReturnsTrue()
    {
        var clock = new FakeClock(TestNow);
        var session = CreateSession(clock);

        clock.Advance(TimeSpan.FromMinutes(5));
        var revoked = session.TryRevoke(clock);

        revoked.Should().BeTrue();
        session.IsRevoked.Should().BeTrue();
        session.RevokedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public void TryRevoke_AlreadyRevoked_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var session = CreateSession(clock);
        session.TryRevoke(clock).Should().BeTrue();
        var firstRevokedAt = session.RevokedAt;

        clock.Advance(TimeSpan.FromMinutes(1));
        var revokedAgain = session.TryRevoke(clock);

        revokedAgain.Should().BeFalse();
        session.RevokedAt.Should().Be(firstRevokedAt);
    }

    [Fact]
    public void IsExpired_BeforeExpiry_ReturnsFalse()
    {
        var clock = new FakeClock(TestNow);
        var session = CreateSession(clock, expiresAt: clock.UtcNow.AddMinutes(1));

        session.IsExpired(clock.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_AfterExpiry_ReturnsTrue()
    {
        var clock = new FakeClock(TestNow);
        var session = CreateSession(clock, expiresAt: clock.UtcNow.AddMinutes(1));

        clock.Advance(TimeSpan.FromMinutes(2));

        session.IsExpired(clock.UtcNow).Should().BeTrue();
    }

    private static Domain.User.UserSession CreateSession(
        FakeClock clock,
        DateTimeOffset? expiresAt = null
    ) =>
        Domain.User.UserSession.Issue(
            Domain.User.UserId.New(),
            Domain.User.TokenHash.From("session-hash"),
            expiresAt ?? clock.UtcNow.AddDays(7),
            clock
        );
}
