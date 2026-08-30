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
output.

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
- [ ] **M4 — Calculations.** Calculation service produces line totals, order
      total, total net/gross weight, carton count, CBM. Displayed live on the
      order screen. Unit-tested.
- [ ] **M5 — Excel order import.** Upload a customer's order spreadsheet; the
      program reads the customer and every line row (part reference, quantity,
      unit price, …), matches them against the catalog, and creates the order in
      one step instead of typing each line. See "Excel order import" below.
- [ ] **M6 — Proforma invoice PDF.** QuestPDF document class renders a proper
      proforma invoice from an order. Company header from config.
- [ ] **M7 — Packing list PDF + order list.** Packing list PDF (weights, cartons,
      dimensions, volume). Order list/search screen. Regenerate PDFs from a
      saved order.

After M7: use it for a real order, then pick up stretch goals or start the
cross-reference project.

## Excel order import (M5)

**Problem:** customers send their purchase orders as Excel files. Re-keying
every line into the order builder is slow and error-prone.

**Flow:**
1. User clicks **Import from Excel** and uploads an `.xlsx` file.
2. Program reads the sheet and extracts:
   - the **customer** (from a labelled cell / header area, or a column value),
   - one **line per row**: part reference, quantity, unit price, and any other
     available fields (description, currency, incoterm).
3. Program **matches** each row to the catalog:
   - customer → existing `Customer` by name (fuzzy/normalised match),
   - part reference → `Product` by `PartNumber` (also check a customer/OEM
     cross-reference once that exists).
4. Show a **review screen**: matched rows pre-filled, unmatched rows flagged with
   a dropdown to pick the right product (or "create later"), editable quantities
   and prices, running totals.
5. On confirm, create the order (draft) via `OrderService` — reusing the same
   validation and order-number logic as the manual builder.

**Scope for M5:**
- Support one agreed spreadsheet layout first (get 2–3 real customer files from
  Kazim); make the column mapping configurable rather than hard-coded.
- Library: `ClosedXML` (MIT) or `EPPlus` (non-commercial licence — check) for
  reading `.xlsx`.
- Nothing is written until the user confirms on the review screen.
- Unit-test the parser against sample files (rows, totals, missing/blank cells).

**Out of scope for M5:** `.xls` (old format), multi-sheet workbooks, importing
new customers/products automatically, learning column layouts.

## Risks / unknowns

- **Document layout requirements** — the exact fields and layout the company /
  customs / banks expect on a proforma invoice. Mitigation: get a real example
  document from Kazim before M5.
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
