namespace PharmaCare.Domain.Models.Configuration;

/// <summary>
/// Represents a pharmacy store/branch
/// </summary>
public class Store
{
    public int StoreID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Unique store code
}
