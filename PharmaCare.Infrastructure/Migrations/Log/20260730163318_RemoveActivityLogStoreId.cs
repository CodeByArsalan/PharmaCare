using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations.Log
{
    /// <summary>
    /// Drops the dead ActivityLogs/ActivityLogsArchive StoreId column (store support was removed
    /// long ago and the column was NULL on every row) and adds the Pharmacy_ID index the model
    /// declares but the database never had.
    ///
    /// <para>
    /// HAND-WRITTEN, deliberately. The scaffolded version renamed StoreId -> Pharmacy_ID, which
    /// would fail on any real database because Pharmacy_ID already exists there: it was added
    /// out-of-band by DbInitializer.EnsureLogPharmacyColumnAsync (raw ALTER TABLE) instead of by a
    /// migration, so the model snapshot never knew about it and EF matched the two columns as a
    /// rename. Every operation below is guarded so it is safe on a database that has been through
    /// DbInitializer and on a freshly migrated one. From here the snapshot matches reality again.
    /// </para>
    /// </summary>
    public partial class RemoveActivityLogStoreId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('ActivityLogs', 'Pharmacy_ID') IS NULL
    ALTER TABLE [ActivityLogs] ADD [Pharmacy_ID] INT NULL;
IF COL_LENGTH('ActivityLogsArchive', 'Pharmacy_ID') IS NULL
    ALTER TABLE [ActivityLogsArchive] ADD [Pharmacy_ID] INT NULL;

IF COL_LENGTH('ActivityLogs', 'StoreId') IS NOT NULL
    ALTER TABLE [ActivityLogs] DROP COLUMN [StoreId];
IF COL_LENGTH('ActivityLogsArchive', 'StoreId') IS NOT NULL
    ALTER TABLE [ActivityLogsArchive] DROP COLUMN [StoreId];

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLogs_Pharmacy_ID')
    CREATE INDEX [IX_ActivityLogs_Pharmacy_ID] ON [ActivityLogs] ([Pharmacy_ID]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the column shape only. The values cannot come back, but every row held NULL
            // — store support had already been removed before this column was ever populated.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLogs_Pharmacy_ID')
    DROP INDEX [IX_ActivityLogs_Pharmacy_ID] ON [ActivityLogs];

IF COL_LENGTH('ActivityLogs', 'StoreId') IS NULL
    ALTER TABLE [ActivityLogs] ADD [StoreId] INT NULL;
IF COL_LENGTH('ActivityLogsArchive', 'StoreId') IS NULL
    ALTER TABLE [ActivityLogsArchive] ADD [StoreId] INT NULL;
");
        }
    }
}
