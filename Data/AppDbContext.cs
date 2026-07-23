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
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "PRAGMA table_info(InventoryMovements)";
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            cmd.Connection.Open();
        using var reader = cmd.ExecuteReader();
        var hasCol = false;
        while (reader.Read())
            if ((string)reader["name"] == "IsCostRecovered") { hasCol = true; break; }
        reader.Close();
        if (!hasCol)
        {
            using var alter = db.Database.GetDbConnection().CreateCommand();
            alter.CommandText = "ALTER TABLE InventoryMovements ADD COLUMN IsCostRecovered INTEGER NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();
        }
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
    }
}
