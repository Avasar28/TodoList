using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TodoListApp.Models;

namespace TodoListApp.Helpers
{
    public class RequirePasskeyAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.GetUserAsync(httpContext.User);

            if (user != null && user.IsPasskeyEnabled)
            {
                // Check for a one-time verification token in a short-lived ephemeral cookie
                // Verification is required for EVERY entry into the module.
                // To allow the redirect FROM verification to work, we check for a token
                // and then IMMEDIATELY delete it (or it expires very fast).
                
                if (!httpContext.Request.Cookies.ContainsKey("PasskeyVerified"))
                {
                    var returnUrl = httpContext.Request.Path + httpContext.Request.QueryString;
                    context.Result = new RedirectToActionResult("Verify", "Passkey", new { returnUrl });
                    return;
                }

                // Delete the cookie immediately after passing the check to ensure 
                // subsequent navigations (re-entry) require verification again.
                // Note: This might be too strict if "per module" means "until you leave the controller".
                // But the user said "Always verify when entering" and "No session unlock".
                httpContext.Response.Cookies.Delete("PasskeyVerified");
            }

            await next();
        }
    }
}
