using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Tests;

public class CatalogServiceTests
{
    [Fact]
    public async Task Cannot_delete_customer_with_orders()
    {
        using var factory = new SqliteTestFactory();
        var customers = new CustomerService(factory);
        var orders = new OrderService(factory, new OrderNumberGenerator(factory));
        var seller = await TestData.SeedSellerAsync(factory);

        var customer = await customers.CreateAsync(new Customer
        {
            Name = "Buyer", SellerCompanyId = seller.Id, AddressLine1 = "x",
            Country = "DE", DefaultCurrency = "EUR",
        });
        Product product;
        await using (var db = factory.CreateDbContext())
        {
            product = new Product { PartNumber = "P-1", Description = "F" };
            db.Add(product);
            await db.SaveChangesAsync();
        }
        await orders.CreateAsync(new Order
        {
            CustomerId = customer.Id, SellerCompanyId = seller.Id, Currency = "EUR",
            OrderDate = new DateOnly(2026, 1, 1),
            Lines = { new OrderLine { ProductId = product.Id, Quantity = 1, UnitPrice = 1m } },
        });

        Assert.False(await customers.DeleteAsync(customer.Id));
        Assert.NotNull(await customers.GetAsync(customer.Id));
    }

    [Fact]
    public async Task Cannot_delete_product_used_on_an_order_line()
    {
        using var factory = new SqliteTestFactory();
        var products = new ProductService(factory);
        var customers = new CustomerService(factory);
        var orders = new OrderService(factory, new OrderNumberGenerator(factory));
        var seller = await TestData.SeedSellerAsync(factory);

        var customer = await customers.CreateAsync(new Customer
        {
            Name = "Buyer", SellerCompanyId = seller.Id, AddressLine1 = "x",
            Country = "DE", DefaultCurrency = "EUR",
        });
        var product = await products.CreateAsync(new Product
        {
            PartNumber = "P-1", Description = "F",
        });
        await orders.CreateAsync(new Order
        {
            CustomerId = customer.Id, SellerCompanyId = seller.Id, Currency = "EUR",
            OrderDate = new DateOnly(2026, 1, 1),
            Lines = { new OrderLine { ProductId = product.Id, Quantity = 1, UnitPrice = 1m } },
        });

        Assert.False(await products.DeleteAsync(product.Id));
    }

    [Fact]
    public async Task PartNumberExists_ignores_the_row_being_edited()
    {
        using var factory = new SqliteTestFactory();
        var products = new ProductService(factory);

        var a = await products.CreateAsync(new Product { PartNumber = "AF-1", Description = "a" });
        await products.CreateAsync(new Product { PartNumber = "AF-2", Description = "b" });

        Assert.True(await products.PartNumberExistsAsync("AF-1"));
        // Editing row a: its own part number must not count as a clash...
        Assert.False(await products.PartNumberExistsAsync("AF-1", excludeId: a.Id));
        // ...but another row's part number still does.
        Assert.True(await products.PartNumberExistsAsync("AF-2", excludeId: a.Id));
    }
}
