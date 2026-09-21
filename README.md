# Green City Reporter

Green City Reporter is an ASP.NET Core MVC web application designed to help citizens report civic and environmental issues directly to local authorities. The system allows citizens to submit reports, track progress, receive notifications, communicate with administrators, and use AI-assisted features such as automatic report categorization, priority detection, report summarization, and an AI chatbot powered through a configurable provider.

## Overview

The application provides a moderated civic reporting workflow for Dhaka. Citizens submit an issue with a map-selected location and optional evidence, review AI-assisted classification, and track updates. Administrators validate reports, assign departments, update statuses, and communicate with citizens.

## Problem Statement

Civic issues are often reported through disconnected channels, making it difficult for residents to track progress and for authorities to prioritize urgent work. Green City Reporter provides one searchable workflow for collecting, classifying, assigning, and resolving those reports.

## Objectives

* Make civic issue reporting simple and location-aware.
* Reduce manual triage through local AI assistance.
* Give administrators clear ownership, status, priority, and history controls.
* Keep citizens informed through in-app status updates and notifications.

## Features

### Citizen Features

* User registration and login
* Submit civic issue reports
* Upload report images
* Select/report location
* Track reports using tracking numbers
* View submitted reports
* View report status history
* Add comments
* Receive notifications
* View report priority and status
* Review AI-generated report information before final submission
* Use an AI chatbot for Green City Reporter-related questions

### AI Features

Green City Reporter supports both local and hosted AI providers through configuration. Local development defaults to Ollama, while production deployments can use Groq through the `AI__Provider` and `AI__Groq__ApiKey` environment variables.

AI is used for:

* Automatic report categorization
* Initial priority detection
* Report summary generation
* Green City AI chatbot

The application remains functional even when the selected AI provider is unavailable. If AI cannot determine a category, the citizen can manually select one during the report review step.

### Admin Features

Administrators can:

* View all submitted reports
* View citizen information
* Filter reports by category
* Filter reports by priority
* Filter reports by status
* Sort reports by priority and age
* View AI-generated report summaries
* Update report status
* Change report priority
* Add administrative comments
* Notify citizens about status updates
* Receive overdue report notifications
* Receive automatic priority escalation alerts

## Smart Report Submission Flow

The report submission process uses a two-step review workflow.

```text
Citizen fills report form
        ↓
Clicks Review Report
        ↓
AI analyzes the report
        ↓
Category Detection
Priority Detection
Summary Generation
        ↓
Review Screen
        ↓
Citizen confirms report
        ↓
Report saved to database
```

The initial report form does not require the citizen to manually select a category.

If AI successfully detects a valid category:

```text
CategorySource = AI
```

If AI cannot determine the category:

```text
CategorySource = Manual
```

A category dropdown is then shown on the review page so the citizen can manually select one.

## Automatic Priority Escalation

Green City Reporter includes a background monitoring service for unattended reports.

        The system monitors active reports with statuses:

```text
Pending
Assigned
```

Completed reports such as:

```text
Resolved
Rejected
```

are excluded.

Priority escalation is based on report age:

```text
Less than 24 hours  → Keep current priority
24–48 hours         → At least Medium
48–72 hours         → At least High
More than 72 hours  → Critical
```

Priority is never automatically decreased.

When a report becomes overdue or its priority is escalated, administrators receive a notification. Duplicate overdue notifications are prevented.

## AI Chatbot

Green City Reporter includes a floating AI chatbot for authenticated users.

The chatbot can answer questions such as:

```text
How do I submit a report?
How can I track my report?
What does Pending mean?
What is the status of my latest report?
What priority does my latest report have?
```

For user-specific report questions, the backend only retrieves reports belonging to the currently authenticated citizen. The chatbot never receives unrestricted database access.

## Technology Stack

### Backend

* ASP.NET Core MVC
* .NET 10
* Entity Framework Core
* ASP.NET Core Identity
* SQL Server / LocalDB

### Frontend

