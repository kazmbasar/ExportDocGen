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
| AddressLine1 | string | required |
| AddressLine2 | string? | |
| City | string? | |
| PostalCode | string? | |
| Country | string | required (ISO country name or code) |
| DefaultIncoterm | string? | e.g. "FOB Istanbul", "CIF Hamburg" |
| DefaultCurrency | string | ISO 4217, e.g. "USD", "EUR" — default "USD" |
| ContactName | string? | |
| ContactEmail | string? | |

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
| OrderNumber | string | required, unique — e.g. "EXP-2026-0001" |
| CustomerId | int | FK → Customer |
| OrderDate | DateOnly | |
| Incoterm | string | copied from customer default, editable |
| Currency | string | copied from customer default, editable |
| PaymentTerms | string? | |
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
Customer 1 ──< Order 1 ──< OrderLine >── 1 Product
```

- `Order.Lines` — collection, cascade delete.
- `Customer` / `Product` — restrict delete if referenced (or soft-delete via
  `IsActive` on Product).

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
