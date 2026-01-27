using Microsoft.AspNetCore.Mvc.ModelBinding;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Domain.ViewModels;

public class HeadsViewModel
{
    public Head CurrentHead { get; set; } = new Head();
    [BindNever]
    public List<Head> HeadsList { get; set; } = new List<Head>();
    public bool IsEditMode { get; set; }
}
