# OrderHub project guidance

## Project

OrderHub is a small internal order-management training application. It uses a
single SQL Server database and intentionally favors a straightforward layered
design over distributed or microservice architecture.

## Technology

- .NET 8 / ASP.NET Core MVC with Razor Views
- Entity Framework Core 8 with SQL Server
- xUnit tests with EF Core InMemory
- Bootstrap 5 and local frontend assets

## Architecture

- `src/OrderHub.Web`: controllers, ViewModels, Razor views, and UI helpers.
- `src/OrderHub.Core`: domain models, repository contracts, services, and
  business rules.
- `src/OrderHub.Infrastructure`: EF Core context, repositories, migrations, and
  development seed data.
- `tests/OrderHub.Tests`: service and repository-focused tests.

Keep controllers thin. Put business rules in Core services and database queries
in Infrastructure repositories. Controllers and services must not access
`OrderHubDbContext` directly. Views bind ViewModels rather than domain models.

## Conventions

- Represent expected business failures with `ServiceResult<T>`.
- Validate user input with DataAnnotations, ModelState, and service-level
  business validation. Invalid input must not become a 500 response.
- Use `decimal` for money. Keep discount calculation in the order pricing
  logic and apply a discount exactly once.
- Store and compare timestamps in UTC; convert only for display.
- Use async EF Core APIs and avoid N+1 queries.
- Preserve existing Traditional Chinese UI messages.
- Make the smallest task-scoped change; do not mix unrelated refactors into a
  bug fix.

## Commands

Run commands from this `training-repo` directory:

- Restore/build: `dotnet build OrderHub.sln`
- Test: `dotnet test OrderHub.sln`
- Release build: `dotnet build OrderHub.sln --configuration Release`
- Run: `dotnet run --project src/OrderHub.Web`
- Format check: `dotnet format OrderHub.sln --verify-no-changes --no-restore`

## Verification

- Every bug fix requires a regression test that fails for the original cause.
- New business behavior requires service-level tests.
- Changes to routing, binding, validation, authentication, or rendered status
  codes should include web integration tests.
- Before handoff, run the narrowest relevant tests and then the full suite.
- Report commands run and any checks that could not be completed.

## Safety and scope

- Do not hand-edit existing files under
  `src/OrderHub.Infrastructure/Migrations/`; create a new migration for schema
  changes.
- Do not add NuGet packages without explaining why they are needed.
- Do not store credentials in tracked appsettings files. Use environment
  variables, user secrets, or deployment secret storage.
- Do not drop or reset a database, force-push, or discard working-tree changes
  without explicit user approval.
- Never push changes unless the user explicitly requests it.
