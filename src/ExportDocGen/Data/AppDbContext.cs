using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Money & measures: fixed precision, never floating point.
        foreach (var property in model.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(3);
        }

        model.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Country).HasMaxLength(100).IsRequired();
            e.Property(c => c.DefaultCurrency).HasMaxLength(3).IsRequired();
            e.Property(c => c.TaxId).HasMaxLength(50);
            e.Property(c => c.ContactPhone).HasMaxLength(50);
        });

        model.Entity<Product>(e =>
        {
            e.Property(p => p.PartNumber).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasMaxLength(400).IsRequired();
            e.HasIndex(p => p.PartNumber).IsUnique();
        });

        model.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
            e.Property(o => o.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(o => o.OrderNumber).IsUnique();

            e.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<OrderLine>(e =>
        {
            e.HasOne(l => l.Order)
                .WithMany(o => o.Lines)
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
