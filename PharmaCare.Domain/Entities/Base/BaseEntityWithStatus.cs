namespace PharmaCare.Domain.Entities.Base;

public abstract class BaseEntityWithStatus : BaseEntity
{
    public bool IsActive { get; set; } = true;
}
