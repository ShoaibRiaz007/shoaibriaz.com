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
