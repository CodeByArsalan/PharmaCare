namespace PharmaCare.Domain.Entities.Base;

/// <summary>
/// Marks an entity as owned by a single pharmacy (tenant).
/// The <see cref="Pharmacy_ID"/> is applied automatically as an EF Core global query
/// filter (reads) and stamped on insert by the DbContext (writes), so no per-service
/// tenant filtering is required. Every per-pharmacy table implements this interface.
/// </summary>
public interface ITenantEntity
{
    int Pharmacy_ID { get; set; }
}
