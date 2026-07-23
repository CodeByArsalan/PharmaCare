using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PharmaCare.Infrastructure;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

// ── CONFIG ───────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

var connStr = config.GetConnectionString("PharmaCareDBConnectionString")!;
var dbOptions = new DbContextOptionsBuilder<PharmaCareDBContext>()
    .UseSqlServer(connStr, o => o.CommandTimeout(300))
    .Options;

int scale = args.Contains("--scale")
    ? int.Parse(args[Array.IndexOf(args, "--scale") + 1])
    : 300;

Console.WriteLine($"=== PharmaCare Load Test Seeder ===");
Console.WriteLine($"Scale: {scale} | DB: PharmaCareDB");
Console.WriteLine("Starting in 3 seconds...");
await Task.Delay(3000);

// Seed everything under the default pharmacy (tenant 1) so the DbContext stamps/filters correctly.
using var db = new PharmaCareDBContext(dbOptions, new LoadTestTenant(1));
var rng = new Random(42);

// ── HELPERS ──────────────────────────────────────────────
decimal RandDec(decimal min, decimal max) =>
    Math.Round(min + (decimal)rng.NextDouble() * (max - min), 2);
DateTime RandDate(int daysBack = 365) =>
    AppTime.Now.AddDays(-rng.Next(1, daysBack));

async Task SaveBatch<T>(List<T> items, string label) where T : class
{
    const int batch = 200;
    for (int i = 0; i < items.Count; i += batch)
    {
        db.Set<T>().AddRange(items.Skip(i).Take(batch));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Console.Write($"\r  {label}: {Math.Min(i + batch, items.Count)}/{items.Count}   ");
    }
    Console.WriteLine();
}

// ════════════════════════════════════════════════════════
// READ EXISTING REAL DATA FROM YOUR DB
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Reading existing DB data...]");

// Use actual account IDs from your DB
// CASH=1, BANK=2, COGS=3, DMG=4, GEN=5, SALE(Revenue)=6, STK(Inventory)=7, AR=10+, AP=9+
var existingAccounts = db.Accounts.Include(a => a.AccountType).ToList();

int GetAccByCode(string code)
{
    var acc = existingAccounts.FirstOrDefault(a => a.AccountType?.Code == code);
    return acc?.AccountID ?? existingAccounts.First().AccountID;
}

var cashAccId = GetAccByCode("CASH");  // ID=1: CASH
var bankAccId = GetAccByCode("BANK");  // ID=2: HBL BANK
var arAccId   = GetAccByCode("AR");    // ID=10/11: AR accounts
var apAccId   = GetAccByCode("AP");    // ID=9/12: AP accounts
var invAccId  = GetAccByCode("STK");   // ID=7: INVENTORY STOCK ACCOUNT
var salAccId  = GetAccByCode("SALE");  // ID=6: INVENTORY SALES REVENUE
var cogsAccId = GetAccByCode("COGS");  // ID=3: Inventory COGS Sold
var dmgAccId  = GetAccByCode("DMG");   // ID=4: Damaged COGS
var expAccId  = GetAccByCode("GEN");   // ID=5/8: General account for expenses

Console.WriteLine($"  Cash={cashAccId} Bank={bankAccId} AR={arAccId} AP={apAccId}");
Console.WriteLine($"  INV={invAccId}   SAL={salAccId}  COGS={cogsAccId} EXP={expAccId}");

// Use existing categories (Medicines=1, Tablets=2, Syrups=3, Injections=4, CardioVascular=6)
var existingCategories = db.Categories.ToList();
if (!existingCategories.Any())
{
    Console.WriteLine("ERROR: No categories found. Please add at least one category in the app first.");
    return;
}
Console.WriteLine($"  Categories: {existingCategories.Count} found");

// Get existing VoucherTypes
var jvTypeId = db.VoucherTypes.FirstOrDefault(v => v.Code == "JV")?.VoucherTypeID;
if (jvTypeId == null)
{
    // Insert JV type if missing
    var jv = new VoucherType { Code = "JV", Name = "Journal Voucher" };
    db.VoucherTypes.Add(jv);
    await db.SaveChangesAsync();
    jvTypeId = jv.VoucherTypeID;
}

