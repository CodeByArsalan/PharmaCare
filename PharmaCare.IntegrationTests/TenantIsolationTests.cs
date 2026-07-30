using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Tenant isolation is the single assumption everything else rests on: it is enforced by EF global
/// query filters plus write-time stamping, applied in one loop over every ITenantEntity. A leak
/// here would expose one pharmacy's trading data to another, so it is worth proving against a real
/// provider rather than trusting the loop by inspection.
/// </summary>
[Collection(Collections.Database)]
public class TenantIsolationTests
{
    private readonly DatabaseFixture _fixture;

    public TenantIsolationTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task One_pharmacy_cannot_see_another_pharmacys_products()
    {
        using var alpha = await _fixture.NewTenantAsync("Alpha");
        using var beta = await _fixture.NewTenantAsync("Beta");

        var alphaWorld = await alpha.SeedWorldAsync();
        await beta.SeedWorldAsync();

        var betaProducts = await beta.Get<IProductService>().GetAllAsync();

        Assert.DoesNotContain(betaProducts, p => p.ProductID == alphaWorld.Product.ProductID);
        Assert.All(await beta.Db.Products.ToListAsync(),
            p => Assert.Equal(beta.PharmacyId, p.Pharmacy_ID));
    }

    [Fact]
    public async Task One_pharmacys_sales_never_appear_in_anothers_reports()
    {
        using var alpha = await _fixture.NewTenantAsync("Alpha");
        using var beta = await _fixture.NewTenantAsync("Beta");

        var world = await alpha.SeedWorldAsync();
        await alpha.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await alpha.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 200m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        var alphaSummary = await alpha.Get<ISalesReportService>().GetDailySalesSummaryAsync(AppTime.Today);
        var betaSummary = await beta.Get<ISalesReportService>().GetDailySalesSummaryAsync(AppTime.Today);

        Assert.Equal(200m, alphaSummary.TotalSales);
        Assert.Equal(0m, betaSummary.TotalSales);
    }

    [Fact]
    public async Task Each_pharmacy_gets_its_own_chart_of_accounts()
    {
        using var alpha = await _fixture.NewTenantAsync("Alpha");
        using var beta = await _fixture.NewTenantAsync("Beta");

        var alphaAccounts = await alpha.Db.Accounts.Select(a => a.AccountID).ToListAsync();
        var betaAccounts = await beta.Db.Accounts.Select(a => a.AccountID).ToListAsync();

        Assert.NotEmpty(alphaAccounts);
        Assert.NotEmpty(betaAccounts);
        Assert.Empty(alphaAccounts.Intersect(betaAccounts));
    }

    [Fact]
    public async Task Inserting_a_tenant_row_with_no_tenant_in_context_is_refused()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();

        // A context built with NO ambient tenant. StampTenant must refuse the insert rather than
        // create an unowned financial record.
        var options = new DbContextOptionsBuilder<PharmaCare.Infrastructure.PharmaCareDBContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        using var untenanted = new PharmaCare.Infrastructure.PharmaCareDBContext(options, new NoTenant());

        untenanted.Products.Add(new Product
        {
            Name = "Orphan",
            SubCategory_ID = subCategory.SubCategoryID,
            Category_ID = category.CategoryID,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantData.TestUserId
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => untenanted.SaveChangesAsync());
        Assert.Contains("no current pharmacy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_row_cannot_be_moved_to_another_pharmacy()
    {
        using var alpha = await _fixture.NewTenantAsync("Alpha");
        using var beta = await _fixture.NewTenantAsync("Beta");

        var world = await alpha.SeedWorldAsync();

        // Try to reassign the product to the other pharmacy.
        var product = await alpha.Db.Products.FirstAsync(p => p.ProductID == world.Product.ProductID);
        product.Pharmacy_ID = beta.PharmacyId;
        await alpha.Db.SaveChangesAsync();

        // StampTenant marks Pharmacy_ID unmodified on updates, so the move is silently ignored.
        var reloaded = await alpha.Db.Products
            .AsNoTracking()
            .FirstAsync(p => p.ProductID == world.Product.ProductID);
        Assert.Equal(alpha.PharmacyId, reloaded.Pharmacy_ID);
    }

    private sealed class NoTenant : ICurrentTenant
    {
        public int? TenantId => null;
        public bool HasValue => false;
        public void SetTenant(int pharmacyId) { }
        public IDisposable BeginScope(int pharmacyId) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
