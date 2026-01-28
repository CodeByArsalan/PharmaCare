namespace PharmaCare.Domain.Entities.Base;

/// <summary>
/// Base entity with comprehensive audit trail and soft delete support.
/// All entities inherit from this class.
/// </summary>
public abstract class BaseEntity
{
    // Audit Trail
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    // Soft Delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
