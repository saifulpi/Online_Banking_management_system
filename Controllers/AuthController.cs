using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBankingSystem.Data;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Controllers;

public class AuthController : Controller
{
    private readonly IUserService _userService;
    private readonly OtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, OtpService otpService, IEmailService emailService, ILogger<AuthController> logger)
    {
        _userService = userService;
        _otpService = otpService;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Signup()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return View(new SignupViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Signup(SignupViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var user = await _userService.RegisterAsync(model);
            if (user == null) return View(model);
            await SignInUserAsync(user);
            TempData["Success"] = $"Welcome, {user.FullName}! Your account has been created.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid)
            return View(model);

        var result = await _userService.ValidateLoginAsync(model.MobileNumber, model.Password);
        if (!result.Success || result.User == null)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        await SignInUserAsync(result.User, model.RememberMe);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid)
            return View(model);

        // Never reveal what failed or whether the email/phone exists. Always show the
        // same generic message whether the lookup fails or the pair doesn't match.
        var user = await _userService.GetUserByEmailAsync(model.Email);
        var byMobile = await _userService.GetUserByMobileAsync(model.MobileNumber);
        var isVerified = user != null &&
                         byMobile != null &&
                         string.Equals(user.Id, byMobile.Id, StringComparison.Ordinal);

        if (!isVerified)
        {
            ModelState.AddModelError(string.Empty, "The provided account information could not be verified.");
            return View(model);
        }

        // Email and phone both exist and belong to the same account. Send OTP to the email.
        var verifiedUser = user!;
        var code = _otpService.Generate(verifiedUser.Email);
        try
        {
            await _emailService.SendAsync(
                verifiedUser.Email,
                "Your Online Banking Verification Code",
                BuildOtpEmailBody(verifiedUser.FullName, code));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email} — {Message}", verifiedUser.Email, ex.Message);
        }

        TempData["ForgotMessage"] = "If an account exists with this email, a verification code has been sent.";
        return RedirectToAction(nameof(ResetPassword), new { email = model.Email.Trim() });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email = "")
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        var vm = new ResetPasswordViewModel { Email = email.Trim() };
        return View(vm);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid)
            return View(model);

        if (!_otpService.Verify(model.Email, model.Code, out var error))
        {
            ModelState.AddModelError(nameof(model.Code), error);
            return View(model);
        }

        var user = await _userService.GetUserByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "We couldn't complete the reset. Please request a new code.");
            return View(model);
        }

        await _userService.UpdatePasswordAsync(user.Id, model.NewPassword);
        _otpService.Invalidate(user.Email);

        TempData["Success"] = "Your password has been reset. Please sign in with your new password.";
        return RedirectToAction(nameof(Login));
    }

    private static string BuildOtpEmailBody(string fullName, string code)
    {
        return $"""
        <div style="font-family:Arial,sans-serif;max-width:520px;margin:0 auto;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden">
          <div style="background:#059669;padding:20px;text-align:center">
            <h2 style="margin:0;color:#ffffff;font-size:18px">Online Banking System</h2>
          </div>
          <div style="padding:28px;color:#0f172a">
            <p style="margin:0 0 16px">Hi {fullName},</p>
            <p style="margin:0 0 16px">Use the verification code below to reset your password. It expires in 5 minutes and can only be used once.</p>
            <div style="background:#f0fdf4;border:1px dashed #10b981;border-radius:8px;text-align:center;padding:16px;font-size:30px;font-weight:bold;letter-spacing:8px;color:#047857">{code}</div>
            <p style="margin:16px 0 0;font-size:13px;color:#64748b">If you didn't request this, you can safely ignore this email.</p>
          </div>
        </div>
        """;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInUserAsync(AppUser user, bool rememberMe = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.MobilePhone, user.MobileNumber),
            new("AccountNumber", user.AccountNumber),
            new("Email", user.Email),
            new("ProfilePictureUrl", user.ProfilePictureUrl),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = rememberMe });
    }
}