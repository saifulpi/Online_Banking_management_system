using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using OnlineBankingSystem.Data;

// Use Bangladeshi Taka (BDT) as the default currency throughout the application so
// that currency formatting (e.g. ToString("C")) renders the Taka sign (৳) instead of $.
var bdCulture = new CultureInfo("bn-BD");
// Keep Latin (Arabic) digits instead of Bengali numerals for readability.
bdCulture.NumberFormat.NativeDigits = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
CultureInfo.DefaultThreadCurrentCulture = bdCulture;
CultureInfo.DefaultThreadCurrentUICulture = bdCulture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Log email configuration on startup so Railway deploy logs show what the app sees.
// Password value is hidden — only shows whether it's set or not.
var log = app.Services.GetRequiredService<ILogger<Program>>();
var es = app.Configuration.GetSection("EmailSettings");
log.LogInformation("EmailSettings loaded: Host={Host} Port={Port} Username=[{Username}] FromEmail=[{FromEmail}] AppPasswordSet={PwSet}",
    es["Host"], es["Port"], es["Username"], es["FromEmail"],
    !string.IsNullOrWhiteSpace(es["AppPassword"]) ? "YES" : "NO");

// Correctly resolve the original scheme/host when running behind a TLS-terminating
// reverse proxy (e.g. Railway). This keeps HSTS/HTTPS redirection working without
// redirect loops. Railway's proxy addresses are dynamic, so we trust forwarded headers.
if (string.Equals(System.Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase) == false)
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        KnownIPNetworks = { },
        KnownProxies = { }
    });
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Seed default admin user on startup.
using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    await userService.EnsureAdminSeededAsync();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();