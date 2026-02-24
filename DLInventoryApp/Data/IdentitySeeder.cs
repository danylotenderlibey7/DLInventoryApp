using DLInventoryApp.Models;
using Microsoft.AspNetCore.Identity;

namespace DLInventoryApp.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = services.GetRequiredService<IConfiguration>();
            const string adminRole = "Admin";
            var emails = configuration.GetSection("Admin:Emails").Get<List<string>>();
            if (emails == null || !emails.Any()) return;
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join("; ", roleResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    throw new Exception($"Failed to create role '{adminRole}'. Errors: {errors}");
                }
            }
            foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()))
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null) continue;
                if (!await userManager.IsInRoleAsync(user, adminRole))
                {
                    var addResult = await userManager.AddToRoleAsync(user, adminRole);
                    if (!addResult.Succeeded)
                    {
                        var errors = string.Join("; ", addResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                        throw new Exception($"Failed to assign role '{adminRole}' to user '{email}'. Errors: {errors}");
                    }
                }
            }
        }
    }
}
