using ExportDocGen.Data;
using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Services;

/// <summary>Produces the next order / proforma number for a seller company. Each
/// company has its own independent sequence, formed per
/// <see cref="SellerCompany.NumberFormat"/>:
/// <list type="bullet">
///   <item><see cref="SellerNumberFormat.ExpYearSeq"/> — "EXP-{year}-{seq:0000}", per year.</item>
///   <item><see cref="SellerNumberFormat.DateSlashSeq"/> — "{yyMMdd}/{seq}", per day.</item>
/// </list>
/// </summary>
public class OrderNumberGenerator(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<string> NextAsync(SellerCompany seller, DateOnly orderDate)
    {
        var prefix = seller.NumberFormat switch
        {
            SellerNumberFormat.DateSlashSeq => $"{orderDate:yyMMdd}/",
            _ => $"EXP-{orderDate.Year}-",
        };

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.Orders
            .Where(o => o.SellerCompanyId == seller.Id && o.OrderNumber.StartsWith(prefix))
            .Select(o => o.OrderNumber)
            .ToListAsync();

        var nextSeq = existing
            .Select(n => int.TryParse(n.AsSpan(prefix.Length), out var s) ? s : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return seller.NumberFormat == SellerNumberFormat.DateSlashSeq
            ? $"{prefix}{nextSeq}"
            : $"{prefix}{nextSeq:0000}";
    }
}
