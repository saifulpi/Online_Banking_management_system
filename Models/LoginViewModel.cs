using System.ComponentModel.DataAnnotations;

namespace OnlineBankingSystem.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Mobile number is required.")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}