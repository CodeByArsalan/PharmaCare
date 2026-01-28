namespace PharmaCare.Domain.Entities.Base;

public abstract class BaseEntity
{
    // Audit Trail
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }


}
