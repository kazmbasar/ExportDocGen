using ExportDocGen.Data;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Services;

/// <summary>Produces the next order number in the form "EXP-{year}-{sequence:0000}".</summary>
public class OrderNumberGenerator(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<string> NextAsync(int? year = null)
    {
        year ??= DateTime.Today.Year;
        var prefix = $"EXP-{year}-";

        await using var db = await dbFactory.CreateDbContextAsync();
        var lastForYear = await db.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (lastForYear is not null &&
            int.TryParse(lastForYear[prefix.Length..], out var lastSeq))
        {
            nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:0000}";
    }
}
