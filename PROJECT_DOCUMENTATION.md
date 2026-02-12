# PharmaCare Project Documentation

## 1. Purpose and Scope

This document describes the current implementation of the PharmaCare system in this repository, with a focus on:

- Functional workflow across modules
- Technical implementation by layer and component
- Accounting effects and financial implications of each business process

It is based on the active code in:

- `PharmaCare.Web`
- `PharmaCare.Application`
- `PharmaCare.Infrastructure`
- `PharmaCare.Domain`

---

## 2. Architecture Overview

PharmaCare is a layered ASP.NET Core MVC application (`net8.0`) with EF Core and ASP.NET Identity.

### 2.1 Layer responsibilities

- `PharmaCare.Web`: UI, MVC controllers, routing, session-based authorization, Razor views
- `PharmaCare.Application`: business services, interfaces, transaction and accounting rules
- `PharmaCare.Infrastructure`: EF Core DbContexts, repositories, auth/session adapters, reporting implementation, audit interceptor
- `PharmaCare.Domain`: entities, enums, core model definitions

### 2.2 Databases

- Main operational database via `PharmaCareDBContext` (`PharmaCare.Infrastructure/PharmaCareDBContext.cs`)
- Separate audit log database via `LogDbContext` (`PharmaCare.Infrastructure/LogDbContext.cs`)

### 2.3 Core technical patterns

- Generic repository pattern (`IRepository<T>`, `Repository<T>`)
- Unit of Work abstraction (`IUnitOfWork`, `UnitOfWork`)
- Service-based use-case orchestration (transactions, finance, configuration, security)
- Session-cached page permissions and sidebar menu
- Automatic entity change auditing through EF `SaveChangesInterceptor`

---

## 3. Core Business and Accounting Data Model

### 3.1 Transaction backbone

- `StockMain`: unified header for PO/GRN/Sale/Returns
- `StockDetail`: item lines (qty, unit price, line total, line cost)
- `TransactionType`: controls direction and behavior (`AffectsStock`, `CreatesVoucher`)

### 3.2 Accounting backbone

- `Voucher` and `VoucherDetail`: double-entry postings
- `AccountFamily -> AccountHead -> AccountSubhead -> Account`: chart-of-accounts hierarchy
- `VoucherType`: posting category (JV, RV, PV, SV, etc.)

### 3.3 Party and settlement model

- `Party`: customer/supplier master with linked `Account_ID`
- `Payment`: cash/bank movements for receipts, supplier payments, refunds

### 3.4 Category-account linkage

Each product category holds posting account references:

- `SaleAccount_ID`
- `StockAccount_ID`
- `COGSAccount_ID`
- `DamageAccount_ID`

These links drive automatic voucher posting logic in sales and inventory flows.

---

## 4. End-to-End Operational Workflow

## 4.1 Setup workflow (required before transactions)

1. Define chart of accounts
2. Create categories and map required accounting accounts
3. Create products under categories/subcategories
4. Create parties (customer/supplier) with linked ledger accounts
5. Ensure system accounts are configured in `appsettings.json`:
   - `SystemAccounts:WalkingCustomerAccountId`
6. Ensure expected transaction types and voucher types exist in DB

## 4.2 Procurement-to-payment workflow

1. Create Purchase Order (`PO`) in Draft
2. Approve PO
3. Create GRN (`GRN`) directly or from approved PO
4. System creates purchase voucher postings
5. Optional initial payment at GRN time (cash/bank)
6. Additional supplier payments recorded via Supplier Payment module
7. Purchase returns (`PRTN`) reduce inventory and payables

## 4.3 Sales-to-collection workflow

1. Create Sale (`SALE`) with line-level quantity/price/cost
2. System posts revenue + COGS + inventory reduction entries
3. Optional immediate payment creates receipt/cash-bank posting
4. Pending balances are settled in Customer Payment module
5. Sale returns (`SRTN`) reverse revenue and COGS, restore inventory
6. Refunds can be issued to customers when required

## 4.4 Reporting and close workflow

1. Operational reports (sales, purchase, stock, movement)
2. Financial reports (P&L, cash flow, trial balance, ledgers, aging)
3. Party ledgers and customer balance monitoring
4. Audit activity review through Activity Log module

---

## 5. Module-by-Module Documentation

## 5.1 Security and Access Control Module

### Functionality

