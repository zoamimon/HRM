# Permission Template System - Documentation

## Overview

The Permission Template System is a declarative, XML-based permission management system that separates **WHAT** capabilities exist from **HOW** they are enforced.

### Key Principles

1. **Separation of Concerns**: XML defines capabilities (declarative), runtime evaluators enforce them (imperative)
2. **Type Safety**: Strong domain models with compile-time validation
3. **Flexibility**: Extensible constraint system for complex business rules
4. **Scope-Based Access**: Hierarchical data visibility (Company → Department → Position → Self)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Permission Template                     │
├─────────────────────────────────────────────────────────────┤
│ Metadata                                                    │
│  ├─ Name, Version, Description                             │
│  ├─ ApplicableTo (User/Operator/Both)                      │
│  └─ Category, IsSystem                                      │
├─────────────────────────────────────────────────────────────┤
│ Permissions                                                  │
│  └─ Modules (Personnel, Attendance, Payroll)                │
│      └─ Entities (Employee, Department, Timesheet)          │
│          └─ Actions (View, Create, Update, Delete)          │
│              ├─ Scopes (Company, Department, Position, Self)│
│              └─ Constraints (ManagerOfTarget, FieldRestrict)│
└─────────────────────────────────────────────────────────────┘
```

## XML Structure

### Basic Template Structure

```xml
<?xml version="1.0" encoding="UTF-8"?>
<PermissionTemplate xmlns="http://hrm.system/permissions">
  <Metadata>
    <Name>TemplateName</Name>
    <DisplayName>Display Name</DisplayName>
    <Description>Template description</Description>
    <Version>1.0</Version>
    <ApplicableTo>User|Operator|Both</ApplicableTo>
    <Category>Optional Category</Category>
    <IsSystem>true|false</IsSystem>
  </Metadata>

  <Permissions>
    <Module name="ModuleName" displayName="Display Name">
      <Entity name="EntityName" displayName="Display Name">
        <Action name="ActionName" displayName="Display Name">
          <!-- Optional scopes and constraints -->
        </Action>
      </Entity>
    </Module>
  </Permissions>
</PermissionTemplate>
```

## Metadata Fields

| Field | Required | Description |
|-------|----------|-------------|
| `Name` | Yes | Unique identifier (e.g., "HRManager") |
| `DisplayName` | Yes | UI-friendly name (e.g., "Quản lý nhân sự") |
| `Description` | Yes | Detailed explanation of template purpose |
| `Version` | Yes | Semantic version (format: `major.minor`, e.g., "1.0") |
| `ApplicableTo` | Yes | Who can use this template: `User`, `Operator`, or `Both` |
| `Category` | No | Group templates (e.g., "Management", "HR") |
| `IsSystem` | No | System templates cannot be deleted (default: `false`) |

### ApplicableTo Behavior

| Value | For Users | For Operators |
|-------|-----------|---------------|
| `User` | ✅ Show in assignment UI<br>✅ Require scope selection | ❌ Hidden |
| `Operator` | ❌ Hidden | ✅ Show in assignment UI<br>❌ No scope selection |
| `Both` | ✅ Show, require scope | ✅ Show, no scope |

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

You can define custom actions specific to entities:

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

**Use Cases:**
- Manager approving subordinate's leave
- Manager updating subordinate's performance review

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

**Use Cases:**
- Hide salary from non-HR users
- Prevent editing official records
- Show only public fields

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

**Parameters:**
- `MinDays`: Minimum days from today (negative = past, positive = future)
- `MaxDays`: Maximum days from today

**Use Cases:**
- Edit attendance only for last 30 days
- Request leave only for future dates
- Limit access to historical data

#### 4. WorkflowState

Restricts actions based on entity state.

```xml
<Constraint type="WorkflowState">
  <Parameters>
    <Parameter name="AllowedStates" value="Pending,Submitted" />
  </Parameters>
</Constraint>
```

**Parameters:**
- `AllowedStates`: Comma-separated allowed states

**Use Cases:**
- Only approve pending requests
- Cannot edit terminated employees
- Only submit draft timesheets

#### 5. CustomRule

For complex business rules not covered by other types.

```xml
<Constraint type="CustomRule">
  <Parameters>
    <Parameter name="RuleName" value="MaxOvertimeCheck" />
    <Parameter name="Threshold" value="10" />
  </Parameters>
</Constraint>
```

## Examples

### Example 1: Employee Self-Service

Simple template for regular employees (Self scope only):

```xml
<PermissionTemplate xmlns="http://hrm.system/permissions">
  <Metadata>
    <Name>EmployeeSelfService</Name>
    <DisplayName>Nhân viên - Tự phục vụ</DisplayName>
    <Description>Nhân viên chỉ xem và cập nhật thông tin cá nhân</Description>
    <Version>1.0</Version>
    <ApplicableTo>User</ApplicableTo>
    <IsSystem>true</IsSystem>
  </Metadata>

  <Permissions>
    <Module name="Personnel" displayName="Quản lý nhân sự">
      <Entity name="Employee" displayName="Nhân viên">

        <Action name="View" displayName="Xem thông tin">
          <Scopes>
            <Scope value="Self" displayName="Chỉ bản thân" />
          </Scopes>
          <Constraints>
            <Constraint type="FieldRestriction">
              <Parameters>
                <Parameter name="Fields" value="Salary,Bonus" />
                <Parameter name="ApplyTo" value="View" />
              </Parameters>
            </Constraint>
          </Constraints>
        </Action>

      </Entity>
    </Module>
  </Permissions>