// Get existing TransactionTypes
var grnTxId  = db.TransactionTypes.FirstOrDefault(t => t.Code == "GRN")?.TransactionTypeID;
var saleTxId = db.TransactionTypes.FirstOrDefault(t => t.Code == "SALE")?.TransactionTypeID;

if (grnTxId == null || saleTxId == null)
{
    Console.WriteLine("ERROR: TransactionTypes GRN and SALE not found.");
    Console.WriteLine("Please run the application once first to seed TransactionTypes.");
    return;
}
Console.WriteLine($"  GRN TxType={grnTxId} | SALE TxType={saleTxId} | JV VoucherType={jvTypeId}");

// ════════════════════════════════════════════════════════
// STEP 1 — SubCategories (use existing categories)
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 1] Sub-Categories...");
var existingSubs = db.SubCategories.ToList();
if (existingSubs.Count < 10)
{
    var subsToAdd = new List<SubCategory>();
    var subNames = new[] { "Type A", "Type B", "Type C", "Type D" };
    foreach (var cat in existingCategories)
        foreach (var sn in subNames)
        {
            var fullName = $"{cat.Name} - {sn}";
            if (!existingSubs.Any(s => s.Name == fullName && s.Category_ID == cat.CategoryID))
                subsToAdd.Add(new SubCategory {
                    Name = fullName, Category_ID = cat.CategoryID,
                    IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1
                });
        }
    if (subsToAdd.Count > 0)
        await SaveBatch(subsToAdd, "SubCategories");
}
db.ChangeTracker.Clear();
var allSubs = db.SubCategories.ToList();
Console.WriteLine($"  Total SubCategories: {allSubs.Count}");

// ════════════════════════════════════════════════════════
// STEP 2 — Expense Categories
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 2] Expense Categories...");
if (!db.ExpenseCategories.Any())
{
    db.ExpenseCategories.AddRange(
        new ExpenseCategory { Name = "Utilities",  DefaultExpenseAccount_ID = expAccId, IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1 },
        new ExpenseCategory { Name = "Rent",        DefaultExpenseAccount_ID = expAccId, IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1 },
        new ExpenseCategory { Name = "Salaries",    DefaultExpenseAccount_ID = expAccId, IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1 },
        new ExpenseCategory { Name = "Transport",   DefaultExpenseAccount_ID = expAccId, IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1 },
        new ExpenseCategory { Name = "Marketing",   DefaultExpenseAccount_ID = expAccId, IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1 }
    );
    await db.SaveChangesAsync();
    Console.WriteLine("  ExpenseCategories: 5 added");
}
db.ChangeTracker.Clear();
var expCatIds = db.ExpenseCategories.Select(e => e.ExpenseCategoryID).ToList();

// ════════════════════════════════════════════════════════
// STEP 3 — Products
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 3] Products...");
int productCount = Math.Max(100, scale / 3);
int existingProductCount = db.Products.Count();

if (existingProductCount < productCount)
{
    int[] packSizes = { 1, 5, 10, 30 };
    var prods = new List<Product>();
    int toAdd = productCount - existingProductCount;
    for (int i = existingProductCount + 1; i <= existingProductCount + toAdd; i++)
    {
        var cat = existingCategories[rng.Next(existingCategories.Count)];
        var sub = allSubs.Where(s => s.Category_ID == cat.CategoryID).OrderBy(_ => rng.Next()).FirstOrDefault()
                  ?? allSubs.First();
        prods.Add(new Product {
            Name = $"Test Product {i:D5}", ShortCode = $"TP{i:D5}",
            Category_ID = cat.CategoryID, SubCategory_ID = sub.SubCategoryID,
            OpeningPrice = RandDec(10, 500), OpeningQuantity = rng.Next(50, 1000),
            ReorderLevel = rng.Next(10, 50),
            UnitsInPack = packSizes[rng.Next(packSizes.Length)],
            IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1
        });
    }
    await SaveBatch(prods, "Products");
}
db.ChangeTracker.Clear();
var productIds = db.Products.Select(p => p.ProductID).ToList();
Console.WriteLine($"  Total Products: {productIds.Count}");

