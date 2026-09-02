# ExportDocGen — Decision Log

ADR-lite. Newest first. Each entry: what was decided, why, and what would make us
revisit.

---

## 2026-09-02 — M9: house theme (Ledger light / Console dark).

Kazim picked a direction from three minimalist mock-ups: **one theme with two
faces**, keyed to the OS light/dark setting.

- **Light = "Ledger"** — warm paper `#FBFBF9`, deep customs-green `#1F5C43`,
  **Newsreader** serif headings + serif small-caps labels, ruled (not carded)
  stat tiles, striped rows. The feel of a trade document.
- **Dark = "Console"** — slate `#13161B` / `#171B21`, filtration-teal `#3FB8A8`
  reserved for actions, amber for state, **Inter**, panelled stat tiles, hover
  rows. An operations console.
- **Body text is Inter in both modes**; only the headings swap serif→sans.
  Kazim chose "serif in light, sans in dark" over one shared face — accepted the
  small extra maintenance for the distinct character of each mode.
- **Follows the OS** (`MudThemeProvider` + `ObserveSystemDarkModeChange`,
  seeded with `GetSystemDarkModeAsync`) — no in-app toggle. An inline script in
  `App.razor` sets `html.edg-boot-dark` from `matchMedia` so a dark-mode user
  doesn't see the light ground flash before the Blazor circuit connects
  (heavier pages still settle their component styling a beat late — acceptable
  for an internal tool; revisit if it annoys).

