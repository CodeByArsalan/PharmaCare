namespace PharmaCare.Domain.Entities.Base;

/// <summary>
/// Base entity with IsActive status flag.
/// Use for master data that can be activated/deactivated.
/// </summary>
public abstract class BaseEntityWithStatus : BaseEntity
{
    public bool IsActive { get; set; } = true;
}