// ════════════════════════════════════════════════════════
// STEP 4 — Parties (Customers & Suppliers)
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 4] Parties...");
int existingPartyCount = db.Parties.Count();
int targetPartyCount   = scale;

if (existingPartyCount < targetPartyCount)
{
    var partiesToAdd = new List<Party>();
    int existingCusts = db.Parties.Count(p => p.PartyType == "Customer");
    int existingSupps = db.Parties.Count(p => p.PartyType == "Supplier");
    int addCusts = Math.Max(0, scale / 2 - existingCusts);
    int addSupps = Math.Max(0, scale / 2 - existingSupps);

    for (int i = existingCusts + 1; i <= existingCusts + addCusts; i++)
        partiesToAdd.Add(new Party {
            Name = $"Test Customer {i:D4}", PartyType = "Customer",
            Phone = $"03{rng.Next(100000000, 999999999)}",
            IsWholeSale = i % 5 == 0,
            CreditLimit = RandDec(10000, 200000),
            Account_ID = arAccId,
            IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1
        });

    for (int i = existingSupps + 1; i <= existingSupps + addSupps; i++)
        partiesToAdd.Add(new Party {
            Name = $"Test Supplier {i:D4}", PartyType = "Supplier",
            Phone = $"03{rng.Next(100000000, 999999999)}",
            Account_ID = apAccId,
            IsActive = true, CreatedAt = AppTime.Now, CreatedBy = 1
        });

    if (partiesToAdd.Count > 0)
        await SaveBatch(partiesToAdd, "Parties");
}
db.ChangeTracker.Clear();
var customerIds = db.Parties.Where(p => p.PartyType == "Customer" && p.IsActive).Select(p => p.PartyID).ToList();
var supplierIds = db.Parties.Where(p => p.PartyType == "Supplier" && p.IsActive).Select(p => p.PartyID).ToList();
Console.WriteLine($"  Customers: {customerIds.Count} | Suppliers: {supplierIds.Count}");

// ════════════════════════════════════════════════════════
// STEP 5 — GRN Purchases
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 5] GRN Purchases...");
int existingGrns   = db.StockMains.Count(s => s.TransactionType_ID == grnTxId);
int targetGrns     = scale;
int grnsToCreate   = Math.Max(0, targetGrns - existingGrns);

// Generate unique TransactionNo based on current max
long vNo = db.Vouchers.Any()    ? (long)db.Vouchers.Max(v => v.VoucherID)    + 1000 : 1000;
long tNo = db.StockMains.Any()  ? (long)db.StockMains.Max(s => s.StockMainID) + 1000 : 1000;

