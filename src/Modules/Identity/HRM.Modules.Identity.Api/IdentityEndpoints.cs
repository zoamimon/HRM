using HRM.Modules.Identity.Application.Features.Auth.Commands;
using HRM.Modules.Identity.Application.Features.Roles.Commands;
using HRM.Modules.Identity.Application.Features.Roles.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HRM.Modules.Identity.Api
{
    public static class IdentityEndpoints
    {
        public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/identity");

            group.MapPost("signup", async (SignUpCommand command, ISender sender) =>
            {
                await sender.Send(command);
                return Results.Ok();
            });

            group.MapPost("signin", async (SignInCommand command, ISender sender) =>
            {
                var response = await sender.Send(command);
                return Results.Ok(response);
            });

            group.MapPost("token/refresh", async (RefreshTokenCommand command, ISender sender) =>
            {
                var response = await sender.Send(command);
                return Results.Ok(response);
            });

            // Roles and Permissions Endpoints
            var rolesGroup = group.MapGroup("roles");

            rolesGroup.MapGet("permissions", async (ISender sender) =>
            {
                var permissions = await sender.Send(new GetAllPermissionsQuery());
                return Results.Ok(permissions);
            });

            rolesGroup.MapGet("/", async (ISender sender) =>
            {
                var roles = await sender.Send(new GetAllRolesQuery());
                return Results.Ok(roles);
            });

            rolesGroup.MapGet("{id:guid}", async (Guid id, ISender sender) =>
            {
                var role = await sender.Send(new GetRoleByIdQuery { Id = id });
                return role is not null ? Results.Ok(role) : Results.NotFound();
            });

            rolesGroup.MapPost("/", async (CreateRoleCommand command, ISender sender) =>
            {
                var roleId = await sender.Send(command);
                return Results.Created($"/api/identity/roles/{roleId}", new { roleId });
            });

            rolesGroup.MapPut("{id:guid}", async (Guid id, UpdateRoleCommand command, ISender sender) =>
            {
                command.RoleId = id;
                await sender.Send(command);
                return Results.NoContent();
            });
        }
    }
}
