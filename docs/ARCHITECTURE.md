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
│   ├── CalculationService.cs     # ✅ M4 — pure; line + order money/weight/carton/CBM
│   ├── ExcelOrderImportParser.cs # ✅ M5 — pure; reads a customer .xlsx into line rows
│   └── OrderDocumentService.cs   # ✅ M6 — loads order + calc + company profile → PDF bytes
├── Documents/                    # QuestPDF IDocument classes
│   ├── ProformaInvoiceModel.cs   # ✅ M6 — flat print model + From(order, calc, company)
│   ├── ProformaInvoiceDocument.cs# ✅ M6 — pure A4 layout
│   └── PackingListDocument.cs    # (M7)
├── Components/
│   ├── Pages/
│   │   ├── Customers/            # ✅ CustomerList + CustomerDialog
│   │   ├── Products/             # ✅ ProductList + ProductDialog
│   │   └── Orders/               # ✅ OrderList + OrderEdit (new+edit) + OrderImport (/orders/import)
│   └── Layout/
├── Migrations/                   # EF Core generated
└── Program.cs                    # + GET /orders/{id}/proforma.pdf minimal-API endpoint
```

`tests/ExportDocGen.Tests/` — xUnit. `SqliteTestFactory` gives each test an
isolated in-memory SQLite database (connection kept open) behind
`IDbContextFactory<AppDbContext>`, so FK/cascade/unique-index behaviour is real.
Current coverage (28 tests): `OrderService` round-trip + line reconciliation,
order-number sequencing, delete guards, `CalculationService` line/order
formulas, carton rounding, money rounding, the proforma model/document/service
(mapping, `%PDF` output, saved-order round-trip), and `ExcelOrderImportParser`
against the two real FILTORQ sample workbooks (row count, totals-row cut-off, code
normalization, bad-cell flagging, header detection below a title row).

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
- **Company header** comes from `CompanyProfile` config bound in `Program.cs`
  (`appsettings.json` → `"CompanyProfile"`: `Name`, `AddressLines`, `TaxId`,
  `Phone`, `Email`, `CountryOfOrigin`, `Bank`, `LogoPath`). Fill in real values
  there — no code change. Collection defaults in the POCO are empty on purpose
  (the config binder *appends* to a non-empty array).
- **PDF download:** `GET /orders/{id}/proforma.pdf` (minimal-API endpoint in
  `Program.cs`) → `OrderDocumentService.BuildProformaAsync` → 404 if the order
  is missing, else `application/pdf`. The order screens link to it with
  `target="_blank"`. `/orders/{id}/packing-list.pdf` follows in M7.
- **Document formatting:** QuestPDF document classes format all numbers and
  dates with `CultureInfo.InvariantCulture` — export documents are in English
  regardless of the server locale.

## Database location

`exportdocgen.db` in the OS app-data folder
(`Environment.SpecialFolder.LocalApplicationData/ExportDocGen/`), created on
startup if missing; connection string built in `Program.cs`. Keeps the DB out of
the source tree.

## Deployment (later)

`Dockerfile` (multi-stage `dotnet publish`) authored during M7 but not part of
MVP. Local run is `dotnet run` or a published self-contained binary.

## Excel order import (M5) — done

- **`Services/ExcelOrderImportParser.cs`** — pure, no DB. `Parse(Stream)` reads
  the first worksheet with **ClosedXML** (0.105.1, MIT), locates the header row
  by name, maps `CODE` / `QTY` / `UNIT PRICE` / `TOTAL` columns via a synonym
  list, and returns an `ImportedSheet` (`ImportedRow` list + warnings). Reading
  stops at the first blank code (skips a trailing totals row). Rows with an
  unreadable quantity/price get an `Error` instead of aborting the file.
  `NormalizeCode` (trim + upper-case + strip whitespace) is the shared key for
  catalog matching. Registered as a singleton (stateless).
- **`Components/Pages/Orders/OrderImport.razor`** (`/orders/import`) — upload →
  review → create wizard. Loads the active catalog, auto-matches each code to a
  `Product` by normalized part number, pre-selects the customer from a filename
  token, and shows an editable per-row table (product, qty, price, line total,
  Import tick) with live totals from `CalculationService`. On confirm it builds
  an `Order` from the ticked matched rows and calls `OrderService.CreateAsync`
  — nothing is written before that.

See `docs/PLANNING.md` → "Excel order import (M5) — as built" for the confirmed
spreadsheet layout and scope boundaries.