* Razor Views
* HTML
* CSS
* Bootstrap
* JavaScript
* Fetch API
* Leaflet with OpenStreetMap tiles; Google Maps is optional when an API key is configured

### AI

* Ollama for local development
* Groq Cloud for production
* Configurable `AI:Provider` selection

## Project Structure

```text
GreenCityReporter/
│
├── Controllers/
│   ├── AccountController.cs
│   ├── AdminController.cs
│   ├── ChatController.cs
│   ├── HomeController.cs
│   ├── NotificationController.cs
│   └── ReportController.cs
│
├── Data/
│   ├── ApplicationDbContext.cs
│   └── DatabaseSeeder.cs
│
├── Migrations/
│
├── Models/
│   ├── ApplicationUser.cs
│   ├── Category.cs
│   ├── Comment.cs
│   ├── Notification.cs
│   ├── Report.cs
│   ├── StatusHistory.cs
│   └── Enums/
│
├── Services/
│   ├── AI/
│   │   ├── AIOptions.cs
│   │   ├── IAIService.cs
│   │   └── OllamaAIService.cs
│   │
│   ├── Background/
│   │   ├── ReportMonitoringOptions.cs
│   │   └── ReportMonitoringService.cs
│   │
        │   ├── Assignment/
        │   ├── Payments/
        │   └── Chat/
│       ├── GreenCityChatService.cs
│       └── IChatService.cs
│
├── ViewModels/
│   └── ReportSubmissionViewModels.cs
│
├── Views/
│   ├── Admin/
│   ├── Report/
│   │   ├── Create.cshtml
│   │   ├── Details.cshtml
│   │   └── Review.cshtml
│   │
│   └── Shared/
│       ├── _Chatbot.cshtml
│       └── _Layout.cshtml
│
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── uploads/
│
├── Program.cs
├── appsettings.json
└── GreenCityReporter.csproj
```

## Database Tables

The application uses tables such as:

```text
AspNetUsers
AspNetRoles
AspNetUserRoles
Categories
Reports
Comments
Notifications
StatusHistories
```

The `Reports` table also contains AI-related fields such as:

```text
AISummary
CategorySource
Priority
```

## User Roles

* **Citizen**: submits reports, selects a Dhaka location, follows status history, comments, receives notifications, and can use the donation checkout.
* **Admin**: reviews all reports, overrides categories, assigns departments, changes status and priority, comments, and manages donation reviews.

## Report Workflow

1. A citizen submits a title, description, evidence, and location from the interactive map.
2. Ollama attempts category, priority, criticality, and summary analysis. AI category selection remains editable.
3. The report is stored as `Pending`, or assigned immediately when a critical report has a configured department.
4. An administrator reviews the report and moves it through `Pending`, `Assigned`, `Resolved`, or `Rejected`.
5. The citizen follows the report and its notifications from the dashboard.

## Requirements

Before running the project, install:

* .NET 10 SDK
* SQL Server LocalDB or SQL Server
* Ollama

## Ollama Setup

The project currently uses:

```text
llama3.2
```

Install Ollama on Windows using:

```powershell
winget install Ollama.Ollama
```

If the `ollama` command is not available in the terminal after installation, use:

```powershell
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe"
```

Pull the model:

```powershell
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" pull llama3.2
```

Verify the model:

```powershell
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" list
```

Ollama normally runs on:

```text
http://localhost:11434
```

If needed, start it manually:

```powershell
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" serve
```

If you receive an error saying the socket address is already in use, Ollama is probably already running.

