using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Application.DTOs.Security;

/// <summary>
/// View model for creating and editing users.
/// </summary>
public class UserViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "Roles")]
    public List<int> SelectedRoleIds { get; set; } = new();

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is a new user (for validation purposes).
    /// </summary>
    public bool IsNew => Id == 0;
}
