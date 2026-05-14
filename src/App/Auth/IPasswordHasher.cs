namespace App.Auth;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hashedPassword);

    /// <summary>
    /// A precomputed hash of a dummy input. Used to perform a constant-cost
    /// Verify on login paths where the user is missing, so the timing of the
    /// missing-user branch matches the existing-user branch.
    /// </summary>
    string DummyHash { get; }
}
