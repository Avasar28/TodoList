using Fido2NetLib;
using Fido2NetLib.Objects;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface IWebAuthnService
    {
        Task<CredentialCreateOptions> GenerateRegistrationOptions(ApplicationUser user);
        Task<(bool Success, string Message)> ValidateRegistrationResponse(ApplicationUser user, AuthenticatorAttestationRawResponse attestationResponse, string deviceName = null);
        Task<AssertionOptions> GenerateAuthenticationOptions(ApplicationUser user);
        Task<(bool Success, string Message, uint? NewSignCount)> ValidateAuthenticationResponse(ApplicationUser user, AuthenticatorAssertionRawResponse assertionResponse);
        Task<List<UserWebAuthnCredential>> GetCredentialsAsync(string userId);
        Task<bool> RemoveCredentialAsync(int credentialId, string userId);
    }
}
