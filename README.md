# MediCore PMS — ASP.NET Core Backend

A full **Pharmacy Management System** backend built with **ASP.NET Core 8** and **Microsoft SQL Server**, designed to power the MediCore PMS frontend.

---

## 🏗️ Project Structure

```
MediCorePMS/
├── Controllers/
│   ├── AuthController.cs        # POST /api/auth/login
│   ├── UsersController.cs       # CRUD /api/users (Admin only)
│   ├── CategoriesController.cs  # CRUD /api/categories
│   ├── MedicinesController.cs   # CRUD /api/medicines + alerts
│   ├── SalesController.cs       # POS transactions + history
│   └── DashboardController.cs   # GET /api/dashboard (KPIs + charts)
├── Data/
│   └── AppDbContext.cs           # EF Core DbContext with seed data
├── DTOs/
│   └── DTOs.cs                   # All request/response types
├── Migrations/                   # EF Core migration files
├── Models/
│   └── Models.cs                 # Entity classes
├── Services/
│   └── TokenService.cs           # JWT generation
├── appsettings.json              # Config (connection string, JWT)
└── Program.cs                    # Startup / DI / middleware
```

---

## ✅ Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 8.0+ |
| SQL Server | 2019+ / LocalDB / Azure SQL |
| Visual Studio 2022 or VS Code + C# extension | Any |

---

## 🚀 Quick Start

### 1. Clone / extract the project
```bash
cd MediCorePMS
```

### 2. Update the connection string
Open `appsettings.json` and update the connection string to match your SQL Server:

**SQL Server Express / LocalDB:**
```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MediCorePMS;Trusted_Connection=True;"
```

**SQL Server with username/password:**
```json
"DefaultConnection": "Server=YOUR_SERVER;Database=MediCorePMS;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

**Azure SQL:**
```json
"DefaultConnection": "Server=yourserver.database.windows.net;Database=MediCorePMS;User Id=youruser;Password=yourpass;Encrypt=True;"
```

### 3. Install dependencies
```bash
dotnet restore
```

### 4. Apply migrations & seed data
```bash
dotnet ef database update
```
> This creates the database and seeds all demo data (users, categories, medicines, sales) automatically.

### 5. Run the API
```bash
dotnet run
```

The API starts at `https://localhost:5001` (or `http://localhost:5000`).  
Swagger UI is available at **`https://localhost:5001`** (root URL).

---

## Deploy to Render

This repository includes a `Dockerfile` and `render.yaml` for a Render web service.

1. Push the repository to GitHub, GitLab, or Bitbucket. Do not commit database credentials or JWT secrets.
2. In Render, select **New > Blueprint**, connect the repository, and deploy the detected `render.yaml`.
3. Before deploying, set `ConnectionStrings__DefaultConnection` to your SQL Server connection string in Render. The database must be externally reachable from Render.
4. Render generates `Jwt__Key` automatically. You may replace it with a unique secret of at least 32 characters.

For local development, set the same values with .NET user secrets or environment variables instead of adding them to `appsettings.json`.

Render checks `GET /health` to confirm the API is running. After deployment, open `https://YOUR-SERVICE.onrender.com/health` to verify it.

> Render does not provide a native SQL Server database. Keep using an external SQL Server provider, or migrate this application to a Render-managed database engine.

---

## 🔌 Connect the Frontend

Open `MediCore_PMS.html` and set the API base URL at the top of the `<script>` section:

```javascript
const API_BASE = 'https://localhost:5001/api';
```

> The frontend file provided with this backend has been updated to call the real API using `fetch()` and JWT authentication.

---

## 📡 API Reference

### Authentication
| Method | Endpoint | Body | Auth |
|--------|----------|------|------|
| POST | `/api/auth/login` | `{ email, password }` | None |

**Response:** Returns JWT token to include in subsequent requests as `Authorization: Bearer <token>`

