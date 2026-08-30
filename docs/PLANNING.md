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

- [ ] **M1 — Setup.** Solution scaffolded, builds and runs. EF Core + SQLite
      wired up, initial migration, DbContext registered. Seed a few sample
      customers and products.
- [ ] **M2 — Customer & product CRUD.** MudBlazor list + add/edit dialog screens
      for both entities. Data persists across restarts.
- [ ] **M3 — Order builder.** Create an order, add/remove line items from the
      catalog, edit quantity and price. Order persists.
- [ ] **M4 — Calculations.** Calculation service produces line totals, order
      total, total net/gross weight, carton count, CBM. Displayed live on the
      order screen. Unit-tested.
- [ ] **M5 — Proforma invoice PDF.** QuestPDF document class renders a proper
      proforma invoice from an order. Company header from config.
- [ ] **M6 — Packing list PDF + order list.** Packing list PDF (weights, cartons,
      dimensions, volume). Order list/search screen. Regenerate PDFs from a
      saved order.

After M6: use it for a real order, then pick up stretch goals or start the
cross-reference project.

## Risks / unknowns

- **Document layout requirements** — the exact fields and layout the company /
  customs / banks expect on a proforma invoice. Mitigation: get a real example
  document from Kazim before M5.
- **Carton / packing math** — whether orders ship in whole cartons only, or
  mixed. Assumption for MVP: quantity is rounded up to whole cartons per line;
  revisit if wrong.
- **Company header assets** — logo, address, bank details. Placeholder config
  until Kazim provides them.
