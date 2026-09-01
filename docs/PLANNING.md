# ExportDocGen — Planning

_Created: 2026-08-31_

## Problem statement

Every export order at the company needs a set of documents — proforma invoice,
commercial invoice, packing list — that repeat mostly the same data:

- Buyer name / address / country
- Incoterm, currency, payment terms
- Line items: filter part number, description, HS code, quantity, unit price
- Logistics: net weight, gross weight, carton count, carton dimensions, total volume

Today this is done by copy-pasting between Excel and Word templates. It is slow
and error-prone: wrong weights, wrong incoterm, totals that don't match between
the invoice and the packing list.

## Goal

A single small app where you enter an order once and it produces correct,
consistent PDF documents — with all totals and logistics figures calculated
automatically.

**Success criterion:** Kazim produces the proforma invoice **and** packing list
for one real export order entirely from the app, with no manual editing of the
output. — **met 2026-09-02** (also produces the commercial invoice + editable
Excel).

## MVP scope

| # | Capability | Definition of done |
|---|-----------|--------------------|
| 1 | **Customer management** | Create / edit / list customers (name, address, country, default incoterm, currency). |
| 2 | **Product catalog** | Create / edit / list products (part number, description, HS code, net & gross weight, units per carton, carton dimensions). |
| 3 | **Order builder** | Create an order: pick a customer, add line items from the catalog, set quantity and unit price per line. |
| 4 | **Auto-calculations** | App computes line totals, order total, total net/gross weight, carton count, total volume (CBM) — shown live on the order screen. |
| 5 | **PDF generation** | Generate a proforma invoice PDF and a packing list PDF from an order, with company header. Totals identical across both. |
| 6 | **Order list & search** | List past orders; search by customer or order number. Re-open an order to regenerate its PDFs. |

## Out of scope for MVP (stretch goals)

- User accounts / authentication (single-user local tool for now)
- Cloud hosting (runs locally; Docker file authored but deployment later)
- Commercial invoice + certificate of origin templates
- Filter **cross-reference lookup** (OEM number → our part number) — strong
  candidate for the *next* project
- Multi-currency with live FX rates
- Emailing the PDF bundle to the customer
- Excel export of order data
- Editing/versioning issued documents

## Milestone plan (~6 weeks, part-time)

- [x] **M1 — Setup.** _(2026-08-31)_ Solution scaffolded, builds and runs. EF
      Core + SQLite via DbContextFactory, InitialCreate migration, seed data,
      MudBlazor shell, home dashboard.
- [x] **M2 — Customer & product CRUD.** _(2026-08-31)_ MudDataGrid list screens +
      MudDialog add/edit forms for both entities, with delete confirmation and
      referential-integrity guards. `CustomerService` / `ProductService`.
- [x] **M3 — Order builder.** _(2026-08-31)_ `/orders` list + `/orders/new` and
      `/orders/{id}` builder: pick customer (prefills incoterm/currency), add/
      remove product lines with quantity + unit price, live money subtotal, auto
      order number `EXP-{year}-{seq}`. `OrderService` + xUnit tests for the
      create/update/delete round-trip and numbering.
- [x] **M4 — Calculations.** _(2026-08-31)_ `CalculationService` (pure, no DB)
      produces per-line and order totals: money, net weight, ship gross weight
      (incl. carton tare), carton count, CBM — per the DATA-MODEL formulas.
      Shown live on the order builder (per-line cartons/net + a summary row).
      9 unit tests.
- [x] **M5 — Excel order import.** _(2026-08-31)_ Upload a customer's `.xlsx`
      order; `ExcelOrderImportParser` reads every line row (code, quantity, unit
      price), the `/orders/import` page matches each code to the catalog by
      normalized part number, and a review screen lets the user fix matches,
      quantities and prices before the order is created via `OrderService`.
      7 parser tests (against the two real FILTORQ sample files). See
      "Excel order import" below.
- [x] **M6 — Proforma invoice PDF.** _(2026-08-31)_ `ProformaInvoiceDocument`
      (QuestPDF) renders an A4 proforma from an order, laid out like the
      company's real template (`leo motors AUGUST ORDER.pdf`): full-page
      letterhead background, buyer/invoice box, delivery & payment terms,
      `Bank Detail (<CUR>)` block, page break, then a `FILTORQ CODE | QUANTITY |
      PRICE | TOTAL` table with a gold grand-total box. Money `$3 624,88`, dates
      `dd.MM.yyyy`. `OrderDocumentService` + `GET /orders/{id}/proforma.pdf`;
      "Proforma PDF" buttons on the order list and editor. Added `Customer.TaxId`
      / `ContactPhone`. 3 tests.
