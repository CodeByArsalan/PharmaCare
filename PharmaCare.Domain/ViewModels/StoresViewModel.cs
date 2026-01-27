using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.ViewModels;

public class StoresViewModel
{
    public Store CurrentStore { get; set; } = new Store();
    public List<Store> StoreList { get; set; } = new List<Store>();
    public bool IsEditMode { get; set; }
}
