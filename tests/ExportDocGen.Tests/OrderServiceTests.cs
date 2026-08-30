using ExportDocGen.Data.Entities;
using ExportDocGen.Services;

namespace ExportDocGen.Tests;

public class OrderServiceTests
{
    private static async Task<(SqliteTestFactory factory, int customerId, int productId, int product2Id)> SeedAsync()
    {
        var factory = new SqliteTestFactory();
        await using var db = factory.CreateDbContext();

        var customer = new Customer
        {
            Name = "Test Buyer", AddressLine1 = "1 Road", Country = "Germany",
            DefaultIncoterm = "CIF Hamburg", DefaultCurrency = "EUR",
        };
        var p1 = new Product { PartNumber = "P-1", Description = "Filter 1", UnitsPerCarton = 10, DefaultUnitPrice = 5m };
        var p2 = new Product { PartNumber = "P-2", Description = "Filter 2", UnitsPerCarton = 20, DefaultUnitPrice = 3m };
        db.AddRange(customer, p1, p2);
        await db.SaveChangesAsync();

        return (factory, customer.Id, p1.Id, p2.Id);
    }

    private static OrderService NewService(SqliteTestFactory factory) =>
        new(factory, new OrderNumberGenerator(factory));

    [Fact]
    public async Task Create_then_get_round_trips_lines()
    {
        var (factory, customerId, productId, product2Id) = await SeedAsync();
        using var _ = factory;
        var service = NewService(factory);

        var order = new Order
        {
            CustomerId = customerId,
            OrderDate = new DateOnly(2026, 5, 1),
            Currency = "EUR",
            Incoterm = "CIF Hamburg",
            Lines =
            {
                new OrderLine { ProductId = productId, Quantity = 100, UnitPrice = 4.5m },
                new OrderLine { ProductId = product2Id, Quantity = 50, UnitPrice = 3m },
            },
        };

        var id = await service.CreateAsync(order);
        var loaded = await service.GetAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal("EXP-2026-0001", loaded!.OrderNumber);
        Assert.Equal(2, loaded.Lines.Count);
        Assert.Equal([1, 2], loaded.Lines.OrderBy(l => l.LineNumber).Select(l => l.LineNumber));
        Assert.Equal(600m, loaded.Lines.Sum(l => l.Quantity * l.UnitPrice));
        Assert.NotEqual(default, loaded.CreatedUtc);
    }

    [Fact]
    public async Task Order_numbers_increment_within_a_year()
    {
        var (factory, customerId, productId, _) = await SeedAsync();
        using var _ = factory;
        var service = NewService(factory);

        var first = await service.CreateAsync(NewOrder(customerId, productId, 2026));
        var second = await service.CreateAsync(NewOrder(customerId, productId, 2026));
        var otherYear = await service.CreateAsync(NewOrder(customerId, productId, 2027));

        Assert.Equal("EXP-2026-0001", (await service.GetAsync(first))!.OrderNumber);
        Assert.Equal("EXP-2026-0002", (await service.GetAsync(second))!.OrderNumber);
        Assert.Equal("EXP-2027-0001", (await service.GetAsync(otherYear))!.OrderNumber);
    }

    [Fact]
    public async Task Update_adds_removes_and_edits_lines()
    {
        var (factory, customerId, productId, product2Id) = await SeedAsync();
        using var _ = factory;
        var service = NewService(factory);

        var id = await service.CreateAsync(new Order
        {
            CustomerId = customerId, Currency = "EUR", OrderDate = new DateOnly(2026, 1, 1),
            Lines = { new OrderLine { ProductId = productId, Quantity = 10, UnitPrice = 5m } },
        });

        var toEdit = await service.GetAsync(id);
        toEdit!.Lines[0].Quantity = 25;                         // edit
        toEdit.Lines.Add(new OrderLine { ProductId = product2Id, Quantity = 7, UnitPrice = 2m }); // add
        toEdit.Notes = "updated";
        await service.UpdateAsync(toEdit);

        var afterAdd = await service.GetAsync(id);
        Assert.Equal(2, afterAdd!.Lines.Count);
        Assert.Equal(25, afterAdd.Lines.Single(l => l.ProductId == productId).Quantity);
        Assert.Equal("updated", afterAdd.Notes);

        afterAdd.Lines.RemoveAll(l => l.ProductId == productId); // remove
        await service.UpdateAsync(afterAdd);

        var afterRemove = await service.GetAsync(id);
        Assert.Single(afterRemove!.Lines);
        Assert.Equal(product2Id, afterRemove.Lines[0].ProductId);
        Assert.Equal(1, afterRemove.Lines[0].LineNumber);
    }

    [Fact]
    public async Task Delete_removes_order_and_its_lines()
    {
        var (factory, customerId, productId, _) = await SeedAsync();
        using var _ = factory;
        var service = NewService(factory);

        var id = await service.CreateAsync(NewOrder(customerId, productId, 2026));
        await service.DeleteAsync(id);

        Assert.Null(await service.GetAsync(id));
        await using var db = factory.CreateDbContext();
        Assert.Empty(db.OrderLines);
    }

    private static Order NewOrder(int customerId, int productId, int year) => new()
    {
        CustomerId = customerId,
        Currency = "EUR",
        OrderDate = new DateOnly(year, 6, 15),
        Lines = { new OrderLine { ProductId = productId, Quantity = 10, UnitPrice = 5m } },
    };
}
