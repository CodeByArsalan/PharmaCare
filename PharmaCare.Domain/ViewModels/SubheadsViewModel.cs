using Microsoft.AspNetCore.Mvc.ModelBinding;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Domain.ViewModels;

public class SubheadsViewModel
{
    public Subhead CurrentSubhead { get; set; } = new Subhead();
    [BindNever]
    public List<Subhead> SubheadsList { get; set; } = new List<Subhead>();
    public bool IsEditMode { get; set; }
}
