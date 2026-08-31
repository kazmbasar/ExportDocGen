# ExportDocGen — Decision Log

ADR-lite. Newest first. Each entry: what was decided, why, and what would make us
revisit.

---

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
