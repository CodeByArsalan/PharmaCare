using System.Net;
using System.Text.RegularExpressions;

namespace PharmaCare.WebTests;

[Collection(WebCollection.Name)]
public class TempDataProbe
{
    private const string TokenPage = "/Home/Index";
    private readonly WebTestFixture _fx;
    public TempDataProbe(WebTestFixture fx) => _fx = fx;

    // Probe 3 — TempData receipt bleed. AddSale stashes the tendered amount in TempData and Receipt
    // calls TempData.Keep, so the value survives into the NEXT receipt view — a different sale then
    // shows the previous sale's tendered/change. Uses a dedicated session so the bleed can't leak
    // into the shared admin client used by other probes.
    [Fact]
    public async Task Tendered_amount_does_not_bleed_between_receipts()
    {
        var purchase = await _fx.SeedPurchaseAsync();          // product with stock, cost 5
        var customerId = await _fx.SeedCustomerAsync();
        var cashAccountId = await _fx.CashAccountIdAsync();

        var client = _fx.Factory.CreateTestClient();
        await HttpTestHelpers.LoginOrThrowAsync(client, _fx.AdminEmail, _fx.AdminPassword);

        // 1. Real sale A over HTTP with a distinctive tendered amount (10). Total = 10.
        var addSale = new Dictionary<string, string>
        {
            ["TransactionDate"] = AppTime.Now.ToString("yyyy-MM-dd"),
            ["Party_ID"] = customerId.ToString(),
            ["TenderedAmount"] = "10",
            ["StockDetails[0].Product_ID"] = purchase.ProductId.ToString(),
            ["StockDetails[0].Quantity"] = "1",
            ["StockDetails[0].UnitPrice"] = "10",
            ["StockDetails[0].CostPrice"] = "5",
            ["PaymentAccountId"] = cashAccountId.ToString(),
        };
        var saleResp = await HttpTestHelpers.PostFormAsync(client, "/Sale/AddSale", addSale, tokenPath: TokenPage);
        Assert.True(saleResp.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Setup: AddSale should redirect to the receipt, got {(int)saleResp.StatusCode}: " +
            $"{await saleResp.Content.ReadAsStringAsync()}");

        // 2. View sale A's receipt (this is what re-persists the TempData via TempData.Keep).
        var receiptA = saleResp.Headers.Location!.ToString();
        var aBody = await client.GetStringAsync(receiptA);
        Assert.Equal("10.00", ExtractTendered(aBody)); // sanity: A shows its own tender

        // 3. A DIFFERENT sale B (total 20, paid 20) created without any HTTP/TempData.
        var encB = await _fx.SeedSaleViaServiceAsync(customerId, purchase.ProductId, cashAccountId,
            qty: 2, unitPrice: 10, paid: 20);

        // 4. View sale B's receipt in the same session. It must reflect B's own paid amount (20),
        //    not sale A's leftover tender (10).
        var bBody = await client.GetStringAsync($"/Sale/Receipt/{encB}");
        var tenderedOnB = ExtractTendered(bBody);

        client.Dispose();

        Assert.Equal("20.00", tenderedOnB);
    }

    private static string ExtractTendered(string html)
    {
        var m = Regex.Match(html, @"Tendered:\s*</td>\s*<td[^>]*>\s*([\d,]+\.\d{2})",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Replace(",", "") : "(not found)";
    }
}