### Users
| Method | Endpoint | Description | Roles |
|--------|----------|-------------|-------|
| GET | `/api/users` | List all users | Admin |
| GET | `/api/users/{id}` | Get user by ID | Admin |
| POST | `/api/users` | Create user | Admin |
| PATCH | `/api/users/{id}` | Update user | Admin |
| PATCH | `/api/users/{id}/toggle` | Enable/disable user | Admin |

### Categories
| Method | Endpoint | Description | Roles |
|--------|----------|-------------|-------|
| GET | `/api/categories` | List all categories | All |
| GET | `/api/categories/{id}` | Get category | All |
| POST | `/api/categories` | Create category | Admin, Pharmacist |
| PATCH | `/api/categories/{id}` | Update category | Admin, Pharmacist |
| DELETE | `/api/categories/{id}` | Delete (if no medicines) | Admin |

### Medicines
| Method | Endpoint | Description | Roles |
|--------|----------|-------------|-------|
| GET | `/api/medicines` | List with filters | All |
| GET | `/api/medicines/{id}` | Get medicine | All |
| GET | `/api/medicines/alerts` | Low/out-of-stock list | All |
| POST | `/api/medicines` | Add medicine | Admin, Pharmacist |
| PATCH | `/api/medicines/{id}` | Update medicine | Admin, Pharmacist |
| DELETE | `/api/medicines/{id}` | Soft-delete | Admin |

**Query params for GET /api/medicines:**  
`search`, `categoryId`, `activeOnly=true`, `lowStock=true`, `page`, `pageSize`

### Sales
| Method | Endpoint | Description | Roles |
|--------|----------|-------------|-------|
| GET | `/api/sales` | Sales history | All |
| GET | `/api/sales/{id}` | Get receipt | All |
| POST | `/api/sales` | Process new sale (POS) | All |
| GET | `/api/sales/revenue/daily?days=7` | Daily revenue chart | All |
| GET | `/api/sales/top-medicines?top=5` | Top medicines | All |

### Dashboard
| Method | Endpoint | Description | Roles |
|--------|----------|-------------|-------|
| GET | `/api/dashboard` | All KPIs + chart data | All |

---

## 🔐 Demo Credentials

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@medicore.com` | `admin123` |
| Pharmacist | `pharma@medicore.com` | `pharma123` |
| Cashier | `cashier@medicore.com` | `cash123` |

---

## 🗄️ Database Schema

```
Users          Categories
  │                │
  │           Medicines ──────┐
  │                           │
  └── Sales                   │
          └── SaleItems ──────┘
```

### Tables
- **Users** — System users with roles (Admin/Pharmacist/Cashier), BCrypt-hashed passwords
- **Categories** — Medicine categories (Antibiotics, Painkillers, etc.)
- **Medicines** — Full inventory with stock levels, pricing, expiry
- **Sales** — Transaction headers with customer info, payment method, totals
- **SaleItems** — Line items linking sales to medicines with qty and price snapshot

---

## 🔒 Security Notes

1. **Change the JWT key** in `appsettings.json` before deploying to production:
   ```json
   "Jwt": { "Key": "YourVeryLongAndSecureRandomKeyHere_AtLeast32Characters" }
   ```

2. **Use HTTPS** in production — the dev cert is auto-trusted for local development.

3. **CORS** is set to allow all origins for development. Restrict it in production:
   ```csharp
   p.WithOrigins("https://yourdomain.com")
   ```

4. **Passwords** are hashed with BCrypt (work factor 11) — never stored in plain text.

---

## 🛠️ Running Migrations Manually

If you need to re-generate migrations after model changes:
```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

To reset the database:
```bash
dotnet ef database drop
dotnet ef database update
```

---

## 📦 NuGet Packages Used

| Package | Purpose |
|---------|---------|
| `Microsoft.EntityFrameworkCore.SqlServer` | SQL Server provider |
| `Microsoft.EntityFrameworkCore.Tools` | CLI migration tools |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT auth middleware |
| `BCrypt.Net-Next` | Password hashing |
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI UI |
