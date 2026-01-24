# Permission Catalog System - Documentation

## Overview

The Permission Catalog System is a declarative, XML-based permission management system that defines all available permissions in the HRM system. Admins select permissions from the catalog via UI and assign them to roles, which are then stored in the database.

### Key Principles

1. **Single Source of Truth**: Permission Catalog defines ALL available permissions
2. **UI-Driven Selection**: Admins select from catalog via UI - no manual XML editing
3. **No Validation Needed**: Permissions are pre-validated in catalog
4. **Type Safety**: Strong domain models with compile-time validation
5. **Scope-Based Access**: Hierarchical data visibility (Company → Department → Position → Self)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Permission Catalog                      │
│              (Single XML file - Read Only)                  │
├─────────────────────────────────────────────────────────────┤
│ Permissions                                                  │
│  └─ Modules (Personnel, Attendance, Payroll, Identity)      │
│      └─ Entities (Employee, Department, Timesheet)          │
│          └─ Actions (View, Create, Update, Delete)          │
│              ├─ Scopes (Company, Department, Position, Self)│
│              └─ Constraints (ManagerOfTarget, FieldRestrict)│
└─────────────────────────────────────────────────────────────┘
                            ↓
                   Admin selects via UI
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                   Database (Role Permissions)               │
│  - Role: System Admin, HR Manager, Department Manager       │
│  - Selected Permissions from Catalog                         │
│  - User/Operator Assignments                                 │
└─────────────────────────────────────────────────────────────┘
```

## Permission Catalog Structure

### File Location

`templates/permissions/PermissionCatalog.xml`

### XML Structure

```xml
<?xml version="1.0" encoding="UTF-8"?>
<PermissionCatalog xmlns="http://hrm.system/permissions">
  <!--
    Permission Catalog - Defines all available permissions in system
    Admin selects permissions from catalog via UI and saves to DB
    No validation needed since permissions are predefined in catalog
  -->

  <Permissions>
    <Module name="ModuleName" displayName="Display Name">
      <Entity name="EntityName" displayName="Display Name">
        <Action name="ActionName" displayName="Display Name">
          <!-- Optional scopes and constraints -->
        </Action>
      </Entity>
    </Module>
  </Permissions>
</PermissionCatalog>
```

## Modules and Entities

The catalog defines permissions for all system modules:

### 1. Personnel Module
- **Employee**: View, Create, Update, Delete, Export, Import
- **Department**: View, Create, Update, Delete
- **Position**: View, Create, Update, Delete
- **Company**: View, Create, Update, Delete

### 2. Attendance Module
- **Timesheet**: View, Create, Update, Delete, Approve, Export
- **LeaveRequest**: View, Create, Update, Delete, Approve, Reject
- **AttendancePolicy**: View, Create, Update, Delete

### 3. Payroll Module
- **Payroll**: View, Create, Update, Delete, Process, Approve, Export
- **SalaryStructure**: View, Create, Update, Delete

### 4. Identity Module
- **User**: View, Create, Update, Delete, ResetPassword, AssignPermission
- **Operator**: View, Create, Update, Delete, ResetPassword, AssignPermission
- **Role**: View, Create, Update, Delete, AssignPermission

### 5. System Module
- **Configuration**: View, Update
- **AuditLog**: View, Export
- **SystemHealth**: View, Monitor

## Scopes

Scopes define **data visibility boundaries** for users. Operators don't use scopes (global access).

### Available Scopes

| Scope | Enum Value | Description | Typical Use Case |
|-------|-----------|-------------|------------------|
| `Company` | `ScopeLevel.Company` | All data in assigned companies | CEO, Company Admin |
| `Department` | `ScopeLevel.Department` | All data in assigned departments | Department Manager, HR Manager |
| `Position` | `ScopeLevel.Position` | Team members with same position | Team Lead, Senior Dev |
| `Self` | `ScopeLevel.Employee` | Only own data | Regular Employee |

### Scope Attributes

```xml
<Scope value="Department"
       displayName="Cùng phòng ban"
       readOnly="false" />
