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

// Company header details for generated documents.
builder.Services.Configure<CompanyProfile>(
    builder.Configuration.GetSection(CompanyProfile.SectionName));

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

// QuestPDF Community license (free for companies under the revenue threshold).
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Apply migrations and seed sample data on startup.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeededAsync(db);
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

app.Run();
