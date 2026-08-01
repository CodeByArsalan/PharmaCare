# PharmaCare 💊

A multi-tenant pharmacy Point of Sale, inventory and accounting system for retail pharmacies.

Every stock movement is also an accounting event: a sale doesn't just decrement stock, it posts a
double-entry voucher (AR / Discount / Sales / COGS / Stock) alongside a separate cash or bank
receipt. Nothing is ever hard-deleted — transactions are voided and reversed.

---

## Scope

**What it does**

- **Point of sale** — retail and wholesale pricing, walk-in or account customers, part payment,
  printed receipt. Server-side gates on cost, discount and credit limit.
- **Purchasing** — Purchase Order → Goods Received Note → Purchase Return, with partial receiving,
  supplier advances and automatic advance offset.
- **Inventory** — stock derived from movement history (never a stored balance), reorder alerts,
  stock adjustments for damage/expiry/loss/bonus, dead-stock analysis.
- **Accounting** — per-tenant chart of accounts, automatic double-entry posting for every
  transaction, manual journal vouchers, financial-period locking.
- **Finance** — customer receipts and refunds, supplier payments, credit notes both directions,
  expenses with approval and budgets, reconciliation screens.
- **Reporting** — 20+ reports: P&L, cash flow, trial balance, general ledger, receivables/payables
  ageing, party ledgers, sales/purchase/inventory analysis.
- **Administration** — page-level RBAC (per controller/action, per role), full audit trail written
  to a separate database, multi-pharmacy tenancy with a platform super-admin.

**What it does NOT do** — worth stating plainly, because pharmacy systems often imply these:

- No prescription capture, dispensing workflow, or clinical records
- No drug-interaction or allergy checking
- No batch / lot / expiry-date tracking (expiry is handled as a stock-adjustment reason only)
- No insurance or claims processing
- No controlled-substance register
- No external integrations — no payment gateway, drug database, email/SMS, or third-party auth

---

## Technology

| Layer | Choice |
|---|---|
| Runtime | .NET 8.0 |
| Web | ASP.NET Core MVC (Razor views; some AJAX JSON endpoints) |
| Data | EF Core 8 + SQL Server |
| Auth | ASP.NET Core Identity, cookie-based |
| Frontend | Bootstrap 5.3, jQuery, DataTables, Select2, SweetAlert2, Chart.js |
| Tests | xUnit |

All frontend assets are vendored under `wwwroot/lib` — the POS works with no internet connection.

## Architecture

Clean Architecture; dependencies point inward.

```
PharmaCare.Domain          entities, enums, AppTime — depends on nothing
PharmaCare.Application     service interfaces + implementations, DTOs, view models
PharmaCare.Infrastructure  DbContext, repositories, unit of work, audit interceptor, reports
PharmaCare.Web             controllers, views, filters, middleware
PharmaCare.Tests           xUnit unit tests
PharmaCare.IntegrationTests xUnit tests against a real SQL Server, rebuilt from migrations
PharmaCare.LoadTests       synthetic data seeder + k6 script
```

Conventions live in [CODING_STANDARDS.md](CODING_STANDARDS.md). Two that surprise people:

- Foreign keys use an `_ID` suffix (`AccountHead_ID`) — deliberate, applied consistently.
- Never use `DateTime.Now`. Use `AppTime.Now` / `AppTime.Today`, which is pinned to the business
  timezone (Pakistan) so behaviour does not depend on where the server runs.

### Multi-tenancy

One pharmacy per tenant. Any entity implementing `ITenantEntity` automatically gets a required
`Pharmacy_ID`, an index, a foreign key, and a global query filter — applied in a single loop in
`PharmaCareDBContext`, so a new tenant-owned table cannot be forgotten. Writes are stamped
automatically and a row can never change its owning pharmacy.

---

## Setup

Prerequisites: .NET 8.0 SDK, SQL Server.

1. **Configure connection strings.** Do not put them in `appsettings.json` — it is committed.
   Use `appsettings.Development.json` (git-ignored), user-secrets, or environment variables:

   ```
   ConnectionStrings:PharmaCareDBConnectionString
   ConnectionStrings:PharmaCareLogDBConnectionString
   ```

   The audit log lives in its own database.

2. **Create the first platform administrator.** On first boot only, the app reads
   `PlatformAdmin:Email` and `PlatformAdmin:Password`. Without them it creates no account and logs
   a critical message — there are deliberately no default credentials.

3. **Apply migrations:**

   ```bash
   dotnet ef database update --project PharmaCare.Infrastructure --startup-project PharmaCare.Web --context PharmaCareDBContext
   ```

4. **Run:**

   ```bash
   dotnet run --project PharmaCare.Web
   ```

5. Sign in as the platform administrator and create a pharmacy. Provisioning seeds that tenant's
   chart of accounts, price types, profit settings, financial period, an Administrator role with
   full permissions, and its first admin user.

### Tests

Unit tests — pure math and rules, no database:

```bash
dotnet test PharmaCare.Tests/PharmaCare.Tests.csproj
```

Integration tests — these need a live SQL Server. They drop and rebuild two throwaway databases
from the migrations on every run, so a broken migration fails here. Point them at a different
server with the `PHARMACARE_TEST_SQL` environment variable:

```bash
dotnet test PharmaCare.IntegrationTests/PharmaCare.IntegrationTests.csproj
```

`PharmaCare.LoadTests` is a console harness that also requires a live SQL Server and is not part of
either suite.

---

## Notes for contributors

- **Navigation and permissions are code, not data.** The `Pages` / `PageUrls` catalog is seeded from
  `PageCatalog.cs` at startup. A controller action with no entry there and no `[LinkedToPage]`
  attribute is unreachable — every user gets Access Denied. Add new actions to that manifest.
- **Stock is derived, never stored.** It is computed from `StockDetail` rows signed by their
  transaction type's `StockDirection`, counting approved transactions only.
- **Money rules belong in services, not controllers**, so they hold for every caller.
