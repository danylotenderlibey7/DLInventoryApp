using DLInventoryApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace DLInventoryApp.Middleware
{
    public class BlockedUserMiddleware
    {
        private readonly RequestDelegate _next;
        public BlockedUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user != null && user.IsBlocked)
                {
                    var path = context.Request.Path;
                    if (!path.StartsWithSegments("/Identity/Account/Blocked") &&
                        !path.StartsWithSegments("/Identity/Account/Login") &&
                        !path.StartsWithSegments("/Identity/Account/Logout"))
                    {
                        await signInManager.SignOutAsync();
                        context.Response.Redirect("/Identity/Account/Blocked");
                        return;
                    }
                }
            }
            await _next(context);
        }
    }
}