- User authentication (`Login`, `Logout`, password change)
- Role-based page access with per-page CRUD permissions
- Dynamic sidebar generation by user permissions
- Session rehydration for "Remember Me" scenarios

### Technical implementation

- Controllers:
  - `PharmaCare.Web/Controllers/AccountController.cs`
  - `PharmaCare.Web/Controllers/Security/UserController.cs`
  - `PharmaCare.Web/Controllers/Security/RoleController.cs`
- Services/repositories:
  - `UserService`, `RoleService`, `SessionService`, `AuthService`
  - `RoleRepository`, `RolePageRepository`, `UserRoleRepository`, `PageRepository`
- Enforcement:
  - `BaseController` + `PageAuthorizationFilter`
  - Optional `LinkedToPageAttribute` for sub-actions/AJAX endpoints

### Accounting effects

- No direct ledger posting
- Indirect financial-control impact:
  - Prevents unauthorized creation/edit/delete of financial transactions
  - Enforces segregation of duties through role permissions

---

## 5.2 Configuration Module (Category, SubCategory, Product, Party)

### Functionality

- Category and subcategory setup
- Product catalog with pricing and stock parameters
- Customer/supplier party setup
- Product multi-price support via `PriceType` and `ProductPrice`

### Technical implementation

- Controllers:
  - `CategoryController`, `SubCategoryController`, `ProductController`, `PartyController`
- Services:
  - `CategoryService`, `SubCategoryService`, `ProductService`, `PartyService`
- Important logic:
  - Category account mapping used by posting engine
  - Product stock is derived from opening qty + stock movements
  - Party creation auto-generates linked account in chart of accounts

### Accounting effects and implications

- Categories define account routing for automatic postings
- Products affect inventory valuation and COGS calculations
- Parties represent AR/AP control accounts:
  - Customer balance tracking
  - Supplier payable tracking
- Party opening balances and credit limits influence receivable exposure

---

## 5.3 Accounting Master Module (COA + Heads/Subheads)

### Functionality

- Maintain account families, heads, subheads
- Maintain chart of accounts
- Provide cash/bank account selection for transaction settlement

### Technical implementation

- Controllers:
  - `AccountHeadController`, `AccountSubHeadController`, `ChartOfAccountController`
- Services:
  - `AccountHeadService`, `AccountSubHeadService`, `AccountService`
- Data model:
  - `AccountFamily`, `AccountHead`, `AccountSubhead`, `Account`, `AccountType`

### Accounting effects

- Foundational module for all financial postings
- Incorrect account configuration directly misstates:
  - Revenue
  - COGS
  - Inventory assets
  - Receivables/payables
  - Cash/bank balances

---

## 5.4 Purchase Order Module

### Functionality

- Create/edit purchase orders
- Draft -> Approved lifecycle
- Cancel by voiding status

### Technical implementation

- Controller: `PurchaseOrderController`
- Service: `PurchaseOrderService`
- Stored in `StockMain` with transaction type `PO`

### Accounting effects

- No direct ledger posting on PO creation/approval
- Financial implication:
  - Represents procurement commitment, not recognized AP or inventory yet

---

## 5.5 Purchase (GRN) Module

### Functionality

- Receive inventory through GRN
- Support GRN from approved PO
- Capture immediate payment during GRN if provided

### Technical implementation

- Controller: `PurchaseController`
- Service: `PurchaseService`
- Voucher generation:
  - Purchase voucher (`PV`)
  - Optional cash/bank payment voucher (`CP`)
- Payment records stored in `Payment` table for cash settlements

### Accounting effects

- Inventory recognition and AP recognition at receipt stage
- Optional settlement reduces AP and cash/bank
- PO-linked advance payments are transferred to GRN and included in paid amount

---

## 5.6 Purchase Return Module

### Functionality

- Return purchased goods against GRN
- Quantity validation against original GRN minus prior returns
- Void support with reversal voucher

### Technical implementation

- Controller: `PurchaseReturnController`
- Service: `PurchaseReturnService`
- Voucher type code: `PRV`
- On void: reversal voucher created by swapping debit/credit lines

### Accounting effects

- Reduces inventory asset
- Reduces AP liability to supplier
- If voided, prior financial effect is reversed

---

## 5.7 Sale Module

### Functionality

- Create sale invoices
- Support customer-linked and walk-in sales
- Optional immediate payment at invoice time
- Void support

