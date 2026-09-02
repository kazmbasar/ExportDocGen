using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ExportDocGen.Auth;

/// <summary>Checks a submitted password against the configured shared credential.</summary>
public sealed class PasswordAuthenticator(
    IOptions<AuthOptions> options,
    ILogger<PasswordAuthenticator> logger)
{
    private readonly AuthOptions _options = options.Value;

    public string UserName => string.IsNullOrWhiteSpace(_options.UserName) ? "Team" : _options.UserName;

    /// <summary>True when a usable credential is configured; false means nobody can sign in.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.PasswordHash) || !string.IsNullOrEmpty(_options.Password);

    public bool Verify(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (!string.IsNullOrWhiteSpace(_options.PasswordHash))
            return PasswordHash.Verify(password, _options.PasswordHash);

        if (!string.IsNullOrEmpty(_options.Password))
        {
            var a = Encoding.UTF8.GetBytes(password);
            var b = Encoding.UTF8.GetBytes(_options.Password);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        logger.LogError(
            "No Auth:PasswordHash or Auth:Password configured — every login attempt is rejected.");
        return false;
    }
}
