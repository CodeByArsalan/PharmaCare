using PharmaCare.Domain.Models.Membership;

namespace PharmaCare.Domain.ViewModels;

public class UserTypesViewModel
{
    public UserTypes CurrentUserType { get; set; } = new UserTypes();
    public List<UserTypes> UserTypesList { get; set; } = new List<UserTypes>();
    public bool IsEditMode { get; set; }
}