### Technical implementation

- Controller: `SaleController`
- Service: `SaleService`
- Voucher generation:
  - Sales voucher (`SV`)
  - Optional payment voucher (`CR`/`BR`/`RV`)
- Category-level account aggregation:
  - Revenue by category sale account
  - COGS and stock reduction by category accounts

### Accounting effects

- Recognizes revenue and receivable
- Recognizes COGS expense and inventory reduction
- If cash is collected, receivable is reduced and cash/bank increases

---

## 5.8 Sale Return Module

### Functionality

- Return goods against prior sale
- Quantity validation against original sale and previous returns
- Auto-cost backfill from reference sale when missing
- Void support

### Technical implementation

- Controller: `SaleReturnController`
- Service: `SaleReturnService`
- Voucher type code: `SRT`
- Recalculates reference sale balance including non-void returns

### Accounting effects

- Reverses revenue (debit sales accounts)
- Reduces receivable/customer balance (credit customer account)
- Restores inventory (debit stock accounts)
- Reverses COGS (credit COGS accounts)

---

## 5.9 Supplier Payment Module

### Functionality

- Pay suppliers against pending GRNs
- Record advance payments not tied to a GRN
- View payment history and payment details

### Technical implementation

- Controller: `SupplierPaymentController`
- Service: `PaymentService`
- Payment types:
  - `PAYMENT` for supplier settlements
- Voucher types:
  - `CP` for cash payment
  - `BP` for bank payment

### Accounting effects

- Debit supplier AP account (liability decreases)
- Credit cash/bank account (asset decreases)
- Advance payments create supplier debit balance to offset future purchases

---

## 5.10 Customer Payment and Refund Module

### Functionality

- Receive payments against pending sales
- Handle walk-in customer receipts through configured system account
- Issue customer refunds
- View receipt/refund histories

### Technical implementation

- Controller: `CustomerPaymentController`
- Service: `CustomerPaymentService`
- Payment types:
  - `RECEIPT`
  - `REFUND`
- Recomputes sale balance after considering sale returns

### Accounting effects

- Receipt:
  - Debit cash/bank
  - Credit customer AR
- Refund:
  - Debit customer AR
  - Credit cash/bank
- Reduces receivable risk when collections are timely

---

## 5.11 Journal Voucher Module

### Functionality

- Intended for manual accounting adjustments and non-operational entries
- Fetch manual voucher entries (type `JV`)
- Support voucher reversal

### Technical implementation

- Service: `JournalVoucherService`
- Controller: `JournalVoucherController`
- Data contracts:
  - `JournalVoucherViewModel`
  - `JournalVoucherDto`

### Accounting effects

- Direct manual impact on ledger balances
- Used for adjustments, corrections, and non-stock financial entries

---

## 5.12 Reporting Module

### Functionality

Provides operational and financial reports:

- Sales:
  - Daily Sales Summary
  - Sales Report
  - Sales by Product
  - Sales by Customer
- Purchases:
  - Purchase Report
  - Purchase by Supplier
- Inventory:
  - Current Stock
  - Low Stock
  - Product Movement
  - Dead Stock
- Financial:
  - Profit and Loss
  - Cash Flow
  - Receivables Aging
  - Payables Aging
  - Expense Report
  - Trial Balance
  - General Ledger
- Party:
  - Customer/Supplier Ledger
  - Customer Balance Summary

### Technical implementation

- Controller: `ReportController`
- Service: `ReportService` (in infrastructure layer)
- Filter model: `DateRangeFilter` with default range (last one month)

### Accounting effects

- Converts transactional postings into financial visibility:
  - Profitability
  - Liquidity
  - Working capital exposure
  - Account-level and party-level balances

---

## 5.13 Activity Logging and Audit Module

### Functionality

- Track login/logout and CRUD events
- Query by user/entity/date/activity
- Provide dashboard summary and history screens

### Technical implementation

- Controller: `ActivityLogController`
- Service: `ActivityLogService`
- Repository: `ActivityLogRepository`
- Automatic DB change capture:
  - `AuditSaveChangesInterceptor` (attached to main DbContext)
  - Writes logs into separate `LogDbContext`

### Accounting effects

- No direct ledger posting
- Strong financial governance impact:
  - Audit trail for financial record changes
  - Improved traceability and accountability

---

## 6. Consolidated Accounting Posting Matrix

