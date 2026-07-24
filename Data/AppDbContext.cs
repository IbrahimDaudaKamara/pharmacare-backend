using Microsoft.EntityFrameworkCore;
using MediCorePMS.Models;

namespace MediCorePMS.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>     Users      => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Medicine> Medicines  => Set<Medicine>();
    public DbSet<Sale>     Sales      => Set<Sale>();
    public DbSet<SaleItem> SaleItems  => Set<SaleItem>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── User ──────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── Category → Medicine (restrict delete) ─────────────
        mb.Entity<Category>(e =>
        {
            e.HasMany(c => c.Medicines)
             .WithOne(m => m.Category)
             .HasForeignKey(m => m.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Medicine ──────────────────────────────────────────
        mb.Entity<Medicine>(e =>
        {
            e.Property(m => m.Price).HasColumnType("decimal(18,2)");
            e.Property(m => m.Cost).HasColumnType("decimal(18,2)");
        });

        // ── Sale → User (restrict delete) ─────────────────────
        mb.Entity<Sale>(e =>
        {
            e.Property(s => s.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(s => s.Discount).HasColumnType("decimal(18,2)");
            e.Property(s => s.Total).HasColumnType("decimal(18,2)");
            e.HasOne(s => s.Cashier)
             .WithMany(u => u.Sales)
             .HasForeignKey(s => s.CashierId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── SaleItem ──────────────────────────────────────────
        mb.Entity<SaleItem>(e =>
        {
            e.Property(si => si.UnitPrice).HasColumnType("decimal(18,2)");
            e.Ignore(si => si.LineTotal);           // computed, not stored
            e.HasOne(si => si.Sale)
             .WithMany(s => s.Items)
             .HasForeignKey(si => si.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(si => si.Medicine)
             .WithMany(m => m.SaleItems)
             .HasForeignKey(si => si.MedicineId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
