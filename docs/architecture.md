# Architecture

Green City Reporter is an ASP.NET Core MVC application.

```text
Razor views and browser JavaScript
              |
         MVC controllers
              |
       application services
       |       |        |
      AI   assignment  payments
              |
   Entity Framework Core DbContext
              |
        SQL Server database
```

## Application layers

- `Controllers/` handles HTTP requests, authorization, validation, view selection, and redirects.
- `ViewModels/` limits form input to the fields each workflow accepts.
- `Models/` contains Identity users, reports, categories, departments, comments, notifications, status history, and donations.
- `Services/AI/` integrates with a local Ollama HTTP endpoint and validates model output.
- `Services/Assignment/` applies the configured category-to-department mapping for critical reports.
- `Services/Background/` escalates overdue report priority and notifies administrators.
- `Services/Payments/` contains demo checkout behavior, the optional hosted-gateway adapter, reconciliation, and receipt email processing.
- `Data/` contains the EF Core context and repeatable reference-data seeding.
- `Migrations/` contains the database schema history.

Authentication uses ASP.NET Core Identity with role-based authorization. Administrative actions are protected server-side with the `Admin` role; hiding an action in a view is not the security boundary.

## Main report flow

A citizen submits a report with a Leaflet map location. The report review step calls the AI service when available. The selected category can be changed manually. Reports start as `Pending`; critical reports can become `Assigned` when their category has a configured default department. Administrators can move reports through `Pending`, `Assigned`, `Resolved`, and `Rejected`.

## External integrations

- Ollama runs locally at the configured URL and receives report title/description or chat context.
- Leaflet and OpenStreetMap provide the default map picker. Google Maps can be enabled with a key, but is not required.
- The Development payment path is local simulation only. The SSLCommerz adapter is disabled unless explicitly configured for sandbox testing.