Console.WriteLine($"  Creating {grnsToCreate} GRNs (existing: {existingGrns})...");
for (int i = 0; i < grnsToCreate; i++)
{
    var suppId    = supplierIds[rng.Next(supplierIds.Count)];
    var txDate    = RandDate(365);
    var lineCount = rng.Next(1, 6);
    var selProds  = productIds.OrderBy(_ => rng.Next()).Take(lineCount).ToList();

    var details = new List<StockDetail>();
    decimal sub = 0;
    foreach (var pid in selProds)
    {
        var qty  = rng.Next(10, 200);
        var cost = RandDec(10, 300);
        var lt   = Math.Round(qty * cost, 2);
        sub += lt;
        details.Add(new StockDetail {
            Product_ID = pid, Quantity = qty,
            UnitPrice = cost, CostPrice = cost,
            LineTotal = lt, LineCost = lt
        });
    }
    var total = sub;
    var paid  = rng.Next(0, 2) == 0 ? total : Math.Round(total * (decimal)rng.NextDouble(), 2);
    var bal   = total - paid;

    // GRN Double-Entry:
    //   DR: Stock/Inventory Account (invAccId=7)  — asset increases
    //   CR: Supplier Account (AP)                 — liability increases
    var supplierAccId = db.Parties
        .Where(p => p.PartyID == suppId)
        .Select(p => p.Account_ID)
        .FirstOrDefault() ?? apAccId;

    var voucher = new Voucher {
        VoucherNo = $"GV-LD-{vNo++:D8}", VoucherDate = txDate,
        VoucherType_ID = jvTypeId.Value, Status = "Posted",
        TotalDebit = total, TotalCredit = total,
        SourceTable = "StockMain", Narration = $"Load Test GRN",
        CreatedBy = 1, CreatedAt = txDate,
        VoucherDetails = new List<VoucherDetail>
        {
            new VoucherDetail { Account_ID = invAccId,      DebitAmount = total, CreditAmount = 0,     Description = "Inventory purchase" },
            new VoucherDetail { Account_ID = supplierAccId, DebitAmount = 0,     CreditAmount = total, Description = "Supplier payable", Party_ID = suppId }
        }
    };
    db.StockMains.Add(new StockMain {
        TransactionType_ID = grnTxId.Value,
        TransactionNo      = $"LD-GRN-{tNo++:D8}",
        TransactionDate    = txDate, Party_ID = suppId,
        SubTotal = sub, DiscountPercent = 0, DiscountAmount = 0,
        TotalAmount = total, PaidAmount = paid, BalanceAmount = bal,
        Status = "Approved",
        PaymentStatus = paid >= total ? "Paid" : paid > 0 ? "Partial" : "Unpaid",
        Voucher = voucher, StockDetails = details,
        CreatedBy = 1, CreatedAt = txDate
    });

    if (i % 100 == 0)
    {
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Console.Write($"\r  GRN: {i}/{grnsToCreate}   ");
    }
}
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
Console.WriteLine($"\r  GRN: {grnsToCreate}/{grnsToCreate} ✓");

// ════════════════════════════════════════════════════════
// STEP 6 — Sales
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 6] Sales...");
int existingSales  = db.StockMains.Count(s => s.TransactionType_ID == saleTxId);
int targetSales    = scale * 3;
int salesToCreate  = Math.Max(0, targetSales - existingSales);
Console.WriteLine($"  Creating {salesToCreate} sales (existing: {existingSales})...");

