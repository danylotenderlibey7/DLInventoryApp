using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace DLInventoryApp.Controllers
{
    public class LanguageController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(string culture, string? returnUrl = null)
        {
            var supportedCultures = new[] { "en", "uk" };
            if (string.IsNullOrWhiteSpace(culture) || !supportedCultures.Contains(culture))
            {
                culture = "en";
            }
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = false,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax
                });
            if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = Url.Content("~/");
            }
            return LocalRedirect(returnUrl);
        }
    }
}