using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using OnlineBankingSystem.Validation;

namespace OnlineBankingSystem.Models;

public class ProfileViewModel
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    // Displayed profile picture relative path (loaded from the current user's DB record).
    public string ProfilePictureUrl { get; set; } = string.Empty;

    // Optional uploaded image file.
    [MaxFileSizeMb(2)]
    [AllowedImageExtensions(new[] { ".jpg", ".jpeg", ".png", ".webp" })]
    public IFormFile? ProfilePicture { get; set; }

    // True when the user chooses to remove their current picture.
    public bool RemoveProfilePicture { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^0\d{10}$", ErrorMessage = "Enter a valid 11-digit mobile number starting with 0.")]
    public string MobileNumber { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    public string Address { get; set; } = string.Empty;
}