# Green City Reporter

Green City Reporter is an ASP.NET Core MVC application for reporting and managing civic and environmental issues in Dhaka. Citizens can submit location-aware reports with evidence, review AI-assisted classification, follow progress, support nearby reports, receive notifications, and communicate with local authorities. Administrators can triage, assign, monitor, and resolve reports through a role-protected workflow.

## Current Project Status

The project currently includes:

- ASP.NET Core Identity with citizen and administrator roles.
- Email-confirmed registration, login, password recovery, and role-based authorization.
- Dhaka-bounded, map-based report submission with optional image evidence.
- Two-step report submission with AI-assisted review before final submission.
- AI category, priority, criticality, summary, and chatbot features.
- Configurable Ollama and Groq providers with graceful fallback when AI is unavailable.
- Nearby duplicate-report detection and one-support-per-citizen report following.
- Department assignment, administrative comments, status and priority management.
- Automatic overdue monitoring and priority escalation notifications.
- In-app notifications and protected citizen-specific chatbot access.
- Development donation simulation and SSLCommerz Hosted Checkout integration.
- Server-side payment validation, IPN processing, idempotent callbacks, risk review, protected receipts, and optional email delivery.
- Local or Supabase file storage.
- SQL Server and PostgreSQL application database support, with SQLite used by isolated checks.
- Docker support, health checks, rate limiting, automatic migrations, and deployment documentation.

## Main Features

### Citizen Features

- Register, confirm an email address, log in, and recover an account.
- Submit civic and environmental issue reports with title, description, location, and evidence.
- Track reports using tracking numbers.
- View reports, status history, comments, notifications, priority, and current status.
- Review AI-generated category, priority, criticality, and summary before submission.
- Manually choose a category when AI cannot determine one.
- Check nearby open reports before submitting a potentially duplicate issue.
- Support an existing report and follow its status from My Reports.
- Use the Green City AI chatbot for general and personal report questions.
- Make donations through the development demo or configured SSLCommerz checkout.
- View donation status and confirmed receipts.

### AI Features

AI is selected with `AI:Provider`:

- **Ollama** for local development.
- **Groq** for hosted deployments.

AI is used for report category detection, initial priority detection, criticality detection, report summaries, and chatbot responses. AI is an enhancement rather than a hard dependency: reports remain creatable when the provider is unavailable, category selection falls back to the review screen, and unavailable summaries do not block submission.

### Duplicate Report Detection

During review, the application checks for open reports in the same category near the selected location. Suggestions are shown nearest first and expose only limited public information such as title, category, approximate distance, status, submission date, and support count.

Citizens can support an existing issue or indicate that their report describes a different issue. The server validates the duplicate-review ticket again at final submission. Tickets are signed, citizen-bound, category-bound, location-bound, and expire after 30 minutes.

Default configuration in `GreenCityReporter/appsettings.json`:

```json
"DuplicateReports": {
  "RadiusMeters": 150,
  "MaxResults": 5
}
```

`RadiusMeters` accepts 25–1000 and `MaxResults` accepts 1–20.

### Admin Features

Administrators can view and search reports; filter and sort by category, priority, status, assignment, and age; view citizen information and AI summaries; assign departments; update status and priority; add comments; notify citizens; monitor overdue reports; and review supporter and donation information.

## Report Workflow

1. A citizen enters a title, description, evidence, and map-selected location.
2. The application validates the location and sends report information to the configured AI provider.
3. The citizen reviews the suggested category, priority, criticality, and summary.
4. If necessary, the citizen selects a category manually.
5. The application checks for nearby open reports in the same category.
6. The citizen supports an existing report or confirms a new report.
7. A new report is stored as `Pending`, or assigned immediately when a critical report has a configured department.
8. An administrator manages it through `Pending`, `Assigned`, `Resolved`, or `Rejected`.
9. Citizens receive notifications and can view status history and comments.

## Automatic Priority Escalation

A hosted background service monitors active `Pending` and `Assigned` reports. Completed `Resolved` and `Rejected` reports are excluded. Priority is never automatically decreased:

```text
Less than 24 hours  → Keep current priority
24–48 hours         → At least Medium
48–72 hours         → At least High
More than 72 hours  → Critical
```

Administrators receive notifications when reports become overdue or their priority is escalated. Duplicate overdue notifications are prevented.

## Donations and Payments

`/Donation` provides a clearly labeled Development simulation for Card, bKash, Nagad, and Rocket. It supports success, failure, and cancellation outcomes without collecting real money.

Deployed environments can use SSLCommerz Hosted Checkout for sandbox or live payments. The application creates pending donations, redirects users to the provider, validates transactions server-side, processes callbacks and IPN notifications idempotently, retries unresolved transactions, supports administrator risk review, and provides protected receipts with optional SMTP delivery. Browser returns alone are never treated as proof of payment.

See [`docs/payment-integration.md`](docs/payment-integration.md) and [`docs/donation-payments.md`](docs/donation-payments.md).

## Technology Stack

### Backend

- ASP.NET Core MVC
- .NET 10
- Entity Framework Core 10
- ASP.NET Core Identity
- SQL Server or PostgreSQL for deployments
- SQLite for isolated automated checks

### Frontend

- Razor Views, HTML, CSS, Bootstrap, and JavaScript
- Fetch API
- Leaflet with OpenStreetMap tiles
- Optional Google Maps integration
- Responsive donation checkout and receipt views

### Integrations and Infrastructure

- Ollama and Groq
- SSLCommerz Hosted Checkout
- Local or Supabase file storage
- SMTP email delivery
- Docker
- Health checks at `/health`
- Fixed-window rate limiting for sensitive endpoints

