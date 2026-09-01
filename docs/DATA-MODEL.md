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

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK, identity |
| PartNumber | string | required, unique |
| Description | string | required |
| HsCode | string? | Harmonized System code |
| FilterType | string? | air / oil / fuel / cabin — free text for now |
| NetWeightKg | decimal | per unit |
| GrossWeightKg | decimal | per unit (incl. individual packaging) |
| UnitsPerCarton | int | required, > 0 |
| CartonLengthCm | decimal | outer carton |
| CartonWidthCm | decimal | outer carton |
| CartonHeightCm | decimal | outer carton |
| CartonTareWeightKg | decimal | empty carton weight — default 0 |
| DefaultUnitPrice | decimal? | optional catalog price |
| IsActive | bool | default true — hide discontinued parts |

### Order

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK, identity |
| OrderNumber | string | required, unique — format per the seller's `NumberFormat`; independent sequence per company |
| CustomerId | int | FK → Customer (restrict delete) |
| SellerCompanyId | int | FK → SellerCompany (restrict delete) — the issuing company |
| OrderDate | DateOnly | |
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

At order time, product attributes (weights, carton data, description, HS code)
are **read live from the Product**. If the catalog might change after an order is
issued, snapshot these onto `OrderLine` later — deferred for MVP, noted in
[DECISIONS.md](DECISIONS.md).

## Relationships

```
SellerCompany 1 ──< Order >── 1 Customer
                     Order 1 ──< OrderLine >── 1 Product
```

- `Order.Lines` — collection, cascade delete.
- `Customer` / `Product` / `SellerCompany` — restrict delete if referenced (or
  soft-delete via `IsActive`).

## Computed values (never stored)

Computed by a `CalculationService` and shown live on the order screen and on the
PDFs.

### Per line

```
lineNet   = quantity * product.NetWeightKg
lineGross = quantity * product.GrossWeightKg
lineTotal = quantity * unitPrice

cartons(line)   = ceil(quantity / product.UnitsPerCarton)
cartonTare(line)= cartons(line) * product.CartonTareWeightKg
lineShipGross   = lineGross + cartonTare(line)

lineVolumeM3 = cartons(line)
             * (product.CartonLengthCm / 100)
             * (product.CartonWidthCm  / 100)
             * (product.CartonHeightCm / 100)
```

### Order totals

```
orderTotal        = sum(lineTotal)
totalNetWeightKg  = sum(lineNet)
totalGrossWeightKg= sum(lineShipGross)          # product gross + carton tare
totalCartons      = sum(cartons(line))
totalVolumeM3     = sum(lineVolumeM3)           # CBM
```

### Assumptions (MVP)

- Each line ships in **whole cartons**; partial cartons round **up**.
- No mixed-product cartons.
- No pallet weight/dimension modelling yet.

Revisit these against a real shipment if they turn out wrong (see PLANNING risks).

## Seed data (for M1)

- 2 customers (e.g. one EU, one non-EU).
- ~5 products across air / oil / fuel filter types with realistic weights and
  carton sizes.
