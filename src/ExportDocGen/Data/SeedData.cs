using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Data;

/// <summary>Seeds the two seller companies (always) and a couple of sample
/// customers (first run only). The product catalogue comes from the real stock
/// database — see <c>StockCatalogImportService</c> — not from seed data.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await EnsureSellerCompaniesAsync(db);

        if (await db.Customers.AnyAsync())
            return;

        var filtorq = await db.SellerCompanies.FirstAsync(s => s.ShortName == "Filtorq");
        var ikiler = await db.SellerCompanies.FirstAsync(s => s.ShortName == "İkiler");

        db.Customers.AddRange(
            new Customer
            {
                Name = "Muster Kfz-Teile GmbH",
                SellerCompanyId = filtorq.Id,
                TaxId = "DE811234567",
                AddressLine1 = "Industriestrasse 12",
                City = "Hamburg",
                PostalCode = "20095",
                Country = "Germany",
                DefaultIncoterm = "CIF Hamburg",
                DefaultCurrency = "EUR",
                ContactName = "H. Muster",
                ContactEmail = "purchasing@muster-example.de",
                ContactPhone = "+49 40 123456",
                PaymentType = PaymentTerm.Prepayment100,
            },
            new Customer
            {
                Name = "Gulf Auto Spare Parts LLC",
                SellerCompanyId = ikiler.Id,
                TaxId = "100234567800003",
                AddressLine1 = "Deira, Al Maktoum Road",
                City = "Dubai",
                Country = "United Arab Emirates",
                DefaultIncoterm = "FOB Izmir",
                DefaultCurrency = "USD",
                ContactName = "A. Rahman",
                ContactEmail = "orders@gulfspare-example.ae",
                ContactPhone = "+971 4 223 4455",
                PaymentType = PaymentTerm.Advance40Balance60,
            });

        await db.SaveChangesAsync();
    }

    /// <summary>Seeds the two exporting companies once. Runs on every startup
    /// (independently of the sample customers/products) so existing databases
    /// pick them up. Order 1 is Filtorq — the migration back-fills existing
    /// orders to it.</summary>
    private static async Task EnsureSellerCompaniesAsync(AppDbContext db)
    {
        if (await db.SellerCompanies.AnyAsync())
            return;

        db.SellerCompanies.AddRange(
            new SellerCompany
            {
                Name = "Filtorq Filtre İthalat İhracat Sanayi ve Tic. A.Ş.",
                ShortName = "Filtorq",
                ProformaTemplate = ProformaTemplate.FiltorqClassic,
                NumberFormat = SellerNumberFormat.ExpYearSeq,
                LetterheadPath = "wwwroot/proforma-letterhead.png",
                DefaultBankDetails = string.Join('\n',
                    "Company Name : Filtorq Filtre İthalat İhracat Sanayi ve Tic. A.Ş.",
                    "Our Bank : TÜRKİYE CUMHURİYETİ ZİRAAT BANKASI A.Ş.",
                    "Swift Code : TCZBTR2AXXX",
                    "IBAN NO : TR62 0001 0020 6383 4792 2750 06"),
            },
            new SellerCompany
            {
                Name = "İkiler Otomotiv Filtre İthalat İhracat Sanayi ve Ticaret A.Ş.",
                ShortName = "İkiler",
                ProformaTemplate = ProformaTemplate.IkilerGrid,
                NumberFormat = SellerNumberFormat.DateSlashSeq,
                LetterheadPath = "wwwroot/ikiler-letterhead.png",
                DefaultDeliveryTime = "6 WEEKS",
                DefaultValidity = "2 WEEKS FROM PROFORMA DATE",
                DefaultBankDetails = string.Join('\n',
                    "Company Name : İkiler Otomotiv Filtre İthalat İhracat Sanayi ve Ticaret Anonim Şirketi",
                    "Our Bank : ZİRAAT BANKASI",
                    "Branch : Denizli Ticari",
                    "Branch Code : 2142",
                    "Account No : 2063 3710 2798 5013",
                    "Swift Code : TCZBTR2AXXX",
                    "IBAN NO : TR 9200 0100 2063 3710 2798 5013"),
            });

        await db.SaveChangesAsync();
    }
}
