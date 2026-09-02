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
| **MudBlazor** | Free, complete Material component set — data grids, dialogs, forms, snackbars — saves building UI primitives. Themed in `AppTheme.cs` (M9): one house theme, two faces — **Ledger** light / **Console** dark — following the OS setting. |

## Project structure (inside `src/ExportDocGen/`)

```
src/ExportDocGen/
├── Program.cs                    # DI, DbContext, MudBlazor, QuestPDF license
├── appsettings.json              # logging + hosts only (CompanyProfile retired in M6.5)
├── Components/
│   ├── App.razor                 # host page — font <link>, pre-hydration dark-ground script
│   ├── AppTheme.cs               # ✅ M9 — MudTheme: Ledger (light) + Console (dark) palettes + typography
│   ├── Layout/MainLayout.razor   # ✅ M9 — theme provider (OS dark mode), t-light/t-dark shell, brand + nav
│   └── Pages/                    # dashboard, customer/product/order screens
├── wwwroot/app.css               # ✅ M9 — theme layer: serif headings (light), nav rail, stat tiles, chips
├── Data/
│   ├── AppDbContext.cs           # entity config lives inline in OnModelCreating for now
│   ├── Entities/                 # Customer, Product, Order, OrderLine, SellerCompany, PaymentTerm
│   └── SeedData.cs               # 2 seller companies (always) + sample customers/products (first run)
├── Services/                    # scoped; injected into components; use IDbContextFactory
│   ├── CustomerService.cs        # ✅ M2 — GetAll/Get/Create/Update/Delete
│   ├── ProductService.cs         # ✅ M2 — + PartNumberExistsAsync, includeInactive filter
│   ├── SellerCompanyService.cs   # ✅ M6.5 — read-only list/get of the seeded companies
│   ├── OrderService.cs           # ✅ M3 — list/get/create/update/delete + line reconcile
│   ├── OrderNumberGenerator.cs   # ✅ M6.5 — per-company sequence, format per SellerNumberFormat
│   ├── CalculationService.cs     # ✅ M4 — pure; per-unit money / net / gross / CBM × quantity
│   ├── ExcelOrderImportParser.cs # ✅ M5 — pure; reads a customer .xlsx into line rows
│   ├── StockCatalogImportService.cs # ✅ loads the stock DB (.xlsx) → product catalogue
│   └── OrderDocumentService.cs   # ✅ M6 — loads order + calc + seller → picks template → proforma / packing list bytes
├── Documents/                    # QuestPDF IDocument classes
│   ├── DocFormat.cs              # ✅ M7 — shared date / weight / money / address / description formatting
│   ├── ProformaInvoiceModel.cs   # ✅ M6 — flat print model + From(order, calc, seller)
│   ├── ProformaInvoiceDocument.cs# ✅ M6 — Filtorq proforma (FiltorqClassic)
│   ├── IkilerProformaDocument.cs # ✅ M6.5 — İkiler proforma (IkilerGrid)
│   ├── MoneyWords.cs             # ✅ M6.5 — grand total spelled out (Humanizer.Core)
│   ├── PackingListModel.cs       # ✅ M7 — flat print model + From(order, calc, seller)
│   ├── PackingListDocument.cs    # ✅ M7 — one shared A4-portrait layout, letterhead per seller
│   ├── CommercialInvoiceModel.cs # ✅ M8 — flat print model
│   ├── CommercialInvoiceDocument.cs # ✅ M8 — one shared layout, letterhead per seller
│   └── OrderWorkbooks.cs         # ✅ M8 — ClosedXML: packing list + commercial invoice .xlsx
├── wwwroot/
│   ├── proforma-letterhead.png   # ✅ M6 — Filtorq full-page A4 letterhead (PDF background)
│   └── ikiler-letterhead.png     # ✅ M6.5 — İkiler header band (drawn at page top)
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
Current coverage (36 tests): `OrderService` round-trip + line reconciliation,
per-company order-number sequencing, delete guards, `CalculationService`
per-unit formulas, money rounding, the proforma & packing-list
model/document/service (mapping, template selection, `%PDF` output, saved-order
round-trip), `StockCatalogImportService` (filter-only, dedupe, Turkish decimals,
gross uplift, in-use refusal), and `ExcelOrderImportParser`
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
- **Seller companies** live in the `SellerCompany` table (seeded, no CRUD UI).
  Each carries its `ProformaTemplate`, `NumberFormat`, `LetterheadPath` and
  default bank / delivery / validity text. Replaced the former `CompanyProfile`
  config (retired in M6.5). **The exporter company is a property of the
  customer** (`Customer.SellerCompanyId`); `OrderService.CreateAsync` copies it
  onto the order, so the whole order/proforma pipeline follows the customer's
  company with no per-order choice.
- **Proforma letterhead:** `SellerCompany.LetterheadPath` — a full-page A4
  background for Filtorq (`wwwroot/proforma-letterhead.png`), a top header band
  for İkiler (`wwwroot/ikiler-letterhead.png`). `OrderDocumentService` resolves
  asset paths against `IHostEnvironment.ContentRootPath` and passes bytes into
  the pure document model. No letterhead → plain text header.
- **PDF download:** `GET /orders/{id}/proforma.pdf` (minimal-API endpoint in
  `Program.cs`) → `OrderDocumentService.BuildProformaAsync` → 404 for a missing
  order, `Problem` on a render error, else `application/pdf` with
  `Content-Disposition: inline`. The order screens link to it with
  `target="_blank"`. `/orders/{id}/packing-list.pdf` follows in M7.
- **Document formatting:** each template matches its company's own —
  currency-symbol prefix + comma decimals, space thousands for Filtorq
  (`$3 624,88`), dot thousands for İkiler (`$11.904,00`); dates `dd.MM.yyyy`.

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

## Stock catalogue import — done

- **`Services/StockCatalogImportService.cs`** — reads the company stock database
  (an `.xlsx` export of `stocks.ods`) with ClosedXML. `Parse(Stream)` locates the
  header by name (accent-folded: `Description / MENŞEİ / MARKA / CİNSİ / GTIP /
  Net weight / m3`), keeps only rows whose type contains "FILTER", dedupes by
  code, and returns `StockRow`s plus a skip / zero-value summary.
  `ReplaceCatalogueAsync` wipes `Products` (refusing if any is on an order line)
  and batch-inserts, with `GrossWeightKg = NetWeightKg × 1.05`.
- **Run it:** `dotnet run --project src/ExportDocGen -- import-stock
  <stocks.xlsx> [--replace]` (a branch at the top of `Program.cs`, before the
  HTTP server starts). Dry run without `--replace`. No browser page yet.
- **`Product`** has no carton fields — `Origin`, `Brand`, `UnitVolumeM3` instead;
  `CalculationService` is pure per-unit arithmetic. The order builder and the
  Excel-import review use `MudAutocomplete` for the ~16k-row catalogue.

See `docs/DECISIONS.md` (2026-09-01, stock catalogue import).

## Proforma invoice (M6 / M6.5) — done

- **`Documents/ProformaInvoiceModel.cs`** — flat, print-ready record;
  `From(order, calculation, seller, letterhead?)` maps a saved order + its
  `CalculationService` totals + the issuing `SellerCompany` into it (bank text,
  amount-in-words, per-line filter description, delivery/validity). No DB/IO.
- **Two template classes, both pure QuestPDF `IDocument`:**
  - **`ProformaInvoiceDocument.cs`** — Filtorq (`FiltorqClassic`): full-page
    letterhead background, buyer/invoice box, delivery & payment, verbatim
    `Bank Detail (<CUR>)` block, page break, then the
    `FILTORQ CODE | QUANTITY | PRICE | TOTAL` table with a gold grand-total box.
    Money `$3 624,88`.
  - **`IkilerProformaDocument.cs`** — İkiler (`IkilerGrid`): drawn header band +
    text office footer, 3-row buyer box, 5-column bordered grid
    `PRODUCT CODE | DESCRIPTION | UNIT PRICE | QUANTITY | TOTAL PRICE` with an
    inline Σqty/Σtotal row, the grand total spelled out (`MoneyWords`, via
    `Humanizer.Core`), `INCOTERMS / DELIVERY TIME / VALIDITY / PAYMENT TERM`,
    verbatim bank block. Money `$11.904,00`.
  - Dates `dd.MM.yyyy` in both.
- **`Services/OrderDocumentService.cs`** — the only DB/IO piece: `PrepareAsync`
  loads the order (incl. `SellerCompany`), runs `CalculationService` and reads
  the seller's letterhead bytes (resolved against
  `IHostEnvironment.ContentRootPath`); `BuildProformaAsync` /
  `BuildPackingListAsync` then build the model, **pick the document class by
  `SellerCompany.ProformaTemplate`**, and return `GeneratedDocument(bytes,
  fileName)`.
- **Endpoints** in `Program.cs` (shared `StreamDocument` helper — try/catch →
  `Problem`, 404; `inline` for PDF, `attachment` for `.xlsx`):
  `GET /orders/{id}/proforma.pdf`,
  `/orders/{id}/packing-list.pdf` · `.xlsx`,
  `/orders/{id}/commercial-invoice.pdf` · `.xlsx`.
- **Commercial invoice (M8):** `CommercialInvoiceModel` +
  `CommercialInvoiceDocument` — one shared layout, per-company letterhead (same
  `ProformaTemplate` branch as the packing list). `Order.InvoiceNumber` /
  `InvoiceDate` / `Pallets` (all optional) feed the `INVOICE NO` / `DATE` and the
  `TOTAL VOLUME : N PALLETS` line, falling back to the order number / date / CBM.
- **Excel (M8):** `OrderWorkbooks` (ClosedXML) builds the packing list and
  commercial invoice as editable `.xlsx` (plain values, no formulas) from the
  same models. The packing-list workbook uses the real 13-column layout.
- The proforma templates omit HS code and weight/carton/CBM figures — neither
  real template has them.

## Packing list (M7 / M7b)

- **`Documents/PackingListModel.cs`** — `From(order, calculation, seller,
  letterhead?)`, mirrors `ProformaInvoiceModel`. `PackingLine` and totals come
  straight from the `CalculationService` `OrderCalculation` (net weight, gross
  weight, CBM), plus the buyer, `InvoiceNumber` / `InvoiceDate` (fall back to the
  order) and `Pallets`.
- **`Documents/PackingListDocument.cs`** — **one** shared A4 layout for both
  companies (letterhead swapped in as for the proforma), matched to the real
  issued document (M7b): a bordered buyer box + `INVOICE NO` / `INVOICE DATE`,
  centred "PACKING LIST", then a **13-column** grid — `PRODUCT CODE ·
  DESCRIPTION · HS CODES · BRAND · ORIGIN · QTY` then **unit + total** for
  volume, net weight and gross weight — with an inline totals row, followed by
  the `TOTAL GROSS / NET WEIGHT / QUANTITY / VOLUME` box. Weights use Turkish
  comma decimals (`DocFormat.Weight` = `771,000`, `.Volume` = `0,0473`).
  The `.xlsx` (`OrderWorkbooks`) uses the same columns.