## Project Structure

```text
GreenCityReporter/
├── Controllers/
├── Data/
├── Migrations/
├── Models/
├── Services/
│   ├── AI/
│   ├── Assignment/
│   ├── Background/
│   ├── Chat/
│   ├── Email/
│   ├── Payments/
│   ├── Reports/
│   └── Storage/
├── ViewModels/
├── Views/
├── wwwroot/
├── Program.cs
├── appsettings.json
└── GreenCityReporter.csproj

GreenCityReporter.Checks/
└── Isolated payment and duplicate-report integration checks

docs/
├── ai-integration.md
├── architecture.md
├── database.md
├── deployment.md
├── donation-payments.md
└── payment-integration.md
```

## Database and Storage

Core data includes:

```text
AspNetUsers
AspNetRoles
AspNetUserRoles
Categories
Departments
Reports
ReportSupports
Comments
Notifications
StatusHistories
Donations
```

The `Reports` data includes location, tracking, assignment, priority, status, AI summary, and category-source information. `ReportSupports` ensures that each citizen is counted once per supported report.

Configure storage with `Storage:Provider`:

- `Local` stores files in the application storage area.
- `Supabase` stores files through the configured Supabase storage service.

## Requirements

- .NET 10 SDK
- SQL Server LocalDB or SQL Server, unless another database provider is configured
- Ollama for local AI, unless Groq is configured
- Entity Framework Core CLI for manual migration operations

## Local Setup

From the repository root:

```powershell
dotnet restore
dotnet ef database update --project GreenCityReporter/GreenCityReporter.csproj
dotnet run --project GreenCityReporter/GreenCityReporter.csproj
```

The application also applies pending migrations during startup before seeding reference data.

## Ollama Setup

The default local provider uses the `llama3.2` model:

```powershell
winget install Ollama.Ollama
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" pull llama3.2
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" list
```

Ollama normally runs at `http://localhost:11434`.

Example test:

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:11434/api/generate" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"model":"llama3.2","prompt":"Reply only with AI_WORKING","stream":false}'
```

## Configuration

Example AI configuration:

```json
"AI": {
  "Provider": "Ollama",
  "TimeoutSeconds": 30,
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2"
  },
  "Groq": {
    "BaseUrl": "https://api.groq.com/openai/v1",
    "Model": "openai/gpt-oss-20b"
  }
}
```

Use .NET User Secrets or environment variables for API keys, database passwords, SMTP credentials, SSLCommerz credentials, and Supabase service keys. Optional seed accounts use `SeedUsers:Admin:*` and `SeedUsers:Citizen:*`.

Example:

```powershell
dotnet user-secrets set "SeedUsers:Admin:Email" "admin@example.test" --project GreenCityReporter/GreenCityReporter.csproj
dotnet user-secrets set "SeedUsers:Admin:Password" "Use-a-development-only-password" --project GreenCityReporter/GreenCityReporter.csproj
```

See [`docs/deployment.md`](docs/deployment.md) for production configuration.

## Build and Checks

```powershell
dotnet build GreenCityReporter/GreenCityReporter.csproj
dotnet run --project GreenCityReporter.Checks/GreenCityReporter.Checks.csproj
```

The checks use an isolated SQLite database and fake payment responses. They do not require AI, network access, or real payment credentials.

## Docker

The repository includes a `Dockerfile` for containerized deployment. Supply database, AI, email, storage, and payment settings through environment variables or the hosting platform's secret store.

## Security and Reliability

- ASP.NET Core Identity and role-based authorization.
- Citizen ownership checks for reports and chatbot data.
- Anti-forgery validation on state-changing forms.
- Server-side category, location, payment, and AI-output validation.
- Signed and expiring duplicate-review tickets.
- Protected payment receipt tokens with no-cache behavior.
- Rate limiting for donation checkout and account-email operations.
- Razor HTML encoding and safe client-side text rendering.
- Retry-enabled SQL Server and PostgreSQL connections.
- Health monitoring through `/health`.
- Graceful AI failure handling.

## Known Limitations

- Report coordinates are restricted to a Dhaka bounding box.
- AI features require a reachable Ollama or Groq provider; fallback behavior keeps report submission available.
- Notifications are primarily in-app. SMTP delivery is optional.
- Real SSLCommerz payments require merchant credentials, a public HTTPS URL, and callback/IPN configuration.
- The Development donation flow is simulated and is not a financial transaction.
- Local file storage is not intended to replace durable production storage.

## Documentation

- [`docs/architecture.md`](docs/architecture.md)
- [`docs/database.md`](docs/database.md)
- [`docs/ai-integration.md`](docs/ai-integration.md)
- [`docs/deployment.md`](docs/deployment.md)
- [`docs/payment-integration.md`](docs/payment-integration.md)
- [`docs/donation-payments.md`](docs/donation-payments.md)

## Project Objective

The goal of Green City Reporter is to improve communication between citizens and local authorities through a simple, secure, and trackable reporting platform enhanced by automation and AI. The current implementation combines civic reporting, duplicate detection, departmental assignment, escalation monitoring, protected payments, and deployment support.

## License

This project was developed for academic and educational purposes.

## Team

- Noman Ahmed Tonmoy — ID: 20230104051
- Ayesha Siddique Turna — ID: 20230104054
- Md Jonayed Bagdadi — ID: 20230104061

All team members are from the Department of Computer Science and Engineering (CSE), Ahsanullah University of Science and Technology (AUST).
