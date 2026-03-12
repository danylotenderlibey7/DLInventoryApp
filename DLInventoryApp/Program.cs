using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using DLInventoryApp.Middleware;
using DLInventoryApp.Hubs;
using DLInventoryApp.Services.Tabs;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequiredLength = 1;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredUniqueChars = 0;
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] 
        ?? throw new Exception("Google ClientId not configured");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
         ?? throw new Exception("Google ClientSecret not configured");
    })
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"]
        ?? throw new Exception("Facebook AppId not configured");
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] 
        ?? throw new Exception("Facebook AppSecret not configured");
    });

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();
builder.Services.AddSignalR();
builder.Services.AddScoped<ICustomIdGenerator, CustomIdGenerator>();
builder.Services.AddScoped<CustomIdTabBuilder>();
builder.Services.AddScoped<FieldsTabBuilder>();
builder.Services.AddScoped<SettingsTabBuilder>();
builder.Services.AddScoped<AccessTabBuilder>();
builder.Services.AddScoped<ChatTabBuilder>();
builder.Services.AddScoped<ItemsTabBuilder>();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ILikeService, ItemLikeService>();
builder.Services.AddScoped<ISearchService, LuceneSearchService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Authentication:Brevo"));
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddSingleton<IMarkdownService, MarkdownService>();

var app = builder.Build();
var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("uk")
};
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
localizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new CookieRequestCultureProvider()
};

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

await IdentitySeeder.SeedAsync(app);
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization(localizationOptions);
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<BlockedUserMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inventories}/{action=Index}/{id?}");
app.MapHub<DiscussionHub>("/hubs/discussion");
app.MapRazorPages();

app.Run();
