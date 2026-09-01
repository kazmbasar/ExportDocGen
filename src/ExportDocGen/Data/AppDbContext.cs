using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<SellerCompany> SellerCompanies => Set<SellerCompany>();

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
            e.Property(c => c.PaymentType).HasConversion<string>().HasMaxLength(40);

            e.HasOne(c => c.SellerCompany)
                .WithMany()
                .HasForeignKey(c => c.SellerCompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<SellerCompany>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.ShortName).HasMaxLength(60).IsRequired();
            e.Property(s => s.ProformaTemplate).HasConversion<string>().HasMaxLength(40);
            e.Property(s => s.NumberFormat).HasConversion<string>().HasMaxLength(40);
            e.Property(s => s.LetterheadPath).HasMaxLength(260);
            e.Property(s => s.DefaultDeliveryTime).HasMaxLength(120);
            e.Property(s => s.DefaultValidity).HasMaxLength(120);
            e.Property(s => s.CountryOfOrigin).HasMaxLength(80);
        });

        model.Entity<Product>(e =>
        {
            e.Property(p => p.PartNumber).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasMaxLength(400).IsRequired();
            e.Property(p => p.Origin).HasMaxLength(80);
            e.Property(p => p.Brand).HasMaxLength(80);
            e.Property(p => p.HsCode).HasMaxLength(40);
            e.Property(p => p.FilterType).HasMaxLength(20);
            e.HasIndex(p => p.PartNumber).IsUnique();
        });

        model.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
            e.Property(o => o.Currency).HasMaxLength(3).IsRequired();
            e.Property(o => o.DeliveryTime).HasMaxLength(120);
            e.Property(o => o.Validity).HasMaxLength(120);
            e.HasIndex(o => o.OrderNumber).IsUnique();

            e.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(o => o.SellerCompany)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.SellerCompanyId)
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
