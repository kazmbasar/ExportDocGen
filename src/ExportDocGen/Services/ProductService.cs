using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Services;

public class ProductService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<Product>> GetAllAsync(bool includeInactive = true)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Products.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.PartNumber).ToListAsync();
    }

    public async Task<Product?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> PartNumberExistsAsync(string partNumber, int? excludeId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Products.AnyAsync(p =>
            p.PartNumber == partNumber && (excludeId == null || p.Id != excludeId));
    }

    public async Task<Product> CreateAsync(Product product)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Products.Update(product);
        await db.SaveChangesAsync();
    }

    /// <summary>Deletes a product. Returns false if it is used on any order line.</summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var inUse = await db.OrderLines.AnyAsync(l => l.ProductId == id);
        if (inUse)
            return false;

        var product = await db.Products.FindAsync(id);
        if (product is null)
            return true;

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return true;
    }
}
