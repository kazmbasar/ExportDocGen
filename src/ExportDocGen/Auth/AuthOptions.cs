namespace ExportDocGen.Auth;

/// <summary>
/// The single shared login, bound from the <c>Auth</c> configuration section.
/// In production set <c>Auth__PasswordHash</c> (from <c>dotnet run -- hash-password</c>)
/// as an environment variable. <c>Auth:Password</c> is a plaintext convenience for
/// local development only.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Label shown after "Signed in as" — cosmetic only.</summary>
    public string UserName { get; set; } = "Team";

    /// <summary>PBKDF2 hash in <see cref="PasswordHash"/> format. Preferred.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Plaintext password — development only; ignored if a hash is set.</summary>
    public string? Password { get; set; }
}
