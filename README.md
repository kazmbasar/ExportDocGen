# ExportDocGen

A small internal tool that generates export documents — **proforma invoice** and
**packing list** — for vehicle-filter export orders, replacing manual
Excel/Word copy-paste.

## Status

**M1 (Setup) complete.** Started 2026-08-31. Solution builds and runs; SQLite
database, EF Core migration, seed data and the MudBlazor UI shell are in place.
Next: M2 — Customer & Product CRUD screens. See [`docs/PLANNING.md`](docs/PLANNING.md).

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
└── src/
    └── ExportDocGen/           # Blazor Web App
        ├── Program.cs          # DI, DbContext factory, MudBlazor, startup migrate + seed
        ├── Data/               # AppDbContext, Entities/, SeedData, CompanyProfile
        ├── Migrations/         # EF Core migrations
        └── Components/         # Pages/, Layout/
```
