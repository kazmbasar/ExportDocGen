using ExportDocGen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExportDocGen.Data;

/// <summary>Inserts sample customers and products on first run so the app is not empty.</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync() || await db.Customers.AnyAsync())
            return;

        db.Customers.AddRange(
            new Customer
            {
                Name = "Muster Kfz-Teile GmbH",
                AddressLine1 = "Industriestrasse 12",
                City = "Hamburg",
                PostalCode = "20095",
                Country = "Germany",
                DefaultIncoterm = "CIF Hamburg",
                DefaultCurrency = "EUR",
                ContactName = "H. Muster",
                ContactEmail = "purchasing@muster-example.de",
            },
            new Customer
            {
                Name = "Gulf Auto Spare Parts LLC",
                AddressLine1 = "Deira, Al Maktoum Road",
                City = "Dubai",
                Country = "United Arab Emirates",
                DefaultIncoterm = "FOB Istanbul",
                DefaultCurrency = "USD",
                ContactName = "A. Rahman",
                ContactEmail = "orders@gulfspare-example.ae",
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
}
