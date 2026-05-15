using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using App.Identity;
using AwesomeAssertions;
using Domain;
using Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OtpNet;
using Web.Models;

namespace Web.IntegrationTests;

public class TwoFactorEndpointsTests(IntegrationTestFixture factory)
    : IClassFixture<IntegrationTestFixture>
{
    private const string Password = "password123";

    [Fact]
    public async Task Setup_WhenAuthenticated_ReturnsQrCodeAndSecret()
    {
        await using var app = CreateApp();
        var user = await RegisterVerifyAndLoginAsync(app);
        SetBearer(app.Client, user.Auth.AccessToken);

        var setup = await SetupAsync(app.Client);

        setup.Base32Secret.Should().NotBeNullOrWhiteSpace();
        setup.OtpAuthUri.Should().Contain(setup.Base32Secret);
        setup.OtpAuthUri.Should().Contain(Uri.EscapeDataString(user.Email));
        setup.QrCodePngBase64.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Setup_WhenAlreadyEnabled_ReturnsConflict()
    {
        await using var app = CreateApp();
        await EnableTwoFactorAsync(app);

        var response = await app.Client.PostAsync("/api/v1/auth/2fa/setup", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Two-factor authentication is already enabled.");
    }

    [Fact]
    public async Task Confirm_WithValidCode_EnablesAndReturnsRecoveryCodes()
    {
        await using var app = CreateApp();
        var user = await RegisterVerifyAndLoginAsync(app);
        SetBearer(app.Client, user.Auth.AccessToken);
        var setup = await SetupAsync(app.Client);
        var code = GenerateTotpCode(app, setup.Base32Secret);

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/2fa/confirm",
            new ConfirmTwoFactorRequest { Code = code }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync<TwoFactorEnabledResponse>(response);
        body.RecoveryCodes.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        body.RecoveryCodes.Should().OnlyContain(code => !string.IsNullOrWhiteSpace(code));
    }

    [Fact]
    public async Task Confirm_WithInvalidCode_ReturnsBadRequest()
    {
        await using var app = CreateApp();
        var user = await RegisterVerifyAndLoginAsync(app);
        SetBearer(app.Client, user.Auth.AccessToken);
        var setup = await SetupAsync(app.Client);
        var invalidCode = GenerateInvalidTotpCode(setup.Base32Secret);

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/2fa/confirm",
            new ConfirmTwoFactorRequest { Code = invalidCode }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Invalid two-factor code.");
    }

    [Fact]
    public async Task Disable_WithValidCode_RemovesTotp()
    {
        await using var app = CreateApp();
        var enabled = await EnableTwoFactorAsync(app);
        var code = GenerateTotpCode(app, enabled.Setup.Base32Secret, TimeSpan.FromSeconds(30));

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/2fa/disable",
            new DisableTwoFactorRequest { Code = code }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Two-factor authentication disabled.");

        app.Client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await LoginAsync(app.Client, enabled.User.Email, enabled.User.Password);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Disable_WithInvalidCode_ReturnsBadRequest()
    {
        await using var app = CreateApp();
        var enabled = await EnableTwoFactorAsync(app);
        var invalidCode = GenerateInvalidTotpCode(enabled.Setup.Base32Secret);

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/2fa/disable",
            new DisableTwoFactorRequest { Code = invalidCode }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Invalid two-factor code.");
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_WithValidCode_ReturnsTenNewCodes()
    {
        await using var app = CreateApp();
        var enabled = await EnableTwoFactorAsync(app);
        var code = GenerateTotpCode(app, enabled.Setup.Base32Secret, TimeSpan.FromSeconds(30));

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/2fa/recovery-codes/regenerate",
            new RegenerateRecoveryCodesRequest { Code = code }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync<RecoveryCodesResponse>(response);
        body.RecoveryCodes.Should().HaveCount(10).And.OnlyHaveUniqueItems();
        body.RecoveryCodes.Should().NotBeEquivalentTo(enabled.RecoveryCodes);
    }

    [Fact]
    public async Task Login_When2FAEnabled_Returns202WithChallengeToken()
    {
        await using var app = CreateApp();
        var enabled = await EnableTwoFactorAsync(app);
        app.Client.DefaultRequestHeaders.Authorization = null;

        var response = await LoginAsync(app.Client, enabled.User.Email, enabled.User.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await ReadJsonAsync<TwoFactorRequiredResponse>(response);
        body.UserId.Should().Be(enabled.User.Auth.UserId);
        body.ChallengeToken.Should().NotBeNullOrWhiteSpace();
        body.AvailableMethods.Should().Equal("totp");
    }

    [Fact]
    public async Task VerifyTotp_WithValidCode_Returns200WithTokens()
    {
        await using var app = CreateApp();
        var enabled = await EnableTwoFactorAsync(app);
        var challenge = await LoginForTwoFactorChallengeAsync(app.Client, enabled.User);
        var code = GenerateTotpCode(app, enabled.Setup.Base32Secret, TimeSpan.FromSeconds(30));

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { ChallengeToken = challenge.ChallengeToken, Code = code }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync<AuthResponse>(response);
        body.UserId.Should().Be(enabled.User.Auth.UserId);
        body.Email.Should().Be(enabled.User.Email);
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task VerifyTotp_WithInvalidChallenge_Returns401()
    {
        await using var app = CreateApp();

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { ChallengeToken = "missing-challenge", Code = "123456" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Invalid challenge token.");
    }

    [Fact]
    public async Task VerifyTotp_WithExpiredChallenge_Returns401WithExpiredError()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        await using var app = CreateApp(clock);
        var enabled = await EnableTwoFactorAsync(app);
        var challenge = await LoginForTwoFactorChallengeAsync(app.Client, enabled.User);
        clock.Advance(TimeSpan.FromMinutes(6));
        var code = GenerateTotpCode(app, enabled.Setup.Base32Secret, TimeSpan.FromSeconds(30));

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { ChallengeToken = challenge.ChallengeToken, Code = code }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Challenge token expired.");
    }

    [Fact]
    public async Task VerifyTotp_WithRecoveryCode_DisablesTwoFactorAndIssuesSession()
    {
        await using var app = CreateApp();
        var enabled = await EnableTwoFactorAsync(app);
        var recoveryCode = enabled.RecoveryCodes[0];
        var challenge = await LoginForTwoFactorChallengeAsync(app.Client, enabled.User);

        var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest
            {
                ChallengeToken = challenge.ChallengeToken,
                Code = recoveryCode,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await ReadJsonAsync<AuthResponse>(response);
        auth.UserId.Should().Be(enabled.User.Auth.UserId);

        // After a recovery code is used, 2FA is disabled — subsequent login no longer
        // returns 202 with a challenge, it returns 200 with tokens directly.
        var loginAfter = await LoginAsync(app.Client, enabled.User.Email, enabled.User.Password);
        loginAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginAfterBody = await ReadJsonAsync<AuthResponse>(loginAfter);
        loginAfterBody.UserId.Should().Be(enabled.User.Auth.UserId);
        loginAfterBody.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    private TestApp CreateApp(MutableClock? clock = null)
    {
        var emailStarter = new CapturingEmailWorkflowStarter();
        clock ??= new MutableClock(DateTimeOffset.UtcNow);
        var identityStore = new InMemoryIdentityStore(clock);
        var keyPath = Path.Combine(
            Path.GetTempPath(),
            "recipes-dotnet-data-protection",
            Guid.NewGuid().ToString("N")
        );

        var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DataProtection:KeyPath", keyPath);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailWorkflowStarter>();
                services.AddSingleton<IEmailWorkflowStarter>(emailStarter);
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(identityStore);
                services.RemoveAll<IUserSessionRepository>();
                services.AddSingleton<IUserSessionRepository>(identityStore);
                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton<IUnitOfWork>(identityStore);
            });
        });

        return new TestApp(app, app.CreateClient(), emailStarter, clock);
    }

    private static async Task<TestUser> RegisterVerifyAndLoginAsync(TestApp app)
    {
        var email = $"twofactor-{Guid.NewGuid():N}@example.com";
        var registerResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest { Email = email, Password = Password }
        );
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var verificationToken = app.EmailStarter.GetVerificationToken(email);
        var verifyResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest { Token = verificationToken }
        );
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await LoginAsync(app.Client, email, Password);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await ReadJsonAsync<AuthResponse>(loginResponse);
        return new TestUser(email, Password, auth);
    }

    private static async Task<EnabledTwoFactor> EnableTwoFactorAsync(TestApp app)
    {
        var user = await RegisterVerifyAndLoginAsync(app);
        SetBearer(app.Client, user.Auth.AccessToken);
        var setup = await SetupAsync(app.Client);
        var code = GenerateTotpCode(app, setup.Base32Secret);
        var confirmResponse = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/2fa/confirm",
            new ConfirmTwoFactorRequest { Code = code }
        );
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var enabled = await ReadJsonAsync<TwoFactorEnabledResponse>(confirmResponse);
        return new EnabledTwoFactor(user, setup, enabled.RecoveryCodes);
    }

    private static async Task<TwoFactorSetupResponse> SetupAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/v1/auth/2fa/setup", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync<TwoFactorSetupResponse>(response);
    }

    private static async Task<TwoFactorRequiredResponse> LoginForTwoFactorChallengeAsync(
        HttpClient client,
        TestUser user
    )
    {
        client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await LoginAsync(client, user.Email, user.Password);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return await ReadJsonAsync<TwoFactorRequiredResponse>(loginResponse);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password
    ) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password }
        );

    private static string GenerateTotpCode(
        TestApp app,
        string base32Secret,
        TimeSpan? offset = null
    )
    {
        using var scope = app.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITotpService>().Should().NotBeNull();
        var secret = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(secret);
        return totp.ComputeTotp(DateTime.UtcNow.Add(offset ?? TimeSpan.Zero));
    }

    private static string GenerateInvalidTotpCode(string base32Secret)
    {
        var secret = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(secret);
        var now = DateTime.UtcNow;
        var validCodes = new HashSet<string>
        {
            totp.ComputeTotp(now.AddSeconds(-30)),
            totp.ComputeTotp(now),
            totp.ComputeTotp(now.AddSeconds(30)),
        };

        for (var i = 0; i <= 999_999; i++)
        {
            var candidate = i.ToString("D6");
            if (!validCodes.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate an invalid TOTP code.");
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        value.Should().NotBeNull();
        return value!;
    }

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed class InMemoryIdentityStore(MutableClock clock)
        : IUserRepository,
            IUserSessionRepository,
            IUnitOfWork
    {
        private readonly object _gate = new();
        private readonly List<User> _users = [];
        private readonly List<UserSession> _sessions = [];

        public Task<User?> GetByIdAsync(
            UserId id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(FindUser(user => user.Id == id));

        public Task<User?> GetByEmailAsync(
            Email email,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(FindUser(user => user.Email == email));

        public Task<User?> GetByEmailVerificationTokenHashAsync(
            TokenHash hash,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                FindUser(user => user.EmailVerificationTokens.Any(token => token.TokenHash == hash))
            );

        public Task<User?> GetByPasswordResetTokenHashAsync(
            TokenHash hash,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                FindUser(user => user.PasswordResetTokens.Any(token => token.TokenHash == hash))
            );

        public Task<User?> GetByTwoFactorChallengeHashAsync(
            TokenHash challengeHash,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                FindUser(user =>
                    user.TwoFactorChallenges.Any(challenge => challenge.TokenHash == challengeHash)
                )
            );

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _users.Add(user);
            }

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<UserSession?> GetByTokenHashAsync(
            TokenHash hash,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(FindSession(session => session.TokenHash == hash));

        public Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(
            UserId userId,
            CancellationToken cancellationToken = default
        )
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<UserSession>>(
                    _sessions
                        .Where(session => session.UserId == userId && session.IsActive(clock.UtcNow))
                        .ToArray()
                );
            }
        }

        public Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _sessions.Add(session);
            }

            return Task.CompletedTask;
        }

        public Task RevokeAllForUserAsync(
            UserId userId,
            CancellationToken cancellationToken = default
        )
        {
            lock (_gate)
            {
                foreach (var session in _sessions.Where(session => session.UserId == userId))
                {
                    session.TryRevoke(clock);
                }
            }

            return Task.CompletedTask;
        }

        Task IUserSessionRepository.SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IUnitOfWorkScope>(new InMemoryUnitOfWorkScope());

        private User? FindUser(Func<User, bool> predicate)
        {
            lock (_gate)
            {
                return _users.SingleOrDefault(predicate);
            }
        }

        private UserSession? FindSession(Func<UserSession, bool> predicate)
        {
            lock (_gate)
            {
                return _sessions.SingleOrDefault(predicate);
            }
        }
    }

    private sealed class InMemoryUnitOfWorkScope : IUnitOfWorkScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingEmailWorkflowStarter : IEmailWorkflowStarter
    {
        private readonly ConcurrentDictionary<string, string> _verificationTokens = new(
            StringComparer.OrdinalIgnoreCase
        );

        public Task StartVerificationWorkflowAsync(
            Guid userId,
            string email,
            string token,
            CancellationToken cancellationToken = default
        )
        {
            _verificationTokens[email] = token;
            return Task.CompletedTask;
        }

        public Task StartPasswordResetWorkflowAsync(
            Guid userId,
            string email,
            string token,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public string GetVerificationToken(string email) =>
            _verificationTokens.TryGetValue(email, out var token)
                ? token
                : throw new InvalidOperationException($"No verification token captured for {email}.");
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan duration)
        {
            UtcNow = UtcNow.Add(duration);
        }
    }

    private sealed record TestApp(
        WebApplicationFactory<Program> Factory,
        HttpClient Client,
        CapturingEmailWorkflowStarter EmailStarter,
        MutableClock Clock
    ) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }

    private sealed record TestUser(string Email, string Password, AuthResponse Auth);

    private sealed record EnabledTwoFactor(
        TestUser User,
        TwoFactorSetupResponse Setup,
        IReadOnlyList<string> RecoveryCodes
    );
}
