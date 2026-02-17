using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Controllers
{
    [Authorize]
    public class WebAuthnController : Controller
    {
        private readonly IWebAuthnService _webAuthnService;
        private readonly UserManager<ApplicationUser> _userManager;

        public WebAuthnController(
            IWebAuthnService webAuthnService,
            UserManager<ApplicationUser> userManager)
        {
            _webAuthnService = webAuthnService;
            _userManager = userManager;
        }

        [HttpPost]
        public async Task<JsonResult> MakeCredentialOptions()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found" });

            var options = await _webAuthnService.GenerateRegistrationOptions(user);
            return Json(options);
        }

        [HttpPost]
        public async Task<JsonResult> MakeCredential([FromBody] RegistrationRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found" });

            var result = await _webAuthnService.ValidateRegistrationResponse(user, request.AttestationResponse, request.DeviceName);
            return Json(new { success = result.Success, message = result.Message });
        }

        public class RegistrationRequest
        {
            public AuthenticatorAttestationRawResponse AttestationResponse { get; set; }
            public string DeviceName { get; set; }
        }

        [HttpPost]
        public async Task<JsonResult> MakeAssertionOptions()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found" });

            var options = await _webAuthnService.GenerateAuthenticationOptions(user);
            return Json(options);
        }

        [HttpPost]
        public async Task<JsonResult> MakeAssertion([FromBody] AuthenticatorAssertionRawResponse assertionResponse)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "User not found" });

            var result = await _webAuthnService.ValidateAuthenticationResponse(user, assertionResponse);

            if (result.Success)
            {
                // Set the ephemeral cookie to unlock the module
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    Expires = DateTimeOffset.UtcNow.AddSeconds(10)
                };
                Response.Cookies.Append("PasskeyVerified", "true", cookieOptions);
            }

            return Json(new { success = result.Success, message = result.Message });
        }
    }
}
