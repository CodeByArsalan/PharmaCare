using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Web.Models;

public class UserProfileViewModel
{
    [Required]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "User Type")]
    public string? UserType { get; set; }
}
