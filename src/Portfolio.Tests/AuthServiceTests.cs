using Portfolio.Application.Facades;
using Portfolio.Domain.Entities;
using Portfolio.Infrastructure.Security;
using Portfolio.Tests.Fakes;
using Xunit;

namespace Portfolio.Tests;

// AuthService's lockout state is a static dictionary shared process-wide, so each test uses its own
// unique username to avoid cross-test interference (tests may run in parallel).
public class AuthServiceTests
{
    private const string Password = "correct horse battery staple";
    private readonly PasswordHasher _hasher = new();

    private (AuthService Service, string Username, AdminUser User) MakeSut()
    {
        var repo = new FakeRepository<AdminUser>();
        var username = $"user-{Guid.NewGuid():N}";
        var user = new AdminUser
        {
            Id = 1, Username = username, PasswordHash = _hasher.Hash(Password),
            SecurityStamp = "stamp-1"
        };
        repo.Items.Add(user);
        return (new AuthService(repo, _hasher), username, user);
    }

    /// <summary>Counts password checks and makes each one slow, so parallel attempts overlap inside it.</summary>
    private sealed class SlowCountingHasher : Portfolio.Application.Abstractions.IPasswordHasher
    {
        public int Verifications;
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string storedHash)
        {
            Interlocked.Increment(ref Verifications);
            Thread.Sleep(30);
            return storedHash == "hash:" + password;
        }
    }

    [Fact]
    public async Task ParallelAttempts_CannotTestMorePasswordsThanTheLimit()
    {
        var hasher = new SlowCountingHasher();
        var repo = new FakeRepository<AdminUser>();
        var username = $"user-{Guid.NewGuid():N}";
        repo.Items.Add(new AdminUser { Id = 1, Username = username, PasswordHash = hasher.Hash(Password), SecurityStamp = "s" });
        var sut = new AuthService(repo, hasher);
        var verificationsBefore = hasher.Verifications;   // the constructor's dummy hash isn't a verification

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            Task.Run(() => sut.CheckAsync(username, $"guess-{i}"))));

        Assert.Equal(5, hasher.Verifications - verificationsBefore);
        Assert.Equal(15, results.Count(r => r.LockedOut));
    }

    [Fact]
    public async Task FindAccountName_MatchesLikeTheLockout()
    {
        var (sut, username, _) = MakeSut();
        Assert.Equal(username, await sut.FindAccountNameAsync("  " + username.ToUpperInvariant() + " "));
        Assert.Null(await sut.FindAccountNameAsync("correct horse battery staple"));
        Assert.Null(await sut.FindAccountNameAsync("   "));
    }

    [Fact]
    public async Task ValidateAsync_CorrectCredentials_ReturnsAuthResult()
    {
        var (sut, username, user) = MakeSut();
        var result = await sut.ValidateAsync(username, Password);
        Assert.NotNull(result);
        Assert.Equal(user.SecurityStamp, result!.SecurityStamp);
    }

    [Fact]
    public async Task ValidateAsync_WrongPassword_ReturnsNull()
    {
        var (sut, username, _) = MakeSut();
        Assert.Null(await sut.ValidateAsync(username, "wrong password"));
    }

    [Fact]
    public async Task ValidateAsync_UnknownUsername_ReturnsNull()
    {
        var (sut, _, _) = MakeSut();
        Assert.Null(await sut.ValidateAsync($"nobody-{Guid.NewGuid():N}", Password));
    }

    [Fact]
    public async Task ValidateAsync_AfterFiveFailures_LocksOutEvenWithCorrectPassword()
    {
        var (sut, username, _) = MakeSut();
        for (var i = 0; i < 5; i++)
            await sut.ValidateAsync(username, "wrong password");

        // Sixth attempt uses the *correct* password, but the account should now be locked out.
        Assert.Null(await sut.ValidateAsync(username, Password));
    }

    [Fact]
    public async Task ValidateAsync_SuccessfulLogin_ResetsFailureCount()
    {
        var (sut, username, _) = MakeSut();
        for (var i = 0; i < 4; i++)
            await sut.ValidateAsync(username, "wrong password");

        Assert.NotNull(await sut.ValidateAsync(username, Password)); // 5th attempt succeeds, clears counter

        for (var i = 0; i < 4; i++)
            await sut.ValidateAsync(username, "wrong password");

        // Only 4 failures since the reset — should not be locked out yet.
        Assert.NotNull(await sut.ValidateAsync(username, Password));
    }

    [Fact]
    public async Task ValidateSecurityStampAsync_MatchingStamp_ReturnsTrue()
    {
        var (sut, username, user) = MakeSut();
        Assert.True(await sut.ValidateSecurityStampAsync(username, user.SecurityStamp));
    }

    [Fact]
    public async Task ValidateSecurityStampAsync_StaleStamp_ReturnsFalse()
    {
        var (sut, username, _) = MakeSut();
        Assert.False(await sut.ValidateSecurityStampAsync(username, "stale-stamp"));
    }
}
