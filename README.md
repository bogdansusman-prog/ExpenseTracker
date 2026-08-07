# Expense Tracker

A full-stack personal finance management application built with ASP.NET Core, Angular and PostgreSQL.

Expense Tracker allows users to manage income and expenses, organize transactions into categories, filter financial records, visualize financial activity and compare income against expenses using interactive charts.

---

## Features

### Dashboard

The dashboard provides a quick overview of the current financial situation:

- Total income
- Total expenses
- Current balance
- Number of transactions
- Recent transactions

### Transaction Management

Users can create and manage financial transactions.

Each transaction contains:

- Category
- Amount
- Transaction type
- Date
- Optional description

Supported transaction types:

- Income
- Expense

Additional functionality includes:

- Delete individual transactions
- Select multiple transactions
- Select all visible transactions
- Bulk delete selected transactions

### Transaction Filters

Transactions can be filtered by:

- Category
- Transaction type
- Start date
- End date

The category filter is dynamic and only displays categories that currently contain transactions.

### Category Management

Users can:

- Create categories
- Rename categories
- Delete unused categories
- View how many transactions use each category

Categories that contain transactions cannot be deleted.

This restriction is enforced both in the frontend and in the backend API.

### Financial Comparator

The Comparator page provides visual financial analysis using Chart.js.

Available visualizations include:

- Monthly income vs expenses
- Expense distribution by category

It also displays:

- Total income
- Total expenses
- Difference between income and expenses

### Swagger API Documentation

The backend exposes interactive API documentation using Swagger UI.

When running in the Development environment:

```text
http://localhost:5112/swagger
```

or, when using the HTTPS Visual Studio profile:

```text
https://localhost:7007/swagger
```

Swagger can be used to inspect and test the REST API directly from the browser.

---

# Tech Stack

## Backend

- C#
- ASP.NET Core 10
- Entity Framework Core 10
- PostgreSQL
- Npgsql
- REST API
- OpenAPI
- Swagger UI

## Frontend

- Angular 22
- TypeScript
- SCSS
- Angular Reactive Forms
- Angular Signals
- Angular Router
- RxJS
- Chart.js

## Database

- PostgreSQL
- Entity Framework Core migrations

## Development Tools

- Visual Studio 2026
- Visual Studio Code
- Git
- GitHub
- npm
- Node.js

---

# Architecture

The application follows a client-server architecture.

```text
Angular Frontend
       |
       | HTTP REST requests
       v
ASP.NET Core Web API
       |
       | Entity Framework Core
       v
PostgreSQL Database
```

The Angular application communicates with the ASP.NET Core API through HTTP requests.

The API handles validation, business logic and database operations through Entity Framework Core.

---

# Project Structure

```text
ExpenseTracker.Api/
│
├── ExpenseTracker.Api/
│   ├── Controllers/
│   │   ├── CategoriesController.cs
│   │   └── TransactionsController.cs
│   │
│   ├── Data/
│   │   └── AppDbContext.cs
│   │
│   ├── Dtos/
│   │   ├── CategoryDto.cs
│   │   └── FinancialTransactionDto.cs
│   │
│   ├── Migrations/
│   │
│   ├── Models/
│   │   ├── Category.cs
│   │   ├── FinancialTransaction.cs
│   │   └── TransactionType.cs
│   │
│   ├── Properties/
│   │   └── launchSettings.json
│   │
│   ├── Program.cs
│   └── ExpenseTracker.Api.csproj
│
├── frontend/
│   ├── src/
│   │   ├── app/
│   │   │   ├── models/
│   │   │   ├── pages/
│   │   │   │   ├── dashboard/
│   │   │   │   ├── transactions/
│   │   │   │   ├── categories/
│   │   │   │   └── comparator/
│   │   │   │
│   │   │   ├── services/
│   │   │   ├── app.routes.ts
│   │   │   ├── app.ts
│   │   │   ├── app.html
│   │   │   └── app.scss
│   │   │
│   │   └── styles.scss
│   │
│   ├── package.json
│   └── angular.json
│
├── ExpenseTracker.Api.slnx
└── README.md
```

---

# API Endpoints

