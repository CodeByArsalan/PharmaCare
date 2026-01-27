namespace PharmaCare.Domain.Models.Base;

public class BaseModelWithStatus : BaseModel
{
    public bool IsActive { get; set; }
}
