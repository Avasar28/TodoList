using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using TodoListApp.Data;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class WebAuthnService : IWebAuthnService
    {
        private readonly IFido2 _fido2;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<WebAuthnService> _logger;

        public WebAuthnService(
            IFido2 fido2,
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<WebAuthnService> logger)
        {
            _fido2 = fido2;
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<CredentialCreateOptions> GenerateRegistrationOptions(ApplicationUser user)
        {
            var fidoUser = new Fido2User
            {
                DisplayName = user.Name ?? user.Email ?? "Unknown",
                Name = user.Email ?? user.Id,
                Id = Encoding.UTF8.GetBytes(user.Id)
            };

            var existingCredentials = await _context.WebAuthnCredentials
                .Where(c => c.UserId == user.Id)
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToListAsync();

            var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
            {
                User = fidoUser,
                ExcludeCredentials = existingCredentials,
                AuthenticatorSelection = new AuthenticatorSelection { 
                    UserVerification = UserVerificationRequirement.Required, 
                    ResidentKey = ResidentKeyRequirement.Discouraged 
                },
                AttestationPreference = AttestationConveyancePreference.None
            });

            _cache.Set($"fido2.regOptions.{user.Id}", options, TimeSpan.FromMinutes(5));
            return options;
        }

        public async Task<(bool Success, string Message)> ValidateRegistrationResponse(ApplicationUser user, AuthenticatorAttestationRawResponse attestationResponse, string deviceName = null)
        {
            if (!_cache.TryGetValue($"fido2.regOptions.{user.Id}", out CredentialCreateOptions options))
            {
                return (false, "Registration options expired or not found");
            }

            try
            {
                var result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
                {
                    AttestationResponse = attestationResponse,
                    OriginalOptions = options,
                    IsCredentialIdUniqueToUserCallback = async (args, cancellationToken) =>
                    {
                        return !await _context.WebAuthnCredentials.AnyAsync(c => c.CredentialId == args.CredentialId);
                    }
                });

                var credential = new UserWebAuthnCredential
                {
                    UserId = user.Id,
                    CredentialId = result.Id,
                    PublicKey = result.PublicKey,
                    SignCount = result.SignCount,
                    CreatedAt = DateTime.UtcNow,
                    DeviceName = deviceName ?? "Secondary Device"
                };

                _context.WebAuthnCredentials.Add(credential);
                await _context.SaveChangesAsync();

                _cache.Remove($"fido2.regOptions.{user.Id}");

                return (true, "Biometric credential registered successfully!");
            }
            catch (Fido2VerificationException ex)
            {
                return (false, "Verification failed: " + ex.Message);
            }
        }

        public async Task<AssertionOptions> GenerateAuthenticationOptions(ApplicationUser user)
        {
            var existingCredentials = await _context.WebAuthnCredentials
                .Where(c => c.UserId == user.Id)
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToListAsync();

            var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = existingCredentials,
                UserVerification = UserVerificationRequirement.Required
            });

            _cache.Set($"fido2.assertionOptions.{user.Id}", options, TimeSpan.FromMinutes(5));
            return options;
        }

        public async Task<(bool Success, string Message, uint? NewSignCount)> ValidateAuthenticationResponse(ApplicationUser user, AuthenticatorAssertionRawResponse assertionResponse)
        {
            if (!_cache.TryGetValue($"fido2.assertionOptions.{user.Id}", out AssertionOptions options))
            {
                return (false, "Assertion options expired or not found", null);
            }

            var dbCredential = await _context.WebAuthnCredentials
                .Where(c => c.UserId == user.Id)
                .ToListAsync() // Pull for byte[] comparison
                .ContinueWith(t => t.Result.FirstOrDefault(c => Enumerable.SequenceEqual(c.CredentialId, assertionResponse.RawId)));

            if (dbCredential == null)
            {
                return (false, "Credential not found in database", null);
            }

            try
            {
                var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
                {
                    AssertionResponse = assertionResponse,
                    OriginalOptions = options,
                    StoredPublicKey = dbCredential.PublicKey,
                    StoredSignatureCounter = dbCredential.SignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = (args, cancellationToken) => Task.FromResult(true)
                });

                dbCredential.SignCount = result.SignCount;
                await _context.SaveChangesAsync();

                _cache.Remove($"fido2.assertionOptions.{user.Id}");

                return (true, "Biometric unlock successful!", result.SignCount);
            }
            catch (Fido2VerificationException ex)
            {
                return (false, "Verification failed: " + ex.Message, null);
            }
        }

        public async Task<List<UserWebAuthnCredential>> GetCredentialsAsync(string userId)
        {
            return await _context.WebAuthnCredentials
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> RemoveCredentialAsync(int credentialId, string userId)
        {
            var credential = await _context.WebAuthnCredentials
                .FirstOrDefaultAsync(c => c.Id == credentialId && c.UserId == userId);

            if (credential == null) return false;

            var deviceName = credential.DeviceName ?? "Unknown Device";
            _context.WebAuthnCredentials.Remove(credential);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Biometric device removed. User: {UserId}, Device: {DeviceName}, CredentialId: {CredentialId}", 
                userId, deviceName, credentialId);

            return true;
        }
    }
}
