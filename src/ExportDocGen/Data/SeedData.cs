using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Data;

/// <summary>Inserts sample customers and products on first run so the app is not empty.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await EnsureSellerCompaniesAsync(db);

        if (await db.Products.AnyAsync() || await db.Customers.AnyAsync())
            return;

        db.Customers.AddRange(
            new Customer
            {
                Name = "Muster Kfz-Teile GmbH",
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

        db.Products.AddRange(
            new Product
            {
                PartNumber = "AF-1042", Description = "Air filter element, heavy-duty truck",
                HsCode = "8421.31", FilterType = "air",
                NetWeightKg = 0.85m, GrossWeightKg = 0.95m,
                UnitsPerCarton = 12,
                CartonLengthCm = 60, CartonWidthCm = 40, CartonHeightCm = 45,
                CartonTareWeightKg = 0.9m, DefaultUnitPrice = 7.40m,
            },
            new Product
            {
                PartNumber = "OF-2210", Description = "Oil filter, spin-on, passenger car",
                HsCode = "8421.23", FilterType = "oil",
                NetWeightKg = 0.32m, GrossWeightKg = 0.36m,
                UnitsPerCarton = 24,
                CartonLengthCm = 40, CartonWidthCm = 30, CartonHeightCm = 25,
                CartonTareWeightKg = 0.6m, DefaultUnitPrice = 2.10m,
            },
            new Product
            {
                PartNumber = "FF-3305", Description = "Fuel filter, diesel, water separator",
                HsCode = "8421.23", FilterType = "fuel",
                NetWeightKg = 0.41m, GrossWeightKg = 0.47m,
                UnitsPerCarton = 20,
                CartonLengthCm = 45, CartonWidthCm = 30, CartonHeightCm = 30,
                CartonTareWeightKg = 0.7m, DefaultUnitPrice = 4.85m,
            },
            new Product
            {
                PartNumber = "CF-4120", Description = "Cabin air filter, activated carbon",
                HsCode = "8421.39", FilterType = "cabin",
                NetWeightKg = 0.22m, GrossWeightKg = 0.26m,
                UnitsPerCarton = 30,
                CartonLengthCm = 50, CartonWidthCm = 40, CartonHeightCm = 20,
                CartonTareWeightKg = 0.5m, DefaultUnitPrice = 3.30m,
            },
            new Product
            {
                PartNumber = "AF-1088", Description = "Air filter panel, passenger car",
                HsCode = "8421.31", FilterType = "air",
                NetWeightKg = 0.18m, GrossWeightKg = 0.21m,
                UnitsPerCarton = 40,
                CartonLengthCm = 55, CartonWidthCm = 40, CartonHeightCm = 30,
                CartonTareWeightKg = 0.6m, DefaultUnitPrice = 2.65m,
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
