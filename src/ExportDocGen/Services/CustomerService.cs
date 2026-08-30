using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Services;

public class CustomerService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<Customer>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Customer?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    public async Task UpdateAsync(Customer customer)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Customers.Update(customer);
        await db.SaveChangesAsync();
    }

    /// <summary>Deletes a customer. Returns false if the customer still has orders.</summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var hasOrders = await db.Orders.AnyAsync(o => o.CustomerId == id);
        if (hasOrders)
            return false;

        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
            return true;

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return true;
    }
}
