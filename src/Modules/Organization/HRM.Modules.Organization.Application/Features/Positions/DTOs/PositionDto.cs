namespace HRM.Modules.Organization.Application.Features.Positions.DTOs
{
    public record PositionDto(Guid PositionId, string Name, Guid DepartmentId);
}
