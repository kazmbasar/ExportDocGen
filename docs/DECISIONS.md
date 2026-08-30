# ExportDocGen — Decision Log

ADR-lite. Newest first. Each entry: what was decided, why, and what would make us
revisit.

---

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
