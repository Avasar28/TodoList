using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Helpers
{
    public class AuthorizeFeatureAttribute : TypeFilterAttribute
    {
        public AuthorizeFeatureAttribute(string technicalName) : base(typeof(FeatureAuthorizationFilter))
        {
            Arguments = new object[] { technicalName };
        }
    }

    public class FeatureAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly string _technicalName;
        private readonly IFeatureService _featureService;
        private readonly UserManager<ApplicationUser> _userManager;

        public FeatureAuthorizationFilter(string technicalName, IFeatureService featureService, UserManager<ApplicationUser> userManager)
        {
            _technicalName = technicalName;
            _featureService = featureService;
            _userManager = userManager;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = await _userManager.GetUserAsync(context.HttpContext.User);
            if (user == null)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var grantedFeatures = await _featureService.GetUserGrantedFeaturesAsync(user.Id);
            if (!grantedFeatures.Any(f => f.TechnicalName == _technicalName))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            }
        }
    }
}
