using Dx.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Dx.Infrastructure.Security;

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher (format V3: PBKDF2-HMACSHA512, 100 000
/// iterations) — the format mock-data/seed users are generated with.
/// </summary>
public sealed class IdentityPasswordHasher : Application.Auth.IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public bool Verify(User user, string password, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;
        var result = _inner.VerifyHashedPassword(user, user.PasswordHash, password);
        needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }

    public string Hash(User user, string password) => _inner.HashPassword(user, password);
}
