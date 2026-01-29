using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces.Security;

/// <summary>
/// Repository interface for Page entity operations.
/// </summary>
public interface IPageRepository : IRepository<Page>
{
    /// <summary>
    /// Get all active pages ordered by parent and display order.
    /// </summary>
    Task<List<Page>> GetActiveOrderedAsync();
}
