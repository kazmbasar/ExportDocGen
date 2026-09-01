using ExportDocGen.Data.Entities;

namespace ExportDocGen.Tests;

internal static class TestData
{
    /// <summary>Inserts a seller company (orders now require one) and returns it.</summary>
    public static async Task<SellerCompany> SeedSellerAsync(
        SqliteTestFactory factory,
        string shortName = "TestCo",
        ProformaTemplate template = ProformaTemplate.FiltorqClassic,
        SellerNumberFormat numberFormat = SellerNumberFormat.ExpYearSeq)
    {
        await using var db = factory.CreateDbContext();
        var seller = new SellerCompany
        {
            Name = $"{shortName} Filtre A.Ş.",
            ShortName = shortName,
            ProformaTemplate = template,
            NumberFormat = numberFormat,
            DefaultBankDetails = $"Company Name : {shortName} Filtre A.Ş.\nIBAN NO : TR00 0000 0000",
        };
        db.SellerCompanies.Add(seller);
        await db.SaveChangesAsync();
        return seller;
    }

    /// <summary>Inserts a customer that exports through <paramref name="sellerId"/>
    /// and returns its id.</summary>
    public static async Task<int> SeedCustomerAsync(
        SqliteTestFactory factory, int sellerId, string name = "Test Buyer")
    {
        await using var db = factory.CreateDbContext();
        var customer = new Customer
        {
            Name = name,
            SellerCompanyId = sellerId,
            AddressLine1 = "1 Road",
            Country = "Testland",
            DefaultCurrency = "USD",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }
}