for (int i = 0; i < salesToCreate; i++)
{
    var custId    = customerIds[rng.Next(customerIds.Count)];
    var txDate    = RandDate(365);
    var lineCount = rng.Next(1, 7);
    var selProds  = productIds.OrderBy(_ => rng.Next()).Take(lineCount).ToList();

    var details = new List<StockDetail>();
    decimal sub = 0;
    foreach (var pid in selProds)
    {
        var qty  = rng.Next(1, 15);
        var unit = RandDec(20, 600);
        var cost = Math.Round(unit * 0.7m, 2);
        var lt   = Math.Round(qty * unit, 2);
        sub += lt;
        details.Add(new StockDetail {
            Product_ID = pid, Quantity = qty,
            UnitPrice = unit, CostPrice = cost,
            LineTotal = lt, LineCost = Math.Round(qty * cost, 2)
        });
    }
    var discPct = rng.Next(0, 8) == 0 ? rng.Next(1, 6) : 0;
    var disc    = Math.Round(sub * discPct / 100, 2);
    var total   = Math.Round(sub - disc, 2);
    var paid    = rng.Next(0, 5) == 0 ? Math.Round(total * 0.5m, 2) : total;
    var bal     = total - paid;

    // Sale Double-Entry (Gross Method, matching SaleService):
    //   DR: Customer Account (AR)           = TotalAmount (what they owe)
    //   DR: COGS Account                    = totalLineCost
    //   CR: Sales Revenue Account           = SubTotal (gross revenue)
    //   CR: Stock/Inventory Account         = totalLineCost
    var customerAccId = db.Parties
        .Where(p => p.PartyID == custId)
        .Select(p => p.Account_ID)
        .FirstOrDefault() ?? arAccId;

    var totalLineCost = details.Sum(d => d.LineCost);
    var saleDebit  = total + totalLineCost;   // Customer + COGS
    var saleCredit = sub   + totalLineCost;   // Revenue  + Stock reduction

    var saleVoucherDetails = new List<VoucherDetail>
    {
        new VoucherDetail { Account_ID = customerAccId, DebitAmount = total,         CreditAmount = 0,             Description = "Sale receivable", Party_ID = custId },
        new VoucherDetail { Account_ID = cogsAccId,     DebitAmount = totalLineCost, CreditAmount = 0,             Description = "Cost of goods sold" },
        new VoucherDetail { Account_ID = salAccId,      DebitAmount = 0,             CreditAmount = sub,           Description = "Sales revenue" },
        new VoucherDetail { Account_ID = invAccId,      DebitAmount = 0,             CreditAmount = totalLineCost, Description = "Inventory reduction" }
    };
    // Add discount line if applicable
    if (disc > 0)
        saleVoucherDetails.Add(new VoucherDetail { Account_ID = expAccId, DebitAmount = disc, CreditAmount = 0, Description = "Discount allowed" });

    var voucher = new Voucher {
        VoucherNo = $"SV-LD-{vNo++:D8}", VoucherDate = txDate,
        VoucherType_ID = jvTypeId.Value, Status = "Posted",
        TotalDebit = saleDebit, TotalCredit = saleCredit,
        SourceTable = "StockMain", Narration = $"Load Test Sale",
        CreatedBy = 1, CreatedAt = txDate,
        VoucherDetails = saleVoucherDetails
    };
    db.StockMains.Add(new StockMain {
        TransactionType_ID = saleTxId.Value,
        TransactionNo      = $"LD-SL-{tNo++:D8}",
        TransactionDate    = txDate, Party_ID = custId,
        SubTotal = sub, DiscountPercent = discPct, DiscountAmount = disc,
        TotalAmount = total, PaidAmount = paid, BalanceAmount = bal,
        Status = "Approved",
        PaymentStatus = paid >= total ? "Paid" : paid > 0 ? "Partial" : "Unpaid",
        Voucher = voucher, StockDetails = details,
        CreatedBy = 1, CreatedAt = txDate
    });

    if (i % 150 == 0)
    {
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Console.Write($"\r  Sales: {i}/{salesToCreate}   ");
    }
}
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
Console.WriteLine($"\r  Sales: {salesToCreate}/{salesToCreate} ✓");

// ════════════════════════════════════════════════════════
// STEP 7 — Customer Receipts (for partial sales)
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 7] Customer Receipts...");
var rvTypeId = db.VoucherTypes.FirstOrDefault(v => v.Code == "RV")?.VoucherTypeID ?? jvTypeId.Value;

var unpaidSales = db.StockMains
    .Where(s => s.TransactionType_ID == saleTxId
             && s.PaymentStatus != "Paid"
             && s.Status == "Approved"
             && s.Party_ID != null
             && s.BalanceAmount > 0)
    .Select(s => new { s.StockMainID, s.Party_ID, s.BalanceAmount })
    .Take(scale)
    .ToList();