```

- `value`: Scope level (Company/Department/Position/Self)
- `displayName`: UI label
- `readOnly`: If `true`, scope only allowed for View action (default: `false`)

### Scope Behavior

**Important**: Scopes are **EXCLUSIVE**, not hierarchical:

- `Department` scope ≠ access to `Position` + `Self`
- `Company` scope ≠ access to all departments

Each scope filters data independently:

```csharp
// Company scope → filter by CompanyId
WHERE ea.CompanyId IN (user's assigned companies)

// Department scope → filter by DepartmentId
WHERE ea.DepartmentId IN (user's assigned departments)

// Position scope → filter by PositionId + DepartmentId
WHERE ea.PositionId IN (user's positions) AND ea.DepartmentId = user's department

// Self scope → filter by EmployeeId
WHERE e.Id = CurrentUserId
```

## Actions

Actions define **operations** that can be performed on entities.

### Standard Actions

| Action | Description | Typical Scopes |
|--------|-------------|----------------|
| `View` | Read data | All scopes |
| `Create` | Create new records | Company, Department (or no scope) |
| `Update` | Modify existing records | Company, Department, Self |
| `Delete` | Remove records | Company, Department |
| `Approve` | Approve workflow items | Company, Department |
| `Reject` | Reject workflow items | Company, Department |
| `Export` | Export data to file | Company, Department |

### Custom Actions

Domain-specific actions for entities:

```xml
<Action name="Process" displayName="Xử lý bảng lương">
  <!-- Payroll-specific action -->
</Action>

<Action name="AssignPermission" displayName="Gán quyền">
  <!-- Identity-specific action -->
</Action>
```

### Action Without Scopes

Actions without scopes apply globally (for operators) or in user's context (for users):

```xml
<!-- Operator: Can create anywhere -->
<!-- User: Creates in their assigned company/department -->
<Action name="Create" displayName="Tạo mới" />
```

### Default Scope

Pre-select a scope in UI:

```xml
<Action name="View" displayName="Xem" defaultScope="Department">
  <Scopes>
    <Scope value="Company" displayName="Toàn công ty" />
    <Scope value="Department" displayName="Cùng phòng ban" />
  </Scopes>
</Action>
```

## Constraints

Constraints are **additional conditions** that must be met for permission to be granted.

### Available Constraint Types

#### 1. ManagerOfTarget

Requires user to be manager of target employee.

```xml
<Constraint type="ManagerOfTarget">
  <Parameters>
    <Parameter name="AllowIndirect" value="false" />
    <Parameter name="MaxLevels" value="1" />
  </Parameters>
</Constraint>
```

**Parameters:**
- `AllowIndirect`: Allow manager of manager (default: `false`)
- `MaxLevels`: Maximum management levels to check (default: `1`)

#### 2. FieldRestriction

Restricts access to specific fields.

```xml
<Constraint type="FieldRestriction">
  <Parameters>
    <Parameter name="Fields" value="Salary,Bonus,TotalCompensation" />
    <Parameter name="ApplyTo" value="View,Update" />
  </Parameters>
</Constraint>
```

**Parameters:**
- `Fields`: Comma-separated field names
- `ApplyTo`: Which actions to restrict (View, Update, or both)

#### 3. DateRange

Restricts actions based on date range.

```xml
<Constraint type="DateRange">
  <Parameters>
    <Parameter name="MinDays" value="-30" />
    <Parameter name="MaxDays" value="0" />
  </Parameters>
</Constraint>
```

#### 4. WorkflowState

Restricts actions based on entity state.

```xml
<Constraint type="WorkflowState">
  <Parameters>
    <Parameter name="AllowedStates" value="Pending,Submitted" />
  </Parameters>
</Constraint>
```

**Use Cases:**
- Only approve pending requests
- Cannot edit terminated employees
- Only submit draft timesheets

## Usage Workflow

### 1. Load Permission Catalog

The catalog is loaded once at application startup and cached:

```csharp
// Load all available permissions from catalog
var permissions = await _catalogService.LoadCatalogAsync();

// Get specific module
var personnelModule = await _catalogService.GetModuleAsync("Personnel");

// Get specific action
var viewAction = await _catalogService.GetActionAsync("Personnel", "Employee", "View");
```

### 2. Admin Creates Role via UI

Admin creates roles (e.g., "System Admin", "HR Manager") and selects permissions from the catalog:

1. UI displays all modules from catalog
2. Admin selects which actions to grant
3. For each action with scopes, admin selects applicable scopes
4. System saves role + selected permissions to database

### 3. Assign Role to User/Operator

```csharp
// Assign role to user
var userRole = new UserRole(
    userId: userId,
    roleId: roleId
);
```

### 4. Check Permission at Runtime

```csharp
// Check if user can update employee
var canUpdate = await _permissionService.HasPermissionAsync(
    userId: currentUserId,
    module: "Personnel",
    entity: "Employee",
    action: "Update",
    targetId: employeeId
);
```

## Example Scenarios

### Scenario 1: Creating "HR Manager" Role

Admin wants to create an "HR Manager" role with these permissions:

**Personnel Module:**
- View Employee (Department scope)
- Create Employee (Department scope)
- Update Employee (Department scope)
- View Department (Company scope)

**Attendance Module:**
- View Timesheet (Department scope)
- Approve Timesheet (Department scope)

**Payroll Module:**
- View Payroll (Department scope)
- Create Payroll (Department scope)

**UI Flow:**
1. Admin navigates to "Roles" → "Create New Role"
2. Enters name: "HR Manager"
3. UI displays all modules from catalog
4. Admin checks permissions and selects scopes
5. Saves to database

### Scenario 2: Creating "Employee Self-Service" Role

**Personnel Module:**
- View Employee (Self scope only)
- Update Employee (Self scope, limited fields)

**Attendance Module:**
- View Timesheet (Self scope)
- Create LeaveRequest (Self scope)

### Scenario 3: Creating "System Administrator" Role (Operator)

Operators have global access without scopes:

**All Modules:**
- Full access to all actions
- No scope restrictions

## Best Practices

### Catalog Management

1. **Version Control**: Keep catalog in source control
2. **Review Changes**: Carefully review any catalog modifications
3. **Reload Cache**: Clear cache after catalog updates
4. **Backup**: Keep backup before editing catalog

### Role Design

1. **Start Simple**: Begin with basic roles (Admin, Manager, Employee)
2. **Principle of Least Privilege**: Grant minimum permissions needed
3. **Role Hierarchy**: Use inheritance where possible
4. **Clear Naming**: Use descriptive role names

### Scope Selection

1. **Default to Department**: Most users work within department scope
2. **Position for Read-Only**: Use Position scope with `readOnly="true"` for visibility
3. **Avoid Company for Everyone**: Company scope is for high-level roles only
4. **Self for Employees**: Regular employees should have Self scope

## Performance Considerations

1. **Catalog Caching**: Catalog is loaded once and cached in memory
2. **Cache Duration**: 1 hour (configurable)
3. **Fast Lookups**: In-memory dictionary for O(1) access
4. **Lazy Loading**: Only load when needed

## Files

- **Permission Catalog**: `templates/permissions/PermissionCatalog.xml`
- **Catalog Service Interface**: `src/Modules/Identity/HRM.Modules.Identity.Domain/Services/IPermissionCatalogService.cs`
- **Catalog Service Implementation**: `src/Modules/Identity/HRM.Modules.Identity.Infrastructure/Services/PermissionCatalogService.cs`
- **Value Objects**: `src/Modules/Identity/HRM.Modules.Identity.Domain/ValueObjects/`
  - `PermissionModule.cs`
  - `PermissionEntity.cs`
  - `PermissionAction.cs`
  - `PermissionScope.cs`
  - `PermissionConstraint.cs`

## Migration from Template System

If migrating from the old Permission Template system:

1. All templates (SystemAdministrator.xml, HRManager.xml, etc.) have been consolidated into PermissionCatalog.xml
2. Template metadata (Name, Version, ApplicableTo) is no longer needed
3. Roles are now created and managed in the database
4. Admin selects permissions from catalog via UI instead of XML files

## Next Steps

1. **API Endpoints**: Create endpoints to load catalog and manage roles
2. **UI Components**: Build role management UI with catalog selection
3. **Database Schema**: Tables for roles and role permissions
4. **User Assignments**: Link users/operators to roles
5. **Runtime Evaluator**: Implement constraint evaluation
6. **Migration Tool**: Import existing role data if needed
