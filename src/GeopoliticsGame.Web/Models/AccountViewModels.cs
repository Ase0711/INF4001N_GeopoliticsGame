using System.ComponentModel.DataAnnotations;

namespace GeopoliticsGame.Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username or email.")]
    [StringLength(254)]
    [Display(Name = "Username or email")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Choose a username.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Usernames are 3–20 characters.")]
    [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Use only letters, numbers and underscores.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Passwords must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Use at least one letter and one number.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
