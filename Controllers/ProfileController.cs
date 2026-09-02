using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBankingSystem.Data;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IUserService _userService;

    public ProfileController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return RedirectToAction("Login", "Auth");

        var model = new ProfileViewModel
        {
            AccountNumber = user.AccountNumber,
            Role = user.Role,
            ProfilePictureUrl = user.ProfilePictureUrl,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            MobileNumber = user.MobileNumber,
            Address = user.Address
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return RedirectToAction("Login", "Auth");

        model.AccountNumber = user.AccountNumber;
        model.Role = user.Role;
        model.ProfilePictureUrl = user.ProfilePictureUrl;

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var picturePath = await HandleProfilePictureAsync(user.Id, model.ProfilePicture, model.RemoveProfilePicture);

            var updated = await _userService.UpdateProfileAsync(user.Id, model, picturePath);
            if (updated == null)
                return View(model);

            // Refresh the authentication cookie so the sidebar/header and other
            // claim-based displays immediately show the updated name and picture.
            await SignInUserAsync(updated);

            TempData["Success"] = "Your profile has been updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return null;
        return await _userService.GetUserByIdAsync(userId);
    }

    private async Task SignInUserAsync(AppUser user)
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
            new AuthenticationProperties { IsPersistent = false });
    }

    /// <summary>
    /// Applies the user's profile-picture choice and returns the resulting picture path.
    /// </summary>
    /// <param name="userId">Id of the current user (used to build per-user folder paths).</param>
    /// <param name="file">The optional uploaded image file.</param>
    /// <param name="removePicture">Whether the user asked to remove their current picture.</param>
    /// <returns>
    /// Relative URL path when a new picture was saved, empty string when it was removed,
    /// or <see langword="null"/> when the current picture should be left unchanged.
    /// </returns>
    private async Task<string?> HandleProfilePictureAsync(string userId, IFormFile? file, bool removePicture)
    {
        var uploadsRoot = ProfilePicturesRoot();
        Directory.CreateDirectory(uploadsRoot);

        if (removePicture)
        {
            DeleteProfilePictureFile(User.FindFirstValue("ProfilePictureUrl"));
            return string.Empty;
        }

        if (file is null || file.Length == 0)
            return null;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{userId}_{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write))
        {
            await file.CopyToAsync(stream);
        }

        DeleteProfilePictureFile(User.FindFirstValue("ProfilePictureUrl"));

        return $"/Profile/Picture/{fileName}";
    }

    /// <summary>
    /// Streams the current user's profile picture. Only files owned by the logged-in user
    /// (files whose name starts with the user's id) can be served, preventing path traversal.
    /// </summary>
    [HttpGet]
    public IActionResult Picture(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrWhiteSpace(id))
            return NotFound();

        // Reject anything that isn't a plain file name (blocks path traversal).
        if (id != Path.GetFileName(id))
            return NotFound();

        if (!id.StartsWith(userId + "_", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        var physicalPath = Path.Combine(ProfilePicturesRoot(), id);
        if (!System.IO.File.Exists(physicalPath))
            return NotFound();

        var ext = Path.GetExtension(id).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(physicalPath, contentType);
    }

    private void DeleteProfilePictureFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        var fileName = Path.GetFileName(relativePath);
        if (string.IsNullOrEmpty(fileName))
            return;

        var physicalPath = Path.Combine(ProfilePicturesRoot(), fileName);
        if (System.IO.File.Exists(physicalPath))
            System.IO.File.Delete(physicalPath);
    }

    private static string ProfilePicturesRoot() =>
        Path.Combine(AppContext.BaseDirectory, "uploads", "profile-pictures");
}