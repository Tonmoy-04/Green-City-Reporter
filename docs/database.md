# Database

The application selects its EF Core provider from `Database:Provider` and uses `ConnectionStrings:DefaultConnection` for either provider.

Local development remains SQL Server / LocalDB:

```powershell
$env:Database__Provider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "<existing SQL Server connection string>"
dotnet ef database update --context ApplicationDbContext --project GreenCityReporter/GreenCityReporter.csproj --startup-project GreenCityReporter/GreenCityReporter.csproj
```

`SqlServer` is the default when `Database:Provider` is omitted, and the existing SQL Server migrations remain in `GreenCityReporter/Migrations` unchanged.

Production uses PostgreSQL, including Supabase:

```powershell
$env:Database__Provider = "PostgreSQL"
$env:ConnectionStrings__DefaultConnection = "<PostgreSQL connection string from the deployment secret store>"
dotnet ef database update --context PostgreSqlApplicationDbContext --project GreenCityReporter/GreenCityReporter.csproj --startup-project GreenCityReporter/GreenCityReporter.csproj
```

The PostgreSQL migration set is kept separately in `GreenCityReporter/Migrations/PostgreSQL`. The `InitialPostgreSql` migration creates the complete current schema, including Identity, application entities, payment-related tables, relationships, indexes, and constraints. Run it explicitly before starting the production application; the application does not automatically call `Database.Migrate()` at startup.

Do not run the PostgreSQL command against LocalDB or the SQL Server migration command against Supabase. The PostgreSQL design-time context requires `ConnectionStrings__DefaultConnection` so a migration command cannot silently target the local SQL Server configuration.

## Core entities

- `ApplicationUser` extends ASP.NET Core Identity and owns reports, comments, notifications, and optional donation history.
- `Report` stores the citizen's issue, Dhaka coordinates, evidence path, AI fields, category, priority, department, current status, and tracking number.
- `Category` stores citizen-facing categories and an optional default department used for critical report assignment.
- `Department` owns the responsible municipal or emergency team and can be referenced by many reports.
- `StatusHistory` records administrator status transitions and remarks.
- `Comment` stores citizen and administrator discussion for a report.
- `Notification` stores in-app updates for citizens and administrators.
- `Donation` stores simulated donation outcomes and, when explicitly enabled, hosted-gateway reconciliation metadata.

## Important relationships

Reports belong to an Identity user and category. A report can optionally reference a department and an AI-suggested category. Comments, notifications, and status histories reference both their report and the responsible Identity user. Department and category deletion uses restrictive or nulling behavior where needed to preserve report history.

Donation indexes enforce checkout idempotency, receipt-token uniqueness, and unique provider bank transaction identifiers. Payment credentials are configuration values, not database fields.

## Seeding

Startup seeding creates the `Admin` and `Citizen` roles plus reference departments and categories. User accounts are optional and are created only when `SeedUsers:Admin` or `SeedUsers:Citizen` email and password settings are supplied through User Secrets or environment variables.

The existing domain timestamps are populated with UTC values. The PostgreSQL baseline stores these `DateTime` fields as `timestamp with time zone`; Identity lockout timestamps remain `DateTimeOffset` values.

File storage is configured independently from the database. Local development uses `Storage:Provider=Local` and `wwwroot/uploads/reports`; Render uses `Storage:Provider=Supabase` with the Supabase settings supplied through environment variables.
