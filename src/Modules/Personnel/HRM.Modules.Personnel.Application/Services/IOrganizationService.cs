namespace HRM.Modules.Personnel.Application.Services
{
    public interface IOrganizationService
    {
        Task<bool> IsValidAssignmentAsync(Guid companyId, Guid departmentId, Guid positionId);
    }
}
