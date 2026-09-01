using ExportDocGen.Components;
using ExportDocGen.Data;
using ExportDocGen.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// SQLite database, stored in the OS local-app-data folder (outside the source tree).
var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ExportDocGen");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "exportdocgen.db");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<SellerCompanyService>();
builder.Services.AddScoped<OrderNumberGenerator>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<CalculationService>();
builder.Services.AddScoped<OrderDocumentService>();
builder.Services.AddSingleton<ExcelOrderImportParser>();
builder.Services.AddScoped<StockCatalogImportService>();

// QuestPDF Community license (free for companies under the revenue threshold).
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Apply migrations and seed the seller companies / sample customers on startup.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeededAsync(db);
}

// One-off / repeatable stock-catalogue import (no HTTP server):
//   dotnet run --project src/ExportDocGen -- import-stock <stocks.xlsx> [--replace]
if (args is ["import-stock", var stockPath, ..])
{
    using var scope = app.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<StockCatalogImportService>();
    await using var file = File.OpenRead(stockPath);
    var result = importer.Parse(file);
    Console.WriteLine(result.Summary());
    if (args.Contains("--replace"))
        Console.WriteLine($"\nReplaced the catalogue — {await importer.ReplaceCatalogueAsync(result.Rows)} products.");
    else
        Console.WriteLine("\n(dry run — pass --replace to write)");
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Generated documents (opened in a new browser tab from the order screens).
app.MapGet("/orders/{id:int}/proforma.pdf", (int id, OrderDocumentService d, HttpContext http) =>
    StreamDocument(http, "proforma invoice", () => d.BuildProformaAsync(id)));

app.MapGet("/orders/{id:int}/packing-list.pdf", (int id, OrderDocumentService d, HttpContext http) =>
    StreamDocument(http, "packing list", () => d.BuildPackingListAsync(id)));

app.MapGet("/orders/{id:int}/packing-list.xlsx", (int id, OrderDocumentService d, HttpContext http) =>
    StreamDocument(http, "packing list", () => d.BuildPackingListXlsxAsync(id)));

app.MapGet("/orders/{id:int}/commercial-invoice.pdf", (int id, OrderDocumentService d, HttpContext http) =>
    StreamDocument(http, "commercial invoice", () => d.BuildCommercialInvoiceAsync(id)));

app.MapGet("/orders/{id:int}/commercial-invoice.xlsx", (int id, OrderDocumentService d, HttpContext http) =>
    StreamDocument(http, "commercial invoice", () => d.BuildCommercialInvoiceXlsxAsync(id)));

static async Task<IResult> StreamDocument(
    HttpContext http, string label, Func<Task<GeneratedDocument?>> build)
{
    GeneratedDocument? doc;
    try
    {
        doc = await build();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not generate the {label}: {ex.Message}");
    }

    if (doc is null)
        return Results.NotFound();

    // PDFs preview inline in a new tab; spreadsheets download.
    var disposition = doc.IsXlsx ? "attachment" : "inline";
    http.Response.Headers.ContentDisposition = $"{disposition}; filename=\"{doc.FileName}\"";
    return Results.Bytes(doc.Bytes, doc.ContentType);
}

app.Run();
