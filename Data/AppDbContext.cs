using Microsoft.EntityFrameworkCore;
using ProductApp.Models;
using System.IO;

namespace ProductApp.Data;

public class AppDbContext : DbContext
{
public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<SupplierInvoiceItem> SupplierInvoiceItems => Set<SupplierInvoiceItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<DebtAccount> DebtAccounts => Set<DebtAccount>();

    private static readonly string DbFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTE Stock");

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!Directory.Exists(DbFolder))
            Directory.CreateDirectory(DbFolder);
        var dbPath = Path.Combine(DbFolder, "inventory.db");
        options.UseSqlite($"Data Source={dbPath}");
    }

    public static void MigrateIfNeeded()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();

        // 1) IsCostRecovered column
        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "PRAGMA table_info(InventoryMovements)";
            using var reader = checkCmd.ExecuteReader();
            var hasCol = false;
            while (reader.Read())
                if ((string)reader["name"] == "IsCostRecovered") { hasCol = true; break; }
            if (!hasCol)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE InventoryMovements ADD COLUMN IsCostRecovered INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }

// 2) MinStockLevel column in ProductUnits
        using (var checkMin = conn.CreateCommand())
        {
            checkMin.CommandText = "PRAGMA table_info(ProductUnits)";
            using var reader = checkMin.ExecuteReader();
            var hasMinCol = false;
            while (reader.Read())
                if ((string)reader["name"] == "MinStockLevel") { hasMinCol = true; break; }
            if (!hasMinCol)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE ProductUnits ADD COLUMN MinStockLevel INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }

        // 5) Barcode column in Products
        using (var checkBarcode = conn.CreateCommand())
        {
            checkBarcode.CommandText = "PRAGMA table_info(Products)";
            using var reader = checkBarcode.ExecuteReader();
            var hasBarcode = false;
            while (reader.Read())
                if ((string)reader["name"] == "Barcode") { hasBarcode = true; break; }
            if (!hasBarcode)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Products ADD COLUMN Barcode TEXT";
                alter.ExecuteNonQuery();
            }
        }

        // 6) IsFavorite column in Products
        using (var checkFav = conn.CreateCommand())
        {
            checkFav.CommandText = "PRAGMA table_info(Products)";
            using var reader = checkFav.ExecuteReader();
            var hasFav = false;
            while (reader.Read())
                if ((string)reader["name"] == "IsFavorite") { hasFav = true; break; }
            if (!hasFav)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Products ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }

        // 10) Soft-delete columns (سلة المحذوفات)
        using (var checkSoft = conn.CreateCommand())
        {
            checkSoft.CommandText = "PRAGMA table_info(Products)";
            using var reader = checkSoft.ExecuteReader();
            var cols = new System.Collections.Generic.HashSet<string>();
            while (reader.Read()) cols.Add((string)reader["name"]);
            if (!cols.Contains("IsDeleted"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Products ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
            if (!cols.Contains("DeletedAt"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Products ADD COLUMN DeletedAt TEXT";
                alter.ExecuteNonQuery();
            }
        }

        using (var checkInvSoft = conn.CreateCommand())
        {
            checkInvSoft.CommandText = "PRAGMA table_info(Invoices)";
            using var reader = checkInvSoft.ExecuteReader();
            var cols = new System.Collections.Generic.HashSet<string>();
            while (reader.Read()) cols.Add((string)reader["name"]);
            if (!cols.Contains("IsDeleted"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Invoices ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
            if (!cols.Contains("DeletedAt"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Invoices ADD COLUMN DeletedAt TEXT";
                alter.ExecuteNonQuery();
            }
        }

        using (var checkSupSoft = conn.CreateCommand())
        {
            checkSupSoft.CommandText = "PRAGMA table_info(SupplierInvoices)";
            using var reader = checkSupSoft.ExecuteReader();
            var cols = new System.Collections.Generic.HashSet<string>();
            while (reader.Read()) cols.Add((string)reader["name"]);
            if (!cols.Contains("IsDeleted"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE SupplierInvoices ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
            if (!cols.Contains("DeletedAt"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE SupplierInvoices ADD COLUMN DeletedAt TEXT";
                alter.ExecuteNonQuery();
            }
        }

        // 7) Supplier tables (only for databases created before this version)
        CreateIfMissing(conn, "Suppliers", @"
CREATE TABLE IF NOT EXISTS ""Suppliers"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Suppliers"" PRIMARY KEY AUTOINCREMENT,
    ""Name"" TEXT NOT NULL,
    ""Phone"" TEXT NULL,
    ""Address"" TEXT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL);");

        CreateIfMissing(conn, "SupplierInvoices", @"
CREATE TABLE IF NOT EXISTS ""SupplierInvoices"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SupplierInvoices"" PRIMARY KEY AUTOINCREMENT,
    ""SupplierId"" INTEGER NULL,
    ""SupplierName"" TEXT NULL,
    ""InvoiceDate"" TEXT NOT NULL,
    ""TotalAmount"" TEXT NOT NULL,
    ""TotalPaid"" TEXT NOT NULL,
    ""Status"" INTEGER NOT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_SupplierInvoices_Suppliers_SupplierId"" FOREIGN KEY (""SupplierId"") REFERENCES ""Suppliers"" (""Id"") ON DELETE SET NULL);");

        CreateIfMissing(conn, "SupplierInvoiceItems", @"
CREATE TABLE IF NOT EXISTS ""SupplierInvoiceItems"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SupplierInvoiceItems"" PRIMARY KEY AUTOINCREMENT,
    ""SupplierInvoiceId"" INTEGER NOT NULL,
    ""ProductId"" INTEGER NOT NULL,
    ""CartonQuantity"" INTEGER NOT NULL,
    ""BoxQuantity"" INTEGER NOT NULL,
    ""PieceQuantity"" INTEGER NOT NULL,
    ""CostPrice"" TEXT NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_SupplierInvoiceItems_SupplierInvoices_SupplierInvoiceId"" FOREIGN KEY (""SupplierInvoiceId"") REFERENCES ""SupplierInvoices"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_SupplierInvoiceItems_Products_ProductId"" FOREIGN KEY (""ProductId"") REFERENCES ""Products"" (""Id"") ON DELETE CASCADE);");

        CreateIfMissing(conn, "SupplierPayments", @"
CREATE TABLE IF NOT EXISTS ""SupplierPayments"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SupplierPayments"" PRIMARY KEY AUTOINCREMENT,
    ""SupplierInvoiceId"" INTEGER NOT NULL,
    ""Amount"" TEXT NOT NULL,
    ""PaymentDate"" TEXT NOT NULL,
    ""PaymentMethod"" TEXT NULL,
    ""Notes"" TEXT NULL,
    CONSTRAINT ""FK_SupplierPayments_SupplierInvoices_SupplierInvoiceId"" FOREIGN KEY (""SupplierInvoiceId"") REFERENCES ""SupplierInvoices"" (""Id"") ON DELETE CASCADE);");

        // 8) Debts tables (الديون اليدوية + بطاقة ديوني للموردين تُحسب تلقائياً)
        CreateIfMissing(conn, "Debts", @"
CREATE TABLE IF NOT EXISTS ""Debts"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Debts"" PRIMARY KEY AUTOINCREMENT,
    ""Name"" TEXT NOT NULL,
    ""Direction"" INTEGER NOT NULL,
    ""TotalAmount"" TEXT NOT NULL,
    ""TotalPaid"" TEXT NOT NULL,
    ""Status"" INTEGER NOT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL);");

        CreateIfMissing(conn, "DebtPayments", @"
CREATE TABLE IF NOT EXISTS ""DebtPayments"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_DebtPayments"" PRIMARY KEY AUTOINCREMENT,
    ""DebtId"" INTEGER NOT NULL,
    ""Amount"" TEXT NOT NULL,
    ""PaymentDate"" TEXT NOT NULL,
    ""PaymentMethod"" TEXT NULL,
    ""Notes"" TEXT NULL,
    CONSTRAINT ""FK_DebtPayments_Debts_DebtId"" FOREIGN KEY (""DebtId"") REFERENCES ""Debts"" (""Id"") ON DELETE CASCADE);");

        // 9) Debts tables upgrades: الأشخاص الثابتون + ربط الديون بهم
        CreateIfMissing(conn, "DebtAccounts", @"
CREATE TABLE IF NOT EXISTS ""DebtAccounts"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_DebtAccounts"" PRIMARY KEY AUTOINCREMENT,
    ""Name"" TEXT NOT NULL,
    ""Phone"" TEXT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL);");

        using (var checkDebtCol = conn.CreateCommand())
        {
            checkDebtCol.CommandText = "PRAGMA table_info(Debts)";
            using var reader = checkDebtCol.ExecuteReader();
            var cols = new System.Collections.Generic.HashSet<string>();
            while (reader.Read()) cols.Add((string)reader["name"]);

            if (!cols.Contains("DebtAccountId"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Debts ADD COLUMN DebtAccountId INTEGER NULL";
                alter.ExecuteNonQuery();
            }
            if (!cols.Contains("AccountName"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Debts ADD COLUMN AccountName TEXT NOT NULL DEFAULT ''";
                alter.ExecuteNonQuery();
            }
            if (cols.Contains("Name"))
            {
                // الجدول القديم كان فيه عمود Name (NOT NULL) — النموذج الجديد يستبدله بـ AccountName
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Debts DROP COLUMN Name";
                alter.ExecuteNonQuery();
            }
        }
    }

    private static void CreateIfMissing(System.Data.Common.DbConnection conn, string tableName, string createSql)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
        var p = check.CreateParameter();
        p.ParameterName = "@name";
        p.Value = tableName;
        check.Parameters.Add(p);
        if ((long)check.ExecuteScalar()! > 0) return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = createSql;
        cmd.ExecuteNonQuery();
    }

protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ProductUnit>()
            .HasOne(u => u.ParentUnit)
            .WithMany(u => u.ChildUnits)
            .HasForeignKey(u => u.ParentUnitId)
            .OnDelete(DeleteBehavior.Restrict);

model.Entity<Invoice>()
            .HasOne(i => i.Customer)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        model.Entity<SupplierInvoice>()
            .HasOne(i => i.Supplier)
            .WithMany(s => s.Invoices)
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        // سلة المحذوفات: الفواتير المحذوفة مخفية عن كل الاستعلامات تلقائياً
        model.Entity<Invoice>().HasQueryFilter(i => !i.IsDeleted);
        model.Entity<SupplierInvoice>().HasQueryFilter(i => !i.IsDeleted);
    }
}