var receipts = new List<Payment>();
foreach (var sale in unpaidSales)
{
    var pDate  = AppTime.Now.AddDays(-rng.Next(0, 60));
    var method = rng.Next(0, 2) == 0 ? "Cash" : "Bank";
    // Receipt Double-Entry:
    //   DR: Cash/Bank Account    — money received
    //   CR: Customer Account     — reduces receivable
    var custAccId2 = db.Parties
        .Where(p => p.PartyID == sale.Party_ID)
        .Select(p => p.Account_ID)
        .FirstOrDefault() ?? arAccId;

    var pmtAccId = method == "Cash" ? cashAccId : bankAccId;
    var rvVoucher = new Voucher {
        VoucherNo = $"RV-LD-{vNo++:D8}", VoucherDate = pDate,
        VoucherType_ID = rvTypeId, Status = "Posted",
        TotalDebit = sale.BalanceAmount, TotalCredit = sale.BalanceAmount,
        SourceTable = "Payment", CreatedBy = 1, CreatedAt = pDate,
        VoucherDetails = new List<VoucherDetail>
        {
            new VoucherDetail { Account_ID = pmtAccId,   DebitAmount = sale.BalanceAmount, CreditAmount = 0,                   Description = "Cash/Bank received" },
            new VoucherDetail { Account_ID = custAccId2, DebitAmount = 0,                  CreditAmount = sale.BalanceAmount, Description = "Customer payment", Party_ID = sale.Party_ID }
        }
    };
    db.Vouchers.Add(rvVoucher);
    receipts.Add(new Payment {
        PaymentType   = PaymentType.RECEIPT.ToString(),
        Party_ID      = sale.Party_ID!.Value,
        StockMain_ID  = sale.StockMainID,
        Account_ID    = pmtAccId,
        Amount        = sale.BalanceAmount,
        PaymentDate   = pDate, PaymentMethod = method,
        IsVoided      = false, Voucher = rvVoucher,
        CreatedBy = 1, CreatedAt = pDate
    });
}
await SaveBatch(receipts, "CustomerReceipts");

// ════════════════════════════════════════════════════════
// STEP 8 — Expenses
// ════════════════════════════════════════════════════════
Console.WriteLine("\n[Step 8] Expenses...");
int existingExpenses = db.Expenses.Count();
int expToCreate = Math.Max(0, scale / 2 - existingExpenses);

if (expToCreate > 0 && expCatIds.Count > 0)
{
    var expenses = new List<Expense>();
    for (int i = 1; i <= expToCreate; i++)
    {
        var d = RandDate(365);
        expenses.Add(new Expense {
            ExpenseCategory_ID = expCatIds[rng.Next(expCatIds.Count)],
            Description        = $"Load Test Expense {i:D5}",
            Amount             = RandDec(500, 50000),
            ExpenseDate        = d,
            SourceAccount_ID   = rng.Next(0, 2) == 0 ? cashAccId : bankAccId,
            ExpenseAccount_ID  = expAccId,
            Status             = rng.Next(0, 2) == 0 ? TransactionStatus.Approved : TransactionStatus.Draft,
            CreatedBy = 1, CreatedAt = d
        });
    }
    await SaveBatch(expenses, "Expenses");
}

// ════════════════════════════════════════════════════════
// FINAL SUMMARY
// ════════════════════════════════════════════════════════
db.ChangeTracker.Clear();
Console.WriteLine("\n========== SEEDING COMPLETE ==========");
Console.WriteLine($"  Products:   {db.Products.Count()}");
Console.WriteLine($"  Parties:    {db.Parties.Count()} ({db.Parties.Count(p => p.PartyType == "Customer")} customers, {db.Parties.Count(p => p.PartyType == "Supplier")} suppliers)");
Console.WriteLine($"  Vouchers:   {db.Vouchers.Count()}");
Console.WriteLine($"  GRNs:       {db.StockMains.Count(s => s.TransactionType_ID == grnTxId)}");
Console.WriteLine($"  Sales:      {db.StockMains.Count(s => s.TransactionType_ID == saleTxId)}");
Console.WriteLine($"  Payments:   {db.Payments.Count()}");
Console.WriteLine($"  Expenses:   {db.Expenses.Count()}");
Console.WriteLine("======================================");

// ── TENANT CONTEXT (multi-tenancy) ───────────────────────
// Fixed tenant used by the seeder so PharmaCareDBContext stamps Pharmacy_ID on inserts and
// filters reads to a single pharmacy.
sealed class LoadTestTenant : PharmaCare.Application.Interfaces.Tenancy.ICurrentTenant
{
    private readonly int _id;
    public LoadTestTenant(int id) => _id = id;
    public int? TenantId => _id;
    public bool HasValue => true;
    public void SetTenant(int pharmacyId) { }
    public IDisposable BeginScope(int pharmacyId) => new Noop();
    private sealed class Noop : IDisposable { public void Dispose() { } }
}
