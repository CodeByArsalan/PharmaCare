using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Web.Models.Inventory;

public class InitiateStockTakeViewModel
{
    [Required(ErrorMessage = "Please select a store")]
    [Display(Name = "Store")]
    public int Store_ID { get; set; }

    [Display(Name = "Category (Optional)")]
    public int? CategoryID { get; set; }

    public string? Remarks { get; set; }
}