## Categories

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/Categories` | Get all categories |
| POST | `/api/Categories` | Create a category |
| PUT | `/api/Categories/{id}` | Update a category |
| DELETE | `/api/Categories/{id}` | Delete a category |

A category cannot be deleted while transactions are associated with it.

---

## Transactions

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/Transactions` | Get all transactions |
| POST | `/api/Transactions` | Create a transaction |
| PUT | `/api/Transactions/{id}` | Update a transaction |
| DELETE | `/api/Transactions/{id}` | Delete a transaction |

---

# Database Model

The application currently uses two main entities.

## Category

```text
Category
--------
Id
Name
```

## FinancialTransaction

```text
FinancialTransaction
--------------------
Id
Amount
Date
Type
Description
CategoryId
```

Relationship:

```text
Category 1 -------- * FinancialTransaction
```

A category can contain multiple transactions, while each transaction belongs to one category.

---

# Getting Started

## Prerequisites

Make sure the following tools are installed:

- .NET 10 SDK
- PostgreSQL
- Node.js
- npm
- Angular CLI
- Git

---

# Clone the Repository

```bash
git clone https://github.com/bogdansusman-prog/ExpenseTracker.git
cd ExpenseTracker
```

---

# Backend Setup

From the repository root:

```bash
dotnet restore
```

The backend uses PostgreSQL.

The database connection string is stored using .NET User Secrets and should not be committed to Git.

Set your local connection string:

```bash
dotnet user-secrets set \
"ConnectionStrings:DefaultConnection" \
"Host=localhost;Port=5432;Database=ExpenseTrackerDb;Username=postgres;Password=YOUR_PASSWORD" \
--project ExpenseTracker.Api/ExpenseTracker.Api.csproj
```

Replace:

```text
YOUR_PASSWORD
```

with your PostgreSQL password.

---

## Apply Database Migrations

Run:

```bash
dotnet ef database update \
--project ExpenseTracker.Api/ExpenseTracker.Api.csproj
```

This creates the required PostgreSQL database tables using the existing Entity Framework Core migrations.

---

## Run the Backend

```bash
dotnet run --project ExpenseTracker.Api/ExpenseTracker.Api.csproj
```

The default HTTP development URL is:

```text
http://localhost:5112
```

Swagger UI:

```text
http://localhost:5112/swagger
```

---

# Frontend Setup

Open another terminal.

From the repository root:

```bash
cd frontend
```

Install dependencies:

```bash
npm install
```

Start the Angular development server:

```bash
npm start
```

The frontend will be available at:

```text
http://localhost:4200
```

The backend CORS configuration allows requests from this Angular development URL.

---

# Application Routes

| Route | Page |
|---|---|
| `/dashboard` | Financial overview |
| `/transactions` | Transaction management |
| `/categories` | Category management |
| `/comparator` | Financial charts and comparison |

Navigating to the root URL automatically redirects to:

```text
/dashboard
```

---

# Example Workflow

A typical workflow is:

1. Create one or more categories.
2. Add income or expense transactions.
3. View financial totals on the Dashboard.
4. Filter transactions by category, type or date.
5. Select and remove multiple transactions when necessary.
6. Use the Comparator to analyze income and expenses.
7. Use Swagger UI to test backend endpoints directly.

---

# Security

Database credentials are not stored directly in source control.

The PostgreSQL connection string is configured using .NET User Secrets during local development.

Sensitive files and generated directories should remain excluded from Git.

Examples include:

```text
bin/
obj/
node_modules/
```

---

# Git Workflow

Development was organized using feature branches and pull requests.

Examples include:

```text
feature/category-management
feature/transaction-editing
feature/app-routing
feature/swagger-ui
```

Completed features are merged into:

```text
main
```

through GitHub pull requests.

---

# Future Improvements

Possible future improvements include:

- User authentication and authorization
- Separate financial data for each user
- JWT authentication
- Monthly budgets
- Budget alerts
- CSV or Excel export
- Advanced financial reports
- Additional charts
- Recurring transactions
- Automated tests
- Docker support
- Deployment to a cloud platform

---

# Author

Developed by **Bogdan Susman**.

GitHub:

```text
https://github.com/bogdansusman-prog
```

---

# License

This project was created for educational and portfolio purposes.


# Screenshots

## Dashboard

![Dashboard](docs/images/dashboard.png)

## Transactions

![Transactions](docs/images/transactions.png)

## Categories

![Categories](docs/images/categories.png)

## Financial Comparator

![Financial Comparator](docs/images/comparator.png)

## Swagger API

![Swagger API](docs/images/swagger.png)