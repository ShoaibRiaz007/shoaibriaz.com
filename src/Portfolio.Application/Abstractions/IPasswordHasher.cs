namespace Portfolio.Application.Abstractions;

/// <summary>Hashes and verifies passwords. Implementation chooses the algorithm.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
