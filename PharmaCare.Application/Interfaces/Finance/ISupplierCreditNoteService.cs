using PharmaCare.Domain.Entities.Finance;

namespace PharmaCare.Application.Interfaces.Finance;

/// <summary>
/// Service for managing supplier credit notes — financial credits provided by suppliers
/// (e.g. for damaged, short-shipped, or incorrectly priced goods). Stock is NOT affected.
/// </summary>
public interface ISupplierCreditNoteService
{
    Task<IEnumerable<SupplierCreditNote>> GetAllAsync();
    Task<IEnumerable<SupplierCreditNote>> GetOpenAsync(int? supplierId = null);
    Task<SupplierCreditNote?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a supplier credit note.
    /// DR: Supplier Payable (reduces what we owe) 
    /// CR: Supplier Credit Note Liability account (tracks the credit).
    /// </summary>
    Task<SupplierCreditNote> CreateAsync(SupplierCreditNote creditNote, int userId);

    /// <summary>Voids an open credit note and reverses the accounting entry.</summary>
    Task<bool> VoidAsync(int id, string reason, int userId);

    /// <summary>Applies an open credit note to an outstanding GRN.</summary>
    Task<bool> ApplyToGrnAsync(int cnId, int grnId, decimal amount, int userId);
}
