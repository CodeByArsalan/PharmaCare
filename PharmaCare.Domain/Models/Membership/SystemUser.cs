using Microsoft.AspNetCore.Identity;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.Models.Base;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Membership;

public class SystemUser : IdentityUser<int>
{
    [Required(ErrorMessage = "Required")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; }
    [Display(Name = "User Type")]
    [Required(ErrorMessage = "Required")]
    [ForeignKey("UserType")]
    public int UserType_ID { get; set; }
    public UserTypes UserType { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedDateTime { get; set; }
    public bool IsActive { get; set; }
    [ForeignKey("Store")]
    public int? Store_ID { get; set; }
    public Store? Store { get; set; }

    // Navigation property for UserPages
    public ICollection<UserPages>? UserPages { get; set; }
    [NotMapped]
    [Required(ErrorMessage = "Required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [NotMapped]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }
}
public class UserTypes : BaseModelWithStatus
{
    [Key]
    public int UserTypeID { get; set; }
    [Required(ErrorMessage = "Required")]
    [DisplayName("User Type")]
    public string UserType { get; set; }
}
public class UserWebPages
{
    [Key]
    public int UserWebPageID { get; set; }

    [ForeignKey("SystemUser")]
    public int SystemUser_ID { get; set; }
    public SystemUser SystemUser { get; set; }

    [ForeignKey("WebPage")]
    public int WebPage_ID { get; set; }
    public WebPages WebPage { get; set; }
}
[NotMapped]
public class UserPages
{
    public int WebPageID { get; set; }
    public int Parent_ID { get; set; }
    public int PageOrder { get; set; }
    public bool IsVisible { get; set; }
    public string? PageIcon { get; set; }
    public string? PageTitle { get; set; }
    public string? ControllerName { get; set; }
    public string? ViewName { get; set; }
    public string? Description { get; set; }
    public bool IsChecked { get; set; }
    public List<UserPages>? ChildPages { get; set; }
    public List<WebPageUrls>? WebPageUrls { get; set; }
}