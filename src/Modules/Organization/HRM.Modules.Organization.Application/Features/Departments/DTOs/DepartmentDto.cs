namespace HRM.Modules.Organization.Application.Features.Departments.DTOs
{
    public record DepartmentDto(Guid DepartmentId, string Name, Guid CompanyId, Guid? ParentId);
}
