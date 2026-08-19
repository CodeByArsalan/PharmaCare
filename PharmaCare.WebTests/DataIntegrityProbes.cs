using System.Net;

namespace PharmaCare.WebTests;

[Collection(WebCollection.Name)]
public class DataIntegrityProbes
{
    private const string TokenPage = "/Home/Index";
    private readonly WebTestFixture _fx;
    public DataIntegrityProbes(WebTestFixture fx) => _fx = fx;

    private Dictionary<string, string> ProductEditForm(TenantSeeding.SeededProduct p, bool isActive) => new()
    {
        ["ProductID"] = p.ProductId.ToString(),
        ["Name"] = "Edited Name",
        ["ShortCode"] = "SC1",
        ["Category_ID"] = p.CategoryId.ToString(),
        ["SubCategory_ID"] = p.SubCategoryId.ToString(),
        ["OpeningPrice"] = "10",
        ["UnitsInPack"] = "1",
        ["OpeningStockBoxes"] = "0",
        ["OpeningStockUnits"] = "0",
        ["ReorderLevel"] = "5",
        // The view hardcodes value="true" on the shadowed IsActive input; mimic that.
        ["IsActive"] = isActive ? "true" : "false",
    };

    // Probe 5a — ProductViewModel shadows IsActive. Editing an ACTIVE product must keep it active.
    [Fact]
    public async Task Editing_active_product_keeps_it_active()
    {
        var product = await _fx.SeedProductAsync(active: true);

        var client = await _fx.AdminClientAsync();

        var resp = await HttpTestHelpers.PostFormAsync(client, $"/Product/EditProduct/{product.EncryptedId}",
            ProductEditForm(product, isActive: true), tokenPath: TokenPage);
        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);

        Assert.True(await _fx.IsProductActiveAsync(product.ProductId),
            "DEFECT: editing an active product silently changed its IsActive (shadowed property not bound to the entity).");
    }

    // Probe 5b — Editing a DEACTIVATED product must NOT silently reactivate it.
    [Fact]
    public async Task Editing_deactivated_product_does_not_reactivate_it()
    {
        var product = await _fx.SeedProductAsync(active: true);
        await _fx.DeactivateProductAsync(product.ProductId);
        Assert.False(await _fx.IsProductActiveAsync(product.ProductId), "Setup: product should be inactive.");

        var client = await _fx.AdminClientAsync();

        await HttpTestHelpers.PostFormAsync(client, $"/Product/EditProduct/{product.EncryptedId}",
            ProductEditForm(product, isActive: true), tokenPath: TokenPage);

        Assert.False(await _fx.IsProductActiveAsync(product.ProductId),
            "DEFECT: editing a deactivated product silently reactivated it (hidden IsActive=true).");
    }

    // Probe 8 — RowVersion stripped = concurrency bypass. An edit POST that omits the RowVersion
    // token must be rejected (optimistic concurrency), not silently applied.
    [Fact]
    public async Task Edit_purchase_without_rowversion_is_rejected()
    {
        var purchase = await _fx.SeedPurchaseAsync();
        var originalRemarks = await _fx.GetPurchaseRemarksAsync(purchase.StockMainId);
        const string marker = "CONCURRENCY-BYPASS-MARKER";

        var client = await _fx.AdminClientAsync();

        var form = new Dictionary<string, string>
        {
            ["StockMainID"] = purchase.StockMainId.ToString(),
            // RowVersion deliberately omitted.
            ["Party_ID"] = purchase.PartyId.ToString(),
            ["TransactionDate"] = AppTime.Now.ToString("yyyy-MM-dd"),
            ["Remarks"] = marker,
            ["StockDetails[0].Product_ID"] = purchase.ProductId.ToString(),
            ["StockDetails[0].Quantity"] = "10",   // unchanged qty → no stock-reduction guard
            ["StockDetails[0].UnitPrice"] = "5",
            ["StockDetails[0].CostPrice"] = "5",
        };

        await HttpTestHelpers.PostFormAsync(client, "/Purchase/EditPurchase", form, tokenPath: TokenPage);

        var after = await _fx.GetPurchaseRemarksAsync(purchase.StockMainId);
        Assert.NotEqual(marker, after);
        Assert.Equal(originalRemarks, after); // untouched
    }

    // Probe 9 — RoleController.SavePermissions takes a raw roleId with no ownership check. A tenant-A
    // admin posting permissions for tenant B's role id must NOT write anything against B's role.
    [Fact]
    public async Task SavePermissions_cannot_target_another_tenants_role()
    {
        var tenantB = await _fx.ProvisionSecondTenantAsync();
        var ownersBefore = await _fx.RolePageOwnerPharmaciesAsync(tenantB.PharmacyId, tenantB.AdminRoleId);

        var client = await _fx.AdminClientAsync(); // tenant A admin

        var form = new Dictionary<string, string>
        {
            ["roleId"] = tenantB.AdminRoleId.ToString(),
            ["permissions[0].PageId"] = tenantB.SamplePageId.ToString(),
            ["permissions[0].CanView"] = "true",
            ["permissions[0].CanCreate"] = "true",
            ["permissions[0].CanEdit"] = "true",
            ["permissions[0].CanDelete"] = "true",
        };

        await HttpTestHelpers.PostFormAsync(client, "/Role/SavePermissions", form, tokenPath: TokenPage);

        var ownersAfter = await _fx.RolePageOwnerPharmaciesAsync(tenantB.PharmacyId, tenantB.AdminRoleId);

        // No pharmacy OTHER than B should now own a RolePage for B's role.
        Assert.DoesNotContain(_fx.PharmacyId, ownersAfter);
        Assert.Equal(ownersBefore.OrderBy(x => x), ownersAfter.OrderBy(x => x));
    }
}