Test Ollama directly:

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:11434/api/generate" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"model":"llama3.2","prompt":"Reply only with AI_WORKING","stream":false}'
```

Expected result:

```text
response : AI_WORKING
done     : True
```

## Configuration

AI configuration is stored in `appsettings.json`.

Example:

```json
"AI": {
  "Provider": "Ollama",
  "TimeoutSeconds": 30,
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2"
  }
}
```

Sensitive values should be supplied with .NET User Secrets or environment variables. Optional seed accounts use `SeedUsers:Admin:*` and `SeedUsers:Citizen:*`; no user is created unless both an email and password are configured. For example:

```powershell
dotnet user-secrets set "SeedUsers:Admin:Email" "admin@example.test" --project GreenCityReporter/GreenCityReporter.csproj
dotnet user-secrets set "SeedUsers:Admin:Password" "Use-a-development-only-password" --project GreenCityReporter/GreenCityReporter.csproj
```

The development configuration enables the local simulated payment flow. It does not collect real money or require payment credentials.

Background monitoring configuration:

```json
"ReportMonitoring": {
  "Enabled": true,
  "CheckIntervalMinutes": 60,
  "OverdueHours": 24
}
```

## Database Setup

From the repository root:

```powershell
dotnet ef database update --project GreenCityReporter/GreenCityReporter.csproj
```

This applies all existing Entity Framework Core migrations.

## Run the Application

From the repository root:

```powershell
dotnet run --project GreenCityReporter/GreenCityReporter.csproj
```

Or from inside the project folder:

```powershell
dotnet run
```

The terminal will show the local application URL, for example:

```text
http://localhost:5024
```

Open that URL in your browser.

## Build the Project

```powershell
dotnet build GreenCityReporter/GreenCityReporter.csproj
```

A successful build should end with:

```text
Build succeeded.
```

## AI Failure Handling

AI is treated as an enhancement rather than a hard dependency.

If Ollama is unavailable:

* users can still log in
* reports can still be created
* citizens can manually select a category during review
* priority falls back to `Low`
* AI summary may remain unavailable
* the rest of the system continues to work

## Security

The project uses ASP.NET Core Identity and role-based authorization.

Security protections include:

* authenticated report submission
* admin-only administrative routes
* citizen ownership checks
* chatbot only accesses the authenticated user's own reports
* anti-forgery validation
* server-side category validation
* AI output validation
* Razor HTML encoding
* JavaScript `textContent` for chatbot messages
* no unrestricted AI database access

## Git Ignore

Recommended ignored files:

```gitignore
/.vs/
[Bb]in/
[Oo]bj/
wwwroot/uploads/reports/
```

Generated build files, Visual Studio workspace data, and uploaded test files should not be committed.

## Future Improvements

Possible future improvements include:

* email or SMS notifications
* map-based issue heatmaps
* duplicate report detection
* image-based issue recognition
* multilingual chatbot support
* AI confidence scoring
* analytics dashboard
* production AI deployment
* cloud storage for report images
* persistent chatbot history
* production payment gateway onboarding, if real donations become a requirement

## Project Objective

The goal of Green City Reporter is to improve communication between citizens and local authorities by providing a simple digital reporting platform enhanced with automation and AI.

The system reduces manual categorization work, highlights urgent reports, alerts administrators about unattended issues, and helps citizens interact with the reporting system more easily.

## Known Limitations

* Report coordinates are restricted to a Dhaka bounding box.
* AI requires a locally running Ollama instance for classification, priority detection, summaries, and chat; fallback behavior keeps report submission available.
* Notifications are in-app. SMTP receipts are optional and apply to confirmed non-demo gateway payments.
* The default development donation flow is simulated and is not a financial transaction.

## License

This project was developed for academic and educational purposes.

## Team

- Noman Ahmed Tonmoy — ID: 20230104051
- Ayesha Siddique Turna — ID: 20230104054
- Md Jonayed Bagdadi — ID: 20230104061

All team members are from the Department of Computer Science and Engineering (CSE), Ahsanullah University of Science and Technology (AUST).

## Donations

`/Donation` provides a clearly labeled simulated checkout for Card, bKash, Nagad, and Rocket in Development. It supports success, failure, and cancellation outcomes without collecting real financial information. Guest and signed-in donors receive a demo receipt, while `/Donation/Manage` is protected for administrators.

The codebase also contains an isolated SSLCommerz adapter for optional future sandbox testing. It is disabled by default and is not required for evaluation. See [payment integration](docs/payment-integration.md) and [donation payment checks](docs/donation-payments.md) for the exact boundaries.
