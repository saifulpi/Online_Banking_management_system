using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
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