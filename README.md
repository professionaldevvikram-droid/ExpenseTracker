# Expense Tracker

A multi-user expense tracker with budgets, charts and Excel export. Built with ASP.NET Core, SQL Server and plain JavaScript.

## Features

- Register and log in (JWT authentication). Every user only sees their own data.
- Add, edit and delete expenses
- Categories with their own colors and optional monthly budgets, with progress bars
- Search, filters (category, dates, amount) and sorting, all done in SQL
- Server-side pagination, so it stays fast with thousands of expenses
- Charts: spending by category and the last six months
- Export to Excel for any date range (today, 1 week, 1 month, 1 year or custom)
- Light and dark mode, responsive layout

## Tech stack

- **Backend:** ASP.NET Core (.NET 10), Entity Framework Core, SQL Server
- **Frontend:** HTML, CSS and vanilla JavaScript, Chart.js
- **Libraries:** ClosedXML (Excel), Serilog (logging)
- **Tests:** xUnit, with unit tests and integration tests against a real SQL Server database

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and SQL Server (LocalDB, Express or Developer edition).

```bash
git clone https://github.com/<your-username>/ExpenseTracker.git
cd ExpenseTracker/ExpenseTracker.Api

# Install the EF Core tool once
dotnet tool install --global dotnet-ef

# Store a random signing key for the login tokens (needs at least 32 characters)
dotnet user-secrets set "Jwt:Key" "<paste-a-long-random-string-here>"

# Create the database
dotnet ef database update

# Run the app
dotnet run --launch-profile http
```

Then open http://localhost:5165 and create an account.

The default connection string uses LocalDB. To use another SQL Server, change `ConnectionStrings:DefaultConnection` in `appsettings.json`.

## Running the tests

```bash
dotnet test
```

The integration tests create a temporary database on LocalDB and delete it when they finish.

## API

In Development, interactive API documentation is available at `/scalar`. A health check is at `/health`.

## Project structure

```
ExpenseTracker.Api/
├── Controllers/     thin HTTP endpoints
├── Services/        business rules and database queries
├── Models/          entities and request/response DTOs
├── Data/            DbContext and migrations
├── Middleware/      error handling and security headers
└── wwwroot/         the web pages (HTML, CSS, JS)
ExpenseTracker.Tests/
├── Unit/            fast tests of rules and services
└── Integration/     tests of the whole API over HTTP
```