using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Services;

public class OrderService(
    IDbContextFactory<AppDbContext> dbFactory,
    OrderNumberGenerator numberGenerator)
{
    /// <summary>Order-list rows with the customer name and a money subtotal.</summary>
    public async Task<List<OrderListItem>> GetListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedUtc)
            .Select(o => new OrderListItem(
                o.Id,
                o.OrderNumber,
                o.OrderDate,
                o.Customer!.Name,
                o.SellerCompany!.ShortName,
                o.Currency,
                o.Lines.Sum(l => l.Quantity * l.UnitPrice),
                o.Lines.Count))
            .ToListAsync();
    }

    /// <summary>Full order with lines and their products, for the builder screen.</summary>
    public async Task<Order?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.SellerCompany)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<int> CreateAsync(Order order)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // The seller company always follows the customer's exporter company.
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == order.CustomerId)
            ?? throw new InvalidOperationException("Choose a customer for the order.");
        order.SellerCompanyId = customer.SellerCompanyId;

        var seller = await db.SellerCompanies.FirstOrDefaultAsync(s => s.Id == order.SellerCompanyId)
            ?? throw new InvalidOperationException("The customer has no exporter company set.");

        order.OrderNumber = await numberGenerator.NextAsync(seller, order.OrderDate);
        order.CreatedUtc = DateTime.UtcNow;
        Renumber(order.Lines);
        // Attach only the FK, not the navigation, so EF doesn't try to insert products.
        foreach (var line in order.Lines) line.Product = null;

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    public async Task UpdateAsync(Order order)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existing = await db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == order.Id)
            ?? throw new InvalidOperationException($"Order {order.Id} not found.");

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == order.CustomerId)
            ?? throw new InvalidOperationException("Choose a customer for the order.");

        existing.CustomerId = order.CustomerId;
        // Seller follows the customer's exporter company (the order number keeps
        // its original format).
        existing.SellerCompanyId = customer.SellerCompanyId;
        existing.OrderDate = order.OrderDate;
        existing.Incoterm = order.Incoterm;
        existing.Currency = order.Currency;
        existing.PaymentTerms = order.PaymentTerms;
        existing.BankDetails = order.BankDetails;
        existing.DeliveryTime = order.DeliveryTime;
        existing.Validity = order.Validity;
        existing.InvoiceNumber = order.InvoiceNumber;
        existing.InvoiceDate = order.InvoiceDate;
        existing.Pallets = order.Pallets;
        existing.Notes = order.Notes;

        Renumber(order.Lines);

        // Remove lines that are gone.
        existing.Lines.RemoveAll(el => order.Lines.All(nl => nl.Id != el.Id));

        foreach (var incoming in order.Lines)
        {
            var current = incoming.Id != 0
                ? existing.Lines.FirstOrDefault(l => l.Id == incoming.Id)
                : null;

            if (current is null)
            {
                existing.Lines.Add(new OrderLine
                {
                    ProductId = incoming.ProductId,
                    Quantity = incoming.Quantity,
                    UnitPrice = incoming.UnitPrice,
                    LineNumber = incoming.LineNumber,
                });
            }
            else
            {
                current.ProductId = incoming.ProductId;
                current.Quantity = incoming.Quantity;
                current.UnitPrice = incoming.UnitPrice;
                current.LineNumber = incoming.LineNumber;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var order = await db.Orders.FindAsync(id);
        if (order is null) return;
        db.Orders.Remove(order); // lines cascade
        await db.SaveChangesAsync();
    }

    private static void Renumber(List<OrderLine> lines)
    {
        for (var i = 0; i < lines.Count; i++)
            lines[i].LineNumber = i + 1;
    }
}

public record OrderListItem(
    int Id,
    string OrderNumber,
    DateOnly OrderDate,
    string CustomerName,
    string SellerShortName,
    string Currency,
    decimal Subtotal,
    int LineCount);