- [x] **M6.5 — Multi-seller rebuild.** _(2026-09-01)_ The group exports through
      **two companies** — Filtorq and İkiler Otomotiv — each with its own
      proforma template.
      - **`SellerCompany` entity** (name, short name, `ProformaTemplate`,
        `NumberFormat`, letterhead, default bank / delivery / validity text)
        replaces the `CompanyProfile` config. Two seeded rows; the
        `AddMultiSeller` migration inserts them and back-fills existing orders to
        Filtorq. No CRUD UI.
      - **The exporter company belongs to the customer**
        (`Customer.SellerCompanyId`, required "Exporter company" field on the
        customer form). `Order.SellerCompanyId` is copied from the customer at
        create time; the order / import screens show it read-only. Each company
        keeps its own customer book.
      - **Independent order-number sequence per company** (`OrderNumberGenerator`
        takes the seller): `EXP-{year}-{seq}` for Filtorq, `{yyMMdd}/{seq}` for
        İkiler.
      - **Two proforma document classes** selected by `ProformaTemplate` in
        `OrderDocumentService`: `ProformaInvoiceDocument` (Filtorq, the M6b
        layout) and `IkilerProformaDocument` (new — 5-column bordered grid,
        inline totals row, amount-in-words via `Humanizer.Core`,
        INCOTERMS/DELIVERY TIME/VALIDITY/PAYMENT TERM, drawn header + text
        footer).
      - **`Customer.PaymentType`** — managed `PaymentTerm` choice, pre-fills
        `Order.PaymentTerms`.
      - **`Order.BankDetails`** — free-text multi-line block, pre-filled from the
        seller default, printed verbatim. Plus optional `Order.DeliveryTime` /
        `Order.Validity`. Replaces the structured `CompanyProfile.Bank`.
      - 33 tests. See `docs/DECISIONS.md` (2026-09-01).
