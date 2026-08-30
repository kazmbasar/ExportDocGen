# ExportDocGen — Architecture

_Created: 2026-08-31_

## Overview

Single ASP.NET Core **Blazor Web App** process. No separate API. One SQLite file
for storage. PDFs generated in-process with QuestPDF and streamed to the browser
as a download.

```
Browser ──HTTP/SignalR──> Blazor Web App (Server interactivity)
                             │
                             ├── EF Core ──> SQLite file (exportdocgen.db)
                             └── QuestPDF ──> PDF byte stream ──> download
```

## Why these choices

| Choice | Reason |
|--------|--------|
| Blazor Web App, **global Server** interactivity (`@rendermode="InteractiveServer"` on `<HeadOutlet>` + `<Routes>` in `App.razor`) | Single language (C#) for UI + logic; no API layer; no WASM download; fine for a local single-user tool. Global (not per-page) because every screen here is interactive. |
| **SQLite** | Zero setup, single file, trivial backup (copy the file). Swap to PostgreSQL later only if it becomes multi-user. |
| **EF Core Code-First + migrations** | Familiar, versioned schema, easy seeding. |
| **QuestPDF** | Clean C# layout API, strong docs, actively maintained; good fit for structured business documents. |
| **MudBlazor** | Free, complete Material component set — data grids, dialogs, forms, snackbars — saves building UI primitives. |

## Project structure (inside `src/ExportDocGen/`)

```
src/ExportDocGen/
├── Program.cs                    # DI, DbContext, MudBlazor, QuestPDF license
├── appsettings.json              # CompanyProfile section (header/bank details)
├── Data/
│   ├── AppDbContext.cs           # entity config lives inline in OnModelCreating for now
│   ├── Entities/                 # Customer, Product, Order, OrderLine
│   ├── CompanyProfile.cs         # options bound from appsettings "CompanyProfile"
│   └── SeedData.cs               # sample customers + products on first run
├── Services/                    # scoped; injected into components; use IDbContextFactory
│   ├── CustomerService.cs        # ✅ M2 — GetAll/Get/Create/Update/Delete
│   ├── ProductService.cs         # ✅ M2 — + PartNumberExistsAsync, includeInactive filter
│   ├── OrderService.cs           # ✅ M3 — list/get/create/update/delete + line reconcile
│   ├── OrderNumberGenerator.cs   # ✅ M3 — "EXP-{year}-{seq:0000}", per-year sequence
│   └── CalculationService.cs     # M4 — all computed values from DATA-MODEL.md
├── Documents/                    # QuestPDF IDocument classes
│   ├── ProformaInvoiceDocument.cs
│   ├── PackingListDocument.cs
│   └── Shared/                   # header/footer components, styles
├── Components/
│   ├── Pages/
│   │   ├── Customers/            # ✅ CustomerList + CustomerDialog
│   │   ├── Products/             # ✅ ProductList + ProductDialog
│   │   └── Orders/               # ✅ OrderList + OrderEdit (new + edit share one page)
│   └── Layout/
└── Migrations/                   # EF Core generated
```

`tests/ExportDocGen.Tests/` — xUnit. `SqliteTestFactory` gives each test an
isolated in-memory SQLite database (connection kept open) behind
`IDbContextFactory<AppDbContext>`, so FK/cascade/unique-index behaviour is real.
Current coverage: `OrderService` round-trip + line reconciliation, order-number
sequencing, delete guards. `CalculationService` is the next target (M4).

## Key conventions

- **Entity configuration** is currently inline in `AppDbContext.OnModelCreating`
  (decimal precision 18,3 for all money/measures; unique indexes on
  `Product.PartNumber` and `Order.OrderNumber`; cascade delete for `OrderLine`).
  Extract to `IEntityTypeConfiguration<T>` classes if it grows.
- **Startup:** `Program.cs` runs `db.Database.MigrateAsync()` then
  `SeedData.EnsureSeededAsync()` on boot.
- **EF tooling:** `dotnet-ef` is a **local** tool (`dotnet-tools.json`); run it as
  `dotnet dotnet-ef …` after `dotnet tool restore`.
- **Services own business logic.** Components call services; components don't
  touch `AppDbContext` directly.
- **DbContext lifetime:** use `AddDbContextFactory` and create a short-lived
  context per operation (Blazor Server + scoped `DbContext` is a known
  footgun with concurrent renders).
- **Money & measures:** `decimal` everywhere; never `double`. Configure SQLite
  decimal precision in entity configurations.
- **Computed values are never persisted** — always via `CalculationService`.
- **Company header** comes from `CompanyProfile` config bound in `Program.cs`:

  ```json
  "CompanyProfile": {
    "Name": "«Company name»",
    "AddressLines": ["«street»", "«city, country»"],
    "TaxId": "«VKN»",
    "Phone": "«phone»",
    "Email": "«email»",
    "Bank": { "BeneficiaryName": "«»", "Iban": "«»", "Swift": "«»", "BankName": "«»" },
    "LogoPath": "wwwroot/company-logo.png"
  }
  ```

- **PDF download:** a minimal API endpoint or Blazor `NavigationManager` to a
  handler returning `application/pdf`, e.g.
  `GET /orders/{id}/proforma.pdf` and `/orders/{id}/packing-list.pdf`.

## Database location

`exportdocgen.db` in the OS app-data folder
(`Environment.SpecialFolder.LocalApplicationData/ExportDocGen/`), created on
startup if missing; connection string built in `Program.cs`. Keeps the DB out of
the source tree.

## Deployment (later)

`Dockerfile` (multi-stage `dotnet publish`) authored during M6 but not part of
MVP. Local run is `dotnet run` or a published self-contained binary.
