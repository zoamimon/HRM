# HRM.Web - Web Interface

**Blazor Server web application for Human Resource Management System**

## Overview

HRM.Web is a modern web interface built with **Blazor Server** (.NET 10) that provides a user-friendly UI for managing HRM system operations. It communicates with HRM.Api backend to perform operations like operator registration, activation, and management.

## Technology Stack

- **Framework**: Blazor Server (.NET 10)
- **UI**: Bootstrap 5.3
- **Icons**: Bootstrap Icons
- **Validation**: FluentValidation
- **HTTP Client**: System.Net.Http

## Features

### Currently Implemented

✅ **Operator Registration**
- User-friendly registration form
- Client-side validation with FluentValidation
- Real-time error feedback
- Success confirmation with created operator details
- Password strength validation

### Coming Soon

🔜 **Operators List** - View and manage all operators
🔜 **Operator Activation** - Activate pending operators
🔜 **Authentication** - Login/logout functionality
🔜 **Dashboard** - System overview and statistics

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- HRM.Api running (default: http://localhost:5000)
- Modern web browser (Chrome, Edge, Firefox, Safari)

### Configuration

Update `appsettings.json` to configure API connection:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5000",
    "Timeout": 30
  }
}
```

### Run the Application

#### Option 1: Using .NET CLI

```bash
cd src/Apps/HRM.Web
dotnet run
```

The application will start at:
- HTTP: http://localhost:5100
- HTTPS: https://localhost:5101

#### Option 2: Using Visual Studio / Rider

1. Open HRM.slnx solution
2. Set HRM.Web as startup project
3. Press F5 to run

### Access the Application

Open your browser and navigate to:
- **Development**: http://localhost:5100

## Project Structure

```
HRM.Web/
├── Components/              # Blazor components
│   ├── Layout/             # Layout components (MainLayout, NavMenu)
│   ├── Pages/              # Routable pages (Home, RegisterOperator)
│   ├── App.razor           # Root component
│   ├── Routes.razor        # Routing configuration
│   └── _Imports.razor      # Global using directives
├── Models/                 # DTOs and view models
│   ├── RegisterOperatorRequest.cs
│   ├── OperatorResponse.cs
│   └── ApiResponse.cs
├── Services/               # API client services
│   ├── IApiClient.cs
│   └── ApiClient.cs
├── Validators/             # FluentValidation validators
│   └── RegisterOperatorRequestValidator.cs
├── Properties/
│   └── launchSettings.json
├── wwwroot/               # Static files
│   └── css/
│       └── app.css
├── Program.cs             # Application entry point
├── appsettings.json
└── HRM.Web.csproj
```

## Usage

### Register a New Operator

1. Navigate to **Register Operator** from the menu
2. Fill in the form:
   - **Username**: 3-50 characters, alphanumeric with dots/underscores/hyphens
   - **Email**: Valid email address
   - **Full Name**: 2-100 characters
   - **Phone Number**: Optional, valid phone format
   - **Password**: Min 8 characters with uppercase, lowercase, number, special character
   - **Confirm Password**: Must match password
3. Click **Register Operator**
4. Upon success, view the created operator details
5. Note: Operator will be in "Pending" status and requires activation

### Validation Rules

#### Username
- Required
- 3-50 characters
- Only letters, numbers, dots, underscores, hyphens
- Examples: `john.doe`, `admin_user`, `operator-01`

#### Email
- Required
- Valid email format
- Max 100 characters

#### Password
- Required
- Minimum 8 characters
- At least one uppercase letter (A-Z)
- At least one lowercase letter (a-z)
- At least one number (0-9)
- At least one special character (!@#$%^&*)

#### Full Name
- Required
- 2-100 characters

#### Phone Number
- Optional
- Valid phone format: `+1234567890`, `(123) 456-7890`, etc.

## API Integration

HRM.Web communicates with HRM.Api using HTTP client:

### Endpoints Used

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/identity/operators/register` | POST | Register new operator |
| `/api/identity/operators/{id}/activate` | POST | Activate operator (coming soon) |
| `/api/identity/operators` | GET | List operators (coming soon) |

### Error Handling

The application handles various error scenarios:

- **Network Errors**: "Failed to connect to API"
- **Validation Errors**: Field-specific error messages
- **Business Rule Errors**: Username/email already exists, etc.
- **Server Errors**: Generic error message with details

## Development

### Run in Development Mode

```bash
dotnet watch run
```

Changes to `.razor`, `.cs`, `.css` files will auto-reload.

### Add New Pages

1. Create new `.razor` file in `Components/Pages/`
2. Add `@page "/route"` directive
3. Implement component logic
4. Add navigation link in `NavMenu.razor`

### Add New API Endpoints

1. Add method to `IApiClient.cs` interface
2. Implement in `ApiClient.cs`
3. Use in Blazor component via dependency injection

## Styling

The application uses:
- **Bootstrap 5.3** for UI components
- **Bootstrap Icons** for icons
- **Custom CSS** in `wwwroot/css/app.css`

### Customize Styling

Edit `wwwroot/css/app.css` to modify:
- Colors and themes
- Layout and spacing
- Component-specific styles

## Troubleshooting

### "Failed to connect to API"

**Problem**: Cannot reach HRM.Api

**Solutions**:
1. Ensure HRM.Api is running on http://localhost:5000
2. Check `appsettings.json` for correct `ApiSettings:BaseUrl`
3. Verify no firewall blocking connections

### Validation Errors Not Showing

**Problem**: FluentValidation not working

**Solutions**:
1. Ensure `FluentValidation` NuGet packages are installed
2. Check `RegisterOperatorRequestValidator` is registered in DI
3. Verify `@inject IValidator<RegisterOperatorRequest>` in component

### Page Not Loading

**Problem**: Blank page or routing issues

**Solutions**:
1. Check browser console for JavaScript errors
2. Verify `@page` directive in `.razor` file
3. Clear browser cache and restart application

## Production Deployment

### Build for Production

```bash
dotnet publish -c Release -o ./publish
```

### Environment Configuration

Create `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "ApiSettings": {
    "BaseUrl": "https://api.yourcompany.com"
  }
}
```

### Hosting Options

- **IIS**: Traditional Windows hosting
- **Azure App Service**: Cloud hosting
- **Docker**: Containerized deployment
- **Linux with Kestrel**: Self-hosted

## Future Enhancements

- [ ] JWT authentication and authorization
- [ ] Operator activation UI
- [ ] Operators list with search/filter
- [ ] Dashboard with statistics
- [ ] User profile management
- [ ] Role-based access control
- [ ] Real-time notifications with SignalR
- [ ] Export operators to Excel/PDF
- [ ] Audit log viewer

## Support

For issues or questions:
- Check HRM.Api documentation
- Review application logs in browser console
- Contact development team

## License

Copyright © 2024 HRM System. All rights reserved.
