using Microsoft.AspNetCore.Identity;
using TodoListApp.Models;
using BC = BCrypt.Net.BCrypt;

namespace TodoListApp.Helpers
{
    public class LegacyPasswordHasher : PasswordHasher<ApplicationUser>
    {
        public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
        {
            // Identity's standard hashes don't start with $2a$ (BCrypt signature)
            if (hashedPassword.StartsWith("$2a$"))
            {
                if (BC.Verify(providedPassword, hashedPassword))
                {
                    // Success, but we want to upgrade to Identity's standard
                    return PasswordVerificationResult.SuccessRehashNeeded;
                }
                return PasswordVerificationResult.Failed;
            }

            // Fallback to standard Identity hashing for new/rehashed passwords
            return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
