using InfraHub.Core.Interfaces;
using InfraHub.Infrastructure.Auth;
using InfraHub.Infrastructure.Data;
using InfraHub.Web.Filters;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ──── EF Core (SQL Server) ────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ──── Authentication (Cookie-based, setara dengan Laravel session auth) ────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Shared/Auth/Login";
        options.LogoutPath = "/Shared/Auth/Logout";
        options.AccessDeniedPath = "/Shared/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "InfraHub.Auth";
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Tracking/Api") ||
                context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                context.Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

// ──── DI: Services ────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, CookieAuthService>();
builder.Services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
builder.Services.AddSingleton<IModuleRegistry, InfraHub.Infrastructure.Services.ModuleRegistry>();
builder.Services.AddScoped<InfraHub.Infrastructure.Services.ReportService>();

// ──── MVC with Filters ────
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<NoCacheFilter>();       // Prevent back-button cache
    options.Filters.Add<RoleBasedAccessFilter>(); // Role-based access per Area
});

var app = builder.Build();

// Respect Nginx / IIS Reverse Proxy X-Forwarded-Proto (HTTPS) and X-Forwarded-For headers
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// ──── Pipeline ────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".kml"] = "application/vnd.google-earth.kml+xml";
provider.Mappings[".kmz"] = "application/vnd.google-earth.kmz";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ──── Area Routes (Modular) ────
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// ──── Default Route (redirect ke login) ────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ──── Root redirect ke login ────
app.MapGet("/", context =>
{
    context.Response.Redirect("/Shared/Auth/Login");
    return Task.CompletedTask;
});

app.Run();