| Process | Debit | Credit | Financial impact |
| --- | --- | --- | --- |
| Sale invoice | Customer AR | Sales revenue | Increases receivable and revenue |
| Sale invoice (cost) | COGS | Inventory stock | Recognizes cost and reduces stock asset |
| Sale immediate payment | Cash/Bank | Customer AR | Converts receivable to liquid asset |
| Customer receipt | Cash/Bank | Customer AR | Collects outstanding receivable |
| Customer refund | Customer AR | Cash/Bank | Pays customer; reduces cash |
| GRN (purchase) | Inventory stock | Supplier AP | Increases inventory and liability |
| Supplier payment | Supplier AP | Cash/Bank | Settles payable; reduces cash |
| Supplier advance payment | Supplier AP | Cash/Bank | Creates supplier debit/advance |
| Purchase return | Supplier AP | Inventory stock | Reduces payable and inventory |
| Sale return (revenue) | Sales account | Customer AR | Reverses revenue and receivable |
| Sale return (cost) | Inventory stock | COGS | Restores stock and reverses cost |
| Voucher void reversal | Original credits | Original debits | Neutralizes prior posting |

---

## 7. Financial Statement Implications

### 7.1 Balance Sheet

- Inventory changes through GRN, sales, purchase returns, sale returns
- AR changes through sales, receipts, sale returns, refunds
- AP changes through purchases, supplier payments, purchase returns, advances
- Cash/Bank changes through receipts/payments/refunds

### 7.2 Income Statement

- Revenue from sale voucher credits
- Contra-revenue from sale return debits
- COGS from sale voucher debits
- COGS reversal from sale return credits
- Expense reporting available from `Expense` model/reporting logic

### 7.3 Cash Flow

- Cash inflows:
  - Paid amount at sale
  - Customer receipts
- Cash outflows:
  - Paid amount at purchase
  - Supplier payments
  - Expense payments

---

## 8. Critical Configuration Dependencies

The following must be correct for reliable financial behavior:

- `SystemAccounts.WalkingCustomerAccountId` exists and points to a valid AR account
- Transaction type codes exist: `PO`, `GRN`, `PRTN`, `SALE`, `SRTN`
- Voucher type codes used by services exist: `JV`, `SV`, `RV`, `PV`, `CP`, `BP`, `PRV`, `SRT` (or configured fallback behavior)
- Account type IDs are aligned with service assumptions:
  - `1` = Cash
  - `2` = Bank
- Every sale-able category has:
  - Sale account
  - Stock account
  - COGS account
- Every transacting party has linked account (`Party.Account_ID`)

---

## 9. Current Implementation Notes and Financial-Control Caveats

These are important when operating the system in production:

1. `JournalVoucherController.AddJournalVoucher` currently builds DTO but does not call `CreateJournalVoucherAsync`, so manual JV posting is not completed in controller flow.
2. `SaleService.VoidAsync` reverses the main sale voucher but does not explicitly reverse separate payment vouchers created for same sale.
3. `PurchaseService.VoidAsync` marks GRN as void without explicit accounting reversal voucher generation.
4. `ReportService.GetCashFlowReportAsync` sums both `StockMain.PaidAmount` and `Payment` records, which can overstate cash movement if both represent same settlements.
5. `PartyService` uses hard-coded account hierarchy/type IDs for customer/supplier account creation; this is environment-sensitive.
6. `ComboboxRepository.GetCashBankAccounts` filters by account type names (`Cash`, `Bank`) while transaction services often rely on account type IDs.
7. Number generation (`TransactionNo`, `VoucherNo`, `Reference`) uses "latest + 1" lookup; under high concurrency, collisions are possible without DB-level sequencing/locking.
8. `Utility.EncryptId` is base64 obfuscation, not cryptographic protection.
9. `PharmaCare.Infrastructure/Scripts/SeedData.sql` appears out of sync with current model in multiple places; validate before using in fresh environments.

---

## 10. Recommended Operational Checklist

1. Validate chart-of-account master and category-account mappings before go-live.
2. Validate system account configuration in `appsettings.json`.
3. Reconcile trial balance frequently against transaction summaries.
4. Review void/reversal workflows and document finance approval controls.
5. Add automated tests for posting rules and report calculations.
6. Add monthly close checklist using Trial Balance, General Ledger, AR/AP aging, and P&L.