- [x] **M7 — Packing list PDF + order list polish.** _(2026-09-01)_
      - **Packing list PDF** — `GET /orders/{id}/packing-list.pdf`.
        `PackingListModel` + `PackingListDocument` (one shared A4-portrait layout;
        each company's letterhead swapped in) + `OrderDocumentService`
        `.BuildPackingListAsync`. Columns: `# · code · description · HS code ·
        origin · qty · net kg · gross kg · CBM`, then the shipment totals
        (quantity, net & gross weight, CBM). All figures from
        `CalculationService`. Formatting shared via a new `DocFormat`
        helper (also now used by the two proforma documents).
      - **Order list** (`/orders`) — Exporter column + company filter; quick
        filter also matches the exporter; per-row "Packing list PDF" button
        (also on the order editor). Newest-first ordering (`CreatedUtc`).
      - Reworked to the real issued layout in M7b (2026-09-02) — see below.
- [x] **Real stock catalogue.** _(2026-09-01)_ Kazim's stock database
      (`stocks.ods`, ~19,400 rows) replaces the sample products. `Product` gains
      Origin / Brand / per-unit CBM and **loses the carton model**;
      `CalculationService` is now pure per-unit arithmetic (gross = net × 1.05).
      `StockCatalogImportService` + a `dotnet run -- import-stock` command load
      ~16,600 filter rows. See `docs/DECISIONS.md`.
- [x] **M8 — Commercial invoice + Excel downloads.** _(2026-09-02)_ New
      commercial-invoice PDF (`CommercialInvoiceModel` / `Document`, one shared
      layout, per-company letterhead) at
      `GET /orders/{id}/commercial-invoice.pdf`. Editable `.xlsx` downloads for
      the packing list and commercial invoice (`OrderWorkbooks`, ClosedXML) at
      `…/{packing-list|commercial-invoice}.xlsx`. New optional `Order` fields
      `InvoiceNumber` / `InvoiceDate` / `Pallets` drive `INVOICE NO` / `DATE` and
      `TOTAL VOLUME : N PALLETS`. 42 tests. See `docs/DECISIONS.md` (2026-09-02).
- [x] **M7b — Packing list PDF matched to the real issued document.**
      _(2026-09-02)_ `PackingListDocument` reworked to the 13-column grid of
      `~/Downloads/sample PACKING LIST.pdf` — `PRODUCT CODE · DESCRIPTION ·
      HS CODES · BRAND · ORIGIN · QTY` then **unit + total** for volume, net
      weight and gross weight, an inline totals row, and the shipment totals
      box. Turkish comma decimals (`DocFormat.Weight` / `.Volume`). The
      per-company letterhead is kept (the sample has none — Kazim's choice).
- [x] **Real end-to-end run.** _(2026-09-02)_ Kazim ran a real export order
      through the app and produced its documents — "works efficiently". The MVP
      success criterion is met.
- [ ] **M9 — UI polish (planned 2026-09-02, do next).** Kazim: *"first make more
      beautiful UI for this generator, after that we will decide."* Purely a
      visual / UX pass over the existing MudBlazor screens (dashboard, customer /
      product / order lists and forms, the order builder, the document menu) —
      theme, spacing, layout, empty states, iconography, responsiveness. No new
      features; no data-model or document changes. Scope the specifics with
      Kazim before starting.
- [ ] **Stock import UX.** A browser `/products/import` page; real gross weights;
      price data.
- [ ] Filtorq-specific commercial-invoice layout (if their real one differs).

Next: **M9 UI polish**, then Kazim decides the direction (stretch goals or the
cross-reference project).

## Excel order import (M5) — as built

**Problem:** customers send their purchase orders as Excel files. Re-keying
every line into the order builder is slow and error-prone.

**Confirmed layout** (from the two real FILTORQ sample files):

- Row 1 headers: `CODE | QTY | UNIT PRICE | TOTAL` (one file: `TOTAL PRICE`).
- One product per row from row 2; the block ends at the first blank code (which
  also skips a trailing grand-total row).
- **No customer, currency, incoterm or description anywhere in the sheet** — the
  filename is the only hint ("FILTORQ …").
- Codes are the company's own scheme with inconsistent spacing/suffixes:
  `F6167G` vs `F6167 G`, `A2669 H`, `U405 KIT`, `A2576-2`.

**Flow:**
1. **Orders → Import from Excel** (`/orders/import`), upload an `.xlsx` (≤ 5 MB).
2. `ExcelOrderImportParser.Parse(Stream)` (pure, no DB): finds the header row by
   name, maps columns by header text (small synonym list), reads one
   `ImportedRow` per row until a blank code, flags rows with a bad quantity/price
   rather than failing the whole file, and collects warnings.
3. The page loads the active catalog and matches each row's code to a `Product`
   by **normalized part number** (`NormalizeCode` = trim + upper-case + strip
   whitespace). The customer is picked manually, pre-selected when a filename
   token matches a customer name.
4. **Review screen**: per row — matched product (editable `MudSelect`), quantity,
   unit price (defaulted from the sheet, rounded to 3 dp), computed line total,
   and an **Import** tick (off by default for unmatched / error rows). Live
   order totals via `CalculationService`.
5. On **Create order**, an `Order` is built from the ticked, matched rows and
   saved through the existing `OrderService.CreateAsync` (same numbering and
   validation as the manual builder); the user lands on `/orders/{id}`.

**Library:** `ClosedXML` 0.105.1 (MIT).

**Out of scope for M5:** `.xls`/`.ods`/`.csv`, multi-sheet workbooks, reading
the customer/currency from the sheet, auto-creating customers or products,
bulk catalog import, persisted per-customer column mappings, OEM
cross-reference matching. Unit prices are rounded to the schema's
`decimal(18,3)` on import (the sample sheets carry ~14 dp of costing noise);
widening the price columns is a later change if needed.

## Risks / unknowns

- **Document layout requirements** — M6 renders a standard proforma layout; the
  exact fields/wording the company's customs broker and bank expect may differ.
  Mitigation: Kazim to review against a real issued proforma and flag changes
  (all layout lives in `ProformaInvoiceDocument`).
- **Carton / packing math** — whether orders ship in whole cartons only, or
  mixed. Assumption for MVP: quantity is rounded up to whole cartons per line;
  revisit if wrong.
- **Company header assets** — logo, address, bank details. Placeholder config
  until Kazim provides them.
- **Excel import — column layout (M5).** Every customer's spreadsheet looks
  different (column order, header names, where the customer name sits, merged
  cells, totals rows). Mitigation: collect 2–3 real customer files before M5,
  support one layout first, keep the mapping configurable, and always show a
  review screen before creating the order.
- **Excel import — part matching (M5).** Customers use their own or OEM part
  numbers, not ours. Mitigation: exact `PartNumber` match first; fall back to a
  manual pick on the review screen; revisit once the cross-reference feature
  exists.