</PermissionTemplate>
```

### Example 2: Department Manager

Manager with department scope and constraints:

```xml
<Action name="Update" displayName="Cập nhật thông tin nhân viên">
  <Scopes>
    <Scope value="Department" displayName="Cùng phòng ban" />
  </Scopes>
  <Constraints>
    <!-- Must be manager -->
    <Constraint type="ManagerOfTarget">
      <Parameters>
        <Parameter name="AllowIndirect" value="false" />
        <Parameter name="MaxLevels" value="1" />
      </Parameters>
    </Constraint>
    <!-- Cannot update salary -->
    <Constraint type="FieldRestriction">
      <Parameters>
        <Parameter name="Fields" value="Salary,Bonus" />
        <Parameter name="ApplyTo" value="Update" />
      </Parameters>
    </Constraint>
  </Constraints>
</Action>
```

### Example 3: System Administrator (Operator)

Full access, no scopes:

```xml
<PermissionTemplate xmlns="http://hrm.system/permissions">
  <Metadata>
    <Name>SystemAdministrator</Name>
    <DisplayName>Quản trị viên hệ thống</DisplayName>
    <Description>Quyền truy cập đầy đủ toàn hệ thống</Description>
    <Version>1.0</Version>
    <ApplicableTo>Operator</ApplicableTo>
    <IsSystem>true</IsSystem>
  </Metadata>

  <Permissions>
    <Module name="Personnel" displayName="Quản lý nhân sự">
      <Entity name="Employee" displayName="Nhân viên">
        <!-- No scopes = global access for operators -->
        <Action name="View" displayName="Xem" />
        <Action name="Create" displayName="Tạo" />
        <Action name="Update" displayName="Cập nhật" />
        <Action name="Delete" displayName="Xóa" />
      </Entity>
    </Module>
  </Permissions>
</PermissionTemplate>
```

## Usage

### 1. Parse XML Template

```csharp
// From string
var template = await _parser.ParseAsync(xmlContent);

// From file
var template = await _parser.ParseFromFileAsync("templates/HRManager.xml");
```

### 2. Validate Template

```csharp
var errors = await _parser.ValidateAndGetErrorsAsync(xmlContent);
if (errors.Any())
{
    // Handle validation errors
}
```

### 3. Store Template

```csharp
await _repository.AddAsync(template);
```

### 4. Assign to User

```csharp
// Assign HR Manager template to user with Department scope
var userPermission = new UserPermission(
    userId: userId,
    templateId: hrManagerTemplate.Id,
    scope: ScopeLevel.Department
);
```

### 5. Check Permission at Runtime

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

## Best Practices

### Template Design

1. **Start Simple**: Begin with basic View/Create/Update/Delete actions
2. **Use System Templates**: Mark built-in templates as `IsSystem="true"`
3. **Version Control**: Increment version when making changes
4. **Clear Naming**: Use descriptive names (e.g., "HRManager", not "Template1")
5. **Group by Category**: Use categories for organization

### Scope Selection

1. **Default to Department**: Most users work within department scope
2. **Position for Read-Only**: Use Position scope with `readOnly="true"` for visibility
3. **Avoid Company for Everyone**: Company scope is for high-level roles only
4. **Self for Employees**: Regular employees should have Self scope

### Constraints

1. **Combine Constraints**: Use multiple constraints for complex rules
2. **ManagerOfTarget for Approval**: Always require manager for approval actions
3. **FieldRestriction for Security**: Hide sensitive data (salary, SSN, etc.)
4. **DateRange for Temporal**: Limit editing to recent records
5. **WorkflowState for Consistency**: Enforce workflow transitions

### Performance

1. **Cache Templates**: Load templates once and cache in memory
2. **Index by Name**: Fast lookup by template name
3. **Lazy Load XML**: Don't parse XML until needed
4. **Batch Permission Checks**: Check multiple permissions at once

## Files

- **XML Schema**: `docs/permissions/PermissionTemplate.xsd`
- **Sample Templates**: `templates/permissions/*.xml`
- **Domain Models**: `src/Modules/Identity/HRM.Modules.Identity.Domain/`
  - Entities: `Entities/Permissions/PermissionTemplate.cs`
  - Value Objects: `ValueObjects/Permission*.cs`
  - Enums: `Enums/ConstraintType.cs`, `Enums/ApplicableTo.cs`
- **Parser**: `src/Modules/Identity/HRM.Modules.Identity.Infrastructure/Services/PermissionTemplateParser.cs`

## Next Steps

1. **Database Schema**: Create tables for storing templates
2. **User Assignments**: Link users/operators to templates
3. **Runtime Evaluator**: Implement constraint evaluation
4. **API Endpoints**: CRUD operations for templates
5. **UI Components**: Template builder and permission assignment screens
6. **Migration Tool**: Import existing permissions to templates
