using JhipsterSampleApplication.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace JhipsterSampleApplication.Security;

public class BCryptPasswordHasher : IPasswordHasher<User>
{
    #nullable enable
    public string HashPassword(User? user, string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
    #nullable restore

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword,
        string providedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
    }
}
