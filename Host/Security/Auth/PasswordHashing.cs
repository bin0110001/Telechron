using Microsoft.AspNetCore.Identity;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Security.Auth;

// Thin wrapper so callers don't need to know PasswordHasher<T> needs a "user"
// generic argument it doesn't actually use for our purposes.
public sealed class PasswordHashing
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string hash, string password) =>
        _hasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
}
