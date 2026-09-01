# ExportDocGen — Data Model

_Created: 2026-08-31_

EF Core Code-First. SQLite. All monetary values stored as `decimal`, all weights
and dimensions as `decimal`. Times stored as UTC.

## Entities

### Customer

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK, identity |
| Name | string | required |
| SellerCompanyId | int | FK → SellerCompany (restrict) — **the group company that exports to this customer**; every order inherits it |
| TaxId | string? | buyer tax / registration no. — shown on the proforma |
| AddressLine1 | string | required |
| AddressLine2 | string? | |
| City | string? | |
| PostalCode | string? | |
| Country | string | required (ISO country name or code) |
| DefaultIncoterm | string? | e.g. "FOB Istanbul", "CIF Hamburg" |
| PaymentType | PaymentTerm? | managed choice (stored as enum name); pre-fills `Order.PaymentTerms` |
| DefaultCurrency | string | ISO 4217, e.g. "USD", "EUR" — default "USD" |
| ContactName | string? | |
| ContactEmail | string? | |
| ContactPhone | string? | shown on the proforma |

`PaymentTerm`: `Prepayment100` · `Advance40Balance60` · `Advance50Balance50` ·
`CashAgainstDocuments` · `LetterOfCreditAtSight` (friendly text via
`PaymentTermText.Of`).

### SellerCompany

The group's exporting companies (Filtorq, İkiler). Seeded, no CRUD UI yet.
Replaces the former `CompanyProfile` config.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK — 1 = Filtorq, 2 = İkiler |
| Name | string | legal name — bank block, PDF metadata |
| ShortName | string | order-form picker label |
| ProformaTemplate | enum | `FiltorqClassic` \| `IkilerGrid` (stored as name) |
| NumberFormat | enum | `ExpYearSeq` (`EXP-{year}-{seq:0000}`) \| `DateSlashSeq` (`{yyMMdd}/{seq}`) |
| LetterheadPath | string? | asset path under the content root; null → text header |
| DefaultBankDetails | string? | multi-line; pre-fills `Order.BankDetails` |
| DefaultDeliveryTime | string? | pre-fills `Order.DeliveryTime` |
| DefaultValidity | string? | pre-fills `Order.Validity` |
| CountryOfOrigin | string | default "Türkiye" |
| IsActive | bool | inactive companies hidden from the picker |

### Product

Loaded from the company stock database (`stocks.ods` → xlsx) via
`StockCatalogImportService`; the columns map to the stock file's
`Description / MENŞEİ / MARKA / CİNSİ / GTIP / Net weight / m3`.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK, identity |
| PartNumber | string | stock code ("Description"), required, unique |
| Description | string | type ("CİNSİ"), e.g. "AIR FILTER"; required |
| Origin | string? | country of manufacture ("MENŞEİ") |
| Brand | string? | "MARKA", e.g. "FLEETGUARD" |
| HsCode | string? | Turkish customs code ("GTİP"), verbatim |
| FilterType | string? | air / oil / fuel / cabin / water — derived from the description |
| NetWeightKg | decimal | per unit ("Net weight", always kg) |
| GrossWeightKg | decimal | per unit — **not in the file; set to net × 1.05 on import** |
| UnitVolumeM3 | decimal | per unit ("m3") |
| DefaultUnitPrice | decimal? | not in the file — optional, hand-entered |
| IsActive | bool | default true — hide discontinued parts |

### Order

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK, identity |
| OrderNumber | string | required, unique — format per the seller's `NumberFormat`; independent sequence per company |
| CustomerId | int | FK → Customer (restrict delete) |
| SellerCompanyId | int | FK → SellerCompany (restrict delete) — copied from `Customer.SellerCompanyId` at create time, then fixed for the order's life |
| OrderDate | DateOnly | |
| InvoiceNumber | string? | commercial-invoice ref, typed at shipment; docs fall back to `OrderNumber` |
| InvoiceDate | DateOnly? | commercial-invoice date; falls back to `OrderDate` |
| Pallets | int? | pallet count; when set the docs show "N PALLETS" instead of CBM |
| Incoterm | string | copied from customer default, editable |
| Currency | string | copied from customer default, editable |
| PaymentTerms | string? | pre-filled from `Customer.PaymentType` |
| BankDetails | string? | multi-line free text, printed verbatim on the proforma; pre-filled from the seller default |
| DeliveryTime | string? | optional, İkiler template only ("6 WEEKS") |
| Validity | string? | optional, İkiler template only |
| Notes | string? | free text shown on the proforma |
| CreatedUtc | DateTime | set on insert |

### OrderLine

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK, identity |
| OrderId | int | FK → Order (cascade delete) |
| ProductId | int | FK → Product |
| Quantity | int | units, > 0 |
| UnitPrice | decimal | in the order's currency |
| LineNumber | int | display order on documents |

At order time, product attributes (weights, volume, description, HS code) are
**read live from the Product**. If the catalog might change after an order is
issued, snapshot these onto `OrderLine` later — deferred for MVP, noted in
[DECISIONS.md](DECISIONS.md).

## Relationships

```
SellerCompany 1 ──< Customer 1 ──< Order 1 ──< OrderLine >── 1 Product
                                   Order >── 1 SellerCompany   (copied from the customer)
```

- `Order.Lines` — collection, cascade delete.
- `Customer` / `Product` / `SellerCompany` — restrict delete if referenced (or
  soft-delete via `IsActive`).

## Computed values (never stored)

Computed by a `CalculationService` and shown live on the order screen and on the
PDFs.

Every figure is a per-unit value from the stock catalogue × quantity — there is
no carton model (the stock file has no per-SKU carton data).

### Per line

```
lineNet      = quantity * product.NetWeightKg
lineGross    = quantity * product.GrossWeightKg    # gross = net × 1.05 (set on import)
lineVolumeM3 = quantity * product.UnitVolumeM3     # CBM
lineTotal    = quantity * unitPrice
```

### Order totals

```
orderTotal         = sum(lineTotal)
totalNetWeightKg   = sum(lineNet)
totalGrossWeightKg = sum(lineGross)
totalVolumeM3      = sum(lineVolumeM3)             # CBM
```

### Assumptions (MVP)

- Gross weight = net × 1.05 (the stock file has no gross figure).
- Weights / volume are per-unit × quantity — no carton or pallet modelling.
- ~215 stock rows have a 0 net weight and ~388 a 0 volume in the source file;
  imported as-is (fix in the product editor when needed).

## Seed data

- 2 sample customers (one EU, one non-EU), one per exporter company.
- **No seed products** — the catalogue is the real stock database, loaded via
  `StockCatalogImportService` (~16,600 filter rows).
