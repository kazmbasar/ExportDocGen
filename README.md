# ExportDocGen

A small internal tool that generates export documents — **proforma invoice** and
**packing list** — for vehicle-filter export orders, replacing manual
Excel/Word copy-paste.

## Status

**M10 (login + deployment) complete.** Started 2026-08-31. Solution builds, runs,
and has 54 passing tests. SQLite database, EF Core migrations, seed data,
Customer/Product CRUD, the order builder, live calculations, Excel order import,
the real ~16,600-row stock catalogue, and three export documents per order.

The UI is themed (M9): one house theme with two faces — **Ledger** (light: warm
paper, customs-green, serif headings) and **Console** (dark: slate,
filtration-teal, Inter) — following the OS light/dark setting.

**M10:** the whole app is behind a **single shared password** (cookie auth), so
it can run on a public server without exposing the catalogue. `Dockerfile` +
`docker-compose.yml` run it as a container; on the server, **nginx + certbot**
reverse-proxy it over HTTPS — see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) and
`docs/DECISIONS.md` (2026-09-03).

The group's **two exporting companies** (Filtorq, İkiler Otomotiv) are modelled
as `SellerCompany` rows. Each **customer** is assigned an exporter company; every
order for that customer is issued by it, and each document renders with that
company's letterhead:

| Document | PDF | Excel |
|---|---|---|
| Proforma invoice | `GET /orders/{id}/proforma.pdf` (per-company template) | — |
| Commercial invoice | `…/commercial-invoice.pdf` | `…/commercial-invoice.xlsx` |
| Packing list | `…/packing-list.pdf` | `…/packing-list.xlsx` |

Each company keeps its own order-number sequence; the customer's payment type and
a free-text bank block flow onto the documents; optional `Invoice no. / date /
pallets` fields on the order feed the commercial invoice and packing list.
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

On startup the app **auto-applies EF Core migrations** and seeds the two seller
companies + 2 sample customers into a SQLite file at
`~/.local/share/ExportDocGen/exportdocgen.db`. Delete that folder to reset.

**Login:** locally the password is `dev` (from `appsettings.Development.json`).
In production set `Auth__PasswordHash` — generate it with
`dotnet run --project src/ExportDocGen -- hash-password`. Deploying to a server:
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

The product catalogue is the real stock database. Export `stocks.ods` to `.xlsx`,
then load it:

```bash
dotnet run --project src/ExportDocGen -- import-stock path/to/stocks.xlsx           # dry run
dotnet run --project src/ExportDocGen -- import-stock path/to/stocks.xlsx --replace  # wipe + import
```

Only rows whose type contains "FILTER" are imported; gross weight is set to
net × 1.05 (the file has no gross figure).

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
├── Dockerfile · docker-compose.yml · .env.example    # deployment (M10)
├── deploy/nginx/               # reverse-proxy config for the server
├── docs/                       # incl. DEPLOYMENT.md
├── tests/
│   └── ExportDocGen.Tests/     # xUnit; SQLite in-memory; service round-trip tests
└── src/
    └── ExportDocGen/           # Blazor Web App
        ├── Program.cs          # DI, DbContext factory, auth, MudBlazor, startup migrate + seed
        ├── Auth/               # single shared password: AuthOptions, PasswordHash, PasswordAuthenticator
        ├── Data/               # AppDbContext, Entities/ (incl. SellerCompany), SeedData
        ├── Services/           # Customer/Product/Order/SellerCompany/Calculation services,
        │                       #   OrderNumberGenerator, ExcelOrderImportParser, OrderDocumentService
        ├── Documents/          # DocFormat/MoneyWords, proforma + packing-list + commercial-invoice models & QuestPDF docs, OrderWorkbooks (ClosedXML)
        ├── Migrations/         # EF Core migrations
        └── Components/
            ├── Layout/
            └── Pages/
                ├── Customers/  # CustomerList (/customers) + CustomerDialog
                ├── Products/   # ProductList (/products) + ProductDialog
                └── Orders/     # OrderList (/orders), OrderEdit (/orders/new, /orders/{id}),
                                #   OrderImport (/orders/import)
```
