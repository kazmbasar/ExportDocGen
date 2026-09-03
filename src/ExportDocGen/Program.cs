using System.Security.Claims;
using ExportDocGen.Auth;
using ExportDocGen.Components;
using ExportDocGen.Data;
using ExportDocGen.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using QuestPDF.Infrastructure;

// One-off password hashing helper — no host, no DB:
//   dotnet run --project src/ExportDocGen -- hash-password [plaintext]
if (args is ["hash-password", ..])
{
    var plain = args.Length > 1 ? args[1] : ReadHidden("New password: ");
    if (string.IsNullOrEmpty(plain))
    {
        Console.Error.WriteLine("No password given.");
        return;
    }
    Console.WriteLine();
    Console.WriteLine("Set this as the Auth__PasswordHash environment variable on the server:");
    Console.WriteLine();
    Console.WriteLine(PasswordHash.Create(plain));
    return;

    static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && buffer.Length > 0) buffer.Length--;
            else if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
        }
        return buffer.ToString();
    }
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Data lives outside the source tree: the OS local-app-data folder for local runs,
// or the DataDir setting (a mounted volume) in the container.
var dataDir = builder.Configuration["DataDir"]
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ExportDocGen");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "exportdocgen.db");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Persist Data Protection keys so auth cookies survive a restart / redeploy.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("ExportDocGen");

// Single shared login (cookie auth). Credential comes from the Auth section.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<PasswordAuthenticator>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ExportDocGen.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.ReturnUrlParameter = "returnUrl";
    });

builder.Services.AddAuthorization(options =>
{
    // Everything that doesn't opt out (the document endpoints, above all) needs a login.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

// Behind nginx (TLS terminator) in production — trust its forwarded headers.
// The container listens on loopback only, so nginx is the only possible peer.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous(); // page-level gate is [Authorize] in _Imports + AuthorizeRouteView

// --- Authentication endpoints ---------------------------------------------------

app.MapPost("/auth/login", async (
    HttpContext http,
    [FromForm] string? password,
    [FromForm] string? returnUrl,
    PasswordAuthenticator auth) =>
{
    var target = ToLocalUrl(returnUrl);

    if (!auth.Verify(password))
        return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(target)}");

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, auth.UserName)],
        CookieAuthenticationDefaults.AuthenticationScheme);

    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });

    return Results.LocalRedirect(target);
}).AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).DisableAntiforgery(); // authenticated + harmless; keeps the form simple inside the interactive layout

static string ToLocalUrl(string? url) =>
    !string.IsNullOrEmpty(url)
    && url.StartsWith('/')
    && !url.StartsWith("//")
    && !url.StartsWith("/\\")
    && Uri.IsWellFormedUriString(url, UriKind.Relative)
        ? url
        : "/";

// --- Generated documents (opened in a new browser tab from the order screens) ---

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
