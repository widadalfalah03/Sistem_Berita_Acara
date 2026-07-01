# AGENTS.md

## Overview

Internal "Berita Acara" (handover document) workflow system for Pertamina Patra Niaga. Blazor Server Interactive SSR app on .NET 10, SQL Server Express, with ONLYOFFICE for in-browser DOCX editing.

## Architecture

Clean Architecture, 3 projects in `SistemBeritaAcara.slnx`:

```
Core           → Entities + service interfaces (no deps)
Infrastructure → EF Core DbContext, service impls, Hangfire jobs (refs Core)
Web            → Blazor Server app, Razor pages, endpoints (refs Core + Infrastructure)
```

- **DB**: SQL Server Express (`localhost\SQLEXPRESS`, database `SistemBeritaAcara`)
- **ORM**: EF Core 10 — schema created via `db.Database.EnsureCreated()` (no migrations)
- **Identity**: ASP.NET Core Identity with `int` key. 3 roles: `AdminIT`, `AdminGudangBarang`, `Reviewer`
- **Background jobs**: Hangfire (SQL Server storage), dashboard at `/hangfire`
- **DOCX generation**: `DocumentFormat.OpenXml` — templates live in `Web/wwwroot/files/templates/`
- **DOCX→PDF**: `FreeSpire.Doc`
- **Excel import/export**: `ClosedXML`
- **Email**: `MailKit` via SMTP
- **ONLYOFFICE**: Docker container on `localhost:8081`, callback endpoint in `Program.cs`

## Commands

```powershell
# Build
dotnet build SistemBeritaAcara.slnx

# Run (dev)
dotnet run --project SistemBeritaAcara.Web

# No CI, no test suite, no linter configured
```

## Database Reset Scripts

| Script | Use |
|---|---|
| `SistemBeritaAcara.sql` | Full schema export from SSMS |
| `cleardb.sql` | Wipe data, keep AdminIT user, reset counter |
| `reset_demo.sql` | Nuclear reset for demos (deletes ALL users) |
| `reset_files_demo.ps1` | Delete uploaded files from `wwwroot/files/` (keeps templates) |
| `fixcounter.sql` | Ensure BACounter row exists |

## Key Conventions

- **Locale**: `id-ID` set globally — dates formatted Indonesian style
- **UI language**: Formal Indonesian for labels; English for technical terms (see `PRODUCT.md`)
- **No migrations**: Schema changes go in entity classes + `EnsureCreated()` + manual `ALTER TABLE` patches in `Program.cs`
- **DI registration**: All infrastructure services wired in `Infrastructure/DependencyInjection.cs` via `AddInfrastructure()`
- **First-run**: `FirstRunMiddleware` redirects to `/setup` when DB has no users
- **Auth**: Cookie-based with POST login/logout endpoints (required for Blazor Server)
- **File uploads**: `wwwroot/files/{documents,photos,signatures,profiles}` — gitignored except `templates/`

## Gotchas

- `appsettings.json` contains hardcoded SMTP credentials and ngrok URLs — do not commit new secrets
- ONLYOFFICE callback rewrites Docker-internal URLs to host URLs in `Program.cs`
- `TestDocx/` is a standalone console app for debugging DOCX templates — not part of the solution
- `local.json` is ONLYOFFICE Docker config (disables JWT tokens for local dev)
- `temp_compare/` and `scratch_compare/` are debug artifacts for DOCX XML diffing
