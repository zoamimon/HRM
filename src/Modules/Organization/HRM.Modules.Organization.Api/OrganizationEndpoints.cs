using HRM.Modules.Organization.Application.Features.Companies.Commands;
using HRM.Modules.Organization.Application.Features.Companies.Queries;
using HRM.Modules.Organization.Application.Features.Departments.Commands;
using HRM.Modules.Organization.Application.Features.Departments.Queries;
using HRM.Modules.Organization.Application.Features.Positions.Commands;
using HRM.Modules.Organization.Application.Features.Positions.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HRM.Modules.Organization.Api
{
    public static class OrganizationEndpoints
    {
        public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/organization");

            // Companies
            group.MapGet("companies", async (ISender sender) =>
            {
                var companies = await sender.Send(new GetAllCompaniesQuery());
                return Results.Ok(companies);
            });

            group.MapPost("companies", async (CreateCompanyCommand command, ISender sender) =>
            {
                var companyId = await sender.Send(command);
                return Results.Created($"/api/organization/companies/{companyId}", new { companyId });
            });

            group.MapGet("companies/{id:guid}", async (Guid id, ISender sender) =>
            {
                var company = await sender.Send(new GetCompanyByIdQuery { CompanyId = id });
                return company is not null ? Results.Ok(company) : Results.NotFound();
            });

            group.MapPut("companies/{id:guid}", async (Guid id, UpdateCompanyCommand command, ISender sender) =>
            {
                command.CompanyId = id;
                await sender.Send(command);
                return Results.NoContent();
            });

            // Departments
            group.MapGet("departments", async (ISender sender) =>
            {
                var departments = await sender.Send(new GetAllDepartmentsQuery());
                return Results.Ok(departments);
            });

            group.MapPost("departments", async (CreateDepartmentCommand command, ISender sender) =>
            {
                var departmentId = await sender.Send(command);
                return Results.Created($"/api/organization/departments/{departmentId}", new { departmentId });
            });

            group.MapGet("departments/{id:guid}", async (Guid id, ISender sender) =>
            {
                var department = await sender.Send(new GetDepartmentByIdQuery { DepartmentId = id });
                return department is not null ? Results.Ok(department) : Results.NotFound();
            });

            group.MapPut("departments/{id:guid}", async (Guid id, UpdateDepartmentCommand command, ISender sender) =>
            {
                command.DepartmentId = id;
                await sender.Send(command);
                return Results.NoContent();
            });

            group.MapDelete("departments/{id:guid}", async (Guid id, ISender sender) =>
            {
                await sender.Send(new DeleteDepartmentCommand { DepartmentId = id });
                return Results.NoContent();
            });

            group.MapGet("companies/{companyId:guid}/departments", async (Guid companyId, ISender sender) =>
            {
                var departments = await sender.Send(new GetDepartmentsByCompanyQuery { CompanyId = companyId });
                return Results.Ok(departments);
            });

            // Positions
            group.MapGet("positions", async (ISender sender) =>
            {
                var positions = await sender.Send(new GetAllPositionsQuery());
                return Results.Ok(positions);
            });

            group.MapPost("positions", async (CreatePositionCommand command, ISender sender) =>
            {
                var positionId = await sender.Send(command);
                return Results.Created($"/api/organization/positions/{positionId}", new { positionId });
            });

            group.MapGet("positions/{id:guid}", async (Guid id, ISender sender) =>
            {
                var position = await sender.Send(new GetPositionByIdQuery { PositionId = id });
                return position is not null ? Results.Ok(position) : Results.NotFound();
            });

            group.MapPut("positions/{id:guid}", async (Guid id, UpdatePositionCommand command, ISender sender) =>
            {
                command.PositionId = id;
                await sender.Send(command);
                return Results.NoContent();
            });

            group.MapDelete("positions/{id:guid}", async (Guid id, ISender sender) =>
            {
                await sender.Send(new DeletePositionCommand { PositionId = id });
                return Results.NoContent();
            });

            group.MapGet("departments/{departmentId:guid}/positions", async (Guid departmentId, ISender sender) =>
            {
                var positions = await sender.Send(new GetPositionsByDepartmentQuery { DepartmentId = departmentId });
                return Results.Ok(positions);
            });

            group.MapGet("positions/validate", async ([AsParameters] ValidatePositionQuery query, ISender sender) =>
            {
                var isValid = await sender.Send(query);
                return Results.Ok(new { isValid });
            });
        }
    }
}
