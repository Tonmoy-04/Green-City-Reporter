# Deployment

## Build and database

Install the .NET 10 SDK and restore packages:

```powershell
dotnet restore "Green City Reporter.slnx"
```

Local development keeps using SQL Server / LocalDB by default:

```powershell
$env:Database__Provider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "<existing SQL Server connection string>"
dotnet ef database update --context ApplicationDbContext --project GreenCityReporter/GreenCityReporter.csproj --startup-project GreenCityReporter/GreenCityReporter.csproj
dotnet run --project GreenCityReporter/GreenCityReporter.csproj
```

Render production uses PostgreSQL through Supabase. Apply the PostgreSQL schema explicitly before starting the application:

```powershell
$env:Database__Provider = "PostgreSQL"
$env:ConnectionStrings__DefaultConnection = "<PostgreSQL connection string from Render's secret environment>"
dotnet ef database update --context PostgreSqlApplicationDbContext --project GreenCityReporter/GreenCityReporter.csproj --startup-project GreenCityReporter/GreenCityReporter.csproj
dotnet run --project GreenCityReporter/GreenCityReporter.csproj
```

The production migration command uses the separate `GreenCityReporter/Migrations/PostgreSQL` baseline. Existing SQL Server migrations are not deleted, moved, or rewritten. Do not use LocalDB for a deployed multi-user application.

## Configuration and secrets

Set `ASPNETCORE_ENVIRONMENT=Production` and provide the connection string through a secret store or environment variable. Use double underscores for nested settings, for example `AI__Provider`, `AI__Groq__ApiKey`, and `AI__Groq__Model`. Keep payment merchant credentials, SMTP passwords, Supabase service-role keys, and optional map keys out of committed files.

For Render production, configure:

```bash
Database__Provider=PostgreSQL
ConnectionStrings__DefaultConnection=<supabase-postgresql-connection-string>
Storage__Provider=Supabase
Storage__Supabase__Url=<supabase-project-url>
Storage__Supabase__ServiceRoleKey=<supabase-service-role-secret>
Storage__Supabase__Bucket=report-images
Storage__Supabase__ProfileBucket=profile-pictures
AI__Provider=Groq
AI__Groq__ApiKey=your-render-secret
AI__Groq__Model=llama-3.1-8b-instant
Email__SmtpHost=<transactional-provider-smtp-host>
Email__SmtpPort=587
Email__SenderName=Green City Reporter
Email__SenderEmail=<verified-sender-address>
Email__Username=<smtp-username>
Email__Password=<smtp-api-key-or-password>
Email__EnableSsl=true
Email__PublicBaseUrl=https://<deployed-green-city-reporter-domain>
```

## Account verification email

Production registration requires a real transactional SMTP account. Brevo SMTP is the recommended deployment provider, while the implementation remains compatible with another authenticated SMTP provider. Copy the SMTP host, login, and SMTP key from the provider dashboard into the environment variables above; never put them in an appsettings file or source control.

The value of `Email__SenderEmail` must be a sender or domain authorized by the provider. Complete the provider's sender/domain verification and publish the SPF and DKIM records it supplies before production use. `Email__PublicBaseUrl` must be the public HTTPS origin of this application and must not be localhost. Production startup rejects missing SMTP settings, disabled TLS, or a non-HTTPS public URL.

After deploying, perform a mailbox test with a new account: verify that the provider accepts the message, it reaches the exact registered address, the link uses the deployed HTTPS origin, confirmation changes `EmailConfirmed` to true, and sign-in works only afterward. Provider acceptance alone is not proof of mailbox delivery.

For local development, configure:

```bash
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<existing-sql-server-connection-string>
Storage__Provider=Local
AI__Provider=Ollama
AI__Ollama__BaseUrl=http://localhost:11434
AI__Ollama__Model=llama3.2
```

Seed accounts are opt-in. Configure `SeedUsers__Admin__Email` and `SeedUsers__Admin__Password` only for a controlled setup, then rotate or remove them after initialization.

Create a Supabase Storage bucket named `report-images` and make it public so report evidence can be rendered directly in the civic UI. Configure the service-role key only in Render's server-side environment variables; never expose it to browser JavaScript or commit it. The application uploads objects under `reports/{yyyy}/{MM}/` and stores the resulting public HTTPS URL in the existing `Report.ImagePath` field.

Also create a public Supabase Storage bucket named `profile-pictures` and configure `Storage__Supabase__ProfileBucket=profile-pictures`. Profile pictures are stored under `users/{user-id}.{extension}` for both Admin and Citizen accounts, using the authenticated Identity user ID.

## HTTPS and hosting

Terminate traffic over HTTPS, configure a restrictive `AllowedHosts` value, and persist ASP.NET Core Data Protection keys when running multiple instances. Configure forwarded headers only for trusted proxies. Use a shared or edge rate limiter for multi-instance deployments.

## AI and payments

The application supports local Ollama and production Groq. If the configured provider is unavailable, report submission still falls back to the existing manual category review flow and normal app behavior. The default evaluation payment flow is local simulation. Treat any future hosted-gateway configuration as a separate sandbox or production rollout with HTTPS callbacks, merchant verification, monitoring, and a secrets manager.

## Operations

Keep application error logs, AI failure logs, payment verification warnings, and unresolved payment monitoring. Do not log passwords, tokens, card data, wallet credentials, callback bodies, or gateway URLs containing credentials. Back up the database and monitor uploaded evidence storage.
