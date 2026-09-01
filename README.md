# ExportDocGen

A small internal tool that generates export documents — **proforma invoice** and
**packing list** — for vehicle-filter export orders, replacing manual
Excel/Word copy-paste.

## Status

**M6.5 (Multi-seller rebuild) complete.** Started 2026-08-31. Solution builds,
runs, and has 33 passing tests. SQLite database, EF Core migrations, seed data,
the MudBlazor UI shell, Customer/Product CRUD, the order builder, live
calculations, Excel order import, and a **proforma invoice PDF** per order.

The group's **two exporting companies** (Filtorq, İkiler Otomotiv) are modelled
as `SellerCompany` rows. Each **customer** is assigned an exporter company; every
order for that customer is issued by it — the proforma renders from that
company's own template (`GET /orders/{id}/proforma.pdf`), each company keeps its
own order-number sequence, and the customer's payment type + a free-text bank
block flow onto the document. Next: M7 — packing list PDF + order list/search.
See [`docs/PLANNING.md`](docs/PLANNING.md).

Seller company details (names, bank text, letterhead paths) are seeded in
`src/ExportDocGen/Data/SeedData.cs` — there is no editing screen yet. Swap
`wwwroot/proforma-letterhead.png` / `wwwroot/ikiler-letterhead.png` for
higher-resolution art when available.

## Stack

- .NET 10 + ASP.NET Core **Blazor Web App** (Server interactivity)
- **EF Core 10** + **SQLite** (single-file local database)
- **QuestPDF** for PDF generation
- **MudBlazor** for UI components

## Getting started

```bash
# from repo root
dotnet restore
dotnet tool restore          # restores the local dotnet-ef tool
dotnet run --project src/ExportDocGen
# then open http://localhost:5083
```

On startup the app **auto-applies EF Core migrations** and **seeds sample data**
(2 customers, 5 filter products) into a SQLite file at
`~/.local/share/ExportDocGen/exportdocgen.db`. Delete that folder to reset.

Add a migration after changing an entity:

```bash
dotnet dotnet-ef migrations add <Name> --project src/ExportDocGen
```

Run the tests:

```bash
dotnet test
```

## Documentation

| Doc | Purpose |
|-----|---------|
| [docs/PLANNING.md](docs/PLANNING.md) | Problem, MVP scope, milestones, success criteria |
| [docs/DATA-MODEL.md](docs/DATA-MODEL.md) | Entities, fields, computed values, formulas |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Project structure, libraries, conventions |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Running log of design decisions |

## Repository layout

```
ExportDocGen/
├── README.md
├── .gitignore
├── ExportDocGen.slnx           # solution (new XML format)
├── dotnet-tools.json           # local tools (dotnet-ef)
├── docs/
├── tests/
│   └── ExportDocGen.Tests/     # xUnit; SQLite in-memory; service round-trip tests
└── src/
    └── ExportDocGen/           # Blazor Web App
        ├── Program.cs          # DI, DbContext factory, MudBlazor, startup migrate + seed
        ├── Data/               # AppDbContext, Entities/ (incl. SellerCompany), SeedData
        ├── Services/           # Customer/Product/Order/SellerCompany/Calculation services,
        │                       #   OrderNumberGenerator, ExcelOrderImportParser, OrderDocumentService
        ├── Documents/          # QuestPDF: ProformaInvoiceModel + Filtorq/İkiler document classes + MoneyWords
        ├── Migrations/         # EF Core migrations
        └── Components/
            ├── Layout/
            └── Pages/
                ├── Customers/  # CustomerList (/customers) + CustomerDialog
                ├── Products/   # ProductList (/products) + ProductDialog
                └── Orders/     # OrderList (/orders), OrderEdit (/orders/new, /orders/{id}),
                                #   OrderImport (/orders/import)
```
