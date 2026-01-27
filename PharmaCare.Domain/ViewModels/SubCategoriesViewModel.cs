using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.ViewModels;

public class SubCategoriesViewModel
{
    public SubCategory CurrentSubCategory { get; set; } = new SubCategory();

    [BindNever]
    public List<SubCategory> SubCategoryList { get; set; } = new List<SubCategory>();

    [BindNever]
    public SelectList Categories { get; set; } = new SelectList(Enumerable.Empty<SelectListItem>());

    public bool IsEditMode { get; set; }
}
