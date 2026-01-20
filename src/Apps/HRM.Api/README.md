# HRM.Api - Main API Entry Point

**Main entry point for the Human Resource Management System (Modular Monolith)**

## Architecture

This is a **Modular Monolith** application that composes multiple bounded contexts (modules) into a single deployable API:

- **BuildingBlocks**: Shared infrastructure (MediatR, Authentication, EventBus, etc.)
- **Identity Module**: Authentication and authorization (Operators, Users)
- **Personnel Module**: Employee management (future)
- **Attendance Module**: Time tracking (future)

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- SQL Server (or SQL Server in Docker)
- Visual Studio 2022 / VS Code / Rider

### Configuration

Update `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "IdentityDb": "Server=localhost;Database=HRM_Identity_Dev;User Id=sa;Password=YourPassword;TrustServerCertificate=True"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-min-32-characters",
    "Issuer": "HRM.Api",
    "Audience": "HRM.Clients",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

### Database Setup

1. Run Identity module migrations:
```bash
cd src/Apps/HRM.Api
dotnet ef database update --project ../../Modules/Identity/HRM.Modules.Identity.Infrastructure
```

2. Or use SQL scripts in `/src/Database/Identity/`:
```bash
# Execute in order:
# 001_CreateOperatorsTable.sql
# 002_CreateIndexes.sql
# 003_SeedAdminOperator.sql
```

### Run the Application

```bash
cd src/Apps/HRM.Api
dotnet run
```

The API will start at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- OpenAPI JSON: http://localhost:5000/openapi/v1.json (Development only)

### OpenAPI Documentation

This API uses **.NET 10 native OpenAPI** (minimal approach):
- ✅ OpenAPI JSON contract: `/openapi/v1.json`
- ✅ Auto-detects security from `.RequireAuthorization()` on endpoints
- ❌ No built-in Swagger UI (by design)

**Why no Swagger UI?**
- .NET 10 OpenAPI is designed for **contract generation**, not UI
- Optimized for **tooling**, **SDK generation**, and **service-to-service** contracts
- Keeps API lightweight and production-ready

**Testing the API:**

**Option 1: Postman / Insomnia (Recommended for development)**
```
1. Import OpenAPI: http://localhost:5000/openapi/v1.json
2. Add Bearer token in Authorization header
3. Send requests
```

**Option 2: HTTP Files (VS Code / Rider)**
```http
### Register Operator
POST http://localhost:5000/api/identity/operators/register
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "username": "john.doe",
  "email": "john.doe@company.com",
  ...
}
```

**Option 3: Scalar UI (Modern OpenAPI viewer)**
```bash
npm install -g @scalar/cli
scalar reference http://localhost:5000/openapi/v1.json
```

**Option 4: Swagger Editor Online**
```
https://editor.swagger.io/
→ File → Import URL → http://localhost:5000/openapi/v1.json
```

## Available Endpoints

### Identity Module

#### Register Operator
```http
POST /api/identity/operators/register
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "username": "john.doe",
  "email": "john.doe@company.com",
  "password": "StrongPassword123!",
  "fullName": "John Doe",
  "phoneNumber": "+1234567890"
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "john.doe",
  "email": "john.doe@company.com",
  "fullName": "John Doe",
  "phoneNumber": "+1234567890",
  "status": "Pending",
  "isTwoFactorEnabled": false,
  "activatedAtUtc": null,
  "lastLoginAtUtc": null,
  "createdAtUtc": "2024-01-15T10:30:00Z",
  "modifiedAtUtc": null
}
```

#### Activate Operator
```http
POST /api/identity/operators/{id}/activate
Authorization: Bearer {admin_token}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "john.doe",
  "status": "Active",
  "activatedAtUtc": "2024-01-15T10:35:00Z",
  ...
}
```

### System Endpoints

#### Health Check
```http
GET /health

Response (200 OK):
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "environment": "Development"
}
```

## Authentication

This API uses **JWT Bearer authentication**:

1. Register an operator (requires admin token)
2. Login to get access token (future endpoint)
3. Include token in requests: `Authorization: Bearer {token}`

### Default Admin Credentials (Seeded)

After running database migrations:
- **Username**: `admin`
- **Email**: `admin@hrm.local`
- **Password**: `Admin@123456`
- **Status**: Active

## Authorization Policies

- **AdminOnly**: Requires `Admin` role (operator management)
- **Manager**: Requires `Admin` or `Manager` role (department/employee management)
- **User**: Any authenticated user

## CORS Configuration

Update `appsettings.json` to allow your frontend origin:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173"
    ]
  }
}
```

## Project Structure

```
HRM.Api/
├── DependencyInjection/
│   └── ModuleExtensions.cs      # Module registration
├── Properties/
│   └── launchSettings.json      # Development server config
├── Program.cs                   # Application entry point
├── appsettings.json             # Configuration
├── appsettings.Development.json # Development config
└── HRM.Api.csproj              # Project file
```

## Error Handling

All errors follow **Problem Details (RFC 7807)** format:

```json
{
  "code": "Operator.UsernameAlreadyExists",
  "message": "Username 'john.doe' is already taken. Please choose a different username.",
  "status": 409
}
```

HTTP Status Codes:
- `200 OK` - Success
- `201 Created` - Resource created
- `400 Bad Request` - Validation error
- `401 Unauthorized` - Missing/invalid token
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Business rule violation
- `500 Internal Server Error` - Unexpected error

## Development

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Watch Mode (auto-rebuild on file changes)
```bash
dotnet watch run
```

## Deployment

### Production Configuration

1. Update `appsettings.Production.json`:
   - Use secure connection strings
   - Use strong JWT secret key (64+ characters)
   - Configure production CORS origins
   - Set appropriate log levels

2. Publish:
```bash
dotnet publish -c Release -o ./publish
```

3. Deploy to your hosting environment (IIS, Azure App Service, Docker, etc.)

## Module Development

To add a new module:

1. Create module structure:
   ```
   Modules/NewModule/
   ├── HRM.Modules.NewModule.Domain/
   ├── HRM.Modules.NewModule.Application/
   ├── HRM.Modules.NewModule.Infrastructure/
   └── HRM.Modules.NewModule.Api/
   ```

2. Register in `ModuleExtensions.cs`:
   ```csharp
   // In AddModules()
   services.AddNewModuleInfrastructure(configuration);

   // In MapModuleEndpoints()
   app.MapNewModuleEndpoints();
   ```

## Support

For issues or questions, see `/docs` folder or contact the development team.
