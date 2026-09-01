using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Services;

/// <summary>Read access to the group's exporting companies. There is no UI to
/// create or edit these yet — they are seeded (see <see cref="SeedData"/>).</summary>
public class SellerCompanyService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<SellerCompany>> GetAllAsync(bool includeInactive = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.SellerCompanies.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.ShortName).ToListAsync();
    }

    public async Task<SellerCompany?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SellerCompanies.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }
}
