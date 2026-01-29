using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureAccountingHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop ALL foreign key constraints referencing accounting tables
            migrationBuilder.Sql(@"
                -- Drop FKs from Categories to Accounts
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Categories_Accounts_COGSAccount_ID')
                    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Accounts_COGSAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Categories_Accounts_SaleAccount_ID')
                    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Accounts_SaleAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Categories_Accounts_StockAccount_ID')
                    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Accounts_StockAccount_ID];

                -- Drop FKs from ExpenseCategories to Accounts
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID')
                    ALTER TABLE [ExpenseCategories] DROP CONSTRAINT [FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID];

                -- Drop FKs from Payments to Accounts
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payments_Accounts_Account_ID')
                    ALTER TABLE [Payments] DROP CONSTRAINT [FK_Payments_Accounts_Account_ID];

                -- Drop FKs from Expenses to Accounts
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Expenses_Accounts_ExpenseAccount_ID')
                    ALTER TABLE [Expenses] DROP CONSTRAINT [FK_Expenses_Accounts_ExpenseAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Expenses_Accounts_SourceAccount_ID')
                    ALTER TABLE [Expenses] DROP CONSTRAINT [FK_Expenses_Accounts_SourceAccount_ID];

                -- Drop FKs from VoucherDetails to Accounts
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VoucherDetails_Accounts_Account_ID')
                    ALTER TABLE [VoucherDetails] DROP CONSTRAINT [FK_VoucherDetails_Accounts_Account_ID];

                -- Drop FK from Accounts to AccountSubheads
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Accounts_AccountSubheads_AccountSubhead_ID')
                    ALTER TABLE [Accounts] DROP CONSTRAINT [FK_Accounts_AccountSubheads_AccountSubhead_ID];

                -- Drop FK from Accounts to AccountTypes
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Accounts_AccountTypes_AccountType_ID')
                    ALTER TABLE [Accounts] DROP CONSTRAINT [FK_Accounts_AccountTypes_AccountType_ID];

                -- Drop FK from AccountSubheads to AccountHeads
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountSubheads_AccountHeads_AccountHead_ID')
                    ALTER TABLE [AccountSubheads] DROP CONSTRAINT [FK_AccountSubheads_AccountHeads_AccountHead_ID];
            ");

            // Step 2: Drop the existing accounting tables
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS [Accounts];
                DROP TABLE IF EXISTS [AccountSubheads];
                DROP TABLE IF EXISTS [AccountHeads];
            ");

            // Step 3: Create the new AccountFamilies table
            migrationBuilder.CreateTable(
                name: "AccountFamilies",
                columns: table => new
                {
                    FamilyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FamilyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountFamilies", x => x.FamilyId);
                });

            // Step 4: Create the new AccountHeads table
            migrationBuilder.CreateTable(
                name: "AccountHeads",
                columns: table => new
                {
                    HeadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HeadName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FamilyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountHeads", x => x.HeadId);
                    table.ForeignKey(
                        name: "FK_AccountHeads_AccountFamilies_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "AccountFamilies",
                        principalColumn: "FamilyId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Step 5: Create the new AccountSubheads table
            migrationBuilder.CreateTable(
                name: "AccountSubheads",
                columns: table => new
                {
                    SubheadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubheadName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HeadId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSubheads", x => x.SubheadId);
                    table.ForeignKey(
                        name: "FK_AccountSubheads_AccountHeads_HeadId",
                        column: x => x.HeadId,
                        principalTable: "AccountHeads",
                        principalColumn: "HeadId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Step 6: Create the new Accounts table
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubheadId = table.Column<int>(type: "int", nullable: false),
                    AccountTypeId = table.Column<int>(type: "int", nullable: false),
                    IsSystemAccount = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_Accounts_AccountSubheads_SubheadId",
                        column: x => x.SubheadId,
                        principalTable: "AccountSubheads",
                        principalColumn: "SubheadId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accounts_AccountTypes_AccountTypeId",
                        column: x => x.AccountTypeId,
                        principalTable: "AccountTypes",
                        principalColumn: "AccountTypeID",
                        onDelete: ReferentialAction.Restrict);
                });

            // Step 7: Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_FamilyId",
                table: "AccountHeads",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_HeadId",
                table: "AccountSubheads",
                column: "HeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SubheadId",
                table: "Accounts",
                column: "SubheadId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountTypeId",
                table: "Accounts",
                column: "AccountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Code",
                table: "Accounts",
                column: "Code",
                unique: true);

            // Step 8: Re-add foreign keys from other tables to Accounts using NO ACTION
            migrationBuilder.Sql(@"
                -- Re-add FK from Categories to Accounts (COGSAccount_ID)
                ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Accounts_COGSAccount_ID]
                    FOREIGN KEY ([COGSAccount_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;

                -- Re-add FK from Categories to Accounts (SaleAccount_ID)
                ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Accounts_SaleAccount_ID]
                    FOREIGN KEY ([SaleAccount_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;

                -- Re-add FK from Categories to Accounts (StockAccount_ID)
                ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Accounts_StockAccount_ID]
                    FOREIGN KEY ([StockAccount_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;

                -- Re-add FK from ExpenseCategories to Accounts
                ALTER TABLE [ExpenseCategories] ADD CONSTRAINT [FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID]
                    FOREIGN KEY ([DefaultExpenseAccount_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;

                -- Re-add FK from Payments to Accounts
                ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Accounts_Account_ID]
                    FOREIGN KEY ([Account_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;

                -- Re-add FK from Expenses to Accounts (ExpenseAccount_ID)
                ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Accounts_ExpenseAccount_ID]
                    FOREIGN KEY ([ExpenseAccount_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;

                -- Re-add FK from Expenses to Accounts (SourceAccount_ID)
                ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Accounts_SourceAccount_ID]
                    FOREIGN KEY ([SourceAccount_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;

                -- Re-add FK from VoucherDetails to Accounts
                ALTER TABLE [VoucherDetails] ADD CONSTRAINT [FK_VoucherDetails_Accounts_Account_ID]
                    FOREIGN KEY ([Account_ID]) REFERENCES [Accounts]([AccountId]) ON DELETE NO ACTION;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys from other tables
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Categories_Accounts_COGSAccount_ID')
                    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Accounts_COGSAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Categories_Accounts_SaleAccount_ID')
                    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Accounts_SaleAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Categories_Accounts_StockAccount_ID')
                    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Accounts_StockAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID')
                    ALTER TABLE [ExpenseCategories] DROP CONSTRAINT [FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payments_Accounts_Account_ID')
                    ALTER TABLE [Payments] DROP CONSTRAINT [FK_Payments_Accounts_Account_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Expenses_Accounts_ExpenseAccount_ID')
                    ALTER TABLE [Expenses] DROP CONSTRAINT [FK_Expenses_Accounts_ExpenseAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Expenses_Accounts_SourceAccount_ID')
                    ALTER TABLE [Expenses] DROP CONSTRAINT [FK_Expenses_Accounts_SourceAccount_ID];
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_VoucherDetails_Accounts_Account_ID')
                    ALTER TABLE [VoucherDetails] DROP CONSTRAINT [FK_VoucherDetails_Accounts_Account_ID];
            ");

            // Drop new tables
            migrationBuilder.DropTable(name: "Accounts");
            migrationBuilder.DropTable(name: "AccountSubheads");
            migrationBuilder.DropTable(name: "AccountHeads");
            migrationBuilder.DropTable(name: "AccountFamilies");

            // Recreate original tables
            migrationBuilder.Sql(@"
                CREATE TABLE [AccountHeads] (
                    [AccountHeadID] int NOT NULL IDENTITY(1,1),
                    [Code] nvarchar(10) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [NormalBalance] nvarchar(10) NOT NULL DEFAULT 'DEBIT',
                    [DisplayOrder] int NOT NULL DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] int NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] int NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    CONSTRAINT [PK_AccountHeads] PRIMARY KEY ([AccountHeadID])
                );

                CREATE TABLE [AccountSubheads] (
                    [AccountSubheadID] int NOT NULL IDENTITY(1,1),
                    [AccountHead_ID] int NOT NULL,
                    [Code] nvarchar(20) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [DisplayOrder] int NOT NULL DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] int NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] int NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    CONSTRAINT [PK_AccountSubheads] PRIMARY KEY ([AccountSubheadID]),
                    CONSTRAINT [FK_AccountSubheads_AccountHeads_AccountHead_ID] FOREIGN KEY ([AccountHead_ID]) REFERENCES [AccountHeads]([AccountHeadID]) ON DELETE NO ACTION
                );

                CREATE TABLE [Accounts] (
                    [AccountID] int NOT NULL IDENTITY(1,1),
                    [AccountSubhead_ID] int NOT NULL,
                    [AccountType_ID] int NOT NULL,
                    [Code] nvarchar(20) NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [IsSystemAccount] bit NOT NULL DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] int NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] int NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    CONSTRAINT [PK_Accounts] PRIMARY KEY ([AccountID]),
                    CONSTRAINT [FK_Accounts_AccountSubheads_AccountSubhead_ID] FOREIGN KEY ([AccountSubhead_ID]) REFERENCES [AccountSubheads]([AccountSubheadID]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Accounts_AccountTypes_AccountType_ID] FOREIGN KEY ([AccountType_ID]) REFERENCES [AccountTypes]([AccountTypeID]) ON DELETE NO ACTION
                );

                -- Re-add FKs from other tables
                ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Accounts_COGSAccount_ID]
                    FOREIGN KEY ([COGSAccount_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
                ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Accounts_SaleAccount_ID]
                    FOREIGN KEY ([SaleAccount_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
                ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Accounts_StockAccount_ID]
                    FOREIGN KEY ([StockAccount_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
                ALTER TABLE [ExpenseCategories] ADD CONSTRAINT [FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID]
                    FOREIGN KEY ([DefaultExpenseAccount_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
                ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Accounts_Account_ID]
                    FOREIGN KEY ([Account_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
                ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Accounts_ExpenseAccount_ID]
                    FOREIGN KEY ([ExpenseAccount_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
                ALTER TABLE [Expenses] ADD CONSTRAINT [FK_Expenses_Accounts_SourceAccount_ID]
                    FOREIGN KEY ([SourceAccount_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
                ALTER TABLE [VoucherDetails] ADD CONSTRAINT [FK_VoucherDetails_Accounts_Account_ID]
                    FOREIGN KEY ([Account_ID]) REFERENCES [Accounts]([AccountID]) ON DELETE NO ACTION;
            ");
        }
    }
}
