# Green City Reporter

Green City Reporter is an ASP.NET Core MVC web application designed to help citizens report civic and environmental issues directly to local authorities. The system allows citizens to submit reports, track progress, receive notifications, communicate with administrators, and use AI-assisted features such as automatic report categorization, priority detection, report summarization, and an AI chatbot powered locally using Ollama.

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

Green City Reporter uses local AI through Ollama with the `llama3.2` model.

AI is used for:

* Automatic report categorization
* Initial priority detection
* Report summary generation
* Green City AI chatbot

The application remains functional even when Ollama is unavailable. If AI cannot determine a category, the citizen can manually select one during the report review step.

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

The system monitors active reports with statuses such as:

```text
Pending
InReview
Assigned
InProgress
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
What does In Review mean?
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

### AI

* Ollama
* Llama 3.2

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
* administrator assignment system
* analytics dashboard
* production AI deployment
* cloud storage for report images
* persistent chatbot history

## Project Objective

The goal of Green City Reporter is to improve communication between citizens and local authorities by providing a simple digital reporting platform enhanced with automation and AI.

The system reduces manual categorization work, highlights urgent reports, alerts administrators about unattended issues, and helps citizens interact with the reporting system more easily.

## License

This project was developed for academic and educational purposes.

## Team

- Noman Ahmed Tonmoy — ID: 20230104051
- Ayesha Siddique Turna — ID: 20230104054
- Md Jonayed Bagdadi — ID: 20230104061

All team members are from the Department of Computer Science and Engineering (CSE), Ahsanullah University of Science and Technology (AUST).
