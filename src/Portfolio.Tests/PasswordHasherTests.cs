using Portfolio.Infrastructure.Security;
using Xunit;

namespace Portfolio.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_CorrectPassword_Succeeds()
    {
        var hash = _hasher.Hash("correct horse battery staple");
        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = _hasher.Hash("correct horse battery staple");
        Assert.False(_hasher.Verify("wrong password", hash));
    }

    [Theory]
    [InlineData("not-a-valid-hash")]
    [InlineData("100000.onlytwoparts")]
    [InlineData("notanumber.c2FsdA==.aGFzaA==")]
    [InlineData("100000.not-valid-base64!!!.aGFzaA==")]
    [InlineData("")]
    public void Verify_MalformedStoredHash_FailsClosedInsteadOfThrowing(string storedHash)
        => Assert.False(_hasher.Verify("anything", storedHash));

    [Fact]
    public void Hash_SameInputTwice_ProducesDifferentSalts()
    {
        // Distinct salts are what make the stored hashes unlinkable even for identical passwords.
        var a = _hasher.Hash("same password");
        var b = _hasher.Hash("same password");
        Assert.NotEqual(a, b);
    }
}
