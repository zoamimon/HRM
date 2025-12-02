using HRM.Modules.Personnel.Application.Features.Employees.Commands;
using HRM.Modules.Personnel.Application.Features.Employees.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HRM.Modules.Personnel.Api
{
    public static class PersonnelEndpoints
    {
        public static void MapPersonnelEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("api/personnel/employees", async (CreateEmployeeCommand command, ISender sender) =>
            {
                var employeeId = await sender.Send(command);
                return Results.Created($"/api/personnel/employees/{employeeId}", new { employeeId });
            });

            app.MapGet("api/personnel/employees", async (ISender sender) =>
            {
                var employees = await sender.Send(new GetAllEmployeesQuery());
                return Results.Ok(employees);
            });

            app.MapGet("api/personnel/employees/{id:guid}", async (Guid id, ISender sender) =>
            {
                var employee = await sender.Send(new GetEmployeeByIdQuery { EmployeeId = id });
                return employee is not null ? Results.Ok(employee) : Results.NotFound();
            });

            app.MapPut("api/personnel/employees/{id:guid}", async (Guid id, UpdateEmployeeCommand command, ISender sender) =>
            {
                command.EmployeeId = id;
                await sender.Send(command);
                return Results.NoContent();
            });

            app.MapDelete("api/personnel/employees/{id:guid}", async (Guid id, ISender sender) =>
            {
                await sender.Send(new DeleteEmployeeCommand { EmployeeId = id });
                return Results.NoContent();
            });

            app.MapPost("api/personnel/employees/{employeeId:guid}/assignments", async (Guid employeeId, AssignEmployeeRoleCommand command, ISender sender) =>
            {
                command.EmployeeId = employeeId;
                var assignmentId = await sender.Send(command);
                return Results.Created($"/api/personnel/employees/{employeeId}/assignments/{assignmentId}", new { assignmentId });
            });

            app.MapPut("api/personnel/employees/{employeeId:guid}/assignments/{assignmentId:guid}/end", async (Guid employeeId, Guid assignmentId, EndAssignmentRequest request, ISender sender) =>
            {
                var command = new EndEmployeeAssignmentCommand
                {
                    EmployeeId = employeeId,
                    AssignmentId = assignmentId,
                    EndDate = request.EndDate
                };
                await sender.Send(command);
                return Results.Ok();
            });
        }
    }

    public class EndAssignmentRequest
    {
        public DateTime EndDate { get; set; }
    }
}
