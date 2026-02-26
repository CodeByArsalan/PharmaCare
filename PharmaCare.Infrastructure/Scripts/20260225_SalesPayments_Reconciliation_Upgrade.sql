/*
    Sales/Returns/Customer-Payments module upgrade
    - Adds payment void audit fields
    - Adds CreditNotes table
    - Adds PaymentAllocations table
    - Adds required indexes and constraints

    Safe to run multiple times (idempotent guards included).
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- -------------------------------------------------------------------
-- Payments: void tracking
-- -------------------------------------------------------------------
IF COL_LENGTH('dbo.Payments', 'IsVoided') IS NULL
BEGIN
    ALTER TABLE dbo.Payments
        ADD IsVoided bit NOT NULL
            CONSTRAINT DF_Payments_IsVoided DEFAULT (0);
END;

IF COL_LENGTH('dbo.Payments', 'VoidReason') IS NULL
BEGIN
    ALTER TABLE dbo.Payments
        ADD VoidReason nvarchar(500) NULL;
END;

IF COL_LENGTH('dbo.Payments', 'VoidedBy') IS NULL
BEGIN
    ALTER TABLE dbo.Payments
        ADD VoidedBy int NULL;
END;

IF COL_LENGTH('dbo.Payments', 'VoidedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Payments
        ADD VoidedAt datetime2 NULL;
END;

-- -------------------------------------------------------------------
-- Credit notes
-- -------------------------------------------------------------------
IF OBJECT_ID('dbo.CreditNotes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CreditNotes
    (
        CreditNoteID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CreditNotes PRIMARY KEY,
        CreditNoteNo nvarchar(50) NOT NULL,
        Party_ID int NOT NULL,
        SourceStockMain_ID int NULL,
        TotalAmount decimal(18,2) NOT NULL,
        AppliedAmount decimal(18,2) NOT NULL,
        BalanceAmount decimal(18,2) NOT NULL,
        CreditDate datetime2 NOT NULL,
        Status nvarchar(20) NOT NULL,
        Remarks nvarchar(500) NULL,
        VoidReason nvarchar(500) NULL,
        VoidedBy int NULL,
        VoidedAt datetime2 NULL,
        Voucher_ID int NULL,
        CreatedAt datetime2 NOT NULL,
        CreatedBy int NOT NULL,
        UpdatedAt datetime2 NULL,
        UpdatedBy int NULL
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_CreditNotes_CreditNoteNo'
      AND object_id = OBJECT_ID('dbo.CreditNotes')
)
BEGIN
    CREATE UNIQUE INDEX IX_CreditNotes_CreditNoteNo
        ON dbo.CreditNotes (CreditNoteNo);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_CreditNotes_Status_Valid'
      AND parent_object_id = OBJECT_ID('dbo.CreditNotes')
)
BEGIN
    ALTER TABLE dbo.CreditNotes
        ADD CONSTRAINT CK_CreditNotes_Status_Valid
            CHECK (Status IN ('Open', 'Applied', 'Void'));
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_CreditNotes_Parties_Party_ID'
)
BEGIN
    ALTER TABLE dbo.CreditNotes WITH CHECK
        ADD CONSTRAINT FK_CreditNotes_Parties_Party_ID
            FOREIGN KEY (Party_ID) REFERENCES dbo.Parties (PartyID);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_CreditNotes_StockMains_SourceStockMain_ID'
)
BEGIN
    ALTER TABLE dbo.CreditNotes WITH CHECK
        ADD CONSTRAINT FK_CreditNotes_StockMains_SourceStockMain_ID
            FOREIGN KEY (SourceStockMain_ID) REFERENCES dbo.StockMains (StockMainID);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_CreditNotes_Vouchers_Voucher_ID'
)
BEGIN
    ALTER TABLE dbo.CreditNotes WITH CHECK
        ADD CONSTRAINT FK_CreditNotes_Vouchers_Voucher_ID
            FOREIGN KEY (Voucher_ID) REFERENCES dbo.Vouchers (VoucherID);
END;

-- -------------------------------------------------------------------
-- Payment allocations (receipt/credit note allocations against sales)
-- -------------------------------------------------------------------
IF OBJECT_ID('dbo.PaymentAllocations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentAllocations
    (
        PaymentAllocationID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentAllocations PRIMARY KEY,
        Payment_ID int NULL,
        CreditNote_ID int NULL,
        StockMain_ID int NOT NULL,
        Amount decimal(18,2) NOT NULL,
        AllocationDate datetime2 NOT NULL,
        SourceType nvarchar(20) NOT NULL,
        Remarks nvarchar(500) NULL,
        CreatedAt datetime2 NOT NULL,
        CreatedBy int NOT NULL,
        UpdatedAt datetime2 NULL,
        UpdatedBy int NULL
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_PaymentAllocations_Source_Valid'
      AND parent_object_id = OBJECT_ID('dbo.PaymentAllocations')
)
BEGIN
    ALTER TABLE dbo.PaymentAllocations
        ADD CONSTRAINT CK_PaymentAllocations_Source_Valid
            CHECK (SourceType IN ('Receipt', 'CreditNote'));
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_PaymentAllocations_Source_NotNull'
      AND parent_object_id = OBJECT_ID('dbo.PaymentAllocations')
)
BEGIN
    ALTER TABLE dbo.PaymentAllocations
        ADD CONSTRAINT CK_PaymentAllocations_Source_NotNull
            CHECK (Payment_ID IS NOT NULL OR CreditNote_ID IS NOT NULL);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_PaymentAllocations_Payments_Payment_ID'
)
BEGIN
    ALTER TABLE dbo.PaymentAllocations WITH CHECK
        ADD CONSTRAINT FK_PaymentAllocations_Payments_Payment_ID
            FOREIGN KEY (Payment_ID) REFERENCES dbo.Payments (PaymentID)
            ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_PaymentAllocations_CreditNotes_CreditNote_ID'
)
BEGIN
    ALTER TABLE dbo.PaymentAllocations WITH CHECK
        ADD CONSTRAINT FK_PaymentAllocations_CreditNotes_CreditNote_ID
            FOREIGN KEY (CreditNote_ID) REFERENCES dbo.CreditNotes (CreditNoteID)
            ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_PaymentAllocations_StockMains_StockMain_ID'
)
BEGIN
    ALTER TABLE dbo.PaymentAllocations WITH CHECK
        ADD CONSTRAINT FK_PaymentAllocations_StockMains_StockMain_ID
            FOREIGN KEY (StockMain_ID) REFERENCES dbo.StockMains (StockMainID);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PaymentAllocations_Payment_ID'
      AND object_id = OBJECT_ID('dbo.PaymentAllocations')
)
BEGIN
    CREATE INDEX IX_PaymentAllocations_Payment_ID
        ON dbo.PaymentAllocations (Payment_ID);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PaymentAllocations_CreditNote_ID'
      AND object_id = OBJECT_ID('dbo.PaymentAllocations')
)
BEGIN
    CREATE INDEX IX_PaymentAllocations_CreditNote_ID
        ON dbo.PaymentAllocations (CreditNote_ID);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PaymentAllocations_StockMain_ID'
      AND object_id = OBJECT_ID('dbo.PaymentAllocations')
)
BEGIN
    CREATE INDEX IX_PaymentAllocations_StockMain_ID
        ON dbo.PaymentAllocations (StockMain_ID);
END;

COMMIT TRANSACTION;