**Implementation.** `Components/AppTheme.cs` = `MudTheme` with `PaletteLight` +
`PaletteDark` + typography (all Inter). The light-only serif headings are CSS,
not theme: `app.css` overrides `--mud-typography-*-family` under
`.app-shell.t-light` (double class beats the theme's `:root` block), scoped to
the shell so portaled dialogs/menus stay sans in both modes. `MainLayout` gets
a `t-light` / `t-dark` wrapper class, a brand mark, and a sectioned nav with an
active rail. New dashboard (stat tiles + recent-orders table + empty state),
exporter chips (`.edg-chip`) on the order and customer lists, monospace
right-aligned figures (`.edg-num`), consistent `.page-head` headers.

**Fonts** load from Google Fonts (`<link>` in `App.razor`, same pattern as the
old Roboto link) — Newsreader, Inter, IBM Plex Sans, IBM Plex Mono. Self-hosting
is a backlog item if the tool ever needs to run offline.

**No behaviour, data-model, document, endpoint or service-logic change**
(one query gained `.Include(c => c.SellerCompany)` for the new customer-list
column). 43 tests unchanged and green.

**Revisit if:** Kazim wants an in-app theme toggle; the pre-hydration flash on
heavy pages is distracting; or the two-font-system upkeep proves annoying (fall
back to Inter headings everywhere).

## 2026-09-02 — M7b: packing list PDF matched to the real document.

`~/Downloads/sample PACKING LIST.pdf` (a real İkiler → LLC Global Expo issued
packing list) arrived. `PackingListDocument` was reworked from the M7 v1
(9-column, portrait) to the real **13-column** grid: `PRODUCT CODE · DESCRIPTION
· HS CODES · BRAND · ORIGIN · QTY`, then **unit and total** columns for volume,
net weight and gross weight; an inline totals row; then the `TOTAL GROSS / NET
WEIGHT / QUANTITY / VOLUME` box. Header block trimmed to the buyer box +
`INVOICE NO` / `INVOICE DATE` (the sample has no proforma-no / incoterm /
country-of-origin lines). Weights print with Turkish comma decimals and trailing
zeros (`771,000`, `0,0473`) — `DocFormat.Weight` (3 dp) and `DocFormat.Volume`
(4 dp) changed to comma-decimal, which also affects the commercial invoice's
weight block (correct — same company style). The per-company letterhead is kept
even though the sample is a plain Excel print (Kazim's earlier decision).

## 2026-09-02 — M8: commercial invoice + Excel downloads.

Kazim supplied a real commercial invoice
(`~/Downloads/sample commercial invoice.pdf`) and the real issued packing list
(`~/Downloads/sample PACKING LIST.pdf`) — both plain Excel prints of an İkiler →
LLC Global Expo shipment.

**Decided:**
- **New document: commercial invoice.** `CommercialInvoiceModel` +
  `CommercialInvoiceDocument` (one shared layout, per-company letterhead — the
  same `ProformaTemplate` branch as `PackingListDocument`). Sections: buyer box +
  `INVOICE NO` / `INVOICE DATE`; green line table `№ · CODE · DESCRIPTION ·
  HS CODES · BRAND · ORIGIN · QUANTITY · UNIT PRICE · TOTAL PRICE`;
  `TERMS OF DELIVERY (<incoterm>)` / `TERMS OF PAYMENT (<terms>)`; grand total;
  `TOTAL GROSS / NET WEIGHT / QUANTITY / VOLUME` block; verbatim bank block.
  Money `$3.555,00`. `GET /orders/{id}/commercial-invoice.pdf`.
- **Editable `.xlsx` downloads** for the packing list and the commercial invoice
  (`OrderWorkbooks`, ClosedXML — plain cell values, no formulas). Not the
  proforma. `GET /orders/{id}/{packing-list|commercial-invoice}.xlsx`
  (Content-Disposition `attachment`). The packing-list workbook uses the real
  13-column layout (unit + total per volume / net / gross weight) even though the
  packing-list **PDF** stays as the M7 v1 for now.
- **New editable `Order` fields:** `InvoiceNumber`, `InvoiceDate`, `Pallets`
  (all optional). `INVOICE NO` / `DATE` fall back to the order number / date;
  `TOTAL VOLUME` shows `{Pallets} PALLETS` when set, else `{cbm} CBM`. Migration
  `AddOrderInvoiceFields`. The packing list now shows these too.
- **Letterhead follows the customer's exporter company** (already the case) —
  Kazim's samples are plain because their Excel has no letterhead; ours carry it.
- One shared commercial-invoice layout — a Filtorq-specific one only if their
  real commercial invoice differs.

**Revisit if:** Filtorq's real commercial invoice differs; a real pallet
calculation is wanted; the proforma needs Excel too.

## 2026-09-01 — Real stock catalogue import.

Kazim's stock database (`~/Documents/stocks.ods`, ~19,400 rows) replaces the 5
sample products. Columns: `Description` (code) · `MENŞEİ` (origin) · `MARKA`
(brand) · `CİNSİ` (type) · `GTIP` (customs code) · `Net weight` · `MU` (unit,
always KG) · `m3` (per-unit volume).

**Decided:**
- **`Product` gains `Origin`, `Brand`, `UnitVolumeM3`; loses the carton model**
  (`UnitsPerCarton`, `CartonLengthCm/WidthCm/HeightCm`, `CartonTareWeightKg`).
  The stock file has no per-SKU carton data, so `CalculationService` is now pure
  per-unit arithmetic: net / gross / volume = quantity × the catalogue figure.
  Migration `AddStockCatalogFields`. The packing list drops its "Cartons" column
  and gains an "Origin" column; the order builder drops the "Cartons" tile.
- **Gross weight = net × 1.05** (not in the file). Confirmed with Kazim.
- **Import filters only** — rows whose `CİNSİ` contains "FILTER" (~16,600 of
  ~19,400). Bedding, promos, wipers, etc. are skipped.
- **`FilterType`** is derived from `CİNSİ` (air/oil/fuel/cabin/water); `HsCode`
  keeps the full Turkish GTİP verbatim.
- **`StockCatalogImportService`** (ClosedXML, mirrors `ExcelOrderImportParser`):
  `Parse(Stream)` → rows + a skip/zero summary; `ReplaceCatalogueAsync` wipes and
  batch-inserts, refusing if any product is on an order line. Run headless:
  `dotnet run --project src/ExportDocGen -- import-stock <stocks.xlsx> --replace`
  (ClosedXML needs `.xlsx`, so export the ODS first). No browser page yet.
- **Order builder / import product picker** switched from `MudSelect` to
  `MudAutocomplete` (16k options); the product list is paged.
- **Replaced the catalogue** — deleted the test order and wiped the 5 samples.

**Revisit if:** Kazim wants real gross weights, a carton/pallet model, price
data, the non-filter stock, or a self-service browser import screen.

## 2026-09-01 — M7: packing list PDF.

**Decided:**
- **One shared packing-list layout** for both companies (not two per-company
  documents like the proforma). `PackingListDocument` swaps in the seller's
  letterhead — Filtorq's full-page A4 background, İkiler's header band — but the
  buyer/reference block, the line table and the totals are identical. Split into
  per-company documents later only if a real issued packing list from each
  company shows they differ.
- **A4 portrait, trimmed columns:** `# · PRODUCT CODE · DESCRIPTION · HS CODE ·
  QTY · CTNS · NET KG · GROSS KG · CBM`. No per-product **Brand** column (not in
  the model) and no per-line **origin** (origin is `SellerCompany.CountryOfOrigin`,
  shown once). The DİİB template's commercial-invoice / pallet / GTIP machinery
  is out of scope.
- **Total volume is CBM** (m³) + carton count — not a pallet estimate. Pallet
  counts need pallet-size / stack rules we don't model.
- **`DocFormat`** static extracted (`Date`, `Weight`, `Count`, `Money`,
  `BuyerAddress`, `FilterDescription`) — the two proforma documents now call it
  too, so all three documents format money/dates the same way.
- **Order number shown as both `PROFORMA NO` and `INVOICE NO`** on the packing
  list for now — separate proforma / commercial-invoice numbering is a later
  change.
- **v1 layout**, to be tightened against a real issued packing list (M7b) — the
  same loop as M6 → M6b.

**Revisit if:** the two companies' real packing lists differ (→ per-company
documents); Kazim wants pallet counts, a Brand column, or per-line origin;
commercial invoice comes into scope.

## 2026-09-01 — M6.5: multi-seller rebuild.

The group exports through **two companies** — Filtorq and İkiler Otomotiv — each
with its own proforma template. Kazim provided İkiler's real proforma
(`PROFORMA INVOICE flowguard solution.pdf`).

**Decided:**
- **`SellerCompany` entity replaces the `CompanyProfile` config.** Two seeded
  rows (Filtorq = 1, İkiler = 2); the `AddMultiSeller` migration inserts them so
  existing orders back-fill to Filtorq behind the new required FK. No CRUD UI
  yet — they are edited in `SeedData` / by hand.
- **The exporter company belongs to the customer** (`Customer.SellerCompanyId`,
  required "Exporter company" field on the customer form). Every order for that
  customer is issued by that company — `OrderService` copies
  `Customer.SellerCompanyId` onto the order at create time (and re-syncs on
  update), so there is **no seller picker on the order** — the order and import
  screens just show it read-only. (Revised from the first cut, which had a
  per-order picker and shared customers; Kazim's two companies keep separate
  customer books.)
- **Independent order-number sequence per company.** `OrderNumberGenerator` takes
  the seller: `EXP-{year}-{seq:0000}` for Filtorq (`ExpYearSeq`),
  `{yyMMdd}/{seq}` for İkiler (`DateSlashSeq`). The `OrderNumber` is shown
  verbatim as the proforma P/I NO.
- **One document class per template**, selected in `OrderDocumentService` by
  `SellerCompany.ProformaTemplate`: `ProformaInvoiceDocument` (Filtorq, the M6b
  layout) and `IkilerProformaDocument` (new). Shared print-ready data in
  `ProformaInvoiceModel`.
- **İkiler template specifics:** drawn letterhead header (logo + brand strip
  image cropped from the sample PDF at 300 dpi → `wwwroot/ikiler-letterhead.png`)
  + text office footer; 3-row buyer box (no tax id / fax); **5-column** bordered
  grid `PRODUCT CODE | DESCRIPTION | UNIT PRICE | QUANTITY | TOTAL PRICE` with an
  inline Σqty / Σtotal row; the grand total **spelled out** ("… DOLLARS ONLY",
  via `Humanizer.Core`); `INCOTERMS / DELIVERY TIME / VALIDITY / PAYMENT TERM`;
  money `$11.904,00` (dot thousands). Line **description = filter category**
  ("AIR FILTER") from `Product.FilterType`.
- **Bank details are free text on the order** (`Order.BankDetails`, multi-line),
  pre-filled from `SellerCompany.DefaultBankDetails` and printed verbatim — the
  two companies have 10+ accounts, so this is not modelled. Replaces the
  structured `CompanyProfile.Bank`.
- **Payment type is a managed choice on the customer** (`Customer.PaymentType`,
  `PaymentTerm` enum) that pre-fills `Order.PaymentTerms`.
- **`DELIVERY TIME` / `VALIDITY`** are optional `Order` fields, pre-filled from
  per-company defaults.

**Revisit if:** a third seller appears (the two-class approach may want a shared
base or a parametrised layout); İkiler's real P/I sequence turns out not to reset
daily; Kazim wants the payment-type list to be data-driven rather than an enum;
`SellerCompany` needs an editing screen.

## 2026-08-31 — M6b: proforma matched to the company's real template.

Kazim provided a real issued proforma (`leo motors AUGUST ORDER.pdf`). The M6
layout was reworked to match it.

**Decided:**
- **The company letterhead is a full-page A4 PNG used as the QuestPDF page
  background** (`wwwroot/proforma-letterhead.png`, extracted and de-masked from
  the sample PDF). Header (logo, tagline) and footer (address, tel/fax, e-mail,
  web) are baked into that image, so the document renders only the middle
  content with large top/bottom margins. `CompanyProfile.LetterheadPath`; when
  unset the document falls back to a plain text header/footer.
- **Layout follows the template:** centered "PROFORMA INVOICE"; a bordered
  buyer/invoice box (`Name` + tax id | `Date`; `P/I NO` | `Tel`; `Email` |
  `Fax`; `Address`); `DELIVERY TERM` / `PAYMENT`; a centered
  `Bank Detail (<CUR>):` block with the labels `Company Name / Our Bank /
  Swift Code / IBAN NO`; a **page break**; then the line-items table with
  columns `FILTORQ CODE | QUANTITY | PRICE | TOTAL` and a **gold grand-total
  box** spanning the price+total columns.
- **The proforma drops** HS code, description, per-line/summary weights, carton
  and CBM figures, and country of origin — the company's template has none of
  them. `CalculationService` still produces them for the packing list (M7).
- **Money is formatted the company's way, not invariant English:** currency
  symbol prefix + comma decimals + space thousands (`$3 624,88`). Dates are
  `dd.MM.yyyy`. (Reverses the M6 "always English" choice for this document.)
- **`P/I NO` shows the order number** (`EXP-2026-nnnn`). The sample uses a
  separate manual sequence (`27326/2`); a dedicated proforma-number field is a
  later change if needed.
- **New fields:** `Customer.TaxId`, `Customer.ContactPhone` (migration
  `AddCustomerTaxIdAndPhone`, added to the customer dialog + seed);
  `CompanyProfile.Fax` / `Website` / `LetterheadPath`. `appsettings.json` now
  carries the real Filtorq company + Ziraat bank details.
- `OrderDocumentService` resolves asset paths against
  `IHostEnvironment.ContentRootPath` (so `wwwroot/...` works under `dotnet run`
  and when published) and passes bytes into the pure model.
- The PDF endpoint now wraps generation in try/catch (→ `Problem`) and sends
  `Content-Disposition: inline` so the links preview instead of downloading.
**Revisit if:** a distinct proforma-number scheme is needed; the table needs
description/HS code for customs; or the letterhead art is updated (replace the
PNG).

## 2026-08-31 — M5 Excel order import.

**Decided:**
- **ClosedXML 0.105.1** (MIT) to read `.xlsx`. Rejected EPPlus (its 5+ licence is
  non-commercial and needs a per-app licence declaration) and raw
  `DocumentFormat.OpenXml` (too low-level for a small feature).
- The parser (`ExcelOrderImportParser`) is **pure / no DB** and tuned to the
  real layout the company receives (header row with `CODE` / `QTY` /
  `UNIT PRICE`, one row per line, stop at the first blank code). It stays
  slightly flexible: header row found by name, columns mapped by header text via
  a synonym list — not hard-coded to columns A–D.
- A row with a bad quantity/price is **flagged, not fatal** — the rest of the
  file still imports and the user fixes it on the review screen.
- **Customer is chosen manually** (pre-selected from a filename token). The
  sample sheets carry no customer/currency/incoterm, so there is nothing to
  parse; currency/incoterm come from the customer defaults like the manual
  builder.
- Part matching = exact match on a **normalized code** (trim, upper-case, strip
  whitespace) so `F6167 G` == `F6167G`. Unmatched rows get a manual product
  picker and are excluded until matched. No OEM cross-reference yet.
- Imported unit prices are **rounded to the existing `decimal(18,3)`** column
  precision; the review screen shows the rounded value so nothing is hidden.
- The import **reuses `OrderService.CreateAsync`** — same numbering/validation as
  the manual builder; nothing is persisted until the user clicks *Create order*.
**Revisit if:** other customers send a materially different layout (customer in a
cell, multiple blocks, `.xls`) → add a configurable per-customer column mapping;
or invoice unit prices need more than 3 dp → widen the price columns; or code
mismatches become common → build the cross-reference lookup.

## 2026-08-31 — M4 calculation rounding.

**Decided:**
- `CalculationService` is pure (no DB) — takes quantity, unit price and a
  `Product`; returns `LineCalculation` / `OrderCalculation` records.
- Rounding: money 2 dp, weights 3 dp, volume 3 dp, all
  `MidpointRounding.AwayFromZero`. Rounded **per line**, then order totals are
  the sum of the rounded lines — so the documents' line figures always add up to
  the shown totals.
- "Gross weight" on screen and documents means **ship gross** = quantity ×
  product gross + (cartons × carton tare). Product-only gross is not surfaced.
- `UnitsPerCarton <= 0` is treated as 1 (defensive; the editor enforces min 1).
**Revisit if:** a customer/bank expects different rounding, or pallet weight
needs to be added on top of carton tare.

## 2026-08-31 — M3 order builder + tests.

**Decided:**
- New and edit orders share one page (`OrderEdit.razor`) on routes
  `/orders/new` and `/orders/{id}`; `Id is null` ⇒ new.
- Order lines are edited inline in a `MudSimpleTable` (product select, quantity,
  unit price, computed line total), not a per-line dialog.
- `OrderService.UpdateAsync` loads the tracked order with its lines and
  reconciles: remove lines absent from the incoming set, update matches by `Id`,
  insert lines with `Id == 0`. `LineNumber` is re-sequenced on every save.
- Order number generated server-side in `OrderService.CreateAsync` from
  `OrderNumberGenerator` (max existing `EXP-{year}-` sequence + 1). The user
  never edits it.
- `CreateAsync` nulls each `line.Product` navigation before insert so EF treats
  products as existing FKs, not new rows.
- Added `tests/ExportDocGen.Tests` (xUnit) with a `SqliteTestFactory`
  (in-memory SQLite, connection held open) implementing
  `IDbContextFactory<AppDbContext>`.
**Revisit if:** orders need issued-state locking, or line editing needs richer
per-line data (discounts, snapshots) → move to a dedicated line dialog.

## 2026-08-31 — Global interactive render mode.

**Decision:** `App.razor` sets `@rendermode="InteractiveServer"` on `<HeadOutlet>`
and `<Routes>`, making every page interactive (equivalent to the template's
`--all-interactive`).
**Why:** The scaffold used per-component interactivity, so pages rendered as
static HTML and **buttons/dialogs did nothing** (no circuit, no event handlers).
This is a line-of-business app where essentially every screen is interactive, so
a global mode is simpler than annotating each page.
**Revisit if:** a mostly-static public page is added where prerender-only would
be faster — annotate that page individually instead.

## 2026-08-31 — M2 CRUD pattern.

**Decided:**
- List screens use `MudDataGrid<T>` with `Items` bound to an in-memory list
  (whole tables are small); quick-filter text box in the toolbar.
- Add/Edit use a `MudDialog` component per entity (`CustomerDialog`,
  `ProductDialog`) with `MudForm` validation; the list clones the entity before
  editing so a cancelled edit does not mutate the grid row.
- Delete uses `DialogService.ShowMessageBoxAsync` to confirm; services refuse to
  delete a customer/product that is referenced by an order and the UI shows a
  snackbar telling the user to mark it inactive instead.
- Services are `Scoped`, injected into components, and each opens a short-lived
  `AppDbContext` from the factory per call (queries use `AsNoTracking`).
**Revisit if:** tables grow large enough to need server-side paging/sorting
(→ `MudDataGrid` `ServerData`).

## 2026-08-31 — M1 scaffold choices.

**Decided during scaffold:**
- Solution file is `ExportDocGen.slnx` (the .NET 10 default XML format).
- Blazor template created with `--empty` (no Counter/Weather samples).
- `AddDbContextFactory<AppDbContext>` (not scoped `AddDbContext`) — safer with
  Blazor Server's concurrent renders; components inject `IDbContextFactory` and
  create a short-lived context per operation.
- Entity configuration kept **inline** in `OnModelCreating` rather than separate
  `IEntityTypeConfiguration<T>` classes — 4 entities, not worth the ceremony yet.
- Migrations + seed run **automatically on startup** in `Program.cs`.
- `dotnet-ef` installed as a **local** tool (`dotnet-tools.json`).
- Decimal columns fixed at precision 18, scale 3.
**Revisit if:** config grows unwieldy (→ extract config classes); or auto-migrate
on startup becomes risky for real data (→ move to an explicit deploy step).

## 2026-08-31 — Snapshot product data on OrderLine? Deferred.

**Decision:** For MVP, `OrderLine` stores only `Quantity`, `UnitPrice`,
`LineNumber`. Weights, HS code, description, carton data are read live from
`Product` at document-generation time.
**Why:** Simpler; the catalog changes rarely.
**Revisit if:** an issued order's documents need to stay fixed after a catalog
edit → add snapshot columns to `OrderLine`.

## 2026-08-31 — Packing math: whole cartons, round up.

**Decision:** Each order line ships in whole cartons; partial cartons round up.
No mixed-product cartons, no pallet modelling.
**Why:** Matches the common case; keeps the calculation simple.
**Revisit if:** real shipments mix products per carton or need pallet
weights/dimensions.

## 2026-08-31 — MudBlazor for UI.

**Decision:** Use MudBlazor component library.
**Why:** Free, complete (grids, dialogs, forms, snackbars), well documented;
avoids hand-building UI primitives.
**Revisit if:** licensing changes or it blocks a needed layout.

## 2026-08-31 — QuestPDF for documents.

**Decision:** Generate PDFs with QuestPDF in-process.
**Why:** Clean C# layout API, good docs, active maintenance, suited to
structured business documents. Community license covers the company's size.
**Revisit if:** revenue crosses the QuestPDF commercial-license threshold, or
layout needs exceed what it supports.

## 2026-08-31 — SQLite for storage (not PostgreSQL).

**Decision:** SQLite single-file database for the MVP.
**Why:** Zero setup, trivial backup, single-user local tool. EF Core makes a
later provider swap cheap.
**Revisit if:** the tool becomes multi-user or is deployed to a shared server →
move to PostgreSQL.

## 2026-08-31 — Blazor Web App, Server interactivity.

**Decision:** Blazor Web App template with Server interactivity, no separate API.
**Why:** One language, no API layer to build, no WASM payload; fine for a local
single-user tool.
**Revisit if:** it needs to be a public multi-user web app or work offline in the
browser → reconsider WASM / a real API.

## 2026-08-31 — Stack: .NET fast path (not Next.js).

**Decision:** .NET 10 + Blazor over TypeScript/Next.js.
**Why:** Builds directly on Kazim's C# experience; fastest path to a working,
useful tool. Learning the mainstream JS stack is a separate, later goal.
**Revisit if:** the priority shifts to market-transferable web skills over
shipping this tool quickly.
